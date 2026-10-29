using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace LidFlip
{
    /// <summary>The tray icon, its menu and the flip animation.</summary>
    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray = new NotifyIcon();
        Icon iconAwake, iconSleep;
        Icon[] toAwake, toSleep;
        Color glyphFg, glyphAccent;
        readonly Timer animTimer = new Timer(), pinTimer = new Timer(), quitTimer = new Timer();
        Icon[] anim; int animIdx; int pinTries;
        MethodInvoker animDone;     // runs when the current animation finishes
        int iconPx;
        bool quitting;

        GuideForm guide;
        TrayMenu menu;

        // The lid state is cached so the UI reacts instantly; powercfg runs in the background.
        readonly Control ui = new Control();          // used to get back onto the UI thread
        readonly object powerLock = new object();
        bool awakeState, desired, workerBusy;

        public TrayApp(bool forceGuide, bool silent, bool replaced)
        {
            ui.CreateControl();
            try { awakeState = Power.GetLid() == Power.DoNothing; } catch { awakeState = false; }

            int px = Math.Max(16, SystemInformation.SmallIconSize.Width);   // 16/20/24/32 depending on display scaling
            iconPx = px;
            BuildIcons();

            // follow the taskbar when the user switches light/dark mode or changes the accent colour
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnPrefsChanged;

            // ----- menu: our own custom-drawn popup (see TrayMenu.cs) -----
            tray.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Toggle();
                else if (e.Button == MouseButtons.Right) ShowMenu();
            };

            // ----- animation player -----
            animTimer.Interval = 22;
            animTimer.Tick += delegate
            {
                if (anim != null && animIdx < anim.Length) { tray.Icon = anim[animIdx++]; return; }
                animTimer.Stop();
                if (animDone != null) { MethodInvoker d = animDone; animDone = null; d(); }
            };

            // start-up animation: pop into the tray
            Icon[] intro = ToIcons(TrayGlyph.Intro(px, awakeState, glyphFg, glyphAccent));
            Refresh(false);
            tray.Icon = intro[0];
            tray.Visible = true;
            Play(intro, delegate { Refresh(false); });

            // ----- re-apply "pin next to clock" after a restart -----
            // Explorer may register the icon a few seconds late at sign-in, so retry for about a minute.
            pinTimer.Interval = 3000;
            pinTimer.Tick += delegate
            {
                pinTries++;
                try
                {
                    if (!Pin.HasChoice) { pinTimer.Stop(); return; }
                    if (Pin.Registered)
                    {
                        if (Pin.Apply(Pin.Wanted)) Reregister();
                        pinTimer.Stop();
                    }
                }
                catch { }
                if (pinTries >= 20) pinTimer.Stop();
            };
            pinTimer.Start();

            // ----- a newer launch asks us to quit -----
            quitTimer.Interval = 400;
            quitTimer.Tick += delegate { if (Program.QuitEvent.WaitOne(0)) Quit(); };
            quitTimer.Start();

            // ----- first run: show the guide -----
            if (forceGuide || Prefs.Get("GuideShown", 0) == 0)
            {
                Prefs.Set("GuideShown", 1);
                ShowGuide();
            }
            else if (!silent)
            {
                tray.ShowBalloonTip(4000, replaced ? "LidFlip restarted" : "LidFlip is running",
                    "It's next to the clock. Click the icon to flip, right-click for options.", ToolTipIcon.Info);
            }
        }

        static Icon ToIcon(Bitmap b) { return Icon.FromHandle(b.GetHicon()); }

        /// <summary>(Re)draws the tray icons in the current taskbar colours.</summary>
        void BuildIcons()
        {
            TrayGlyph.Colors(out glyphFg, out glyphAccent);
            iconAwake = ToIcon(TrayGlyph.Still(iconPx, true, glyphFg, glyphAccent));
            iconSleep = ToIcon(TrayGlyph.Still(iconPx, false, glyphFg, glyphAccent));
            toAwake = ToIcons(TrayGlyph.Transition(iconPx, true, glyphFg, glyphAccent));
            toSleep = ToIcons(TrayGlyph.Transition(iconPx, false, glyphFg, glyphAccent));
        }

        void OnPrefsChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category != Microsoft.Win32.UserPreferenceCategory.General &&
                e.Category != Microsoft.Win32.UserPreferenceCategory.Color &&
                e.Category != Microsoft.Win32.UserPreferenceCategory.VisualStyle) return;
            try
            {
                ui.BeginInvoke((MethodInvoker)delegate
                {
                    if (quitting) return;
                    Color fg, acc;
                    TrayGlyph.Colors(out fg, out acc);
                    if (fg == glyphFg && acc == glyphAccent) return;
                    BuildIcons();
                    if (!animTimer.Enabled) Refresh(false);
                });
            }
            catch { }
        }

        static Icon[] ToIcons(List<Bitmap> frames)
        {
            Icon[] r = new Icon[frames.Count];
            for (int i = 0; i < r.Length; i++) r[i] = ToIcon(frames[i]);
            return r;
        }

        void Refresh(bool animate)
        {
            bool awake = awakeState;
            tray.Text = awake ? "LidFlip: lid closed = stays awake" : "LidFlip: lid closed = sleeps";
            if (animate) Play(awake ? toAwake : toSleep, null);
            else tray.Icon = awake ? iconAwake : iconSleep;
        }

        void Play(Icon[] frames, MethodInvoker done)
        {
            animTimer.Stop();
            anim = frames; animIdx = 0; animDone = done;
            animTimer.Start();
        }

        bool IsAwake() { return awakeState; }

        /// <summary>Re-reads the real setting in the background and fixes the UI if it changed elsewhere.</summary>
        void VerifyInBackground()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool actual;
                try { actual = Power.GetLid() == Power.DoNothing; } catch { return; }
                lock (powerLock) { if (workerBusy) return; }    // a change is in flight; it will settle the state
                ui.BeginInvoke((MethodInvoker)delegate
                {
                    if (actual == awakeState) return;
                    awakeState = actual;
                    Refresh(false);
                    if (menu != null && !menu.IsDisposed) menu.Resync();
                });
            });
        }

        void ShowMenu()
        {
            if (menu != null && !menu.IsDisposed) { menu.BeginClose(); return; }
            List<MenuRow> rows = new List<MenuRow>();
            rows.Add(MenuRow.Switch("Stay awake when lid is closed", TrayMenu.GlyphBolt, IsAwake, Toggle));
            rows.Add(MenuRow.Switch("Start with Windows", TrayMenu.GlyphStartup, delegate { return Startup.IsOn; }, ToggleStartup));
            if (Pin.Supported)
                rows.Add(MenuRow.Switch("Pin next to clock", TrayMenu.GlyphPin, delegate { return Pin.Wanted; }, TogglePin));
            rows.Add(MenuRow.Separator());
            rows.Add(MenuRow.Command("How LidFlip works", TrayMenu.GlyphHelp, ShowGuide));
            rows.Add(MenuRow.Command("Exit", TrayMenu.GlyphExit, Quit));
            menu = new TrayMenu(IsAwake, rows);
            menu.ShowNear(Cursor.Position);
            VerifyInBackground();
        }

        /// <summary>
        /// Flips instantly in the UI, then applies the change in the background.
        /// Rapid clicks are coalesced: the worker keeps going until the setting matches the last click.
        /// </summary>
        void Toggle()
        {
            if (quitting) return;
            awakeState = !awakeState;
            Refresh(true);
            lock (powerLock)
            {
                desired = awakeState;
                if (workerBusy) return;
                workerBusy = true;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                while (true)
                {
                    bool want;
                    lock (powerLock) want = desired;
                    try { Power.SetLid(want ? Power.DoNothing : Power.Sleep); }
                    catch (Exception ex)
                    {
                        lock (powerLock) workerBusy = false;
                        string msg = ex.Message;
                        ui.BeginInvoke((MethodInvoker)delegate
                        {
                            try { awakeState = Power.GetLid() == Power.DoNothing; } catch { }
                            Refresh(false);
                            if (menu != null && !menu.IsDisposed) menu.Resync();
                            Program.Error(msg);
                        });
                        return;
                    }
                    lock (powerLock)
                    {
                        if (desired == want) { workerBusy = false; return; }
                    }
                }
            });
        }

        void ToggleStartup()
        {
            try { Startup.Set(!Startup.IsOn); }
            catch (Exception ex) { Program.Error(ex.Message); }
        }

        void TogglePin()
        {
            try
            {
                if (!Pin.Registered)
                {
                    Program.Error("Windows hasn't registered the LidFlip icon yet. Drag the icon from the ^ arrow onto the taskbar instead.");
                    return;
                }
                bool want = !Pin.Wanted;
                Pin.Wanted = want;
                if (Pin.Apply(want)) Reregister();
            }
            catch (Exception ex) { Program.Error(ex.Message); }
        }

        // hide and show the icon so Explorer re-reads its pin setting (without blocking the UI)
        void Reregister()
        {
            tray.Visible = false;
            Timer t = new Timer();
            t.Interval = 300;
            t.Tick += delegate { t.Stop(); t.Dispose(); tray.Visible = true; };
            t.Start();
        }

        void ShowGuide()
        {
            if (guide == null || guide.IsDisposed)
            {
                guide = new GuideForm();
                guide.Show();
            }
            guide.Activate();
        }

        /// <summary>Exit with the closing animation (lid shuts, icon shrinks away), then quit.</summary>
        void Quit()
        {
            if (quitting) return;
            quitting = true;
            quitTimer.Stop(); pinTimer.Stop();
            if (guide != null && !guide.IsDisposed) guide.Close();
            if (menu != null && !menu.IsDisposed) menu.BeginClose();
            Icon[] outro;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnPrefsChanged;
            try { outro = ToIcons(TrayGlyph.Outro(iconPx, awakeState, glyphFg, glyphAccent)); }
            catch { outro = new Icon[0]; }
            Play(outro, delegate
            {
                tray.Visible = false;
                tray.Dispose();
                ui.Dispose();
                ExitThread();
            });
        }
    }
}
