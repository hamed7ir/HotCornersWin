using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HotCornersWin
{
    internal static class ActionDispatcher
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LockWorkStation();

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_LWIN     = 0x5B;
        private const byte VK_LSHIFT   = 0xA0;
        private const byte VK_LCONTROL = 0xA2;
        private const byte VK_LMENU    = 0xA4; // Alt
        private const byte VK_D        = 0x44;
        private const byte VK_TAB      = 0x09;
        private const byte VK_A        = 0x41;
        private const byte VK_R        = 0x52;
        private const byte VK_T        = 0x54;
        private const byte VK_VOL_UP   = 0xAF;
        private const byte VK_VOL_DN   = 0xAE;

        public static void Execute(CornerAction action, string customShortcut = null, int volumeStep = 10)
        {
            switch (action)
            {
                case CornerAction.ShowDesktop:   SendCombo(VK_LWIN, VK_D);   break;
                case CornerAction.TaskView:      SendCombo(VK_LWIN, VK_TAB); break;
                case CornerAction.StartMenu:     SendKey(VK_LWIN);            break;
                case CornerAction.ActionCenter:  SendCombo(VK_LWIN, VK_A);   break;
                case CornerAction.LockScreen:    LockWorkStation();           break;
                case CornerAction.ShowTaskbar:   SendCombo(VK_LWIN, VK_T);   break;
                case CornerAction.OpenRun:       SendCombo(VK_LWIN, VK_R);   break;
                case CornerAction.OpenExplorer:  OpenExplorer();              break;
                case CornerAction.VolumeUp:
                {
                    // Each VK_VOLUME_UP keypress = 2% on Windows; round to nearest press
                    int presses = Math.Max(1, (int)Math.Round(volumeStep / 2.0));
                    for (int i = 0; i < presses; i++) SendKey(VK_VOL_UP);
                    break;
                }
                case CornerAction.VolumeDown:
                {
                    int presses = Math.Max(1, (int)Math.Round(volumeStep / 2.0));
                    for (int i = 0; i < presses; i++) SendKey(VK_VOL_DN);
                    break;
                }
                case CornerAction.CustomShortcut:
                    if (!string.IsNullOrEmpty(customShortcut))
                        SendParsedShortcut(customShortcut);
                    break;
            }
        }

        private static void OpenExplorer()
        {
            try { Process.Start("explorer.exe"); }
            catch { /* shell unavailable — nothing useful to do */ }
        }

        private static void SendParsedShortcut(string shortcut)
        {
            var mods = new List<byte>();
            byte mainKey = 0;

            foreach (var part in shortcut.Split('+'))
            {
                switch (part.Trim().ToUpperInvariant())
                {
                    case "WIN":   mods.Add(VK_LWIN);     break;
                    case "CTRL":  mods.Add(VK_LCONTROL); break;
                    case "ALT":   mods.Add(VK_LMENU);    break;
                    case "SHIFT": mods.Add(VK_LSHIFT);   break;
                    default:      mainKey = ParseVk(part.Trim()); break;
                }
            }

            if (mainKey == 0) return;

            foreach (var m in mods) keybd_event(m, 0, 0, UIntPtr.Zero);
            keybd_event(mainKey, 0, 0, UIntPtr.Zero);
            keybd_event(mainKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            // Release modifiers in reverse order
            for (int i = mods.Count - 1; i >= 0; i--)
                keybd_event(mods[i], 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private static byte ParseVk(string key)
        {
            if (key.Length == 1)
            {
                char c = char.ToUpperInvariant(key[0]);
                if (c >= 'A' && c <= 'Z') return (byte)c;         // VK_A=0x41…VK_Z=0x5A
                if (c >= '0' && c <= '9') return (byte)c;         // VK_0=0x30…VK_9=0x39
            }
            // F1–F24: VK_F1=0x70, so Fn = 0x70 + (n-1)
            if (key.Length >= 2 && (key[0] == 'F' || key[0] == 'f'))
            {
                if (int.TryParse(key.Substring(1), out int fn) && fn >= 1 && fn <= 24)
                    return (byte)(0x6F + fn);
            }
            // NumPad0–NumPad9: VK_NUMPAD0=0x60
            if (key.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) && key.Length == 7)
            {
                char c = key[6];
                if (c >= '0' && c <= '9') return (byte)(0x60 + c - '0');
            }
            switch (key.ToUpperInvariant())
            {
                case "ENTER":                    return 0x0D;
                case "ESCAPE": case "ESC":       return 0x1B;
                case "SPACE":                    return 0x20;
                case "TAB":                      return 0x09;
                case "BACKSPACE": case "BACK":   return 0x08;
                case "DELETE":  case "DEL":      return 0x2E;
                case "INSERT":  case "INS":      return 0x2D;
                case "HOME":                     return 0x24;
                case "END":                      return 0x23;
                case "PAGEUP":  case "PGUP":     return 0x21;
                case "PAGEDOWN": case "PGDN":    return 0x22;
                case "LEFT":                     return 0x25;
                case "UP":                       return 0x26;
                case "RIGHT":                    return 0x27;
                case "DOWN":                     return 0x28;
                case "PRINTSCREEN":              return 0x2C;
                case "PAUSE":                    return 0x13;
                case "NUMLOCK":                  return 0x90;
                case "SCROLLLOCK":               return 0x91;
                case "CAPSLOCK":                 return 0x14;
                default:                         return 0;
            }
        }

        private static void SendKey(byte vk)
        {
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private static void SendCombo(byte mod, byte key)
        {
            keybd_event(mod, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(mod, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
