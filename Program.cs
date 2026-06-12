using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace HotCornersWin
{
    static class Program
    {
        // Must be called before any window is created so Screen.Bounds returns
        // physical pixel coordinates — the same space WH_MOUSE_LL reports in.
        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);
        // 0 = unaware, 1 = system-DPI-aware, 2 = per-monitor-DPI-aware

        // MaterialSkin.dll is embedded in this exe so we ship a single file.
        // Resolve it from the embedded resource the first time the CLR needs it.
        // Registered before any MaterialSkin type is touched (Form1 is the first).
        private static Assembly ResolveEmbeddedMaterialSkin(object sender, ResolveEventArgs args)
        {
            if (!new AssemblyName(args.Name).Name.Equals("MaterialSkin", StringComparison.OrdinalIgnoreCase))
                return null;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MaterialSkin.dll"))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                int read, offset = 0;
                while ((read = stream.Read(bytes, offset, bytes.Length - offset)) > 0) offset += read;
                return Assembly.Load(bytes);
            }
        }

        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedMaterialSkin;

            // Per-monitor DPI awareness: Screen.Bounds and WH_MOUSE_LL both
            // use physical pixels, so corner zone math is consistent on all
            // monitors regardless of their individual DPI scaling factors.
            // The Settings dialog opts OUT of this per-window (see Form1.OnSettings)
            // because MaterialSkin renders text at fixed pixel sizes that ignore
            // DPI — letting Windows bitmap-scale that window keeps its text and
            // controls growing together.
            try { SetProcessDpiAwareness(2); } catch { /* pre-Win8.1 fallback */ }

            bool createdNew;
            using (var mutex = new Mutex(true, "HotCornersWin_SingleInstance", out createdNew))
            {
                if (!createdNew)
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
        }
    }
}
