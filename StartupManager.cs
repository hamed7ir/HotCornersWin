using System.Windows.Forms;
using Microsoft.Win32;

namespace HotCornersWin
{
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName  = "HotCornersWin";

        public static bool IsEnabled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                    return key?.GetValue(ValueName) != null;
            }
        }

        public static void Enable()
        {
            // Quote the path so spaces are handled correctly by the shell at login
            var exePath = Application.ExecutablePath;
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
                key?.SetValue(ValueName, "\"" + exePath + "\"");
        }

        public static void Disable()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
