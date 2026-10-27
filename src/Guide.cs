using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LidFlip
{
    /// <summary>Light/dark colours, following the Windows app theme.</summary>
    class Theme
    {
        public bool Dark;
        public Color Bg, Card, Text, Sub, Border, Accent, AccentHover, MenuBg, MenuHi, Bar, BarText, Warn;

        public static Theme Current()
        {
            bool dark = false;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    dark = v is int && (int)v == 0;
                }
            }
            catch { }
            return dark ? DarkTheme() : LightTheme();
        }

        static Color C(string h) { return ColorTranslator.FromHtml(h); }

        public static Theme LightTheme()
        {
            Theme t = new Theme();
            t.Dark = false; t.Bg = C("#FFFFFF"); t.Card = C("#F1F3F9"); t.Text = C("#111827"); t.Sub = C("#4B5563");
            t.Border = C("#D1D5DB"); t.Accent = C("#4F46E5"); t.AccentHover = C("#4338CA");
            t.MenuBg = C("#FFFFFF"); t.MenuHi = C("#E8EAF6"); t.Bar = C("#E9ECF2"); t.BarText = C("#1F2937"); t.Warn = C("#F59E0B");
            return t;
        }

        public static Theme DarkTheme()
        {
            Theme t = new Theme();
            t.Dark = true; t.Bg = C("#16181F"); t.Card = C("#20232D"); t.Text = C("#F3F4F6"); t.Sub = C("#A3A9B8");
            t.Border = C("#3A3F4C"); t.Accent = C("#6366F1"); t.AccentHover = C("#818CF8");
            t.MenuBg = C("#2B2E38"); t.MenuHi = C("#3B3F66"); t.Bar = C("#101217"); t.BarText = C("#E5E7EB"); t.Warn = C("#FBBF24");
            return t;
        }
    }

    /// <summary>The illustrations for each guide page (also used for the README images).</summary>
    static class GuideArt
    {
        public const int Pages = 6;

        public static readonly string[] Titles = {
            "Welcome to LidFlip",
            "Click to flip",
            "Right-click for options",
            "Start with Windows",
            "Pin next to the clock",
            "Before it goes in a bag" };

        public static readonly string[] Bodies = {
            "Choose what your laptop does when you close the lid: go to sleep, or keep running. LidFlip lives next to the clock, so it's always one click away.",
            "Click the LidFlip icon to switch. The moon means the laptop sleeps when you close the lid. The lightning bolt means it keeps running: downloads, builds and remote sessions carry on.",
            "Right-click the icon for the menu. A check mark shows which options are on. \"Stay awake when lid is closed\" does the same as clicking the icon.",
            "Turn this on and LidFlip starts every time you sign in, so the icon is always there. Keep LidFlip.exe in a permanent folder first (not Downloads), because Windows remembers where it is.",
            "Windows hides new icons under the ^ arrow. \"Pin next to clock\" keeps LidFlip on the taskbar, and it stays there after restarts. You can also just drag the icon out of the arrow.",
            "With the lightning bolt on, a closed laptop keeps running and can get hot inside a bag. Flip back to the moon before you pack it away." };

        static Font F(float pt, FontStyle st) { return new Font("Segoe UI", pt, st, GraphicsUnit.Point); }

        public static void Draw(Graphics g, int page, RectangleF r, Theme t, float s)
        {
            switch (page)
            {
                case 0: Welcome(g, r, t, s); break;
                case 1: ClickToFlip(g, r, t, s); break;
                case 2: MenuMock(g, Center(r, 300 * s, 196 * s), t, s, -1, true); break;
                case 3: MenuMock(g, Center(r, 300 * s, 196 * s), t, s, 1, true); break;
                case 4: PinNextToClock(g, r, t, s); break;
                case 5: BagWarning(g, r, t, s); break;
            }
        }

        static RectangleF Center(RectangleF r, float w, float h)
        {
            return new RectangleF(r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h);
        }

        static void DrawTileAt(Graphics g, float x, float y, float px, bool awake)
        {
            using (Bitmap b = Art.Still(Math.Max(8, (int)px), awake))
                g.DrawImage(b, x, y, px, px);
        }

        static void Caption(Graphics g, string text, RectangleF r, Color c, float pt, FontStyle st)
        {
            using (Font f = F(pt, st))
                TextRenderer.DrawText(g, text, f, Rectangle.Round(r), c,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }

        static void Arrow(Graphics g, PointF a, PointF b, Color c, float s)
        {
            using (Pen p = new Pen(c, 3 * s))
            {
                p.StartCap = LineCap.Round;
                p.CustomEndCap = new AdjustableArrowCap(3.2f, 3.2f, true);
                g.DrawLine(p, a, b);
            }
        }

        // ---- page 0: the two states ----
        static void Welcome(Graphics g, RectangleF r, Theme t, float s)
        {
            float px = 104 * s, gap = 84 * s;
            float x1 = r.X + (r.Width - 2 * px - gap) / 2, y = r.Y + 38 * s;
            DrawTileAt(g, x1, y, px, false);
            DrawTileAt(g, x1 + px + gap, y, px, true);
            Caption(g, "Lid closed:\nsleeps", new RectangleF(x1 - 30 * s, y + px + 14 * s, px + 60 * s, 50 * s), t.Text, 10f, FontStyle.Regular);
            Caption(g, "Lid closed:\nstays on", new RectangleF(x1 + px + gap - 30 * s, y + px + 14 * s, px + 60 * s, 50 * s), t.Text, 10f, FontStyle.Regular);
            using (Font f = F(16f, FontStyle.Regular))
                TextRenderer.DrawText(g, "or", f, new Rectangle((int)(x1 + px), (int)(y + px / 2 - 14 * s), (int)gap, (int)(28 * s)), t.Sub,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ---- a slice of the taskbar: [^] [icon] wifi battery clock ----
        static void Taskbar(Graphics g, RectangleF r, Theme t, float s, int iconState /* -1 none, 0 sleep, 1 awake */, bool chevronHi)
        {
            using (GraphicsPath p = Art.RoundRect(r, 10 * s))
            using (SolidBrush b = new SolidBrush(t.Bar))
                g.FillPath(b, p);

            float cy = r.Y + r.Height / 2;
            float x = r.Right - 14 * s;
            // clock
            using (Font f = F(8.5f, FontStyle.Regular))
            {
                Rectangle cr = new Rectangle((int)(x - 74 * s), (int)(r.Y), (int)(74 * s), (int)r.Height);
                TextRenderer.DrawText(g, "12:38\n27/10/2026", f, cr, t.BarText,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
            x -= 74 * s + 16 * s;
            // battery
            using (Pen p = new Pen(t.BarText, 1.6f * s))
            using (SolidBrush b = new SolidBrush(t.BarText))
            {
                g.DrawRectangle(p, x - 20 * s, cy - 6 * s, 18 * s, 12 * s);
                g.FillRectangle(b, x - 2 * s, cy - 3 * s, 2.5f * s, 6 * s);
                g.FillRectangle(b, x - 17.5f * s, cy - 3.5f * s, 11 * s, 7 * s);
            }
            x -= 34 * s;
            // wifi
            using (Pen p = new Pen(t.BarText, 1.8f * s))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                for (int i = 1; i <= 3; i++)
                {
                    float rr = 4.5f * s * i;
                    g.DrawArc(p, x - rr, cy + 5 * s - rr, rr * 2, rr * 2, 225, 90);
                }
            }
            x -= 30 * s;
            // our icon
            if (iconState >= 0)
            {
                float ip = 22 * s;
                DrawTileAt(g, x - ip, cy - ip / 2, ip, iconState == 1);
                x -= ip + 14 * s;
            }
            // chevron ^
            if (chevronHi)
                using (GraphicsPath hp = Art.RoundRect(new RectangleF(x - 22 * s, cy - 14 * s, 26 * s, 28 * s), 6 * s))
                using (SolidBrush hb = new SolidBrush(t.Dark ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(30, 0, 0, 0)))
                    g.FillPath(hb, hp);
            using (Pen p = new Pen(t.BarText, 1.8f * s))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                g.DrawLines(p, new PointF[] { new PointF(x - 14 * s, cy + 3 * s), new PointF(x - 9 * s, cy - 3 * s), new PointF(x - 4 * s, cy + 3 * s) });
            }
        }

        static void Cursor(Graphics g, float x, float y, float s)
        {
            PointF[] pts = {
                new PointF(0, 0), new PointF(0, 17), new PointF(4.5f, 13), new PointF(7.5f, 20),
                new PointF(10, 19), new PointF(7, 12), new PointF(12.5f, 12) };
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(x + pts[i].X * s, y + pts[i].Y * s);
            using (SolidBrush b = new SolidBrush(Color.Black)) g.FillPolygon(b, pts);
            using (Pen p = new Pen(Color.White, 1.4f * s)) { p.LineJoin = LineJoin.Round; g.DrawPolygon(p, pts); }
        }

        // ---- page 1: click the icon ----
        static void ClickToFlip(Graphics g, RectangleF r, Theme t, float s)
        {
            float w = 290 * s, h = 46 * s, x = r.X + (r.Width - w) / 2;
            RectangleF top = new RectangleF(x, r.Y + 26 * s, w, h);
            RectangleF bot = new RectangleF(x, r.Bottom - 26 * s - h, w, h);
            Taskbar(g, top, t, s, 0, false);
            Taskbar(g, bot, t, s, 1, false);
            // the icon sits at a fixed offset from the right edge; point the cursor at it
            float iconX = top.Right - 14 * s - 74 * s - 16 * s - 34 * s - 30 * s - 11 * s;
            Cursor(g, iconX + 2 * s, top.Y + top.Height / 2 + 2 * s, s);
            float mid = (top.Bottom + bot.Y) / 2;
            Arrow(g, new PointF(iconX, top.Bottom + 22 * s), new PointF(iconX, bot.Y - 10 * s), t.Accent, s);
            using (Font f = F(10f, FontStyle.Bold))
                TextRenderer.DrawText(g, "Click", f, new Point((int)(iconX + 14 * s), (int)(mid - 10 * s)), t.Accent);
            using (Font f = F(9f, FontStyle.Regular))
            {
                TextRenderer.DrawText(g, "sleeps", f, new Point((int)(x - 0 * s), (int)(top.Bottom + 6 * s)), t.Sub);
                TextRenderer.DrawText(g, "stays awake", f, new Point((int)(x), (int)(bot.Y - 22 * s)), t.Sub);
            }
        }

        // ---- pages 2 & 3: the right-click menu ----
        static void MenuMock(Graphics g, RectangleF r, Theme t, float s, int highlight, bool withChecks)
        {
            string[] items = { "Stay awake when lid is closed", "Start with Windows", "Pin next to clock", "-", "How LidFlip works", "Exit" };
            bool[] checks = { false, true, true, false, false, false };

            for (int i = 6; i >= 1; i--)          // soft shadow
                using (GraphicsPath sp = Art.RoundRect(new RectangleF(r.X - i * s, r.Y - i * s + 4 * s, r.Width + 2 * i * s, r.Height + 2 * i * s), (10 + i) * s))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(t.Dark ? 18 : 9, 0, 0, 0)))
                    g.FillPath(sb, sp);
            using (GraphicsPath p = Art.RoundRect(r, 10 * s))
            {
                using (SolidBrush b = new SolidBrush(t.MenuBg)) g.FillPath(b, p);
                using (Pen pen = new Pen(t.Border, 1)) g.DrawPath(pen, p);
            }

            float y = r.Y + 8 * s, rowH = 32 * s;
            using (Font f = F(9.5f, FontStyle.Regular))
            using (Font fb = F(9.5f, FontStyle.Bold))
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] == "-")
                    {
                        using (Pen sp = new Pen(t.Border, 1)) g.DrawLine(sp, r.X + 12 * s, y + 5 * s, r.Right - 12 * s, y + 5 * s);
                        y += 10 * s;
                        continue;
                    }
                    RectangleF row = new RectangleF(r.X + 6 * s, y, r.Width - 12 * s, rowH - 2 * s);
                    if (i == highlight)
                        using (GraphicsPath hp = Art.RoundRect(row, 6 * s))
                        using (SolidBrush hb = new SolidBrush(t.MenuHi))
                            g.FillPath(hb, hp);
                    if (withChecks && checks[i])
                        using (Pen cp = new Pen(i == highlight ? t.Accent : t.Text, 2 * s))
                        {
                            cp.StartCap = LineCap.Round; cp.EndCap = LineCap.Round; cp.LineJoin = LineJoin.Round;
                            float cx = row.X + 12 * s, cy = row.Y + row.Height / 2;
                            g.DrawLines(cp, new PointF[] { new PointF(cx, cy), new PointF(cx + 4 * s, cy + 4 * s), new PointF(cx + 11 * s, cy - 5 * s) });
                        }
                    TextRenderer.DrawText(g, items[i], i == highlight ? fb : f,
                        new Rectangle((int)(row.X + 34 * s), (int)row.Y, (int)(row.Width - 40 * s), (int)row.Height),
                        t.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                    y += rowH;
                }
        }

        // ---- page 4: from the ^ overflow to the taskbar ----
        static void PinNextToClock(Graphics g, RectangleF r, Theme t, float s)
        {
            float w = 214 * s, h = 46 * s, gap = 26 * s;
            float x1 = r.X + (r.Width - 2 * w - gap) / 2, y = r.Bottom - 36 * s - h;
            RectangleF left = new RectangleF(x1, y, w, h), right = new RectangleF(x1 + w + gap, y, w, h);

            // left: icon hidden in the overflow flyout above the ^
            Taskbar(g, left, t, s, -1, true);
            float chevX = left.Right - 14 * s - 74 * s - 16 * s - 34 * s - 30 * s - 9 * s;
            RectangleF fly = new RectangleF(chevX - 34 * s, left.Y - 70 * s, 68 * s, 56 * s);
            using (GraphicsPath fp = Art.RoundRect(fly, 10 * s))
            {
                using (SolidBrush fb = new SolidBrush(t.MenuBg)) g.FillPath(fb, fp);
                using (Pen pen = new Pen(t.Border, 1)) g.DrawPath(pen, fp);
            }
            DrawTileAt(g, fly.X + (fly.Width - 26 * s) / 2, fly.Y + (fly.Height - 26 * s) / 2, 26 * s, false);

            // right: pinned on the taskbar
            Taskbar(g, right, t, s, 0, false);

            Arrow(g, new PointF(fly.Right + 8 * s, fly.Y + fly.Height / 2), new PointF(right.X + 40 * s, right.Y - 12 * s), t.Accent, s);
            using (Font f = F(9f, FontStyle.Regular))
            {
                TextRenderer.DrawText(g, "hidden under ^", f, new Rectangle((int)left.X, (int)(left.Bottom + 6 * s), (int)left.Width, (int)(20 * s)), t.Sub, TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(g, "pinned next to the clock", f, new Rectangle((int)right.X, (int)(right.Bottom + 6 * s), (int)right.Width, (int)(20 * s)), t.Sub, TextFormatFlags.HorizontalCenter);
            }
        }

        // ---- page 5: flip back before packing ----
        static void BagWarning(Graphics g, RectangleF r, Theme t, float s)
        {
            float px = 96 * s, gap = 92 * s;
            float x1 = r.X + (r.Width - 2 * px - gap) / 2, y = r.Y + 40 * s;
            DrawTileAt(g, x1, y, px, true);
            DrawTileAt(g, x1 + px + gap, y, px, false);

            // warning triangle on the awake tile
            float tx = x1 + px - 18 * s, ty = y - 10 * s, ts = 34 * s;
            PointF[] tri = { new PointF(tx + ts / 2, ty), new PointF(tx + ts, ty + ts * 0.9f), new PointF(tx, ty + ts * 0.9f) };
            using (SolidBrush wb = new SolidBrush(t.Warn)) g.FillPolygon(wb, tri);
            using (Pen wp = new Pen(t.Bg, 2.5f * s)) { wp.LineJoin = LineJoin.Round; g.DrawPolygon(wp, tri); }
            using (Font f = F(12f, FontStyle.Bold))
                TextRenderer.DrawText(g, "!", f, new Rectangle((int)tx, (int)(ty + 6 * s), (int)ts, (int)(ts * 0.8f)), Color.FromArgb(40, 30, 0),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            Arrow(g, new PointF(x1 + px + 18 * s, y + px / 2), new PointF(x1 + px + gap - 18 * s, y + px / 2), t.Accent, s);
            Caption(g, "Keeps running\nin the bag", new RectangleF(x1 - 30 * s, y + px + 14 * s, px + 60 * s, 50 * s), t.Text, 10f, FontStyle.Regular);
            Caption(g, "Safe to\npack away", new RectangleF(x1 + px + gap - 30 * s, y + px + 14 * s, px + 60 * s, 50 * s), t.Text, 10f, FontStyle.Regular);
        }
    }

    /// <summary>A short, step-by-step guide window with an illustration for each option.</summary>
    class GuideForm : Form
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        readonly Theme t = Theme.Current();
        readonly float s;
        readonly Timer anim = new Timer();
        int page, dir = 1, hover = -1;    // hover: 0 back, 1 next, 10+n dot n
        double animT = 1;
        Bitmap illus;

        public const int W = 560, H = 560;

        public GuideForm()
        {
            using (Graphics g = CreateGraphics()) s = g.DpiX / 96f;
            Text = "LidFlip";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size((int)(W * s), (int)(H * s));
            BackColor = t.Bg;
            DoubleBuffered = true;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            anim.Interval = 15;
            anim.Tick += delegate
            {
                animT = Math.Min(1, animT + 0.09);
                if (animT >= 1) anim.Stop();
                Invalidate();
            };
            SetPage(0, 1);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (t.Dark)
            {
                int one = 1;
                try { DwmSetWindowAttribute(Handle, 20, ref one, 4); DwmSetWindowAttribute(Handle, 19, ref one, 4); } catch { }
            }
        }

        RectangleF IllusRect { get { return new RectangleF(32 * s, 28 * s, 496 * s, 250 * s); } }
        Rectangle BackRect { get { return new Rectangle((int)(32 * s), (int)(488 * s), (int)(110 * s), (int)(42 * s)); } }
        Rectangle NextRect { get { return new Rectangle((int)(418 * s), (int)(488 * s), (int)(110 * s), (int)(42 * s)); } }
        RectangleF DotRect(int i)
        {
            float total = (GuideArt.Pages - 1) * 16 * s + 22 * s;
            float x = (W * s - total) / 2 + i * 16 * s + (i > page ? 14 * s : 0);
            return new RectangleF(x, 505 * s, i == page ? 22 * s : 8 * s, 8 * s);
        }

        void SetPage(int p, int d)
        {
            page = Math.Max(0, Math.Min(GuideArt.Pages - 1, p));
            dir = d;
            if (illus != null) illus.Dispose();
            illus = RenderIllustration(page, t, s);
            animT = 0;
            anim.Start();
            Invalidate();
        }

        public static Bitmap RenderIllustration(int page, Theme t, float s)
        {
            Bitmap b = new Bitmap((int)(496 * s), (int)(250 * s), PixelFormat.Format32bppArgb);
            b.SetResolution(96 * s, 96 * s);    // so text inside the picture scales with the display
            using (Graphics g = Art.HQ(b))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                GuideArt.Draw(g, page, new RectangleF(0, 0, b.Width, b.Height), t, s);
            }
            return b;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintAll(e.Graphics, page, t, s, illus, animT, dir, hover);
        }

        /// <summary>Paints the whole window (static so the build can render README previews).</summary>
        public static void PaintAll(Graphics g, int page, Theme t, float s, Bitmap illus, double animT, int dir, int hover)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(t.Bg);

            // illustration card
            RectangleF ir = new RectangleF(32 * s, 28 * s, 496 * s, 250 * s);
            using (GraphicsPath cp = Art.RoundRect(ir, 18 * s))
            using (SolidBrush cb = new SolidBrush(t.Card))
                g.FillPath(cb, cp);
            if (illus != null)
            {
                double e = Art.EaseOut(animT);
                float off = (float)((1 - e) * 26 * s * dir);
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ColorMatrix cm = new ColorMatrix();
                    cm.Matrix33 = (float)e;
                    ia.SetColorMatrix(cm);
                    Region old = g.Clip;
                    using (GraphicsPath cp = Art.RoundRect(ir, 18 * s)) g.SetClip(cp);
                    g.DrawImage(illus, new Rectangle((int)(ir.X + off), (int)ir.Y, illus.Width, illus.Height),
                        0, 0, illus.Width, illus.Height, GraphicsUnit.Pixel, ia);
                    g.Clip = old;
                }
            }

            // step, title and text
            using (Font fs = new Font("Segoe UI", 9f, FontStyle.Bold))
                TextRenderer.DrawText(g, string.Format("STEP {0} OF {1}", page + 1, GuideArt.Pages), fs,
                    new Point((int)(32 * s), (int)(298 * s)), t.Accent);
            using (Font ft = new Font("Segoe UI Semibold", 17f))
                TextRenderer.DrawText(g, GuideArt.Titles[page], ft, new Point((int)(30 * s), (int)(318 * s)), t.Text);
            using (Font fb = new Font("Segoe UI", 10.5f))
                TextRenderer.DrawText(g, GuideArt.Bodies[page], fb,
                    new Rectangle((int)(32 * s), (int)(362 * s), (int)(496 * s), (int)(116 * s)), t.Sub,
                    TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top);

            // dots
            float total = (GuideArt.Pages - 1) * 16 * s + 22 * s;
            for (int i = 0; i < GuideArt.Pages; i++)
            {
                float x = (W * s - total) / 2 + i * 16 * s + (i > page ? 14 * s : 0);
                RectangleF d = new RectangleF(x, 505 * s, i == page ? 22 * s : 8 * s, 8 * s);
                using (GraphicsPath dp = Art.RoundRect(d, 4 * s))
                using (SolidBrush db = new SolidBrush(i == page ? t.Accent : (hover == 10 + i ? t.Sub : t.Border)))
                    g.FillPath(db, dp);
            }

            // buttons
            using (Font bf = new Font("Segoe UI Semibold", 10f))
            {
                if (page > 0)
                {
                    Rectangle br = new Rectangle((int)(32 * s), (int)(488 * s), (int)(110 * s), (int)(42 * s));
                    using (GraphicsPath bp = Art.RoundRect(br, 10 * s))
                    {
                        if (hover == 0) using (SolidBrush hb = new SolidBrush(t.Card)) g.FillPath(hb, bp);
                        using (Pen pen = new Pen(t.Border, 1.2f * s)) g.DrawPath(pen, bp);
                    }
                    TextRenderer.DrawText(g, "Back", bf, br, t.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                Rectangle nr = new Rectangle((int)(418 * s), (int)(488 * s), (int)(110 * s), (int)(42 * s));
                using (GraphicsPath np = Art.RoundRect(nr, 10 * s))
                using (SolidBrush nb = new SolidBrush(hover == 1 ? t.AccentHover : t.Accent))
                    g.FillPath(nb, np);
                TextRenderer.DrawText(g, page == GuideArt.Pages - 1 ? "Get started" : "Next", bf, nr, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        int HitTest(Point p)
        {
            if (page > 0 && BackRect.Contains(p)) return 0;
            if (NextRect.Contains(p)) return 1;
            for (int i = 0; i < GuideArt.Pages; i++)
            {
                RectangleF d = DotRect(i);
                d.Inflate(5 * s, 8 * s);
                if (d.Contains(p)) return 10 + i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hover != -1) { hover = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h == 0) Prev();
            else if (h == 1) Next();
            else if (h >= 10 && h - 10 != page) SetPage(h - 10, h - 10 > page ? 1 : -1);
            base.OnMouseClick(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Right || keyData == Keys.Enter) { Next(); return true; }
            if (keyData == Keys.Left) { Prev(); return true; }
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void Next() { if (page == GuideArt.Pages - 1) Close(); else SetPage(page + 1, 1); }
        void Prev() { if (page > 0) SetPage(page - 1, -1); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { anim.Dispose(); if (illus != null) illus.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
