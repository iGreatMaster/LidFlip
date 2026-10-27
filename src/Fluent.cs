using System;
using System.Drawing;
using System.Drawing.Text;
using Microsoft.Win32;

namespace LidFlip
{
    /// <summary>
    /// Windows 11 (Fluent) look: system light/dark mode, the user's accent colour,
    /// Segoe UI Variable text and Segoe Fluent Icons — with fallbacks for Windows 10.
    /// </summary>
    class Theme
    {
        public bool Dark;
        public Color Bg, Card, CardBorder, Text, Sub, Border, Stroke,
                     Accent, AccentHover, OnAccent, Button, ButtonHover,
                     MenuBg, MenuHi, Bar, BarText, Warn;

        /// <summary>Theme for windows (follows the "app mode" setting).</summary>
        public static Theme Current() { return Build(IsDark("AppsUseLightTheme")); }

        /// <summary>Theme for things that pop up from the taskbar (follows the "Windows mode" setting).</summary>
        public static Theme ForTaskbar() { return Build(IsDark("SystemUsesLightTheme")); }

        public static Theme LightTheme() { return Build(false); }
        public static Theme DarkTheme() { return Build(true); }

        static bool IsDark(string name)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue(name);
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        /// <summary>The accent colour chosen in Settings > Personalisation > Colours.</summary>
        static Color SystemAccent()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    object v = k == null ? null : k.GetValue("AccentColor");    // stored as 0xAABBGGRR
                    if (v is int)
                    {
                        int c = (int)v;
                        return Color.FromArgb(255, c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF);
                    }
                }
            }
            catch { }
            return ColorTranslator.FromHtml("#0067C0");   // Windows 11 default blue
        }

        static Color C(string h) { return ColorTranslator.FromHtml(h); }

        static Theme Build(bool dark)
        {
            Theme t = new Theme();
            t.Dark = dark;
            Color acc = SystemAccent();
            if (dark)
            {
                // Windows 11 colour values (dark)
                t.Bg = C("#202020"); t.Card = C("#2B2B2B"); t.CardBorder = C("#1C1C1C");
                t.Text = C("#FFFFFF"); t.Sub = C("#C5C5C5"); t.Border = C("#3A3A3A"); t.Stroke = C("#9E9E9E");
                t.Button = C("#2D2D2D"); t.ButtonHover = C("#323232");
                t.MenuBg = C("#2C2C2C"); t.MenuHi = C("#383838");
                t.Bar = C("#1C1C1C"); t.BarText = C("#FFFFFF"); t.Warn = C("#FCE100");
                t.Accent = Art.Mix(acc, Color.White, 0.45);        // controls use a lighter accent in dark mode
                t.AccentHover = Art.Mix(t.Accent, t.Bg, 0.10);
                t.OnAccent = Color.Black;
            }
            else
            {
                // Windows 11 colour values (light)
                t.Bg = C("#F3F3F3"); t.Card = C("#FBFBFB"); t.CardBorder = C("#E5E5E5");
                t.Text = C("#1A1A1A"); t.Sub = C("#5D5D5D"); t.Border = C("#E5E5E5"); t.Stroke = C("#8A8A8A");
                t.Button = C("#FBFBFB"); t.ButtonHover = C("#F6F6F6");
                t.MenuBg = C("#F9F9F9"); t.MenuHi = C("#EAEAEA");
                t.Bar = C("#EEEEEE"); t.BarText = C("#1A1A1A"); t.Warn = C("#F7630C");
                t.Accent = Art.Mix(acc, Color.Black, 0.12);        // and a slightly darker one in light mode
                t.AccentHover = Art.Mix(t.Accent, t.Bg, 0.10);
                t.OnAccent = Color.White;
            }
            return t;
        }
    }

    /// <summary>Windows 11 fonts, falling back to Windows 10 equivalents.</summary>
    static class Fonts
    {
        static string textFamily, displayFamily, iconFamily;
        static bool probed;

        static bool Installed(string name)
        {
            using (InstalledFontCollection fc = new InstalledFontCollection())
                foreach (FontFamily f in fc.Families)
                    if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void Probe()
        {
            if (probed) return;
            probed = true;
            textFamily = Installed("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
            displayFamily = Installed("Segoe UI Variable Display") ? "Segoe UI Variable Display" : "Segoe UI";
            iconFamily = Installed("Segoe Fluent Icons") ? "Segoe Fluent Icons"
                       : Installed("Segoe MDL2 Assets") ? "Segoe MDL2 Assets" : null;
        }

        public static Font Text(float pt) { return Text(pt, FontStyle.Regular); }
        public static Font Text(float pt, FontStyle st) { Probe(); return new Font(textFamily, pt, st); }

        /// <summary>Semibold titles (Segoe UI Variable Display Semibold on Windows 11).</summary>
        public static Font Title(float pt)
        {
            Probe();
            if (displayFamily == "Segoe UI Variable Display")
            {
                Font f = new Font("Segoe UI Variable Display Semibold", pt);
                if (f.Name == "Segoe UI Variable Display Semibold") return f;
                f.Dispose();
                return new Font(displayFamily, pt, FontStyle.Bold);
            }
            Font sb = new Font("Segoe UI Semibold", pt);
            if (sb.Name == "Segoe UI Semibold") return sb;
            sb.Dispose();
            return new Font("Segoe UI", pt, FontStyle.Bold);
        }

        /// <summary>The system icon font, or null if neither Fluent nor MDL2 icons are installed.</summary>
        public static Font Icons(float pt) { Probe(); return iconFamily == null ? null : new Font(iconFamily, pt); }

        // Glyphs shared by Segoe Fluent Icons and Segoe MDL2 Assets
        public const char LightningBolt = '';
        public const char Play = '';
        public const char Pin = '';
        public const char Help = '';
        public const char Power = '';
    }
}
