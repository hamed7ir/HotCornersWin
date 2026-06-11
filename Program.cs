using System;
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

        [STAThread]
        static void Main()
        {
            // Per-monitor DPI awareness: Screen.Bounds and WH_MOUSE_LL both
            // use physical pixels, so corner zone math is consistent on all
            // monitors regardless of their individual DPI scaling factors.
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
