using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace HotCornersWin
{
    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    public sealed class CornerTriggeredEventArgs : EventArgs
    {
        public Screen Screen { get; private set; }
        public Corner Corner { get; private set; }

        public CornerTriggeredEventArgs(Screen screen, Corner corner)
        {
            Screen = screen;
            Corner = corner;
        }
    }

    internal sealed class CornerDetector
    {
        // Hot zone: 4×4 pixels at each corner of every screen
        private const int CornerSize = 4;

        // After a corner action fires, suppress re-entry for this many ms.
        // Prevents double-firing when cursor jitter causes rapid leave/re-enter
        // (e.g. Win+Tab animation briefly shifts the reported cursor position).
        private const int CooldownMs = 1000;

        public event EventHandler<CornerTriggeredEventArgs> CornerEntered;
        public event EventHandler<CornerTriggeredEventArgs> CornerLeft;

        private ActiveCorner _active;
        private DateTime _lastFiredAt = DateTime.MinValue;

        private sealed class ActiveCorner
        {
            public Screen Screen;
            public Corner Corner;
        }

        public void Update(Point cursor)
        {
            Screen hitScreen;
            Corner hitCorner;
            bool found = FindCorner(cursor, out hitScreen, out hitCorner);

            if (found && _active == null)
            {
                // Cooldown: don't re-fire until 1000ms have passed since the last action,
                // even if the cursor briefly left and came back.
                if ((DateTime.UtcNow - _lastFiredAt).TotalMilliseconds < CooldownMs)
                    return;

                _lastFiredAt = DateTime.UtcNow;
                _active = new ActiveCorner { Screen = hitScreen, Corner = hitCorner };
                CornerEntered?.Invoke(this, new CornerTriggeredEventArgs(_active.Screen, _active.Corner));
            }
            else if (!found && _active != null)
            {
                var prev = _active;
                _active = null;
                CornerLeft?.Invoke(this, new CornerTriggeredEventArgs(prev.Screen, prev.Corner));
            }
            else if (found && _active != null &&
                     (_active.Screen != hitScreen || _active.Corner != hitCorner))
            {
                _lastFiredAt = DateTime.UtcNow;
                _active = new ActiveCorner { Screen = hitScreen, Corner = hitCorner };
                CornerEntered?.Invoke(this, new CornerTriggeredEventArgs(_active.Screen, _active.Corner));
            }
        }

        private static bool FindCorner(Point p, out Screen screen, out Corner corner)
        {
            foreach (var s in Screen.AllScreens)
            {
                var b = s.Bounds;

                // b.Contains uses logical coords; WH_MOUSE_LL gives physical coords.
                // With per-monitor DPI awareness (set in app.manifest + Program.cs),
                // Screen.Bounds returns physical coords so these match correctly.
                if (!b.Contains(p)) continue;

                bool nearLeft   = p.X <  b.Left   + CornerSize;
                bool nearRight  = p.X >= b.Right  - CornerSize;
                bool nearTop    = p.Y <  b.Top    + CornerSize;
                bool nearBottom = p.Y >= b.Bottom - CornerSize;

                if (nearLeft  && nearTop)    { screen = s; corner = Corner.TopLeft;     return true; }
                if (nearRight && nearTop)    { screen = s; corner = Corner.TopRight;    return true; }
                if (nearLeft  && nearBottom) { screen = s; corner = Corner.BottomLeft;  return true; }
                if (nearRight && nearBottom) { screen = s; corner = Corner.BottomRight; return true; }
            }
            screen = null;
            corner = default(Corner);
            return false;
        }

        /// <summary>
        /// Dumps Screen.AllScreens bounds to the VS output window and to
        /// %TEMP%\HotCornersWin.log. No-op in Release builds — the compiler
        /// removes all call sites due to [Conditional("DEBUG")].
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void DumpScreenInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== HotCornersWin Screen Dump (" + DateTime.Now.ToString("u") + ") ===");
            sb.AppendLine(string.Format("Screen.AllScreens count: {0}", Screen.AllScreens.Length));
            sb.AppendLine();

            foreach (var s in Screen.AllScreens)
            {
                var b = s.Bounds;
                sb.AppendLine(string.Format(
                    "{0}{1}",
                    s.DeviceName,
                    s.Primary ? "  [Primary]" : ""));
                sb.AppendLine(string.Format(
                    "  Bounds : Left={0}  Top={1}  Right={2}  Bottom={3}  ({4}x{5})",
                    b.Left, b.Top, b.Right, b.Bottom, b.Width, b.Height));
                sb.AppendLine(string.Format(
                    "  Corner zones: TL=[{0},{1})  TR=[{2},{3})  BL=Y[{4},{5})  BR=X[{2},{3}) Y[{4},{5})",
                    b.Left, b.Left + CornerSize,
                    b.Right - CornerSize, b.Right,
                    b.Bottom - CornerSize, b.Bottom));
                sb.AppendLine();
            }

            var msg = sb.ToString();
            System.Diagnostics.Debug.Write(msg);

            try
            {
                var path = Path.Combine(Path.GetTempPath(), "HotCornersWin.log");
                File.WriteAllText(path, msg);
            }
            catch { }
        }
    }
}
