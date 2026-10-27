using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LidFlip
{
    /// <summary>The tray icon, its menu and the flip animation.</summary>
    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Icon iconAwake, iconSleep;
        readonly Icon[] toAwake, toSleep;
        readonly Timer animTimer = new Timer(), pinTimer = new Timer(), quitTimer = new Timer();
        Icon[] anim; int animIdx; int pinTries;

        readonly ToolStripMenuItem miAwake, miStartup, miPin, miGuide, miExit;
        GuideForm guide;

        public TrayApp(bool forceGuide, bool silent, bool replaced)
        {
            int px = Math.Max(16, SystemInformation.SmallIconSize.Width);   // 16/20/24/32 depending on display scaling
            iconAwake = ToIcon(Art.Still(px, true));
            iconSleep = ToIcon(Art.Still(px, false));
            toAwake = ToIcons(Art.Transition(px, true));
            toSleep = ToIcons(Art.Transition(px, false));

            // ----- menu -----
            ContextMenuStrip menu = new ContextMenuStrip();
            miAwake   = new ToolStripMenuItem("Stay awake when lid is closed", null, delegate { Toggle(); });
            miStartup = new ToolStripMenuItem("Start with Windows", null, delegate { ToggleStartup(); });
            miPin     = new ToolStripMenuItem("Pin next to clock", null, delegate { TogglePin(); });
            miGuide   = new ToolStripMenuItem("How LidFlip works", null, delegate { ShowGuide(); });
            miExit    = new ToolStripMenuItem("Exit", null, delegate { Quit(); });
            menu.Items.AddRange(new ToolStripItem[] { miAwake, miStartup, miPin, new ToolStripSeparator(), miGuide, miExit });
            menu.Opening += delegate
            {
                try { miAwake.Checked = Power.GetLid() == Power.DoNothing; } catch { miAwake.Checked = false; }
                miStartup.Checked = Startup.IsOn;
                miPin.Visible = Pin.Supported;
                miPin.Checked = Pin.Wanted;
            };
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Toggle(); };

            // ----- animation player -----
            animTimer.Interval = 22;
            animTimer.Tick += delegate
            {
                if (anim != null && animIdx < anim.Length) tray.Icon = anim[animIdx++];
                else animTimer.Stop();
            };

            Refresh(false);
            tray.Visible = true;

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

        static Icon[] ToIcons(List<Bitmap> frames)
        {
            Icon[] r = new Icon[frames.Count];
            for (int i = 0; i < r.Length; i++) r[i] = ToIcon(frames[i]);
            return r;
        }

        void Refresh(bool animate)
        {
            int v;
            try { v = Power.GetLid(); } catch { v = -1; }
            bool awake = v == Power.DoNothing;
            tray.Text = awake ? "LidFlip: lid closed = stays awake" : "LidFlip: lid closed = sleeps";
            if (animate)
            {
                anim = awake ? toAwake : toSleep;
                animIdx = 0;
                animTimer.Stop();
                animTimer.Start();
            }
            else tray.Icon = awake ? iconAwake : iconSleep;
        }

        void Toggle()
        {
            try
            {
                Power.SetLid(Power.GetLid() == Power.DoNothing ? Power.Sleep : Power.DoNothing);
                Refresh(true);
            }
            catch (Exception ex)
            {
                Program.Error(ex.Message);
                Refresh(false);
            }
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

        // hide and show the icon so Explorer re-reads its pin setting
        void Reregister()
        {
            tray.Visible = false;
            System.Threading.Thread.Sleep(300);
            tray.Visible = true;
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

        void Quit()
        {
            quitTimer.Stop(); pinTimer.Stop(); animTimer.Stop();
            if (guide != null && !guide.IsDisposed) guide.Close();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }
}
