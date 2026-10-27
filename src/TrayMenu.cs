using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LidFlip
{
    /// <summary>One row of the tray menu.</summary>
    class MenuRow
    {
        public string Text;
        public int Glyph;              // see TrayMenu.DrawGlyph
        public bool IsToggle, IsSeparator;
        public Func<bool> Get;         // toggle state
        public System.Action Do;              // click action
        public bool On;
        public float Knob;             // 0..1 animated switch position
        public float Hover;            // 0..1 animated highlight
        public RectangleF Rect;

        public static MenuRow Switch(string text, int glyph, Func<bool> get, System.Action act)
        {
            MenuRow r = new MenuRow(); r.Text = text; r.Glyph = glyph; r.IsToggle = true; r.Get = get; r.Do = act;
            r.On = SafeGet(get); r.Knob = r.On ? 1 : 0; return r;
        }
        public static MenuRow Command(string text, int glyph, System.Action act)
        {
            MenuRow r = new MenuRow(); r.Text = text; r.Glyph = glyph; r.Do = act; return r;
        }
        public static MenuRow Separator() { MenuRow r = new MenuRow(); r.IsSeparator = true; return r; }

        public static bool SafeGet(Func<bool> f) { try { return f(); } catch { return false; } }
    }

    /// <summary>
    /// A modern, custom-drawn tray menu: header with the live state, toggle switches,
    /// icons, rounded corners, and animations (open/close, hover, switches, header icon).
    /// </summary>
    class TrayMenu : Form
    {
        public const int GlyphNone = 0, GlyphBolt = 1, GlyphStartup = 2, GlyphPin = 3, GlyphHelp = 4, GlyphExit = 5;

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        readonly Theme t = Theme.ForTaskbar();
        readonly float s;
        readonly List<MenuRow> rows;
        readonly Func<bool> getAwake;
        readonly Timer timer = new Timer();
        bool awake;
        Bitmap headerTile;
        List<Bitmap> headerFrames; int headerIdx, headerTicket;
        double openT; bool closing;
        int targetY, hot = -1;
        Font fTitle, fState, fItem, fIcon;

        public const int Width0 = 288;

        public TrayMenu(Func<bool> getAwake, List<MenuRow> rows)
        {
            this.getAwake = getAwake;
            this.rows = rows;
            using (Graphics g = CreateGraphics()) s = g.DpiX / 96f;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = t.MenuBg;
            fTitle = Fonts.Title(10.5f);
            fState = Fonts.Text(9f);
            fItem = Fonts.Text(10f);
            fIcon = Fonts.Icons(11f);       // null when no icon font is installed

            awake = MenuRow.SafeGet(getAwake);
            headerTile = Art.Still(TilePx, awake);
            ClientSize = new Size((int)(Width0 * s), (int)LayoutRows(rows, s));

            timer.Interval = 15;
            timer.Tick += delegate { Step(); };
        }

        int TilePx { get { return (int)(36 * s); } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80;          // WS_EX_TOOLWINDOW: no taskbar button, no Alt+Tab
                cp.ClassStyle |= 0x20000;    // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int round = 2;   // DWMWCP_ROUND (Windows 11)
            bool ok = false;
            try { ok = DwmSetWindowAttribute(Handle, 33, ref round, 4) == 0; } catch { }
            if (!ok)        // Windows 10: clip to a rounded shape ourselves
                using (GraphicsPath p = Art.RoundRect(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 8 * s))
                    Region = new Region(p);
            if (t.Dark)
            {
                int one = 1;
                try { DwmSetWindowAttribute(Handle, 20, ref one, 4); } catch { }
            }
        }

        // ---------- layout & painting (static so previews and the guide can render it) ----------
        // Windows 11 menu metrics: 4px outer padding, ~36px rows, 4px hover radius, 16px icons.
        public const float HeaderH = 60, RowH = 36, SepH = 9, Pad = 4;

        public static float LayoutRows(List<MenuRow> rows, float s)
        {
            float y = (Pad + HeaderH + SepH) * s;
            foreach (MenuRow r in rows)
            {
                float h = (r.IsSeparator ? SepH : RowH) * s;
                r.Rect = new RectangleF(Pad * s, y, (Width0 - 2 * Pad) * s, h);
                y += h;
            }
            return y + Pad * s;
        }

        public static void PaintMenu(Graphics g, Theme t, float s, Size client, Bitmap tile, bool awake,
                                     List<MenuRow> rows, Font fTitle, Font fState, Font fItem, Font fIcon, bool drawFrame)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(t.MenuBg);
            if (drawFrame)
                using (GraphicsPath fp = Art.RoundRect(new RectangleF(0.5f, 0.5f, client.Width - 1, client.Height - 1), 8 * s))
                using (Pen pen = new Pen(t.Border, 1)) g.DrawPath(pen, fp);

            // header: live tile + name + state
            float hy = Pad * s;
            if (tile != null) g.DrawImage(tile, 14 * s, hy + 12 * s, 36 * s, 36 * s);
            TextRenderer.DrawText(g, "LidFlip", fTitle, new Point((int)(60 * s), (int)(hy + 11 * s)), t.Text, TextFormatFlags.NoPadding);
            using (SolidBrush db = new SolidBrush(awake ? Art.AwakeTop : Art.SleepTop))
                g.FillEllipse(db, 61 * s, hy + 37 * s, 6 * s, 6 * s);
            TextRenderer.DrawText(g, awake ? "Lid closed: stays awake" : "Lid closed: sleeps", fState,
                new Point((int)(72 * s), (int)(hy + 32 * s)), t.Sub, TextFormatFlags.NoPadding);

            float sepY = (Pad + HeaderH + SepH / 2) * s;
            using (Pen sp = new Pen(t.Border, 1)) g.DrawLine(sp, 0, sepY, client.Width, sepY);

            foreach (MenuRow r in rows)
            {
                if (r.IsSeparator)
                {
                    float y = r.Rect.Y + r.Rect.Height / 2;
                    using (Pen sp = new Pen(t.Border, 1)) g.DrawLine(sp, 0, y, client.Width, y);
                    continue;
                }
                RectangleF item = new RectangleF(r.Rect.X, r.Rect.Y + 2 * s, r.Rect.Width, r.Rect.Height - 4 * s);
                if (r.Hover > 0.01f)
                    using (GraphicsPath hp = Art.RoundRect(item, 4 * s))
                    using (SolidBrush hb = new SolidBrush(Color.FromArgb((int)(255 * r.Hover), t.MenuHi)))
                        g.FillPath(hb, hp);

                float cy = r.Rect.Y + r.Rect.Height / 2;
                DrawGlyph(g, r.Glyph, new PointF(r.Rect.X + 20 * s, cy), s, t.Text, fIcon);

                TextRenderer.DrawText(g, r.Text, fItem,
                    new Rectangle((int)(r.Rect.X + 42 * s), (int)r.Rect.Y, (int)(r.Rect.Width - 100 * s), (int)r.Rect.Height),
                    t.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

                if (r.IsToggle) DrawSwitch(g, new RectangleF(r.Rect.Right - 52 * s, cy - 10 * s, 40 * s, 20 * s), r.Knob, r.Hover, t, s);
            }
        }

        /// <summary>Windows 11 ToggleSwitch: 40x20 track, 12px knob (14px on hover).</summary>
        static void DrawSwitch(Graphics g, RectangleF tr, float k, float hover, Theme t, float s)
        {
            double e = Art.EaseInOut(k);
            using (GraphicsPath p = Art.RoundRect(tr, tr.Height / 2))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * e), t.Accent))) g.FillPath(b, p);
                if (e < 0.999)
                    using (Pen pen = new Pen(Color.FromArgb((int)(255 * (1 - e)), t.Stroke), 1f * s)) g.DrawPath(pen, p);
            }
            float d = (12 + 2 * hover) * s;
            float x0 = tr.X + 4 * s + 6 * s, x1 = tr.Right - 4 * s - 6 * s;     // knob centre travel
            float cx = (float)(x0 + (x1 - x0) * e);
            using (SolidBrush kb = new SolidBrush(Art.Mix(t.Stroke, t.OnAccent, e)))
                g.FillEllipse(kb, cx - d / 2, tr.Y + (tr.Height - d) / 2, d, d);
        }

        static char GlyphChar(int glyph)
        {
            switch (glyph)
            {
                case GlyphBolt: return Fonts.LightningBolt;
                case GlyphStartup: return Fonts.Play;
                case GlyphPin: return Fonts.Pin;
                case GlyphHelp: return Fonts.Help;
                case GlyphExit: return Fonts.Power;
            }
            return ' ';
        }

        /// <summary>Icons from Segoe Fluent Icons (Windows 11) / MDL2 (Windows 10); drawn by hand if neither exists.</summary>
        public static void DrawGlyph(Graphics g, int glyph, PointF c, float s, Color col, Font iconFont)
        {
            if (glyph == GlyphNone) return;
            if (iconFont != null)
            {
                Rectangle r = new Rectangle((int)(c.X - 12 * s), (int)(c.Y - 12 * s), (int)(24 * s), (int)(24 * s));
                TextRenderer.DrawText(g, GlyphChar(glyph).ToString(), iconFont, r, col,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                return;
            }
            using (Pen p = new Pen(col, 1.3f * s))
            using (SolidBrush b = new SolidBrush(col))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                float x = c.X, y = c.Y, u = s * 0.9f;
                switch (glyph)
                {
                    case GlyphBolt:
                        g.DrawPolygon(p, new PointF[] {
                            new PointF(x + 1.5f * u, y - 8 * u), new PointF(x - 5 * u, y + 1 * u), new PointF(x - 0.5f * u, y + 1 * u),
                            new PointF(x - 1.5f * u, y + 8 * u), new PointF(x + 5 * u, y - 1.5f * u), new PointF(x + 0.5f * u, y - 1.5f * u) });
                        break;
                    case GlyphStartup:
                        g.DrawPolygon(p, new PointF[] { new PointF(x - 4 * u, y - 6.5f * u), new PointF(x + 6 * u, y), new PointF(x - 4 * u, y + 6.5f * u) });
                        break;
                    case GlyphPin:
                        g.DrawEllipse(p, x - 4.5f * u, y - 8 * u, 9 * u, 9 * u);
                        g.DrawLine(p, x - 7 * u, y + 1 * u, x + 7 * u, y + 1 * u);
                        g.DrawLine(p, x, y + 1 * u, x, y + 8 * u);
                        break;
                    case GlyphHelp:
                        g.DrawEllipse(p, x - 8 * u, y - 8 * u, 16 * u, 16 * u);
                        g.DrawArc(p, x - 2.8f * u, y - 5 * u, 5.6f * u, 5.6f * u, 180, 225);
                        g.DrawLine(p, x + 0.4f * u, y + 0.4f * u, x, y + 1.6f * u);
                        g.FillEllipse(b, x - 1.1f * u, y + 3.6f * u, 2.2f * u, 2.2f * u);
                        break;
                    case GlyphExit:
                        g.DrawArc(p, x - 7 * u, y - 6.5f * u, 14 * u, 14 * u, -55, 290);
                        g.DrawLine(p, x, y - 8 * u, x, y - 1 * u);
                        break;
                }
            }
        }

        /// <summary>Renders the menu as a picture (used by the guide and the README images).</summary>
        public static Bitmap RenderPicture(Theme t, float s, int highlight, bool awake, bool startup, bool pin)
        {
            List<MenuRow> rows = StandardRows(awake, startup, pin);
            if (highlight >= 0 && highlight < rows.Count) rows[highlight].Hover = 1;
            int w = (int)(Width0 * s), h = (int)LayoutRows(rows, s);
            Bitmap b = new Bitmap(w, h);
            b.SetResolution(96 * s, 96 * s);
            using (Graphics g = Graphics.FromImage(b))
            using (Bitmap tile = Art.Still((int)(36 * s), awake))
            using (Font f1 = Fonts.Title(10.5f)) using (Font f2 = Fonts.Text(9f)) using (Font f3 = Fonts.Text(10f))
            {
                Font fi = Fonts.Icons(11f);
                PaintMenu(g, t, s, new Size(w, h), tile, awake, rows, f1, f2, f3, fi, true);
                if (fi != null) fi.Dispose();
            }
            return b;
        }

        static List<MenuRow> StandardRows(bool awake, bool startup, bool pin)
        {
            List<MenuRow> rows = new List<MenuRow>();
            rows.Add(MenuRow.Switch("Stay awake when lid is closed", GlyphBolt, delegate { return awake; }, null));
            rows.Add(MenuRow.Switch("Start with Windows", GlyphStartup, delegate { return startup; }, null));
            rows.Add(MenuRow.Switch("Pin next to clock", GlyphPin, delegate { return pin; }, null));
            rows.Add(MenuRow.Separator());
            rows.Add(MenuRow.Command("How LidFlip works", GlyphHelp, null));
            rows.Add(MenuRow.Command("Exit", GlyphExit, null));
            return rows;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Bitmap tile = headerFrames != null && headerIdx > 0 ? headerFrames[Math.Min(headerIdx, headerFrames.Count) - 1] : headerTile;
            PaintMenu(e.Graphics, t, s, ClientSize, tile, awake, rows, fTitle, fState, fItem, fIcon, Region != null);
        }

        // ---------- showing / closing ----------
        public void ShowNear(Point cursor)
        {
            Rectangle wa = Screen.FromPoint(cursor).WorkingArea;
            int x = Math.Max(wa.Left + 4, Math.Min(cursor.X - Width + (int)(28 * s), wa.Right - Width - 4));
            int y = cursor.Y - Height - (int)(10 * s);
            if (y < wa.Top) y = cursor.Y + (int)(10 * s);
            y = Math.Max(wa.Top + 4, Math.Min(y, wa.Bottom - Height - 4));
            targetY = y;
            Location = new Point(x, y + (int)(12 * s));
            Opacity = 0;
            Show();
            Activate();
            openT = 0;
            timer.Start();
        }

        public void BeginClose()
        {
            if (closing) return;
            closing = true;
            timer.Start();
        }

        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); BeginClose(); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { BeginClose(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // one timer drives every animation; it stops itself when nothing is moving
        void Step()
        {
            bool busy = false;

            if (closing)
            {
                openT = Math.Max(0, openT - 0.16);
                Opacity = Art.EaseOut(openT);
                if (openT <= 0) { timer.Stop(); Close(); return; }
                busy = true;
            }
            else if (openT < 1)
            {
                openT = Math.Min(1, openT + 0.11);
                double e = Art.EaseOut(openT);
                Opacity = e;
                Top = targetY + (int)((1 - e) * 12 * s);
                busy = true;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                MenuRow r = rows[i];
                float goal = i == hot ? 1 : 0;
                if (Math.Abs(r.Hover - goal) > 0.01f) { r.Hover += (goal - r.Hover) * 0.35f; busy = true; } else r.Hover = goal;
                float kg = r.On ? 1 : 0;
                if (Math.Abs(r.Knob - kg) > 0.01f) { r.Knob += Math.Sign(kg - r.Knob) * 0.11f; r.Knob = Math.Max(0, Math.Min(1, r.Knob)); busy = true; } else r.Knob = kg;
            }

            if (headerFrames != null)
            {
                if (headerIdx < headerFrames.Count) { headerIdx++; busy = true; }
                else
                {
                    if (headerTile != null) headerTile.Dispose();
                    headerTile = Art.Still(TilePx, awake);   // matches the last frame
                    foreach (Bitmap b in headerFrames) b.Dispose();
                    headerFrames = null; headerIdx = 0;
                }
            }

            Invalidate();
            if (!busy) timer.Stop();
        }

        int RowAt(Point p)
        {
            for (int i = 0; i < rows.Count; i++)
                if (!rows[i].IsSeparator && rows[i].Rect.Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = RowAt(e.Location);
            if (h != hot) { hot = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; timer.Start(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot = -1; timer.Start();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = RowAt(e.Location);
            if (i < 0 || e.Button != MouseButtons.Left) return;
            MenuRow r = rows[i];
            if (r.Do != null) r.Do();
            if (IsDisposed || closing) return;
            if (r.IsToggle) Resync();
            else BeginClose();
        }

        /// <summary>Re-reads every switch and the header state, and animates whatever changed.</summary>
        public void Resync()
        {
            if (IsDisposed) return;
            foreach (MenuRow x in rows) if (x.IsToggle) x.On = MenuRow.SafeGet(x.Get);
            bool nowAwake = MenuRow.SafeGet(getAwake);
            if (nowAwake != awake)
            {
                awake = nowAwake;
                // draw the header animation frames off the UI thread, so the switch starts moving at once
                bool target = awake; int px = TilePx; int ticket = ++headerTicket;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    List<Bitmap> frames = Art.Transition(px, target);
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (IsDisposed || ticket != headerTicket) { foreach (Bitmap b in frames) b.Dispose(); return; }
                            if (headerFrames != null) foreach (Bitmap b in headerFrames) b.Dispose();
                            headerFrames = frames; headerIdx = 0;
                            timer.Start();
                        });
                    }
                    catch { foreach (Bitmap b in frames) b.Dispose(); }   // menu already closed
                });
            }
            timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                if (headerTile != null) headerTile.Dispose();
                if (headerFrames != null) foreach (Bitmap b in headerFrames) b.Dispose();
                fTitle.Dispose(); fState.Dispose(); fItem.Dispose();
                if (fIcon != null) fIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
