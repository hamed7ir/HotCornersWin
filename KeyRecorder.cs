using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HotCornersWin
{
    /// <summary>
    /// Installs a temporary WH_KEYBOARD_LL hook to record a single key combination.
    /// Fires ShortcutRecorded with a string like "Ctrl+Shift+F5" or "Win+E".
    /// Fires ShortcutRecorded with null if the user presses Escape alone (cancel).
    /// Must be installed from the UI thread; the callback runs on the same thread.
    /// </summary>
    internal sealed class KeyRecorder : IDisposable
    {
        public event EventHandler<string> ShortcutRecorded;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN    = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP      = 0x0101;
        private const int WM_SYSKEYUP   = 0x0105;

        private delegate IntPtr LowLevelKeyProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode, scanCode, flags, time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyProc fn, IntPtr mod, uint tid);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string n);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);

        private IntPtr _hook = IntPtr.Zero;
        private readonly LowLevelKeyProc _proc; // keep ref to prevent GC
        private bool _winDown;

        public KeyRecorder() { _proc = HookCallback; }

        public void Start()
        {
            if (_hook != IntPtr.Zero) return;
            _winDown = false;
            using (var proc = Process.GetCurrentProcess())
            using (var mod = proc.MainModule)
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(mod.ModuleName), 0);
        }

        public void Stop()
        {
            if (_hook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(_hook, nCode, wParam, lParam);

            var kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
            int msg = (int)wParam;
            bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isUp   = msg == WM_KEYUP   || msg == WM_SYSKEYUP;

            // Track Win key and suppress it to prevent the Start Menu from opening
            if (kb.vkCode == 0x5B || kb.vkCode == 0x5C)
            {
                if (isDown) _winDown = true;
                if (isUp)   _winDown = false;
                return (IntPtr)1; // suppress
            }

            if (!isDown) return CallNextHookEx(_hook, nCode, wParam, lParam);

            var vk = kb.vkCode;

            // Let pure modifier key-downs pass through (we read their state via GetAsyncKeyState)
            if (IsModifier(vk)) return CallNextHookEx(_hook, nCode, wParam, lParam);

            // Bare Escape = cancel recording
            if (vk == 0x1B && !_winDown && !AnyNonWinModifier())
            {
                Stop();
                _winDown = false;
                ShortcutRecorded?.Invoke(this, null);
                return (IntPtr)1;
            }

            // Build the shortcut string
            var parts = new List<string>();
            if (_winDown)                                                       parts.Add("Win");
            if ((GetAsyncKeyState(0xA2) & 0x8000) != 0 ||
                (GetAsyncKeyState(0xA3) & 0x8000) != 0)                        parts.Add("Ctrl");
            if ((GetAsyncKeyState(0xA4) & 0x8000) != 0 ||
                (GetAsyncKeyState(0xA5) & 0x8000) != 0)                        parts.Add("Alt");
            if ((GetAsyncKeyState(0xA0) & 0x8000) != 0 ||
                (GetAsyncKeyState(0xA1) & 0x8000) != 0)                        parts.Add("Shift");
            parts.Add(VkToString(vk));

            Stop();
            _winDown = false;
            ShortcutRecorded?.Invoke(this, string.Join("+", parts));
            return (IntPtr)1; // suppress — don't let the recorded key act on the system
        }

        private static bool IsModifier(uint vk) =>
            vk == 0x10 || vk == 0x11 || vk == 0x12 ||           // generic Shift/Ctrl/Alt
            vk == 0xA0 || vk == 0xA1 ||                          // LShift, RShift
            vk == 0xA2 || vk == 0xA3 ||                          // LCtrl,  RCtrl
            vk == 0xA4 || vk == 0xA5;                            // LAlt,   RAlt

        private bool AnyNonWinModifier() =>
            (GetAsyncKeyState(0xA0) & 0x8000) != 0 ||
            (GetAsyncKeyState(0xA1) & 0x8000) != 0 ||
            (GetAsyncKeyState(0xA2) & 0x8000) != 0 ||
            (GetAsyncKeyState(0xA3) & 0x8000) != 0 ||
            (GetAsyncKeyState(0xA4) & 0x8000) != 0 ||
            (GetAsyncKeyState(0xA5) & 0x8000) != 0;

        internal static string VkToString(uint vk)
        {
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();        // A–Z
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();        // 0–9
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x70 + 1);       // F1–F24
            if (vk >= 0x60 && vk <= 0x69) return "NumPad" + (vk - 0x60);      // NumPad0–9
            switch (vk)
            {
                case 0x0D: return "Enter";
                case 0x1B: return "Escape";
                case 0x20: return "Space";
                case 0x09: return "Tab";
                case 0x08: return "Backspace";
                case 0x2E: return "Delete";
                case 0x2D: return "Insert";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";
                case 0x2C: return "PrintScreen";
                case 0x13: return "Pause";
                case 0x90: return "NumLock";
                case 0x91: return "ScrollLock";
                case 0x14: return "CapsLock";
                default:   return string.Format("0x{0:X2}", vk);
            }
        }

        public void Dispose() => Stop();
    }
}
