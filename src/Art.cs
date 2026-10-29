using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LidFlip
{
    /// <summary>
    /// Draws the LidFlip tile: a gradient rounded square with a half-shut laptop
    /// (side view) and a badge — moon = sleeps, lightning bolt = stays awake.
    /// Everything is designed on a 256-unit canvas and rendered at any size.
    /// </summary>
    static class Art
    {
        public static readonly Color AwakeTop = ColorTranslator.FromHtml("#34D399");
        public static readonly Color AwakeBot = ColorTranslator.FromHtml("#047857");
        public static readonly Color SleepTop = ColorTranslator.FromHtml("#818CF8");
        public static readonly Color SleepBot = ColorTranslator.FromHtml("#3730A3");
        public const float LidRest = 24f;       // resting lid angle: half-shut reads as "closing the lid"

        static readonly PointF[] Moon = BuildCrescent();
        static readonly PointF[] Bolt = {
            new PointF(10, -48), new PointF(-28, 8), new PointF(-4, 8),
            new PointF(-12, 48), new PointF(28, -10), new PointF(4, -10) };

        // ---------- helpers ----------
        public static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Graphics HQ(Image img)
        {
            Graphics g = Graphics.FromImage(img);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            return g;
        }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb(255,
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static double EaseInOut(double t)
        {
            return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        public static double EaseOut(double t) { return 1 - Math.Pow(1 - t, 3); }

        public static double EaseBack(double t)
        {
            const double c1 = 1.9, c3 = c1 + 1;
            return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
        }

        // Crescent = points of the outer circle that lie outside the inner circle,
        // joined to the points of the inner circle that lie inside the outer one.
        static PointF[] BuildCrescent()
        {
            const double r1 = 42, cx2 = 20, cy2 = -20, r2 = 40;
            List<PointF> outer = new List<PointF>(), inner = new List<PointF>();
            // walk the outer circle starting from a point that is surely outside the cut-out
            for (int i = 0; i < 360; i++)
            {
                double a = (135 + i) * Math.PI / 180;
                double x = r1 * Math.Cos(a), y = r1 * Math.Sin(a);
                if (Math.Sqrt((x - cx2) * (x - cx2) + (y - cy2) * (y - cy2)) >= r2) outer.Add(new PointF((float)x, (float)y));
                else break;
            }
            for (int i = 359; i > 0; i--)   // and backwards, to get the arc in one contiguous piece
            {
                double a = (135 + i) * Math.PI / 180;
                double x = r1 * Math.Cos(a), y = r1 * Math.Sin(a);
                if (Math.Sqrt((x - cx2) * (x - cx2) + (y - cy2) * (y - cy2)) >= r2) outer.Insert(0, new PointF((float)x, (float)y));
                else break;
            }
            for (int i = 0; i < 360; i++)
            {
                double a = i * Math.PI / 180;
                double x = cx2 + r2 * Math.Cos(a), y = cy2 + r2 * Math.Sin(a);
                if (Math.Sqrt(x * x + y * y) <= r1) inner.Add(new PointF((float)x, (float)y));
            }
            // inner points may wrap around 0 deg: rotate so they are contiguous
            int split = 0;
            for (int i = 1; i < inner.Count; i++)
            {
                double dx = inner[i].X - inner[i - 1].X, dy = inner[i].Y - inner[i - 1].Y;
                if (dx * dx + dy * dy > 16) { split = i; break; }
            }
            List<PointF> innerOrdered = new List<PointF>();
            for (int i = 0; i < inner.Count; i++) innerOrdered.Add(inner[(split + i) % inner.Count]);
            // connect outer's end to the nearest end of the inner arc
            PointF end = outer[outer.Count - 1];
            PointF i0 = innerOrdered[0], i1 = innerOrdered[innerOrdered.Count - 1];
            double d0 = (i0.X - end.X) * (i0.X - end.X) + (i0.Y - end.Y) * (i0.Y - end.Y);
            double d1 = (i1.X - end.X) * (i1.X - end.X) + (i1.Y - end.Y) * (i1.Y - end.Y);
            if (d1 < d0) innerOrdered.Reverse();
            List<PointF> all = new List<PointF>(outer);
            all.AddRange(innerOrdered);
            return all.ToArray();
        }

        // ---------- the tile ----------
        /// <summary>Draws one tile into g, in a 256x256 box at the origin of g's current transform.</summary>
        static void DrawTile(Graphics g, Color top, Color bot, float tileScale, float lid,
                             bool awakeBadge, float badgeScale, float badgeRot, float ripple)
        {
            float size = 236 * tileScale, off = (256 - size) / 2;
            if (size < 3) return;                                   // nothing visible (start/exit animations)
            RectangleF tr = new RectangleF(off, off, size, size);
            using (GraphicsPath tile = RoundRect(tr, 70 * tileScale))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(tr, top, bot, 90f))
                    g.FillPath(b, tile);

                Region oldClip = g.Clip;
                g.SetClip(tile, CombineMode.Intersect);
                RectangleF hr = new RectangleF(off, off, size, size / 2 + 1);
                using (LinearGradientBrush hb = new LinearGradientBrush(hr,
                    Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                    g.FillRectangle(hb, hr);                                    // glass highlight

                if (ripple > 0 && ripple < 1)                                    // light ripple
                {
                    float rr = 20 + 200 * ripple;
                    using (Pen rp = new Pen(Color.FromArgb((int)(150 * (1 - ripple)), 255, 255, 255), 26 * (1 - ripple) + 4))
                        g.DrawEllipse(rp, 128 - rr, 128 - rr, 2 * rr, 2 * rr);
                }
                g.Clip = oldClip;
            }

            // the laptop and badge scale with the tile (squish, start-up and exit animations)
            GraphicsState glyphState = g.Save();
            g.TranslateTransform(128, 128);
            g.ScaleTransform(tileScale, tileScale);
            g.TranslateTransform(-128, -128);
            using (SolidBrush white = new SolidBrush(Color.White))
            using (SolidBrush notch = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
            using (Pen lidPen = new Pen(Color.White, 18))
            {
                // base (keyboard) with a little front notch, and the hinged lid
                using (GraphicsPath basePath = RoundRect(new RectangleF(36, 178, 184, 26), 12)) g.FillPath(white, basePath);
                using (GraphicsPath n = RoundRect(new RectangleF(108, 192, 40, 8), 4)) g.FillPath(notch, n);
                lidPen.StartCap = LineCap.Round; lidPen.EndCap = LineCap.Round;
                double a = lid * Math.PI / 180;
                g.DrawLine(lidPen, 50, 166, (float)(50 + 116 * Math.Cos(a)), (float)(166 - 116 * Math.Sin(a)));

                if (badgeScale > 0.02f)
                {
                    GraphicsState st = g.Save();
                    g.TranslateTransform(182, 68);
                    g.RotateTransform(badgeRot);
                    g.ScaleTransform(badgeScale * 0.85f, badgeScale * 0.85f);
                    g.FillPolygon(white, awakeBadge ? Bolt : Moon);
                    g.Restore(st);
                }
            }
            g.Restore(glyphState);
        }

        /// <summary>Renders a tile at any pixel size (supersampled, then downscaled).</summary>
        public static Bitmap Tile(int px, Color top, Color bot, float tileScale, float lid,
                                  bool awakeBadge, float badgeScale, float badgeRot, float ripple)
        {
            int big = Math.Max(48, px * 3);     // 3x supersampling is plenty and keeps animations cheap
            using (Bitmap hi = new Bitmap(big, big, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = HQ(hi))
                {
                    g.Clear(Color.Transparent);
                    g.ScaleTransform(big / 256f, big / 256f);
                    DrawTile(g, top, bot, tileScale, lid, awakeBadge, badgeScale, badgeRot, ripple);
                }
                Bitmap outB = new Bitmap(px, px, PixelFormat.Format32bppArgb);
                using (Graphics g2 = HQ(outB))
                {
                    g2.Clear(Color.Transparent);
                    g2.DrawImage(hi, new Rectangle(0, 0, px, px));
                }
                return outB;
            }
        }

        public static Bitmap Still(int px, bool awake)
        {
            return awake
                ? Tile(px, AwakeTop, AwakeBot, 1, LidRest, true, 1, 0, 0)
                : Tile(px, SleepTop, SleepBot, 1, LidRest, false, 1, 0, 0);
        }

        /// <summary>Start-up: the tile pops in, the lid swings open and settles, the badge appears.</summary>
        public static List<Bitmap> Intro(int px, bool awake)
        {
            const int N = 20;
            List<Bitmap> frames = new List<Bitmap>();
            Color top = awake ? AwakeTop : SleepTop, bot = awake ? AwakeBot : SleepBot;
            for (int i = 1; i <= N; i++)
            {
                double t = (double)i / N;
                float ts = (float)Math.Min(1.05, EaseBack(Math.Min(1, t / 0.55)));
                float lid = (float)(LidRest + 46 * Math.Sin(Math.PI * t));
                double bp = t < 0.35 ? 0 : (t - 0.35) / 0.65;
                float bs = (float)EaseBack(bp), br = (float)(-120 * (1 - bp));     // clockwise, like the flips
                float ripple = t > 0.55 ? (float)((t - 0.55) / 0.45) : 0;
                frames.Add(Tile(px, top, bot, Math.Max(0, ts), lid, awake, bs, br, ripple));
            }
            return frames;
        }

        /// <summary>Exit: the badge goes, the lid shuts all the way (the laptop "sleeps"), the tile shrinks away.</summary>
        public static List<Bitmap> Outro(int px, bool awake)
        {
            const int N = 18;
            List<Bitmap> frames = new List<Bitmap>();
            Color top = awake ? AwakeTop : SleepTop, bot = awake ? AwakeBot : SleepBot;
            for (int i = 1; i <= N; i++)
            {
                double t = (double)i / N;
                float bs = (float)(1 - EaseInOut(Math.Min(1, t / 0.35)));
                float br = (float)(-120 * Math.Min(1, t / 0.35));   // counter-clockwise on the way out
                float lid = (float)(LidRest * (1 - EaseInOut(Math.Min(1, t / 0.5))));
                float ts = t < 0.5 ? (float)(1 + 0.06 * Math.Sin(Math.PI * t / 0.5)) : (float)(1 - EaseInOut((t - 0.5) / 0.5));
                frames.Add(Tile(px, top, bot, ts, lid, awake, bs, br, 0));
            }
            return frames;
        }

        /// <summary>
        /// Transition frames: the lid swings open and shuts with a bounce, the tile squishes
        /// and recolours, the old badge spins away and the new one pops in, a ripple fires.
        /// </summary>
        public static List<Bitmap> Transition(int px, bool toAwake)
        {
            const int N = 22;
            List<Bitmap> frames = new List<Bitmap>();
            Color fT = toAwake ? SleepTop : AwakeTop, fB = toAwake ? SleepBot : AwakeBot;
            Color tT = toAwake ? AwakeTop : SleepTop, tB = toAwake ? AwakeBot : SleepBot;
            for (int i = 1; i <= N; i++)
            {
                double t = (double)i / N, c = EaseInOut(t);
                float tileScale = (float)(1 - 0.10 * Math.Sin(Math.PI * t));
                double lift = t < 0.8 ? Math.Sin(Math.PI * t / 0.8) : 0.13 * Math.Sin(Math.PI * (t - 0.8) / 0.2);
                float lid = (float)(LidRest + 46 * lift);
                bool badgeAwake = t < 0.4 ? !toAwake : toAwake;          // swap at the top of the swing
                float bs = (float)Math.Max(0, 1 - lift);
                float br = 3.2f * 46 * (float)lift;                      // meshes with the lid like a gear
                float ripple = t > 0.7 ? (float)((t - 0.7) / 0.3) : 0;
                frames.Add(Tile(px, Mix(fT, tT, c), Mix(fB, tB, c), tileScale, lid, badgeAwake, bs, br, ripple));
            }
            return frames;
        }
    }
}
