using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using MaterialSkin;

namespace HotCornersWin
{
    /// <summary>
    /// MaterialSkin renders all of its text (labels, buttons, combo boxes, tab
    /// titles, the form title bar) with a set of fixed-pixel GDI fonts that it
    /// caches once and never scales for DPI. On a high-DPI display the manually
    /// scaled layout grows but that text stays at its 96-DPI size.
    ///
    /// This patch reaches into MaterialSkinManager's private font cache
    /// (`logicalFonts`, a map of fontType -> HFONT) and replaces each handle with
    /// the same font enlarged by the DPI scale factor. Because MaterialSkin draws
    /// through these handles, every Material control's text then scales crisply —
    /// no bitmap stretching, no layout changes. Runs once per process.
    /// </summary>
    internal static class MaterialFontScaler
    {
        private static bool _applied;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class LOGFONT
        {
            public int lfHeight;
            public int lfWidth;
            public int lfEscapement;
            public int lfOrientation;
            public int lfWeight;
            public byte lfItalic;
            public byte lfUnderline;
            public byte lfStrikeOut;
            public byte lfCharSet;
            public byte lfOutPrecision;
            public byte lfClipPrecision;
            public byte lfQuality;
            public byte lfPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string lfFaceName;
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, [In, Out] LOGFONT target);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFontIndirect([In] LOGFONT lf);

        /// <summary>
        /// Enlarge MaterialSkin's cached fonts by <paramref name="scale"/>. Safe to
        /// call more than once; only the first call (with scale &gt; 1) has effect.
        /// </summary>
        internal static void EnsureScaled(float scale)
        {
            if (_applied || scale <= 1.0f) return;
            _applied = true;

            try
            {
                var mgr = MaterialSkinManager.Instance;
                var field = typeof(MaterialSkinManager)
                    .GetField("logicalFonts", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return;

                // Dictionary<fontType, IntPtr>; iterate non-generically so we don't
                // depend on the internal enum key type.
                var fonts = field.GetValue(mgr) as IDictionary;
                if (fonts == null) return;

                foreach (var key in fonts.Keys.Cast<object>().ToList())
                {
                    if (!(fonts[key] is IntPtr handle) || handle == IntPtr.Zero) continue;

                    var lf = new LOGFONT();
                    if (GetObject(handle, Marshal.SizeOf(typeof(LOGFONT)), lf) == 0) continue;

                    // lfHeight is a negative character height; scaling preserves the sign.
                    lf.lfHeight = (int)Math.Round(lf.lfHeight * scale);
                    if (lf.lfWidth != 0)
                        lf.lfWidth = (int)Math.Round(lf.lfWidth * scale);

                    var scaled = CreateFontIndirect(lf);
                    if (scaled != IntPtr.Zero)
                        fonts[key] = scaled; // old handle intentionally left (one-time, ~19 objects)
                }
            }
            catch
            {
                // Reflection/GDI failure must never crash the app — worst case the
                // Material text simply stays unscaled, as before.
            }
        }
    }
}
