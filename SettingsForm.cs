using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;

namespace HotCornersWin
{
    internal sealed class SettingsForm : MaterialForm
    {
        [DllImport("dwmapi.dll", PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref int pvAttribute, uint cbAttribute);

        private readonly AppSettings _settings;
        private readonly float _dpiScale;
        private KeyRecorder _recorder;
        private Button _recordingButton;

        private sealed class CornerControls
        {
            public MaterialComboBox Combo;
            public TextBox          ShortcutBox;
            public Button           RecordBtn;
            public NumericUpDown    VolumeStep;
        }

        private readonly Dictionary<string, Dictionary<Corner, CornerControls>> _controls =
            new Dictionary<string, Dictionary<Corner, CornerControls>>();

        private static readonly string[] ActionLabels =
        {
            "None", "Show Desktop", "Task View", "Start Menu", "Action Center",
            "Volume Up", "Volume Down", "Show Taskbar", "Lock Screen", "Custom Shortcut",
            "Open Run", "Open File Explorer"
        };

        // Base layout values at 96 DPI — all multiplied by _dpiScale via S().
        private const int BaseColLabelX   = 10;
        private const int BaseColLabelW   = 100;
        private const int BaseColComboX   = 114;
        private const int BaseColComboW   = 150;
        private const int BaseColBoxX     = 268;
        private const int BaseColBoxW     = 118;
        private const int BaseColBtnX     = 389;
        private const int BaseColBtnW     = 72;
        private const int BaseColStepX    = 268;
        private const int BaseColStepW    = 60;
        private const int BaseColStepLblX = 332;
        private const int BaseColStepLblW = 22;
        private const int BaseRowPadTop   = 55;
        private const int BaseRowH        = 48;

        // Scale a base-96-DPI pixel value to physical pixels for the current monitor.
        private int S(int v) => (int)(v * _dpiScale);

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            AutoScaleMode = AutoScaleMode.None;

            // CreateGraphics() before the handle is created returns a screen DC —
            // primary-monitor DPI. Good enough for per-form sizing.
            using (var g = CreateGraphics())
                _dpiScale = Math.Max(1f, g.DpiX / 96f);

            // MaterialSkin's text fonts are fixed at 96-DPI size; enlarge them to
            // match the layout so labels/buttons/titles scale with the controls.
            MaterialFontScaler.EnsureScaled(_dpiScale);

            MaterialSkinManager.Instance.AddFormToManage(this);
            BuildUI();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Environment.OSVersion.Version.Major >= 10 &&
                Environment.OSVersion.Version.Build >= 22000)
            {
                try
                {
                    int preference = 2; // DWMWCP_ROUND
                    DwmSetWindowAttribute(this.Handle, 33, ref preference, 4);
                }
                catch { }
            }
        }

        // Roboto at dpiScale-adjusted pixel size; falls back to Segoe UI.
        private static Font MakeFont(float dpiScale)
        {
            float px = 9f * dpiScale;
            try   { return new Font("Roboto",   px, FontStyle.Regular, GraphicsUnit.Pixel); }
            catch { return new Font("Segoe UI", px, FontStyle.Regular, GraphicsUnit.Pixel); }
        }

