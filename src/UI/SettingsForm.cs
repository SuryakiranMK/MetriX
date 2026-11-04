using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using MetriX.Model;
using MetriX.Services;

namespace MetriX.UI
{
    public class SettingsForm : Form
    {
        private readonly ConfigService _configService;
        private readonly NetworkMonitor _networkMonitor;
        private readonly Action _onSettingsUpdated;

        private AppConfig _workingConfig;

        // UI Controls
        private Panel _previewPanel;
        private ComboBox _cbPosition;
        private NumericUpDown _nudOffsetX;
        private NumericUpDown _nudOffsetY;

        private ComboBox _cbLayout;
        private ComboBox _cbFont;
        private NumericUpDown _nudFontSize;
        private NumericUpDown _nudUnitFontSize;
        private CheckBox _chkFontBold;

        private CheckBox _chkAutoTheme;
        private Button _btnUploadColor;
        private Button _btnDownloadColor;
        private Button _btnLabelColor;
        private CheckBox _chkHoverHighlight;

        private CheckBox _chkIdleDimming;
        private NumericUpDown _nudIdleOpacity;
        private NumericUpDown _nudOverallOpacity;

        private ComboBox _cbArrowStyle;
        private ComboBox _cbUnitMode;
        private NumericUpDown _nudDecimals;
        private NumericUpDown _nudPaddingX;
        private NumericUpDown _nudPaddingY;
        private NumericUpDown _nudSpacing;

        private ComboBox _cbAdapter;
        private ComboBox _cbRefreshInterval;
        private CheckBox _chkStartWithWindows;
        private ComboBox _cbLeftClickAction;

        public SettingsForm(ConfigService configService, NetworkMonitor networkMonitor, Action onSettingsUpdated)
        {
            _configService = configService;
            _networkMonitor = networkMonitor;
            _onSettingsUpdated = onSettingsUpdated;

            _workingConfig = CloneConfig(_configService.Config);

            InitializeComponent();
            PopulateValues();
        }

        private static AppConfig CloneConfig(AppConfig src)
        {
            return new AppConfig
            {
                Position = src.Position,
                OffsetX = src.OffsetX,
                OffsetY = src.OffsetY,
                Layout = src.Layout,
                FontFamily = src.FontFamily,
                FontSize = src.FontSize,
                UnitFontSize = src.UnitFontSize,
                FontBold = src.FontBold,
                AutoTheme = src.AutoTheme,
                UploadColor = src.UploadColor,
                DownloadColor = src.DownloadColor,
                LabelColor = src.LabelColor,
                ArrowStyle = src.ArrowStyle,
                UnitMode = src.UnitMode,
                DecimalPlaces = src.DecimalPlaces,
                PaddingX = src.PaddingX,
                PaddingY = src.PaddingY,
                ItemSpacing = src.ItemSpacing,
                ShowHoverHighlight = src.ShowHoverHighlight,
                IdleDimming = src.IdleDimming,
                IdleThresholdKb = src.IdleThresholdKb,
                IdleOpacityPercent = src.IdleOpacityPercent,
                OverallOpacityPercent = src.OverallOpacityPercent,
                UpdateIntervalMs = src.UpdateIntervalMs,
                SelectedAdapterAlias = src.SelectedAdapterAlias,
                StartWithWindows = src.StartWithWindows,
                LeftClickAction = src.LeftClickAction
            };
        }

