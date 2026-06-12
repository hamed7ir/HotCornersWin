using System;
using System.Drawing;
using MaterialSkin;
using Microsoft.Win32;

namespace HotCornersWin
{
    internal static class ThemeHelper
    {
        internal static Color GetWindowsAccentColor()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    var raw = key?.GetValue("AccentColor");
                    if (raw is int v)
                        return Color.FromArgb(255, v & 0xFF, (v >> 8) & 0xFF, (v >> 16) & 0xFF);
                }
            }
            catch { }
            return Color.FromArgb(255, 0, 120, 215);
        }

        internal static bool GetSystemDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var raw = key?.GetValue("AppsUseLightTheme");
                    if (raw is int v) return v == 0;
                }
            }
            catch { }
            return false;
        }

        internal static bool IsDark(AppSettings settings)
        {
            switch (settings.ThemePreference)
            {
                case "Dark":  return true;
                case "Light": return false;
                default:      return GetSystemDarkMode();
            }
        }

        internal static void Apply(AppSettings settings)
        {
            var mgr = MaterialSkinManager.Instance;
            mgr.Theme = IsDark(settings)
                ? MaterialSkinManager.Themes.DARK
                : MaterialSkinManager.Themes.LIGHT;

            var accent = GetWindowsAccentColor();
            mgr.ColorScheme = new ColorScheme(accent, Darken(accent, 0.15f), Lighten(accent, 0.3f),
                accent, IsLight(accent) ? TextShade.BLACK : TextShade.WHITE);
        }

        private static Color Darken(Color c, float f) =>
            Color.FromArgb(c.A,
                Math.Max(0, (int)(c.R * (1 - f))),
                Math.Max(0, (int)(c.G * (1 - f))),
                Math.Max(0, (int)(c.B * (1 - f))));

        private static Color Lighten(Color c, float f) =>
            Color.FromArgb(c.A,
                Math.Min(255, (int)(c.R + (255 - c.R) * f)),
                Math.Min(255, (int)(c.G + (255 - c.G) * f)),
                Math.Min(255, (int)(c.B + (255 - c.B) * f)));

        private static bool IsLight(Color c) =>
            0.299 * c.R + 0.587 * c.G + 0.114 * c.B > 186;
    }
}
