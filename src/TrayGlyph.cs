using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LidFlip
{
    /// <summary>
    /// The tray icon in the Windows 11 style: a one-colour line glyph (white on a dark
    /// taskbar, near-black on a light one) — a half-shut laptop seen from the side, with
    /// a moon (sleeps) or a lightning bolt in the accent colour (stays awake).
    /// Designed on a 24-unit grid, like Segoe Fluent Icons.
    /// </summary>
    static class TrayGlyph
    {
        public const float LidRest = 22f;

        // geometry (24-unit grid)
        const float HingeX = 3f, HingeY = 16.6f, LidLen = 12f, LidW = 1.7f;
        static readonly RectangleF BaseRect = new RectangleF(1.5f, 18.5f, 21f, 2.5f);
        static readonly PointF BadgeAt = new PointF(18f, 5.5f);

        static readonly PointF[] Moon = Crescent(4.6, 2.6, -2.2, 3.9);
        static readonly PointF[] Bolt = {          // a little larger than the moon's box, so it reads at 16 px
            new PointF(1.0f, -5.6f), new PointF(-3.4f, 0.8f), new PointF(-0.4f, 0.8f),
            new PointF(-1.1f, 5.6f), new PointF(3.4f, -1.1f), new PointF(0.4f, -1.1f) };

        // a gentle overshoot: the badge pops without growing into the lid
        static double Pop(double t) { const double c1 = 0.9, c3 = c1 + 1; return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2); }

        /// <summary>Glyph and accent colours for the current taskbar (Windows mode light/dark).</summary>
        public static void Colors(out Color fg, out Color accent)
        {
            Theme t = Theme.ForTaskbar();
            fg = t.Dark ? Color.White : Color.FromArgb(255, 0x1A, 0x1A, 0x1A);
            accent = t.Accent;
        }

        /// <summary>
        /// A crescent as one polygon (so it is antialiased): the outer circle minus a second
        /// circle offset by (ox, oy) — both arcs sampled every degree and joined at the tips.
        /// </summary>
        static PointF[] Crescent(double r1, double ox, double oy, double r2)
        {
            List<PointF> outer = new List<PointF>(), inner = new List<PointF>();
            double away = Math.Atan2(-oy, -ox);                // direction pointing away from the cut-out
            for (int i = -180; i <= 180; i++)
            {
                double a = away + i * Math.PI / 180;
                double x = r1 * Math.Cos(a), y = r1 * Math.Sin(a);
                if (Math.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy)) >= r2) outer.Add(new PointF((float)x, (float)y));
            }
            for (int i = -180; i <= 180; i++)
            {
                double a = away + i * Math.PI / 180;           // side of the cut-out that faces the moon's centre
                double x = ox + r2 * Math.Cos(a), y = oy + r2 * Math.Sin(a);
                if (Math.Sqrt(x * x + y * y) <= r1) inner.Add(new PointF((float)x, (float)y));
            }
            PointF end = outer[outer.Count - 1];
            PointF i0 = inner[0], i1 = inner[inner.Count - 1];
            double d0 = (i0.X - end.X) * (i0.X - end.X) + (i0.Y - end.Y) * (i0.Y - end.Y);
            double d1 = (i1.X - end.X) * (i1.X - end.X) + (i1.Y - end.Y) * (i1.Y - end.Y);
            if (d1 < d0) inner.Reverse();
            outer.AddRange(inner);
            return outer.ToArray();
        }

        /// <summary>Draws the glyph into g, in a 24x24 box at the origin of g's current transform.</summary>
        public static void Draw(Graphics g, Color fg, Color accent, float scale, float lid,
                                bool awakeBadge, float badgeScale, float badgeRot, float ring)
        {
            if (scale <= 0.02f) return;
            GraphicsState st = g.Save();
            g.TranslateTransform(12, 12);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-12, -12);

            using (SolidBrush b = new SolidBrush(fg))
            using (GraphicsPath basePath = Art.RoundRect(BaseRect, 1.25f))
                g.FillPath(b, basePath);

            using (Pen p = new Pen(fg, LidW))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                double a = lid * Math.PI / 180;
                g.DrawLine(p, HingeX, HingeY, (float)(HingeX + LidLen * Math.Cos(a)), (float)(HingeY - LidLen * Math.Sin(a)));
            }

            Color badgeColor = awakeBadge ? accent : fg;
            if (ring > 0 && ring < 1)                                    // a small ring of light around the new badge
                using (Pen rp = new Pen(Color.FromArgb((int)(140 * (1 - ring)), badgeColor), 0.9f * (1 - ring) + 0.3f))
                {
                    float r = 3.2f + 2.2f * ring;           // stays inside the 24-unit box
                    g.DrawEllipse(rp, BadgeAt.X - r, BadgeAt.Y - r, 2 * r, 2 * r);
                }

            if (badgeScale > 0.02f)
            {
                GraphicsState bs = g.Save();
                g.TranslateTransform(BadgeAt.X, BadgeAt.Y);
                g.RotateTransform(badgeRot);
                g.ScaleTransform(badgeScale, badgeScale);
                using (SolidBrush bb = new SolidBrush(badgeColor))
                    g.FillPolygon(bb, awakeBadge ? Bolt : Moon);
                g.Restore(bs);
            }
            g.Restore(st);
        }

        /// <summary>Renders the glyph at a pixel size (supersampled, then downscaled).</summary>
        public static Bitmap Render(int px, Color fg, Color accent, float scale, float lid,
                                    bool awakeBadge, float badgeScale, float badgeRot, float ring)
        {
            int big = Math.Max(48, px * 4);
            using (Bitmap hi = new Bitmap(big, big, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Art.HQ(hi))
                {
                    g.Clear(Color.Transparent);
                    g.ScaleTransform(big / 24f, big / 24f);
                    Draw(g, fg, accent, scale, lid, awakeBadge, badgeScale, badgeRot, ring);
                }
                Bitmap outB = new Bitmap(px, px, PixelFormat.Format32bppArgb);
                using (Graphics g2 = Art.HQ(outB))
                {
                    g2.Clear(Color.Transparent);
                    g2.DrawImage(hi, new Rectangle(0, 0, px, px));
                }
                return outB;
            }
        }

        public static Bitmap Still(int px, bool awake, Color fg, Color accent)
        {
            return Render(px, fg, accent, 1, LidRest, awake, 1, 0, 0);
        }

        // The badge meshes with the lid like a gear: every degree the lid moves turns the badge this many
        // degrees the opposite way (lid up = badge clockwise, lid down = badge counter-clockwise).
        const float Gear = 3.5f;
        const float Swing = 40f;     // how far the lid opens above its resting angle during a flip

        /// <summary>
        /// Flip, in sync with the lid: as the lid rises the old badge turns with it and shrinks away;
        /// at the top the badges swap; as the lid comes down the new one turns back with it and grows in.
        /// </summary>
        public static List<Bitmap> Transition(int px, bool toAwake, Color fg, Color accent)
        {
            const int N = 24;
            List<Bitmap> frames = new List<Bitmap>();
            for (int i = 1; i <= N; i++)
            {
                double t = (double)i / N;
                double lift = t < 0.8 ? Math.Sin(Math.PI * t / 0.8) : 0.14 * Math.Sin(Math.PI * (t - 0.8) / 0.2);
                float lid = (float)(LidRest + Swing * lift);
                bool rising = t < 0.4;                                  // the lid peaks at t = 0.4
                bool badgeAwake = rising ? !toAwake : toAwake;
                float bs = (float)Math.Max(0, 1 - lift);                // fully gone at the top, back as the lid lands
                float br = Gear * Swing * (float)lift;                  // meshes with the lid like a gear: lid up = badge clockwise
                float ring = t > 0.6 ? (float)((t - 0.6) / 0.4) : 0;
                frames.Add(Render(px, fg, accent, 1, lid, badgeAwake, bs, br, ring));
            }
            return frames;
        }

        /// <summary>Start-up: the laptop pops in, the lid swings up, and the badge rides in as it comes down.</summary>
        public static List<Bitmap> Intro(int px, bool awake, Color fg, Color accent)
        {
            const int N = 22;
            List<Bitmap> frames = new List<Bitmap>();
            for (int i = 1; i <= N; i++)
            {
                double t = (double)i / N;
                float scale = (float)Math.Max(0, Math.Min(1.06, Art.EaseBack(Math.Min(1, t / 0.45))));
                double lift = Math.Sin(Math.PI * Math.Min(1, t / 0.9));                 // up and back down
                double open = Art.EaseOut(Math.Min(1, t / 0.3));                        // from shut to resting
                float lid = (float)(LidRest * open + Swing * lift);
                bool down = t > 0.45;                                                    // past the top
                float bs = down ? (float)Math.Max(0, 1 - lift) : 0;
                float br = Gear * Swing * (float)lift;
                float ring = t > 0.65 ? (float)((t - 0.65) / 0.35) : 0;
                frames.Add(Render(px, fg, accent, scale, lid, awake, bs, br, ring));
            }
            return frames;
        }

        /// <summary>Exit: the lid shuts and carries the badge away with it, then the glyph shrinks away.</summary>
        public static List<Bitmap> Outro(int px, bool awake, Color fg, Color accent)
        {
            const int N = 18;
            List<Bitmap> frames = new List<Bitmap>();
            for (int i = 1; i <= N; i++)
            {
                double t = (double)i / N;
                double shut = Art.EaseInOut(Math.Min(1, t / 0.55));                     // 0 = resting, 1 = closed
                float lid = (float)(LidRest * (1 - shut));
                float bs = (float)(1 - shut);
                float br = -Gear * LidRest * (float)shut;                               // lid closing = badge counter-clockwise
                float scale = t < 0.55 ? (float)(1 + 0.05 * Math.Sin(Math.PI * t / 0.55)) : (float)(1 - Art.EaseInOut((t - 0.55) / 0.45));
                frames.Add(Render(px, fg, accent, scale, lid, awake, bs, br, 0));
            }
            return frames;
        }
    }
}
