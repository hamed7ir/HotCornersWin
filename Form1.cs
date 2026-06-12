using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MaterialSkin;
using Microsoft.Win32;

namespace HotCornersWin
{
    public partial class Form1 : Form
    {
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _toggleItem;
        private ToolStripMenuItem _startupItem;
        private MouseHook _mouseHook;
        private CornerDetector _cornerDetector;
        private AppSettings _settings;

        public Form1()
        {
            InitializeComponent();

            _settings = AppSettings.Load();
            ThemeHelper.Apply(_settings);

            BuildTrayIcon();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            _cornerDetector = new CornerDetector();
            _cornerDetector.CornerEntered += OnCornerEntered;

            _mouseHook = new MouseHook();
            _mouseHook.MouseMoved += OnMouseMoved;
            _mouseHook.Install();

            CornerDetector.DumpScreenInfo(); // writes to %TEMP%\HotCornersWin.log
        }

        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

        private void BuildTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();

            var header = (ToolStripMenuItem)_trayMenu.Items.Add("HotCornersWin");
            header.Enabled = false;

            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Settings", null, OnSettings);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _startupItem = (ToolStripMenuItem)_trayMenu.Items.Add("Start with Windows", null, OnStartupToggle);
            _startupItem.Checked = StartupManager.IsEnabled;
            _trayMenu.Items.Add(new ToolStripSeparator());
            _toggleItem = (ToolStripMenuItem)_trayMenu.Items.Add("Disable", null, OnToggle);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Exit", null, OnExit);

            _trayIcon = new NotifyIcon(components)
            {
                Icon = BuildIcon(_settings.Enabled),
                Text = "HotCornersWin",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => OnSettings(null, EventArgs.Empty);

            UpdateToggleUI();
        }

        private void OnMouseMoved(object sender, Point pt)
        {
            if (_settings.Enabled)
                _cornerDetector.Update(pt);
        }

        private void OnCornerEntered(object sender, CornerTriggeredEventArgs e)
        {
            var ms = _settings.GetOrCreateMonitor(e.Screen.DeviceName);
            var action = ms.GetAction(e.Corner);
            if (action == CornerAction.None) return;
            var shortcut    = action == CornerAction.CustomShortcut ? ms.GetShortcut(e.Corner) : null;
            var volumeStep  = (action == CornerAction.VolumeUp || action == CornerAction.VolumeDown)
                              ? ms.GetVolumeStep(e.Corner) : 10;
            ActionDispatcher.Execute(action, shortcut, volumeStep);
        }

        private void OnSettings(object sender, EventArgs e)
        {
            using (var form = new SettingsForm(_settings))
            {
                if (form.ShowDialog() == DialogResult.OK)
                    _settings.Save();
            }
        }

        private void OnStartupToggle(object sender, EventArgs e)
        {
            if (StartupManager.IsEnabled)
                StartupManager.Disable();
            else
                StartupManager.Enable();
            _startupItem.Checked = StartupManager.IsEnabled;
        }

        private void OnToggle(object sender, EventArgs e)
        {
            _settings.Enabled = !_settings.Enabled;
            _settings.Save();
            UpdateToggleUI();
        }

        private void UpdateToggleUI()
        {
            bool on = _settings.Enabled;
            _toggleItem.Text = on ? "Disable" : "Enable";
            SetTrayIcon(on);
            _trayIcon.Text = on ? "HotCornersWin" : "HotCornersWin (disabled)";
        }

        // Replace the tray icon, disposing the previous one so we don't accumulate
        // icon handles as the state/accent changes.
        private void SetTrayIcon(bool on)
        {
            var previous = _trayIcon.Icon;
            _trayIcon.Icon = BuildIcon(on);
            previous?.Dispose();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // General covers theme/accent-color changes. Re-theme if following the
            // system, and always refresh the tray icon to the new accent color.
            if (e.Category == UserPreferenceCategory.General)
            {
                BeginInvoke(new Action(() =>
                {
                    if (_settings.ThemePreference == "Auto")
                        ThemeHelper.Apply(_settings);
                    SetTrayIcon(_settings.Enabled);
                }));
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            Application.Exit();
        }

        // 16x16 black tile with a colored square in each corner — a tiny
        // "hot corners" glyph. Uses the live Windows accent color (same as the
        // title bar) when enabled, gray when disabled. Rebuilt on accent changes.
        private static Icon BuildIcon(bool active)
        {
            var color = active ? ThemeHelper.GetWindowsAccentColor() : Color.Gray;
            using (var bmp = new Bitmap(16, 16))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    using (var b = new SolidBrush(color))
                    {
                        g.FillRectangle(b, 0, 0, 6, 6);
                        g.FillRectangle(b, 10, 0, 6, 6);
                        g.FillRectangle(b, 0, 10, 6, 6);
                        g.FillRectangle(b, 10, 10, 6, 6);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    // Clone into a managed icon so we can free the GDI handle now
                    // instead of leaking one on every enable/disable toggle.
                    using (var tmp = Icon.FromHandle(hIcon))
                        return (Icon)tmp.Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                _mouseHook?.Dispose();
                _trayIcon?.Dispose();
                _trayMenu?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
