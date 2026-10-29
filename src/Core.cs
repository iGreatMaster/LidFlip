// LidFlip - choose what happens when you close your laptop lid.
// Written in C# 5 so it builds with the compiler that ships with Windows (see build.cmd).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("LidFlip")]
[assembly: AssemblyProduct("LidFlip")]
[assembly: AssemblyDescription("Choose what happens when you close your laptop lid")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Ahmed. MIT License.")]
[assembly: AssemblyVersion("1.0.2.0")]
[assembly: AssemblyFileVersion("1.0.2.0")]

namespace LidFlip
{
    static class Program
    {
        public const string AppName = "LidFlip";
        public static EventWaitHandle QuitEvent;
        static Mutex mutex;

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Single instance: a new launch asks the running copy to quit, then takes over.
            QuitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\LidFlip_Quit");
            mutex = new Mutex(false, @"Local\LidFlip_Single");
            bool replaced = false;
            if (!Acquire(0))
            {
                QuitEvent.Set();
                if (!Acquire(8000)) return;
                replaced = true;
            }

            bool startup = Array.IndexOf(args, "--startup") >= 0;
            bool guide = Array.IndexOf(args, "--guide") >= 0;
            try
            {
                Application.Run(new TrayApp(guide, startup, replaced));
            }
            catch (Exception ex)
            {
                Error("LidFlip stopped unexpectedly:\n" + ex.Message);
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch { }
            }
        }

        static bool Acquire(int ms)
        {
            try { return mutex.WaitOne(ms); }
            catch (AbandonedMutexException) { return true; }   // previous copy was killed
        }

        public static string DataDir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static void Log(string msg)
        {
            try { File.AppendAllText(Path.Combine(DataDir, "LidFlip.log"), DateTime.Now.ToString("s") + "  " + msg + Environment.NewLine); }
            catch { }
        }

        public static void Error(string msg)
        {
            Log(msg);
            MessageBox.Show(msg, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Reads and writes the lid-close action of the active power plan.</summary>
    static class Power
    {
        public const int DoNothing = 0;
        public const int Sleep = 1;

        // /qh also lists settings that the laptop maker has hidden (plain /q doesn't).
        public static int GetLid()
        {
            int code;
            string o = Run("/qh SCHEME_CURRENT SUB_BUTTONS LIDACTION", out code);
            Match m = Regex.Match(o, "0x[0-9a-fA-F]{8}");      // first value = on charger (AC)
            if (!m.Success) throw new Exception("Could not read the lid setting. powercfg said:\n" + o.Trim());
            return Convert.ToInt32(m.Value, 16);
        }

        public static void SetLid(int v)
        {
            string[] cmds = {
                "/setacvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION " + v,
                "/setdcvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION " + v,
                "/setactive SCHEME_CURRENT" };
            foreach (string c in cmds)
            {
                int code;
                string o = Run(c, out code);
                if (code != 0) throw new Exception("powercfg " + c + " failed:\n" + o.Trim());
            }
            int now = GetLid();
            if (now != v) throw new Exception("The setting did not stick (still " + now + "). Your power plan may be locked by a policy.");
        }

        static string Run(string args, out int code)
        {
            ProcessStartInfo psi = new ProcessStartInfo("powercfg.exe", args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                code = p.ExitCode;
                return o;
            }
        }
    }

    /// <summary>App settings under HKCU\Software\LidFlip.</summary>
    static class Prefs
    {
        const string KeyPath = @"Software\LidFlip";

        public static bool Has(string name)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath))
                return k != null && k.GetValue(name) != null;
        }

        public static int Get(string name, int def)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                if (k == null) return def;
                object v = k.GetValue(name);
                return v is int ? (int)v : def;
            }
        }

        public static void Set(string name, int value)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath))
                k.SetValue(name, value, RegistryValueKind.DWord);
        }
    }

    /// <summary>"Start with Windows" via the per-user Run key.</summary>
    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsOn
        {
            get
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    string v = k == null ? null : k.GetValue(Program.AppName) as string;
                    return v != null && v.IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
        }

        public static void Set(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(Program.AppName, "\"" + Application.ExecutablePath + "\" --startup");
                else if (k.GetValue(Program.AppName) != null) k.DeleteValue(Program.AppName);
            }
        }
    }

    /// <summary>
    /// "Pin next to clock" (Windows 11): sets IsPromoted=1 on this app's entry under
    /// HKCU\Control Panel\NotifyIconSettings. The choice is also saved in Prefs and
    /// re-applied on launch, in case Windows re-creates the entry.
    /// </summary>
    static class Pin
    {
        const string Root = @"Control Panel\NotifyIconSettings";

        public static bool Supported
        {
            get { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Root)) return k != null; }
        }

        static List<string> OurEntries()
        {
            List<string> list = new List<string>();
            string exe = Path.GetFileName(Application.ExecutablePath);
            using (RegistryKey root = Registry.CurrentUser.OpenSubKey(Root))
            {
                if (root == null) return list;
                foreach (string name in root.GetSubKeyNames())
                {
                    using (RegistryKey k = root.OpenSubKey(name))
                    {
                        string path = k == null ? null : k.GetValue("ExecutablePath") as string;
                        if (path != null && path.EndsWith("\\" + exe, StringComparison.OrdinalIgnoreCase))
                            list.Add(Root + "\\" + name);
                    }
                }
            }
            return list;
        }

        public static bool Registered { get { return OurEntries().Count > 0; } }

        public static bool Wanted
        {
            get { return Prefs.Get("Pinned", 0) == 1; }
            set { Prefs.Set("Pinned", value ? 1 : 0); }
        }

        public static bool HasChoice { get { return Prefs.Has("Pinned"); } }

        /// <summary>Applies the value to every entry; returns true if anything changed.</summary>
        public static bool Apply(bool pinned)
        {
            int v = pinned ? 1 : 0;
            bool changed = false;
            foreach (string path in OurEntries())
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(path, true))
                {
                    if (k == null) continue;
                    object cur = k.GetValue("IsPromoted");
                    if (!(cur is int) || (int)cur != v)
                    {
                        k.SetValue("IsPromoted", v, RegistryValueKind.DWord);
                        changed = true;
                    }
                }
            }
            return changed;
        }
    }
}