        private void BuildUI()
        {
            var font = MakeFont(_dpiScale);

            Text          = "HotCornersWin — Settings";
            Sizable       = false;
            MaximizeBox   = false;
            MinimizeBox   = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;

            int formW = S(600);
            int formH = S(460);
            int maxW  = Screen.PrimaryScreen.Bounds.Width - 100;
            formW = Math.Min(formW, maxW);

            ClientSize  = new Size(formW, formH);
            MinimumSize = new Size(formW, formH);
            MaximumSize = new Size(formW, formH);

            FormClosing += (s, e) => StopRecording();

            // Preserve MaterialSkin's Padding.Top (title-bar reservation); clear sides/bottom.
            Load += (s, e) => Padding = new Padding(0, Padding.Top, 0, 0);

            // ── Tab control + selector ──────────────────────────────────────────
            var tabs = new MaterialTabControl
            {
                Dock    = DockStyle.Fill,
                Margin  = new Padding(0),
                Padding = new Point(0, 0),
                Font    = font
            };
            ((Control)tabs).Padding = Padding.Empty;

            foreach (var screen in Screen.AllScreens)
            {
                var tabLabel = screen.Primary
                    ? screen.DeviceName + " (Primary)"
                    : screen.DeviceName;
                var tab            = new TabPage(tabLabel);
                var cornerControls = new Dictionary<Corner, CornerControls>();
                _controls[screen.DeviceName] = cornerControls;
                var ms = _settings.GetOrCreateMonitor(screen.DeviceName);
                AddRow(tab, "Top Left:",     Corner.TopLeft,     ms, cornerControls, 0, font);
                AddRow(tab, "Top Right:",    Corner.TopRight,    ms, cornerControls, 1, font);
                AddRow(tab, "Bottom Left:",  Corner.BottomLeft,  ms, cornerControls, 2, font);
                AddRow(tab, "Bottom Right:", Corner.BottomRight, ms, cornerControls, 3, font);
                tabs.TabPages.Add(tab);
            }

            // Custom selector: MaterialTabSelector hard-codes a 12px tab font that
            // ignores DPI, so its labels stay tiny on high-DPI displays. This one
            // draws crisp labels with the scaled font and matches the theme.
            var tabSelector = new ScaledTabSelector(font, S(3), S(16))
            {
                BaseTabControl = tabs,
                Dock           = DockStyle.Top,
                Height         = S(46),
                Margin         = new Padding(0)
            };

            // ── Dark Mode toggle ────────────────────────────────────────────────
            var themeSwitch = new MaterialSwitch
            {
                Text     = "Dark",
                Checked  = ThemeHelper.IsDark(_settings),
                AutoSize = true,
                Font     = font,
                Anchor   = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(S(12), S(10))
            };
            themeSwitch.CheckedChanged += (s, e) =>
            {
                _settings.ThemePreference = themeSwitch.Checked ? "Dark" : "Light";
                _settings.Save();
                ThemeHelper.Apply(_settings);
                tabSelector.Invalidate(); // repaint tabs with the new theme colours
            };

            // ── Check Updates button ─────────────────────────────────────────────
            var checkUpdatesBtn = new MaterialButton
            {
                Text     = "Check Updates",
                Type     = MaterialButton.MaterialButtonType.Text,
                AutoSize = false,
                Font     = font,
                Size     = new Size(S(130), S(36)),
                Margin   = new Padding(S(2), S(10), S(2), S(10))
            };
            checkUpdatesBtn.Click += (s, e) =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = "https://github.com/hamed7ir/HotCornersWin/releases",
                    UseShellExecute = true
                });

            // ── Donate button ────────────────────────────────────────────────────
            var donateBtn = new MaterialButton
            {
                Text     = "Donate ❤",
                Type     = MaterialButton.MaterialButtonType.Text,
                AutoSize = false,
                Font     = font,
                Size     = new Size(S(90), S(36)),
                Margin   = new Padding(S(2), S(10), S(2), S(10))
            };
            donateBtn.Click += (s, e) =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = "https://github.com/hamed7ir/HotCornersWin",
                    UseShellExecute = true
                });

            // ── OK / Cancel buttons ──────────────────────────────────────────────
            var okBtn = new MaterialButton
            {
                Text         = "OK",
                DialogResult = DialogResult.OK,
                Type         = MaterialButton.MaterialButtonType.Contained,
                AutoSize     = false,
                Font         = font,
                Size         = new Size(S(90), S(36)),
                Margin       = new Padding(S(4), S(10), S(4), S(10))
            };
            okBtn.Click += (s, e) => ApplyToSettings();

            var cancelBtn = new MaterialButton
            {
                Text         = "Cancel",
                DialogResult = DialogResult.Cancel,
                Type         = MaterialButton.MaterialButtonType.Outlined,
                AutoSize     = false,
                Font         = font,
                Size         = new Size(S(90), S(36)),
                Margin       = new Padding(S(4), S(10), S(4), S(10))
            };

            // ── Bottom bar: [Dark] ←spacer→ [Check Updates] [Donate] [OK] [Cancel]
            var rightBtns = new FlowLayoutPanel
            {
                Dock          = DockStyle.Right,
                AutoSize      = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false
            };
            rightBtns.Controls.Add(checkUpdatesBtn);
            rightBtns.Controls.Add(donateBtn);
            rightBtns.Controls.Add(okBtn);
            rightBtns.Controls.Add(cancelBtn);

            var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = S(48) };
            bottomBar.Controls.Add(rightBtns);
            bottomBar.Controls.Add(themeSwitch);

            // Dock order (reverse-add): tabs→Fill, tabSelector→Top, bottomBar→Bottom.
            Controls.Add(bottomBar);
            Controls.Add(tabSelector);
            Controls.Add(tabs);

            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private void AddRow(TabPage tab, string labelText, Corner corner,
            MonitorCornerSettings ms, Dictionary<Corner, CornerControls> cornerControls,
            int rowIndex, Font font)
        {
            int y = S(BaseRowPadTop) + rowIndex * S(BaseRowH);

            var label = new MaterialLabel
            {
                Text      = labelText,
                TextAlign = ContentAlignment.MiddleRight,
                Font      = font,
                Location  = new Point(S(BaseColLabelX), y + S(5)),
                Size      = new Size(S(BaseColLabelW), S(50))
            };

            var combo = new MaterialComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = font,
                Location      = new Point(S(BaseColComboX), y + S(5)),
                Width         = S(BaseColComboW)
            };
            combo.Items.AddRange(ActionLabels);
            combo.SelectedIndex = (int)ms.GetAction(corner);

            var action    = ms.GetAction(corner);
            bool isCustom = action == CornerAction.CustomShortcut;
            bool isVolume = action == CornerAction.VolumeUp || action == CornerAction.VolumeDown;

            var shortcutBox = new TextBox
            {
                Width     = S(BaseColBoxW),
                ReadOnly  = true,
                Font      = font,
                Text      = ms.GetShortcut(corner),
                BackColor = SystemColors.Window,
                Location  = new Point(S(BaseColBoxX), y + S(19))
            };
            var recordBtn = new MaterialButton
            {
                Text     = "Record",
                Font     = font,
                Width    = S(BaseColBtnW),
                Height   = S(36),
                Location = new Point(S(BaseColBtnX), y + S(12)),
                Type     = MaterialButton.MaterialButtonType.Outlined,
                AutoSize = false
            };

            var stepSpinner = new NumericUpDown
            {
                Minimum  = 1,
                Maximum  = 100,
                Value    = ms.GetVolumeStep(corner),
                Font     = font,
                Width    = S(BaseColStepW),
                Location = new Point(S(BaseColStepX), y + S(19))
            };
            var stepPctLabel = new MaterialLabel
            {
                Text     = "%",
                Font     = font,
                Location = new Point(S(BaseColStepLblX), y + S(19)),
                Size     = new Size(S(BaseColStepLblW), S(22))
            };

            shortcutBox.Visible  = isCustom;
            recordBtn.Visible    = isCustom;
            stepSpinner.Visible  = isVolume;
            stepPctLabel.Visible = isVolume;

            combo.SelectedIndexChanged += (s, e) =>
            {
                bool nowCustom = combo.SelectedIndex == (int)CornerAction.CustomShortcut;
                bool nowVolume = combo.SelectedIndex == (int)CornerAction.VolumeUp
                              || combo.SelectedIndex == (int)CornerAction.VolumeDown;
                shortcutBox.Visible  = nowCustom;
                recordBtn.Visible    = nowCustom;
                stepSpinner.Visible  = nowVolume;
                stepPctLabel.Visible = nowVolume;
                if (!nowCustom && _recorder != null && _recordingButton == recordBtn)
                    StopRecording();
            };

            recordBtn.Click += (s, e) =>
            {
                if (_recorder != null)
                {
                    bool sameBtn = _recordingButton == recordBtn;
                    StopRecording();
                    if (sameBtn) return;
                }
                StartRecording(shortcutBox, recordBtn);
            };

            tab.Controls.Add(label);
            tab.Controls.Add(combo);
            tab.Controls.Add(shortcutBox);
            tab.Controls.Add(recordBtn);
            tab.Controls.Add(stepSpinner);
            tab.Controls.Add(stepPctLabel);

            cornerControls[corner] = new CornerControls
            {
                Combo       = combo,
                ShortcutBox = shortcutBox,
                RecordBtn   = recordBtn,
                VolumeStep  = stepSpinner
            };
        }

        private void StartRecording(TextBox target, Button btn)
        {
            _recordingButton = btn;
            btn.Text = "Press keys…";
            if (btn is MaterialButton mb) mb.Type = MaterialButton.MaterialButtonType.Contained;

            _recorder = new KeyRecorder();
            _recorder.ShortcutRecorded += (s, shortcut) =>
            {
                if (shortcut != null) target.Text = shortcut;
                StopRecording();
            };
            _recorder.Start();
        }

        private void StopRecording()
        {
            if (_recorder == null) return;
            _recorder.Dispose();
            _recorder = null;
            if (_recordingButton != null)
            {
                _recordingButton.Text = "Record";
                if (_recordingButton is MaterialButton mb) mb.Type = MaterialButton.MaterialButtonType.Outlined;
                _recordingButton = null;
            }
        }

        private void ApplyToSettings()
        {
            foreach (var monKvp in _controls)
            {
                var ms = _settings.GetOrCreateMonitor(monKvp.Key);
                foreach (var c in monKvp.Value)
                {
                    ms.SetAction(c.Key, (CornerAction)c.Value.Combo.SelectedIndex);
                    ms.SetShortcut(c.Key, c.Value.ShortcutBox.Text);
                    ms.SetVolumeStep(c.Key, (int)c.Value.VolumeStep.Value);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) StopRecording();
            base.Dispose(disposing);
        }
    }
}