        private void InitializeComponent()
        {
            Text = "metriX Settings & Customization";
            Width = 540;
            Height = 680;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(32, 32, 32);
            ForeColor = Color.FromArgb(240, 240, 240);
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            try
            {
                if (System.IO.File.Exists("app.ico")) Icon = new Icon("app.ico");
                else Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            // Live Preview Card
            var previewGroup = new GroupBox
            {
                Text = "Live Preview (Interactive)",
                Left = 16,
                Top = 12,
                Width = 492,
                Height = 85,
                ForeColor = Color.FromArgb(200, 200, 200)
            };

            _previewPanel = new Panel
            {
                Left = 12,
                Top = 22,
                Width = 468,
                Height = 48,
                BackColor = Color.FromArgb(24, 24, 24)
            };
            _previewPanel.Paint += OnPreviewPaint;
            previewGroup.Controls.Add(_previewPanel);
            Controls.Add(previewGroup);

            // Tab Control
            var tabControl = new TabControl
            {
                Left = 16,
                Top = 105,
                Width = 492,
                Height = 475
            };

            // 1. Placement Tab
            var tabPos = new TabPage("Position & Layout") { BackColor = Color.FromArgb(32, 32, 32) };
            BuildPositionTab(tabPos);
            tabControl.TabPages.Add(tabPos);

            // 2. Typography & Colors Tab
            var tabStyle = new TabPage("Typography & Colors") { BackColor = Color.FromArgb(32, 32, 32) };
            BuildStyleTab(tabStyle);
            tabControl.TabPages.Add(tabStyle);

            // 3. Spacing & Units Tab
            var tabGeometry = new TabPage("Spacing & Data") { BackColor = Color.FromArgb(32, 32, 32) };
            BuildGeometryTab(tabGeometry);
            tabControl.TabPages.Add(tabGeometry);

            // 4. System & Network Tab
            var tabSystem = new TabPage("System & Network") { BackColor = Color.FromArgb(32, 32, 32) };
            BuildSystemTab(tabSystem);
            tabControl.TabPages.Add(tabSystem);

            Controls.Add(tabControl);

            // Bottom action buttons
            var btnApply = new Button
            {
                Text = "Save & Apply",
                Width = 110,
                Height = 32,
                Left = 280,
                Top = 595,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnApply.Click += (s, e) => ApplyAndClose();

            var btnCancel = new Button
            {
                Text = "Cancel",
                Width = 90,
                Height = 32,
                Left = 400,
                Top = 595,
                BackColor = Color.FromArgb(48, 48, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.Click += (s, e) => Close();

            Controls.Add(btnApply);
            Controls.Add(btnCancel);
        }

        private void BuildPositionTab(TabPage page)
        {
            int y = 20;

            AddLabel(page, "Taskbar Position:", 20, y);
            _cbPosition = new ComboBox { Left = 160, Top = y - 3, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbPosition.Items.AddRange(new object[] {
                "Right (Left of System Tray) - Recommended",
                "Left (Near Start / Widgets)",
                "Center (Right of Open Apps)",
                "Custom Coordinates"
            });
            _cbPosition.SelectedIndexChanged += (s, e) =>
            {
                _workingConfig.Position = (PositionMode)_cbPosition.SelectedIndex;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(_cbPosition);
            y += 40;

            AddLabel(page, "Fine-tune X Offset (px):", 20, y);
            _nudOffsetX = new NumericUpDown { Left = 160, Top = y - 3, Width = 100, Minimum = -2000, Maximum = 2000 };
            _nudOffsetX.ValueChanged += (s, e) => { _workingConfig.OffsetX = (int)_nudOffsetX.Value; _previewPanel.Invalidate(); };
            page.Controls.Add(_nudOffsetX);
            y += 35;

            AddLabel(page, "Fine-tune Y Offset (px):", 20, y);
            _nudOffsetY = new NumericUpDown { Left = 160, Top = y - 3, Width = 100, Minimum = -100, Maximum = 100 };
            _nudOffsetY.ValueChanged += (s, e) => { _workingConfig.OffsetY = (int)_nudOffsetY.Value; _previewPanel.Invalidate(); };
            page.Controls.Add(_nudOffsetY);
            y += 45;

            AddLabel(page, "Display Layout Mode:", 20, y);
            _cbLayout = new ComboBox { Left = 160, Top = y - 3, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbLayout.Items.AddRange(new object[] { "Stacked (Two Rows)", "Horizontal (Single Row)" });
            _cbLayout.SelectedIndexChanged += (s, e) =>
            {
                _workingConfig.Layout = (LayoutMode)_cbLayout.SelectedIndex;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(_cbLayout);
            y += 45;

            var lblHint = new Label
            {
                Text = "💡 Tip: You can also hold Ctrl + Left Click Drag directly on the taskbar indicator to reposition it freely anywhere on your screen!",
                Left = 20,
                Top = y,
                Width = 430,
                Height = 60,
                ForeColor = Color.FromArgb(160, 160, 160)
            };
            page.Controls.Add(lblHint);
        }

        private void BuildStyleTab(TabPage page)
        {
            int y = 20;

            AddLabel(page, "Font Family:", 20, y);
            _cbFont = new ComboBox { Left = 150, Top = y - 3, Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbFont.Items.AddRange(new object[] {
                "Segoe UI Variable Text",
                "Segoe UI",
                "Segoe UI Semibold",
                "Consolas",
                "JetBrains Mono",
                "Aptos",
                "Calibri",
                "Arial"
            });
            _cbFont.SelectedIndexChanged += (s, e) =>
            {
                _workingConfig.FontFamily = _cbFont.SelectedItem.ToString();
                _previewPanel.Invalidate();
            };
            page.Controls.Add(_cbFont);
            y += 38;

            AddLabel(page, "Font Size (pt):", 20, y);
            _nudFontSize = new NumericUpDown { Left = 130, Top = y - 3, Width = 65, Minimum = 7, Maximum = 20, DecimalPlaces = 1 };
            _nudFontSize.ValueChanged += (s, e) => { _workingConfig.FontSize = (float)_nudFontSize.Value; _previewPanel.Invalidate(); };
            page.Controls.Add(_nudFontSize);

            AddLabel(page, "Unit Size:", 205, y);
            _nudUnitFontSize = new NumericUpDown { Left = 275, Top = y - 3, Width = 65, Minimum = 5, Maximum = 16, DecimalPlaces = 1 };
            _nudUnitFontSize.ValueChanged += (s, e) => { _workingConfig.UnitFontSize = (float)_nudUnitFontSize.Value; _previewPanel.Invalidate(); };
            page.Controls.Add(_nudUnitFontSize);

            _chkFontBold = new CheckBox { Text = "Bold", Left = 360, Top = y - 3, AutoSize = true };
            _chkFontBold.CheckedChanged += (s, e) => { _workingConfig.FontBold = _chkFontBold.Checked; _previewPanel.Invalidate(); };
            page.Controls.Add(_chkFontBold);
            y += 38;

            _chkAutoTheme = new CheckBox { Text = "Auto-detect Windows Dark/Light taskbar theme colors", Left = 20, Top = y, AutoSize = true };
            _chkAutoTheme.CheckedChanged += (s, e) =>
            {
                _workingConfig.AutoTheme = _chkAutoTheme.Checked;
                _btnUploadColor.Enabled = !_workingConfig.AutoTheme;
                _btnDownloadColor.Enabled = !_workingConfig.AutoTheme;
                _btnLabelColor.Enabled = !_workingConfig.AutoTheme;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(_chkAutoTheme);
            y += 36;

            AddLabel(page, "Upload Speed Color:", 20, y);
            _btnUploadColor = new Button { Left = 150, Top = y - 5, Width = 100, Height = 28, FlatStyle = FlatStyle.Flat };
            _btnUploadColor.Click += (s, e) => PickColor(c => _workingConfig.UploadColor = c, _workingConfig.UploadColor, _btnUploadColor);
            page.Controls.Add(_btnUploadColor);
            y += 38;

            AddLabel(page, "Download Speed Color:", 20, y);
            _btnDownloadColor = new Button { Left = 150, Top = y - 5, Width = 100, Height = 28, FlatStyle = FlatStyle.Flat };
            _btnDownloadColor.Click += (s, e) => PickColor(c => _workingConfig.DownloadColor = c, _workingConfig.DownloadColor, _btnDownloadColor);
            page.Controls.Add(_btnDownloadColor);
            y += 38;

            AddLabel(page, "Unit / Label Color:", 20, y);
            _btnLabelColor = new Button { Left = 150, Top = y - 5, Width = 100, Height = 28, FlatStyle = FlatStyle.Flat };
            _btnLabelColor.Click += (s, e) => PickColor(c => _workingConfig.LabelColor = c, _workingConfig.LabelColor, _btnLabelColor);
            page.Controls.Add(_btnLabelColor);
            y += 42;

            _chkHoverHighlight = new CheckBox { Text = "Enable Windows 11 rounded hover highlight effect", Left = 20, Top = y, AutoSize = true };
            _chkHoverHighlight.CheckedChanged += (s, e) => { _workingConfig.ShowHoverHighlight = _chkHoverHighlight.Checked; _previewPanel.Invalidate(); };
            page.Controls.Add(_chkHoverHighlight);
            y += 40;

            // Color Presets
            AddLabel(page, "Color Presets:", 20, y);
            var btnPresetWin = new Button { Text = "Native Win11", Left = 120, Top = y - 5, Width = 95, Height = 26, FlatStyle = FlatStyle.Flat };
            btnPresetWin.Click += (s, e) => SetPreset("#E4E4E4", "#E4E4E4", "#A0A0A0");
            page.Controls.Add(btnPresetWin);

            var btnPresetCyan = new Button { Text = "Cyan & Green", Left = 225, Top = y - 5, Width = 95, Height = 26, FlatStyle = FlatStyle.Flat };
            btnPresetCyan.Click += (s, e) => SetPreset("#4CC2FF", "#6CCB5F", "#D0D0D0");
            page.Controls.Add(btnPresetCyan);

            var btnPresetAmber = new Button { Text = "Amber & Blue", Left = 330, Top = y - 5, Width = 95, Height = 26, FlatStyle = FlatStyle.Flat };
            btnPresetAmber.Click += (s, e) => SetPreset("#FFA066", "#60CDFF", "#D8D8D8");
            page.Controls.Add(btnPresetAmber);
        }

        private void SetPreset(string up, string dn, string lbl)
        {
            _workingConfig.UploadColor = up;
            _workingConfig.DownloadColor = dn;
            _workingConfig.LabelColor = lbl;
            UpdateColorButtons();
            _previewPanel.Invalidate();
        }

        private void BuildGeometryTab(TabPage page)
        {
            int y = 20;

            AddLabel(page, "Arrow Glyphs:", 20, y);
            _cbArrowStyle = new ComboBox { Left = 160, Top = y - 3, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbArrowStyle.Items.AddRange(new object[] { "Triangles (▲ / ▼)", "Arrows (↑ / ↓)", "Letters (U: / D:)", "None" });
            _cbArrowStyle.SelectedIndexChanged += (s, e) =>
            {
                _workingConfig.ArrowStyle = (ArrowStyle)_cbArrowStyle.SelectedIndex;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(_cbArrowStyle);
            y += 38;

            AddLabel(page, "Unit Format:", 20, y);
            _cbUnitMode = new ComboBox { Left = 160, Top = y - 3, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbUnitMode.Items.AddRange(new object[] {
                "Auto Bytes (B/s, KB/s, MB/s, GB/s)",
                "Auto Bits (bps, Kbps, Mbps, Gbps)",
                "Always KB/s",
                "Always MB/s"
            });
            _cbUnitMode.SelectedIndexChanged += (s, e) =>
            {
                _workingConfig.UnitMode = (UnitMode)_cbUnitMode.SelectedIndex;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(_cbUnitMode);
            y += 38;

            AddLabel(page, "Decimal Places:", 20, y);
            _nudDecimals = new NumericUpDown { Left = 160, Top = y - 3, Width = 90, Minimum = 0, Maximum = 3 };
            _nudDecimals.ValueChanged += (s, e) => { _workingConfig.DecimalPlaces = (int)_nudDecimals.Value; _previewPanel.Invalidate(); };
            page.Controls.Add(_nudDecimals);
            y += 38;

            AddLabel(page, "Horizontal Padding (px):", 20, y);
            _nudPaddingX = new NumericUpDown { Left = 160, Top = y - 3, Width = 90, Minimum = 0, Maximum = 40 };
            _nudPaddingX.ValueChanged += (s, e) => { _workingConfig.PaddingX = (int)_nudPaddingX.Value; _previewPanel.Invalidate(); };
            page.Controls.Add(_nudPaddingX);
            y += 38;

            AddLabel(page, "Vertical Padding (px):", 20, y);
            _nudPaddingY = new NumericUpDown { Left = 160, Top = y - 3, Width = 90, Minimum = 0, Maximum = 15 };
            _nudPaddingY.ValueChanged += (s, e) => { _workingConfig.PaddingY = (int)_nudPaddingY.Value; _previewPanel.Invalidate(); };
            page.Controls.Add(_nudPaddingY);
            y += 38;

            AddLabel(page, "Item Spacing (px):", 20, y);
            _nudSpacing = new NumericUpDown { Left = 160, Top = y - 3, Width = 90, Minimum = 0, Maximum = 30 };
            _nudSpacing.ValueChanged += (s, e) => { _workingConfig.ItemSpacing = (int)_nudSpacing.Value; _previewPanel.Invalidate(); };
            page.Controls.Add(_nudSpacing);
            y += 38;

            AddLabel(page, "Overall Opacity (%):", 20, y);
            _nudOverallOpacity = new NumericUpDown { Left = 160, Top = y - 3, Width = 90, Minimum = 30, Maximum = 100 };
            _nudOverallOpacity.ValueChanged += (s, e) => { _workingConfig.OverallOpacityPercent = (int)_nudOverallOpacity.Value; _previewPanel.Invalidate(); };
            page.Controls.Add(_nudOverallOpacity);
            y += 36;

            _chkIdleDimming = new CheckBox { Text = "Dim display when network is idle (< 1 KB/s)", Left = 20, Top = y, AutoSize = true };
            _chkIdleDimming.CheckedChanged += (s, e) =>
            {
                _workingConfig.IdleDimming = _chkIdleDimming.Checked;
                _nudIdleOpacity.Enabled = _chkIdleDimming.Checked;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(_chkIdleDimming);
            y += 30;

            AddLabel(page, "Idle Dim Opacity (%):", 40, y);
            _nudIdleOpacity = new NumericUpDown { Left = 175, Top = y - 3, Width = 75, Minimum = 15, Maximum = 90 };
            _nudIdleOpacity.ValueChanged += (s, e) => { _workingConfig.IdleOpacityPercent = (int)_nudIdleOpacity.Value; _previewPanel.Invalidate(); };
            page.Controls.Add(_nudIdleOpacity);
        }

        private void BuildSystemTab(TabPage page)
        {
            int y = 20;

            AddLabel(page, "Network Adapter:", 20, y);
            _cbAdapter = new ComboBox { Left = 160, Top = y - 3, Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbAdapter.Items.Add("All Active Adapters (Aggregated)");

            var adapters = _networkMonitor.GetAvailableAdapters();
            foreach (var a in adapters)
            {
                _cbAdapter.Items.Add(a.Alias);
            }
            _cbAdapter.SelectedIndex = 0;
            _cbAdapter.SelectedIndexChanged += (s, e) =>
            {
                if (_cbAdapter.SelectedIndex <= 0)
                {
                    _workingConfig.SelectedAdapterAlias = "";
                }
                else
                {
                    _workingConfig.SelectedAdapterAlias = _cbAdapter.SelectedItem.ToString();
                }
            };
            page.Controls.Add(_cbAdapter);
            y += 45;

            AddLabel(page, "Refresh Interval:", 20, y);
            _cbRefreshInterval = new ComboBox { Left = 160, Top = y - 3, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbRefreshInterval.Items.AddRange(new object[] { "500 ms (Fast)", "1000 ms (1 sec - Recommended)", "1500 ms", "2000 ms (2 sec)", "3000 ms" });
            _cbRefreshInterval.SelectedIndexChanged += (s, e) =>
            {
                switch (_cbRefreshInterval.SelectedIndex)
                {
                    case 0: _workingConfig.UpdateIntervalMs = 500; break;
                    case 1: _workingConfig.UpdateIntervalMs = 1000; break;
                    case 2: _workingConfig.UpdateIntervalMs = 1500; break;
                    case 3: _workingConfig.UpdateIntervalMs = 2000; break;
                    case 4: _workingConfig.UpdateIntervalMs = 3000; break;
                }
            };
            page.Controls.Add(_cbRefreshInterval);
            y += 45;

            AddLabel(page, "Left-Click Action:", 20, y);
            _cbLeftClickAction = new ComboBox { Left = 160, Top = y - 3, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbLeftClickAction.Items.AddRange(new object[] { "Open Windows Task Manager", "Do Nothing" });
            _cbLeftClickAction.SelectedIndexChanged += (s, e) =>
            {
                _workingConfig.LeftClickAction = _cbLeftClickAction.SelectedIndex == 0 ? LeftClickAction.TaskManager : LeftClickAction.None;
            };
            page.Controls.Add(_cbLeftClickAction);
            y += 45;

            _chkStartWithWindows = new CheckBox { Text = "Start automatically when Windows boots", Left = 20, Top = y, AutoSize = true };
            _chkStartWithWindows.CheckedChanged += (s, e) => { _workingConfig.StartWithWindows = _chkStartWithWindows.Checked; };
            page.Controls.Add(_chkStartWithWindows);
        }

        private static void AddLabel(Control parent, string text, int x, int y)
        {
            var lbl = new Label { Text = text, Left = x, Top = y, AutoSize = true, ForeColor = Color.FromArgb(220, 220, 220) };
            parent.Controls.Add(lbl);
        }

        private void PopulateValues()
        {
            _cbPosition.SelectedIndex = (int)_workingConfig.Position;
            _nudOffsetX.Value = _workingConfig.OffsetX;
            _nudOffsetY.Value = _workingConfig.OffsetY;
            _cbLayout.SelectedIndex = (int)_workingConfig.Layout;

            int fIdx = _cbFont.FindStringExact(_workingConfig.FontFamily);
            _cbFont.SelectedIndex = fIdx >= 0 ? fIdx : 0;
            _nudFontSize.Value = (decimal)_workingConfig.FontSize;
            _nudUnitFontSize.Value = (decimal)_workingConfig.UnitFontSize;
            _chkFontBold.Checked = _workingConfig.FontBold;

            _chkAutoTheme.Checked = _workingConfig.AutoTheme;
            _btnUploadColor.Enabled = !_workingConfig.AutoTheme;
            _btnDownloadColor.Enabled = !_workingConfig.AutoTheme;
            _btnLabelColor.Enabled = !_workingConfig.AutoTheme;

            UpdateColorButtons();
            _chkHoverHighlight.Checked = _workingConfig.ShowHoverHighlight;

            _nudOverallOpacity.Value = _workingConfig.OverallOpacityPercent;
            _chkIdleDimming.Checked = _workingConfig.IdleDimming;
            _nudIdleOpacity.Value = _workingConfig.IdleOpacityPercent;
            _nudIdleOpacity.Enabled = _workingConfig.IdleDimming;

            _cbArrowStyle.SelectedIndex = (int)_workingConfig.ArrowStyle;
            _cbUnitMode.SelectedIndex = (int)_workingConfig.UnitMode;
            _nudDecimals.Value = _workingConfig.DecimalPlaces;
            _nudPaddingX.Value = _workingConfig.PaddingX;
            _nudPaddingY.Value = _workingConfig.PaddingY;
            _nudSpacing.Value = _workingConfig.ItemSpacing;

            if (!string.IsNullOrEmpty(_workingConfig.SelectedAdapterAlias))
            {
                int aIdx = _cbAdapter.FindStringExact(_workingConfig.SelectedAdapterAlias);
                _cbAdapter.SelectedIndex = aIdx >= 0 ? aIdx : 0;
            }
            else
            {
                _cbAdapter.SelectedIndex = 0;
            }

            if (_workingConfig.UpdateIntervalMs <= 700) _cbRefreshInterval.SelectedIndex = 0;
            else if (_workingConfig.UpdateIntervalMs <= 1200) _cbRefreshInterval.SelectedIndex = 1;
            else if (_workingConfig.UpdateIntervalMs <= 1700) _cbRefreshInterval.SelectedIndex = 2;
            else if (_workingConfig.UpdateIntervalMs <= 2500) _cbRefreshInterval.SelectedIndex = 3;
            else _cbRefreshInterval.SelectedIndex = 4;

            _cbLeftClickAction.SelectedIndex = _workingConfig.LeftClickAction == LeftClickAction.TaskManager ? 0 : 1;
            _chkStartWithWindows.Checked = _workingConfig.StartWithWindows;
        }

        private void UpdateColorButtons()
        {
            _btnUploadColor.BackColor = ColorTranslator.FromHtml(_workingConfig.UploadColor);
            _btnDownloadColor.BackColor = ColorTranslator.FromHtml(_workingConfig.DownloadColor);
            _btnLabelColor.BackColor = ColorTranslator.FromHtml(_workingConfig.LabelColor);
        }

        private void PickColor(Action<string> setter, string currentColorHex, Button btn)
        {
            using (var cd = new ColorDialog())
            {
                cd.Color = ColorTranslator.FromHtml(currentColorHex);
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    string colorHex = "#" + cd.Color.R.ToString("X2") + cd.Color.G.ToString("X2") + cd.Color.B.ToString("X2");
                    setter(colorHex);
                    btn.BackColor = cd.Color;
                    _previewPanel.Invalidate();
                }
            }
        }

        private void OnPreviewPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // Draw simulated taskbar background
            g.Clear(Color.FromArgb(28, 28, 28));

            string fontName = string.IsNullOrEmpty(_workingConfig.FontFamily) ? "Segoe UI Variable Text" : _workingConfig.FontFamily;
            FontStyle style = _workingConfig.FontBold ? FontStyle.Bold : FontStyle.Regular;
            float unitSizePt = _workingConfig.UnitFontSize > 0 ? _workingConfig.UnitFontSize : Math.Max(6.0f, _workingConfig.FontSize * 0.8f);

            string uHex, dHex, lHex;
            if (_workingConfig.AutoTheme)
            {
                ThemeHelper.GetThemeColors(false, out uHex, out dHex, out lHex);
            }
            else
            {
                uHex = _workingConfig.UploadColor;
                dHex = _workingConfig.DownloadColor;
                lHex = _workingConfig.LabelColor;
            }

            int alpha = (int)(Math.Max(0.3f, _workingConfig.OverallOpacityPercent / 100.0f) * 255);

            using (Font font = new Font(fontName, _workingConfig.FontSize, style))
            using (Font unitFont = new Font(fontName, unitSizePt, FontStyle.Regular))
            using (Brush upBrush = new SolidBrush(Color.FromArgb(alpha, ColorTranslator.FromHtml(uHex))))
            using (Brush dnBrush = new SolidBrush(Color.FromArgb(alpha, ColorTranslator.FromHtml(dHex))))
            using (Brush lblBrush = new SolidBrush(Color.FromArgb(alpha, ColorTranslator.FromHtml(lHex))))
            {
                float baselineOffset = (font.GetHeight(g) - unitFont.GetHeight(g)) * 0.75f;

                string upArrow = _workingConfig.ArrowStyle == ArrowStyle.Triangle ? "▲" :
                                 _workingConfig.ArrowStyle == ArrowStyle.Arrow ? "↑" :
                                 _workingConfig.ArrowStyle == ArrowStyle.Letter ? "U:" : "";
                string dnArrow = _workingConfig.ArrowStyle == ArrowStyle.Triangle ? "▼" :
                                 _workingConfig.ArrowStyle == ArrowStyle.Arrow ? "↓" :
                                 _workingConfig.ArrowStyle == ArrowStyle.Letter ? "D:" : "";

                string sampleUpVal = "1.4";
                string sampleDnVal = "18.2";
                string sampleUnit = "MB/s";

                int startX = 20;

                if (_workingConfig.Layout == LayoutMode.Stacked)
                {
                    int y1 = 4;
                    int y2 = 22;

                    float x1 = startX;
                    if (!string.IsNullOrEmpty(upArrow))
                    {
                        g.DrawString(upArrow, font, upBrush, x1, y1);
                        x1 += g.MeasureString(upArrow, font).Width + 2;
                    }
                    g.DrawString(sampleUpVal, font, upBrush, x1, y1);
                    x1 += g.MeasureString(sampleUpVal, font).Width + 2;
                    g.DrawString(sampleUnit, unitFont, lblBrush, x1, y1 + baselineOffset);

                    float x2 = startX;
                    if (!string.IsNullOrEmpty(dnArrow))
                    {
                        g.DrawString(dnArrow, font, dnBrush, x2, y2);
                        x2 += g.MeasureString(dnArrow, font).Width + 2;
                    }
                    g.DrawString(sampleDnVal, font, dnBrush, x2, y2);
                    x2 += g.MeasureString(sampleDnVal, font).Width + 2;
                    g.DrawString(sampleUnit, unitFont, lblBrush, x2, y2 + baselineOffset);
                }
                else
                {
                    float y = (_previewPanel.Height - font.GetHeight(g)) / 2f;
                    float x = startX;

                    if (!string.IsNullOrEmpty(upArrow))
                    {
                        g.DrawString(upArrow, font, upBrush, x, y);
                        x += g.MeasureString(upArrow, font).Width + 2;
                    }
                    g.DrawString(sampleUpVal, font, upBrush, x, y);
                    x += g.MeasureString(sampleUpVal, font).Width + 2;
                    g.DrawString(sampleUnit, unitFont, lblBrush, x, y + baselineOffset);
                    x += g.MeasureString(sampleUnit, unitFont).Width + _workingConfig.ItemSpacing + 10;

                    if (!string.IsNullOrEmpty(dnArrow))
                    {
                        g.DrawString(dnArrow, font, dnBrush, x, y);
                        x += g.MeasureString(dnArrow, font).Width + 2;
                    }
                    g.DrawString(sampleDnVal, font, dnBrush, x, y);
                    x += g.MeasureString(sampleDnVal, font).Width + 2;
                    g.DrawString(sampleUnit, unitFont, lblBrush, x, y + baselineOffset);
                }
            }
        }

        private void ApplyAndClose()
        {
            _configService.Save(_workingConfig);
            if (_onSettingsUpdated != null) _onSettingsUpdated();
            Close();
        }
    }
}
