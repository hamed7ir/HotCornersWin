using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace HotCornersWin
{
    [DataContract]
    public sealed class MonitorCornerSettings
    {
        [DataMember] public string DeviceName { get; set; }

        // All four default to CornerAction.None (enum value 0) — users configure from scratch
        [DataMember] public CornerAction TopLeft { get; set; }
        [DataMember] public CornerAction TopRight { get; set; }
        [DataMember] public CornerAction BottomLeft { get; set; }
        [DataMember] public CornerAction BottomRight { get; set; }

        // Custom shortcut strings per corner, e.g. "Ctrl+Shift+F5" or "Win+E"
        [DataMember] public string TopLeftShortcut { get; set; }
        [DataMember] public string TopRightShortcut { get; set; }
        [DataMember] public string BottomLeftShortcut { get; set; }
        [DataMember] public string BottomRightShortcut { get; set; }

        // Volume step (%) per corner — default 10, range 1-100
        // Property initializer ensures new instances and old JSON (missing key) both get 10
        [DataMember] public int TopLeftVolumeStep     { get; set; } = 10;
        [DataMember] public int TopRightVolumeStep    { get; set; } = 10;
        [DataMember] public int BottomLeftVolumeStep  { get; set; } = 10;
        [DataMember] public int BottomRightVolumeStep { get; set; } = 10;

        public CornerAction GetAction(Corner corner)
        {
            switch (corner)
            {
                case Corner.TopLeft:     return TopLeft;
                case Corner.TopRight:    return TopRight;
                case Corner.BottomLeft:  return BottomLeft;
                case Corner.BottomRight: return BottomRight;
                default: return CornerAction.None;
            }
        }

        public void SetAction(Corner corner, CornerAction action)
        {
            switch (corner)
            {
                case Corner.TopLeft:     TopLeft = action;     break;
                case Corner.TopRight:    TopRight = action;    break;
                case Corner.BottomLeft:  BottomLeft = action;  break;
                case Corner.BottomRight: BottomRight = action; break;
            }
        }

        public string GetShortcut(Corner corner)
        {
            switch (corner)
            {
                case Corner.TopLeft:     return TopLeftShortcut     ?? string.Empty;
                case Corner.TopRight:    return TopRightShortcut    ?? string.Empty;
                case Corner.BottomLeft:  return BottomLeftShortcut  ?? string.Empty;
                case Corner.BottomRight: return BottomRightShortcut ?? string.Empty;
                default: return string.Empty;
            }
        }

        public void SetShortcut(Corner corner, string shortcut)
        {
            switch (corner)
            {
                case Corner.TopLeft:     TopLeftShortcut     = shortcut; break;
                case Corner.TopRight:    TopRightShortcut    = shortcut; break;
                case Corner.BottomLeft:  BottomLeftShortcut  = shortcut; break;
                case Corner.BottomRight: BottomRightShortcut = shortcut; break;
            }
        }

        public int GetVolumeStep(Corner corner)
        {
            int v;
            switch (corner)
            {
                case Corner.TopLeft:     v = TopLeftVolumeStep;     break;
                case Corner.TopRight:    v = TopRightVolumeStep;    break;
                case Corner.BottomLeft:  v = BottomLeftVolumeStep;  break;
                case Corner.BottomRight: v = BottomRightVolumeStep; break;
                default:                 v = 10;                    break;
            }
            return v < 1 ? 1 : v > 100 ? 100 : v;
        }

        public void SetVolumeStep(Corner corner, int step)
        {
            switch (corner)
            {
                case Corner.TopLeft:     TopLeftVolumeStep     = step; break;
                case Corner.TopRight:    TopRightVolumeStep    = step; break;
                case Corner.BottomLeft:  BottomLeftVolumeStep  = step; break;
                case Corner.BottomRight: BottomRightVolumeStep = step; break;
            }
        }
    }

    [DataContract]
    public sealed class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        [DataMember] public bool Enabled { get; set; }
        [DataMember] public List<MonitorCornerSettings> Monitors { get; set; }
        [DataMember] public string ThemePreference { get; set; }

        public AppSettings()
        {
            Enabled = true;
            Monitors = new List<MonitorCornerSettings>();
            ThemePreference = "Auto";
        }

        // Called before deserialization fills in values — ensures defaults if a key is absent
        [OnDeserializing]
        private void OnDeserializing(StreamingContext ctx)
        {
            Enabled = true;
            Monitors = new List<MonitorCornerSettings>();
            ThemePreference = "Auto";
        }

        public MonitorCornerSettings GetOrCreateMonitor(string deviceName)
        {
            var ms = Monitors.FirstOrDefault(m => m.DeviceName == deviceName);
            if (ms == null)
            {
                ms = new MonitorCornerSettings { DeviceName = deviceName };
                Monitors.Add(ms);
            }
            return ms;
        }

        public CornerAction GetAction(string deviceName, Corner corner)
            => GetOrCreateMonitor(deviceName).GetAction(corner);

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var ser = new DataContractJsonSerializer(typeof(AppSettings));
                    using (var stream = File.OpenRead(SettingsPath))
                        return (AppSettings)ser.ReadObject(stream);
                }
            }
            catch { /* corrupt file — fall through to defaults */ }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                var ser = new DataContractJsonSerializer(typeof(AppSettings));
                using (var stream = File.Open(SettingsPath, FileMode.Create))
                    ser.WriteObject(stream, this);
            }
            catch { /* best-effort */ }
        }
    }
}
