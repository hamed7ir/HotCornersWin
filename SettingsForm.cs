using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace HotCornersWin
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private KeyRecorder _recorder;
        private Button _recordingButton;

        private sealed class CornerControls
        {
            public ComboBox      Combo;
            public TextBox       ShortcutBox;
            public Button        RecordBtn;
            public NumericUpDown VolumeStep;
        }

        // DeviceName → (Corner → CornerControls)
        private readonly Dictionary<string, Dictionary<Corner, CornerControls>> _controls =
            new Dictionary<string, Dictionary<Corner, CornerControls>>();

        private static readonly string[] ActionLabels =
        {
            "None", "Show Desktop", "Task View", "Start Menu", "Action Center",
            "Volume Up", "Volume Down", "Show Taskbar", "Lock Screen", "Custom Shortcut"
        };

        // Fixed x-coordinates for each column (absolute, relative to TabPage client area)
        private const int ColLabelX   = 10;
        private const int ColLabelW   = 100;
        private const int ColComboX   = 114;
        private const int ColComboW   = 150;
        // Custom Shortcut controls
        private const int ColBoxX     = 268;
        private const int ColBoxW     = 118;
        private const int ColBtnX     = 389;
        private const int ColBtnW     = 72;
        // Volume step controls (share the same x-start as shortcut controls — mutually exclusive)
        private const int ColStepX    = 268;
        private const int ColStepW    = 60;
        private const int ColStepLblX = 268 + 60 + 4;  // 332
        private const int ColStepLblW = 22;
        private const int RowPadTop   = 8;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "HotCornersWin — Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 295);

            FormClosing += (s, e) => StopRecording();

            // Row height scales with DPI: TextBox.PreferredHeight gives the natural
            // single-line text height on the current display; add 14px for breathing room.
            int rowH;
            using (var dummy = new TextBox()) rowH = dummy.PreferredHeight + 14;

            var tabs = new TabControl { Dock = DockStyle.Fill };

            foreach (var screen in Screen.AllScreens)
            {
                var tabLabel = screen.Primary
                    ? screen.DeviceName + " (Primary)"
                    : screen.DeviceName;

                var tab = new TabPage(tabLabel);
                var cornerControls = new Dictionary<Corner, CornerControls>();
                _controls[screen.DeviceName] = cornerControls;

                var ms = _settings.GetOrCreateMonitor(screen.DeviceName);

                // Controls are positioned with explicit pixel coordinates directly on
                // the TabPage — no layout manager involved, so nothing can misplace them.
                AddRow(tab, "Top Left:",     Corner.TopLeft,     ms, cornerControls, 0, rowH);
                AddRow(tab, "Top Right:",    Corner.TopRight,    ms, cornerControls, 1, rowH);
                AddRow(tab, "Bottom Left:",  Corner.BottomLeft,  ms, cornerControls, 2, rowH);
                AddRow(tab, "Bottom Right:", Corner.BottomRight, ms, cornerControls, 3, rowH);

                tabs.TabPages.Add(tab);
            }

            var okBtn = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(80, 26)
            };
            okBtn.Click += (s, e) => ApplyToSettings();

            var cancelBtn = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(80, 26)
            };

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(4)
            };
            btnPanel.Controls.Add(cancelBtn);
            btnPanel.Controls.Add(okBtn);

            Controls.Add(tabs);
            Controls.Add(btnPanel);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private void AddRow(TabPage tab, string labelText, Corner corner,
            MonitorCornerSettings ms, Dictionary<Corner, CornerControls> cornerControls,
            int rowIndex, int rowH)
        {
            int y = RowPadTop + rowIndex * rowH;

            var label = new Label
            {
                Text      = labelText,
                TextAlign = ContentAlignment.MiddleRight,
                Location  = new Point(ColLabelX, y + 2),
                Size      = new Size(ColLabelW, rowH - 4)
            };

            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(ColComboX, y + 4),
                Width    = ColComboW
            };
            combo.Items.AddRange(ActionLabels);
            combo.SelectedIndex = (int)ms.GetAction(corner);

            var action    = ms.GetAction(corner);
            bool isCustom = action == CornerAction.CustomShortcut;
            bool isVolume = action == CornerAction.VolumeUp || action == CornerAction.VolumeDown;

            // ── Custom Shortcut controls ─────────────────────────────────────────
            var shortcutBox = new TextBox
            {
                Width     = ColBoxW,
                ReadOnly  = true,
                Text      = ms.GetShortcut(corner),
                BackColor = SystemColors.Window,
                Location  = new Point(ColBoxX, y + 4)
            };
            var recordBtn = new Button
            {
                Text     = "Record",
                Width    = ColBtnW,
                Height   = shortcutBox.PreferredHeight,
                Location = new Point(ColBtnX, y + 4)
            };

            // ── Volume step controls ─────────────────────────────────────────────
            var stepSpinner = new NumericUpDown
            {
                Minimum  = 1,
                Maximum  = 100,
                Value    = ms.GetVolumeStep(corner),
                Width    = ColStepW,
                Location = new Point(ColStepX, y + 4)
            };
            var stepPctLabel = new Label
            {
                Text      = "%",
                TextAlign = ContentAlignment.MiddleLeft,
                Location  = new Point(ColStepLblX, y + 4),
                Size      = new Size(ColStepLblW, shortcutBox.PreferredHeight)
            };

            // Set initial visibility
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

        // ── Recording ────────────────────────────────────────────────────────────

        private void StartRecording(TextBox target, Button btn)
        {
            _recordingButton = btn;
            btn.Text = "Press keys…";
            btn.BackColor = Color.LightSalmon;

            _recorder = new KeyRecorder();
            _recorder.ShortcutRecorded += (s, shortcut) =>
            {
                if (shortcut != null)
                    target.Text = shortcut;
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
                _recordingButton.BackColor = SystemColors.Control;
                _recordingButton = null;
            }
        }

        // ── Apply ────────────────────────────────────────────────────────────────

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
