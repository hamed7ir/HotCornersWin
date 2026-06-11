using System;
using System.Drawing;
using System.Windows.Forms;

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

            BuildTrayIcon();

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
            _trayIcon.Icon = BuildIcon(on);
            _trayIcon.Text = on ? "HotCornersWin" : "HotCornersWin (disabled)";
        }

        private void OnExit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            Application.Exit();
        }

        private static Icon BuildIcon(bool active)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                var color = active ? Color.DodgerBlue : Color.Gray;
                using (var b = new SolidBrush(color))
                {
                    g.FillRectangle(b, 0, 0, 6, 6);
                    g.FillRectangle(b, 10, 0, 6, 6);
                    g.FillRectangle(b, 0, 10, 6, 6);
                    g.FillRectangle(b, 10, 10, 6, 6);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _mouseHook?.Dispose();
                _trayIcon?.Dispose();
                _trayMenu?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
