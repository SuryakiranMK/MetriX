using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using MetriX.Model;
using MetriX.Native;
using MetriX.Services;

namespace MetriX.UI
{
    public class SettingsForm : Form
    {
        private readonly ConfigService _configService;
        private readonly NetworkMonitor _networkMonitor;
        private readonly Action _onSettingsUpdated;

        private AppConfig _workingConfig;

        // UI Architecture: Left Nav + Content Pages
        private Panel _navRail;
        private Panel _contentContainer;
        private readonly List<Button> _navButtons = new List<Button>();
        private readonly List<Panel> _pages = new List<Panel>();
        private int _selectedPageIndex = 0;

        // Header Live Preview
        private Panel _previewPanel;

        // Controls - Page 0: Position & Layout
        private FluentDropdown _cbPosition;
        private FluentDropdown _cbLayout;
        private FluentNumericStepper _stepperOffsetX;
        private FluentNumericStepper _stepperOffsetY;
        private FluentToggleSwitch _toggleAllMonitors;
        private FluentToggleSwitch _toggleFloatingMode;

        // Controls - Page 1: Typography & Colors
        private FluentDropdown _cbFont;
        private FluentNumericStepper _stepperFontSize;
        private FluentNumericStepper _stepperUnitFontSize;
        private FluentToggleSwitch _toggleFontBold;
        private FluentToggleSwitch _toggleAutoTheme;
        private Panel _chipUploadColor;
        private Panel _chipDownloadColor;
        private Panel _chipLabelColor;
        private FluentToggleSwitch _toggleHoverHighlight;

        // Controls - Page 2: Spacing & Geometry
        private FluentDropdown _cbArrowStyle;
        private FluentDropdown _cbUnitMode;
        private FluentNumericStepper _stepperDecimals;
        private FluentNumericStepper _stepperPaddingX;
        private FluentNumericStepper _stepperPaddingY;
        private FluentNumericStepper _stepperSpacing;
        private FluentNumericStepper _stepperOverallOpacity;
        private FluentToggleSwitch _toggleIdleDimming;
        private FluentNumericStepper _stepperIdleOpacity;

        // Controls - Page 3: System & Network
        private readonly UpdateService _updateService;
        private FluentDropdown _cbAdapter;
        private FluentDropdown _cbRefreshInterval;
        private FluentDropdown _cbLeftClickAction;
        private FluentNumericStepper _stepperDataQuota;
        private FluentToggleSwitch _toggleStartWithWindows;
        private FluentToggleSwitch _toggleSilentUpdate;

        public SettingsForm(ConfigService configService, NetworkMonitor networkMonitor, UpdateService updateService, Action onSettingsUpdated)
        {
            _configService = configService;
            _networkMonitor = networkMonitor;
            _updateService = updateService;
            _onSettingsUpdated = onSettingsUpdated;

            _workingConfig = CloneConfig(_configService.Config);

            InitializeFluentWindow();
            BuildNavigationRail();
            BuildContentPages();
            BuildBottomActionBar();
            PopulateValues();

            SwitchPage(0);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDwmStyling();
        }

        private void ApplyDwmStyling()
        {
            try
            {
                // Enable Windows 11 Immersive Dark Mode for title bar
                int darkMode = 1;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                // Assert Windows 11 12px rounded corners
                int cornerPreference = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

                // Request Mica / Acrylic backdrop
                int backdrop = NativeMethods.DWMSBT_MAINWINDOW;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
            catch
            {
            }
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
                LeftClickAction = src.LeftClickAction,
                ShowOnAllMonitors = src.ShowOnAllMonitors,
                MonthlyDataQuotaGb = src.MonthlyDataQuotaGb,
                IsFloatingMode = src.IsFloatingMode,
                FloatingX = src.FloatingX,
                FloatingY = src.FloatingY,
                FloatingAlwaysOnTop = src.FloatingAlwaysOnTop
            };
        }

        private void InitializeFluentWindow()
        {
            Text = "metriX Settings";
            Width = 840;
            Height = 720;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(28, 28, 30);
            ForeColor = Color.FromArgb(240, 240, 240);
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            try
            {
                if (System.IO.File.Exists("app.ico")) Icon = new Icon("app.ico");
                else Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
        }

        private static Font GetSegoeGlyphFont(float size)
        {
            string[] preferredFonts = new string[] { "Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol" };
            foreach (var name in preferredFonts)
            {
                try
                {
                    using (var test = new Font(name, size))
                    {
                        if (test.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            return new Font(name, size, FontStyle.Regular);
                        }
                    }
                }
                catch { }
            }
            return new Font("Segoe UI", size, FontStyle.Regular);
        }

        private struct NavItemDef
        {
            public string Glyph;
            public string Title;
            public NavItemDef(string glyph, string title) { Glyph = glyph; Title = title; }
        }

        private void BuildNavigationRail()
        {
            _navRail = new Panel
            {
                Location = new Point(14, 14),
                Size = new Size(198, 620),
                BackColor = Color.FromArgb(24, 24, 26)
            };
            _navRail.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(42, 42, 46), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, _navRail.Width - 1, _navRail.Height - 1);
                }
            };

            // App Brand Header inside Rail
            var lblBrand = new Label
            {
                Text = "metriX",
                Font = new Font("Segoe UI", 12.0f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(16, 16),
                AutoSize = true
            };
            var lblSubBrand = new Label
            {
                Text = "Taskbar Indicator",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(140, 140, 145),
                Location = new Point(17, 38),
                AutoSize = true
            };
            _navRail.Controls.Add(lblBrand);
            _navRail.Controls.Add(lblSubBrand);

            // Nav category items with native Segoe vector glyphs
            NavItemDef[] navItems = new NavItemDef[]
            {
                new NavItemDef("\uE707", "Position & Layout"),
                new NavItemDef("\uE790", "Typography & Colors"),
                new NavItemDef("\uE947", "Spacing & Geometry"),
                new NavItemDef("\uE713", "System & Network")
            };

            int btnY = 75;
            for (int i = 0; i < navItems.Length; i++)
            {
                int index = i;
                var itemDef = navItems[i];
                var btn = new Button
                {
                    Text = "",
                    Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(210, 210, 215),
                    BackColor = Color.Transparent,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(182, 42),
                    Location = new Point(8, btnY),
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 38, 42);
                btn.Click += (s, e) => SwitchPage(index);

                btn.Paint += (s, e) =>
                {
                    Graphics g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                    bool isSelected = (_selectedPageIndex == index);
                    if (isSelected)
                    {
                        using (var brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                        {
                            g.FillRectangle(brush, 0, 8, 3, btn.Height - 16);
                        }
                    }

                    Color textCol = isSelected ? Color.White : Color.FromArgb(200, 200, 205);

                    // Draw crisp Segoe vector glyph
                    using (Font glyphFont = GetSegoeGlyphFont(11.0f))
                    {
                        Rectangle glyphRect = new Rectangle(12, 0, 24, btn.Height);
                        TextRenderer.DrawText(g, itemDef.Glyph, glyphFont, glyphRect, textCol,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    }

                    // Draw Nav Title text
                    Rectangle labelRect = new Rectangle(38, 0, btn.Width - 42, btn.Height);
                    TextRenderer.DrawText(g, itemDef.Title, btn.Font, labelRect, textCol,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                };

                _navButtons.Add(btn);
                _navRail.Controls.Add(btn);
                btnY += 48;
            }

            Controls.Add(_navRail);
        }

        private void SwitchPage(int index)
        {
            _selectedPageIndex = index;
            for (int i = 0; i < _navButtons.Count; i++)
            {
                if (i == index)
                {
                    _navButtons[i].BackColor = Color.FromArgb(36, 36, 40);
                    _navButtons[i].ForeColor = Color.White;
                    _navButtons[i].Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                }
                else
                {
                    _navButtons[i].BackColor = Color.Transparent;
                    _navButtons[i].ForeColor = Color.FromArgb(190, 190, 195);
                    _navButtons[i].Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
                }
                _navButtons[i].Invalidate();
            }

            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].Visible = (i == index);
            }
        }

        private void BuildContentPages()
        {
            _contentContainer = new Panel
            {
                Location = new Point(226, 14),
                Size = new Size(585, 620),
                BackColor = Color.Transparent
            };

            // Top Live Preview Hero Card
            var previewCard = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(585, 80),
                BackColor = Color.FromArgb(24, 24, 26)
            };
            previewCard.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(42, 42, 46), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, previewCard.Width - 1, previewCard.Height - 1);
                }
            };

            var lblPreviewHeader = new Label
            {
                Text = "LIVE INTERACTIVE PREVIEW",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 140, 145),
                Location = new Point(14, 8),
                AutoSize = true
            };
            previewCard.Controls.Add(lblPreviewHeader);

            _previewPanel = new Panel
            {
                Location = new Point(14, 26),
                Size = new Size(557, 44),
                BackColor = Color.FromArgb(18, 18, 20)
            };
            _previewPanel.Paint += OnPreviewPaint;
            previewCard.Controls.Add(_previewPanel);

            _contentContainer.Controls.Add(previewCard);

            // Container for page contents
            int pageY = 92;
            int pageH = 528;

            // Page 0: Position & Layout
            var pagePos = CreateScrollablePage(pageY, pageH);
            BuildPositionCards(pagePos);
            _pages.Add(pagePos);
            _contentContainer.Controls.Add(pagePos);

            // Page 1: Typography & Colors
            var pageStyle = CreateScrollablePage(pageY, pageH);
            BuildStyleCards(pageStyle);
            _pages.Add(pageStyle);
            _contentContainer.Controls.Add(pageStyle);

            // Page 2: Spacing & Geometry
            var pageSpacing = CreateScrollablePage(pageY, pageH);
            BuildSpacingCards(pageSpacing);
            _pages.Add(pageSpacing);
            _contentContainer.Controls.Add(pageSpacing);

            // Page 3: System & Network
            var pageSystem = CreateScrollablePage(pageY, pageH);
            BuildSystemCards(pageSystem);
            _pages.Add(pageSystem);
            _contentContainer.Controls.Add(pageSystem);

            Controls.Add(_contentContainer);
        }

        private Panel CreateScrollablePage(int y, int height)
        {
            return new Panel
            {
                Location = new Point(0, y),
                Size = new Size(585, height),
                AutoScroll = false,
                BackColor = Color.Transparent,
                Visible = false
            };
        }

        private void BuildPositionCards(Panel page)
        {
            int y = 6;

            // Card 1: Taskbar Position
            _cbPosition = new FluentDropdown { Width = 240 };
            _cbPosition.Items.Add("Right (Left of System Tray)");
            _cbPosition.Items.Add("Left (Near Start / Widgets)");
            _cbPosition.Items.Add("Center (Near Running Apps)");
            _cbPosition.SelectedIndexChanged += (s, e) =>
            {
                _workingConfig.Position = (PositionMode)_cbPosition.SelectedIndex;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Taskbar Position", "Anchor indicator relative to taskbar shell icons", _cbPosition) { Location = new Point(0, y) });
            y += 72;

            // Card 2: Display Layout Mode
            _cbLayout = new FluentDropdown { Width = 240 };
            _cbLayout.Items.Add("Stacked (Two Rows - Clock Height)");
            _cbLayout.Items.Add("Horizontal (Single Row)");
            _cbLayout.SelectedIndexChanged += (s, e) =>
            {
                _workingConfig.Layout = (LayoutMode)_cbLayout.SelectedIndex;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Display Layout Mode", "Choose between two-row stacked or single-row display", _cbLayout) { Location = new Point(0, y) });
            y += 72;

            // Card 3: Horizontal Offset X
            _stepperOffsetX = new FluentNumericStepper { Minimum = -2000, Maximum = 2000, Increment = 5, Suffix = " px", Size = new Size(120, 28) };
            _stepperOffsetX.ValueChanged += (s, e) =>
            {
                _workingConfig.OffsetX = (int)_stepperOffsetX.Value;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Fine-tune Horizontal Offset", "Shift meter horizontally along taskbar", _stepperOffsetX) { Location = new Point(0, y) });
            y += 72;

            // Card 4: Vertical Offset Y
            _stepperOffsetY = new FluentNumericStepper { Minimum = -100, Maximum = 100, Increment = 1, Suffix = " px", Size = new Size(120, 28) };
            _stepperOffsetY.ValueChanged += (s, e) =>
            {
                _workingConfig.OffsetY = (int)_stepperOffsetY.Value;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Fine-tune Vertical Offset", "Adjust vertical baseline centering", _stepperOffsetY) { Location = new Point(0, y) });
            y += 72;

            // Card 5: Multi-Monitor Mirroring
            _toggleAllMonitors = new FluentToggleSwitch();
            _toggleAllMonitors.CheckedChanged += (s, e) =>
            {
                _workingConfig.ShowOnAllMonitors = _toggleAllMonitors.Checked;
            };
            page.Controls.Add(new FluentSettingCard("Multi-Monitor Mirroring", "Mirror synchronized indicator on all secondary display taskbars", _toggleAllMonitors) { Location = new Point(0, y) });
            y += 72;

            // Card 6: Detachable Floating Mini-Widget
            _toggleFloatingMode = new FluentToggleSwitch();
            _toggleFloatingMode.CheckedChanged += (s, e) =>
            {
                _workingConfig.IsFloatingMode = _toggleFloatingMode.Checked;
            };
            page.Controls.Add(new FluentSettingCard("Detachable Floating Mini-Widget", "Detach meter from taskbar into a draggable desktop widget", _toggleFloatingMode) { Location = new Point(0, y) });
            y += 72;
        }

        private void BuildStyleCards(Panel page)
        {
            int y = 6;

            // Card 1: Font Family
            _cbFont = new FluentDropdown { Width = 240 };
            _cbFont.Items.Add("Segoe UI Variable Text");
            _cbFont.Items.Add("Segoe UI");
            _cbFont.Items.Add("Consolas");
            _cbFont.Items.Add("Calibri");
            _cbFont.Items.Add("Arial");
            _cbFont.Items.Add("Bahnschrift");
            _cbFont.SelectedIndexChanged += (s, e) =>
            {
                if (_cbFont.SelectedItem != null)
                {
                    _workingConfig.FontFamily = _cbFont.SelectedItem;
                    _previewPanel.Invalidate();
                }
            };
            page.Controls.Add(new FluentSettingCard("Typeface (Font Family)", "Select typeface for telemetry numbers and units", _cbFont) { Location = new Point(0, y) });
            y += 72;

            // Card 2: Font Size & Bold
            var pnlFontProps = new Panel { Size = new Size(245, 32), BackColor = Color.Transparent };
            _stepperFontSize = new FluentNumericStepper { Minimum = 6, Maximum = 24, Increment = 0.5m, DecimalPlaces = 1, Suffix = " pt", Size = new Size(110, 28) };
            _stepperFontSize.ValueChanged += (s, e) =>
            {
                _workingConfig.FontSize = (float)_stepperFontSize.Value;
                _previewPanel.Invalidate();
            };
            pnlFontProps.Controls.Add(_stepperFontSize);

            _toggleFontBold = new FluentToggleSwitch { Location = new Point(122, 4) };
            _toggleFontBold.CheckedChanged += (s, e) =>
            {
                _workingConfig.FontBold = _toggleFontBold.Checked;
                _previewPanel.Invalidate();
            };
            pnlFontProps.Controls.Add(_toggleFontBold);

            var lblBold = new Label { Text = "Bold", Font = new Font("Segoe UI", 8.5f, FontStyle.Regular), ForeColor = Color.FromArgb(180, 180, 185), Location = new Point(172, 6), AutoSize = true, UseMnemonic = false };
            pnlFontProps.Controls.Add(lblBold);

            page.Controls.Add(new FluentSettingCard("Digits Font Size & Weight", "Configure size (pt) and bold weight for speed numbers", pnlFontProps) { Location = new Point(0, y) });
            y += 72;

            // Card 3: Unit Size
            _stepperUnitFontSize = new FluentNumericStepper { Minimum = 5, Maximum = 20, Increment = 0.5m, DecimalPlaces = 1, Suffix = " pt", Size = new Size(120, 28) };
            _stepperUnitFontSize.ValueChanged += (s, e) =>
            {
                _workingConfig.UnitFontSize = (float)_stepperUnitFontSize.Value;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Unit Label Font Size", "Independent font size for KB/s, MB/s unit suffixes", _stepperUnitFontSize) { Location = new Point(0, y) });
            y += 72;

            // Card 4: Auto Theme
            _toggleAutoTheme = new FluentToggleSwitch();
            _toggleAutoTheme.CheckedChanged += (s, e) =>
            {
                _workingConfig.AutoTheme = _toggleAutoTheme.Checked;
                UpdateColorChipStates();
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Windows Auto-Theme Detection", "Automatically harmonize colors with Dark or Light taskbar", _toggleAutoTheme) { Location = new Point(0, y) });
            y += 72;

            // Card 5: Speed Accent Colors
            var pnlColorChips = new Panel { Size = new Size(255, 34), BackColor = Color.Transparent };
            _chipUploadColor = CreateColorChip(0, 76, "Upload", hex => _workingConfig.UploadColor = hex);
            _chipDownloadColor = CreateColorChip(82, 86, "Download", hex => _workingConfig.DownloadColor = hex);
            _chipLabelColor = CreateColorChip(174, 76, "Units", hex => _workingConfig.LabelColor = hex);

            pnlColorChips.Controls.Add(_chipUploadColor);
            pnlColorChips.Controls.Add(_chipDownloadColor);
            pnlColorChips.Controls.Add(_chipLabelColor);

            page.Controls.Add(new FluentSettingCard("Speed Accent Colors", "Upload (▲), Download (▼), and Unit Label colors", pnlColorChips) { Location = new Point(0, y) });
            y += 72;

            // Card 6: Color Presets
            var pnlPresets = new Panel { Size = new Size(280, 32), BackColor = Color.Transparent };
            Button btnFluent = CreatePresetButton("Default", 0, 64, "#4CC2FF", "#6CCB5F", "#D0D0D0");
            Button btnBlue = CreatePresetButton("Windows", 68, 64, "#60CDFF", "#0078D4", "#CCCCCC");
            Button btnAmber = CreatePresetButton("Amber", 136, 64, "#FFA726", "#FB8C00", "#F5F5F5");
            Button btnMono = CreatePresetButton("Mono", 204, 64, "#FFFFFF", "#CCCCCC", "#999999");
            pnlPresets.Controls.Add(btnFluent);
            pnlPresets.Controls.Add(btnBlue);
            pnlPresets.Controls.Add(btnAmber);
            pnlPresets.Controls.Add(btnMono);

            page.Controls.Add(new FluentSettingCard("Color Palette Presets", "One-click harmonious Fluent palettes", pnlPresets) { Location = new Point(0, y) });
            y += 72;

            // Card 7: Hover Highlight Pill
            _toggleHoverHighlight = new FluentToggleSwitch();
            _toggleHoverHighlight.CheckedChanged += (s, e) =>
            {
                _workingConfig.ShowHoverHighlight = _toggleHoverHighlight.Checked;
            };
            page.Controls.Add(new FluentSettingCard("Taskbar Hover Highlight Pill", "Display subtle Windows 11 rounded background pill on cursor hover", _toggleHoverHighlight) { Location = new Point(0, y) });
            y += 72;
        }

        private void BuildSpacingCards(Panel page)
        {
            int y = 6;

            // Card 1: Arrow Glyphs
            _cbArrowStyle = new FluentDropdown { Width = 240 };
            _cbArrowStyle.Items.Add("Triangles (▲ / ▼)");
            _cbArrowStyle.Items.Add("Arrows (↑ / ↓)");
            _cbArrowStyle.Items.Add("Letters (U: / D:)");
            _cbArrowStyle.Items.Add("None (Clean Digits)");
            _cbArrowStyle.SelectedIndexChanged += (s, e) =>
            {
                _workingConfig.ArrowStyle = (ArrowStyle)_cbArrowStyle.SelectedIndex;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Direction Glyphs", "Choose indicator prefix symbol style", _cbArrowStyle) { Location = new Point(0, y) });
            y += 72;

            // Card 2: Unit Mode
            _cbUnitMode = new FluentDropdown { Width = 240 };
            _cbUnitMode.Items.Add("Auto Bytes (B/s, KB/s, MB/s)");
            _cbUnitMode.Items.Add("Auto Bits (bps, Kbps, Mbps)");
            _cbUnitMode.Items.Add("Fixed KB/s");
            _cbUnitMode.Items.Add("Fixed MB/s");
            _cbUnitMode.SelectedIndexChanged += (s, e) =>
            {
                _workingConfig.UnitMode = (UnitMode)_cbUnitMode.SelectedIndex;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Bandwidth Unit Mode", "Format data rates in bytes or bits", _cbUnitMode) { Location = new Point(0, y) });
            y += 72;

            // Card 3: Decimal Places
            _stepperDecimals = new FluentNumericStepper { Minimum = 0, Maximum = 3, Increment = 1, Suffix = " digits", Size = new Size(120, 28) };
            _stepperDecimals.ValueChanged += (s, e) =>
            {
                _workingConfig.DecimalPlaces = (int)_stepperDecimals.Value;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Decimal Precision", "Number of fractional decimal places to display", _stepperDecimals) { Location = new Point(0, y) });
            y += 72;

            // Card 4: Horizontal & Vertical Padding
            var pnlPaddings = new Panel { Size = new Size(250, 32), BackColor = Color.Transparent };
            _stepperPaddingX = new FluentNumericStepper { Minimum = 0, Maximum = 40, Increment = 1, Suffix = " X", Size = new Size(118, 28) };
            _stepperPaddingX.ValueChanged += (s, e) =>
            {
                _workingConfig.PaddingX = (int)_stepperPaddingX.Value;
                _previewPanel.Invalidate();
            };
            pnlPaddings.Controls.Add(_stepperPaddingX);

            _stepperPaddingY = new FluentNumericStepper { Minimum = 0, Maximum = 20, Increment = 1, Suffix = " Y", Size = new Size(118, 28), Location = new Point(126, 0) };
            _stepperPaddingY.ValueChanged += (s, e) =>
            {
                _workingConfig.PaddingY = (int)_stepperPaddingY.Value;
                _previewPanel.Invalidate();
            };
            pnlPaddings.Controls.Add(_stepperPaddingY);

            page.Controls.Add(new FluentSettingCard("Internal Margin Padding", "Horizontal (X) and vertical (Y) padding around text", pnlPaddings) { Location = new Point(0, y) });
            y += 72;

            // Card 5: Item Spacing
            _stepperSpacing = new FluentNumericStepper { Minimum = 0, Maximum = 30, Increment = 1, Suffix = " px", Size = new Size(120, 28) };
            _stepperSpacing.ValueChanged += (s, e) =>
            {
                _workingConfig.ItemSpacing = (int)_stepperSpacing.Value;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Glyph & Item Spacing", "Space between speed readouts and glyph arrows", _stepperSpacing) { Location = new Point(0, y) });
            y += 72;

            // Card 6: Overall Opacity
            _stepperOverallOpacity = new FluentNumericStepper { Minimum = 30, Maximum = 100, Increment = 5, Suffix = "%", Size = new Size(120, 28) };
            _stepperOverallOpacity.ValueChanged += (s, e) =>
            {
                _workingConfig.OverallOpacityPercent = (int)_stepperOverallOpacity.Value;
                _previewPanel.Invalidate();
            };
            page.Controls.Add(new FluentSettingCard("Overall Meter Opacity", "Configure transparency percentage (30% to 100%)", _stepperOverallOpacity) { Location = new Point(0, y) });
            y += 72;

            // Card 7: Idle Traffic Dimming
            var pnlIdle = new Panel { Size = new Size(250, 32), BackColor = Color.Transparent };
            _stepperIdleOpacity = new FluentNumericStepper { Minimum = 10, Maximum = 90, Increment = 5, Suffix = "%", Size = new Size(120, 28) };
            _stepperIdleOpacity.ValueChanged += (s, e) =>
            {
                _workingConfig.IdleOpacityPercent = (int)_stepperIdleOpacity.Value;
            };
            pnlIdle.Controls.Add(_stepperIdleOpacity);

            _toggleIdleDimming = new FluentToggleSwitch { Location = new Point(140, 4) };
            _toggleIdleDimming.CheckedChanged += (s, e) =>
            {
                _workingConfig.IdleDimming = _toggleIdleDimming.Checked;
                _stepperIdleOpacity.Enabled = _toggleIdleDimming.Checked;
            };
            pnlIdle.Controls.Add(_toggleIdleDimming);

            page.Controls.Add(new FluentSettingCard("Idle Bandwidth Dimming", "Dim meter font opacity when traffic drops below 1 KB/s", pnlIdle) { Location = new Point(0, y) });
            y += 72;
        }

        private void BuildSystemCards(Panel page)
        {
            int y = 6;

            // Card 1: Network Adapter
            _cbAdapter = new FluentDropdown { Width = 240 };
            _cbAdapter.Items.Add("All Active Adapters (Aggregated)");
            try
            {
                var adapters = _networkMonitor.GetAvailableAdapters();
                foreach (var a in adapters)
                {
                    _cbAdapter.Items.Add(a.Alias);
                }
            }
            catch { }
            _cbAdapter.SelectedIndexChanged += (s, e) =>
            {
                if (_cbAdapter.SelectedIndex <= 0) _workingConfig.SelectedAdapterAlias = "";
                else _workingConfig.SelectedAdapterAlias = _cbAdapter.SelectedItem;
            };
            page.Controls.Add(new FluentSettingCard("Target Network Interface", "Aggregate all adapters or monitor a single NIC", _cbAdapter) { Location = new Point(0, y) });
            y += 72;

            // Card 2: Sampling Refresh Rate
            _cbRefreshInterval = new FluentDropdown { Width = 240 };
            _cbRefreshInterval.Items.Add("500 ms (Fast)");
            _cbRefreshInterval.Items.Add("1000 ms (Recommended)");
            _cbRefreshInterval.Items.Add("1500 ms (Balanced)");
            _cbRefreshInterval.Items.Add("2000 ms (Energy Saving)");
            _cbRefreshInterval.Items.Add("3000 ms (Minimal Poll)");
            _cbRefreshInterval.SelectedIndexChanged += (s, e) =>
            {
                int[] intervals = new int[] { 500, 1000, 1500, 2000, 3000 };
                int idx = _cbRefreshInterval.SelectedIndex;
                if (idx >= 0 && idx < intervals.Length) _workingConfig.UpdateIntervalMs = intervals[idx];
            };
            page.Controls.Add(new FluentSettingCard("Telemetry Polling Rate", "Frequency of network kernel table sampling", _cbRefreshInterval) { Location = new Point(0, y) });
            y += 72;

            // Card 3: Left-Click Action
            _cbLeftClickAction = new FluentDropdown { Width = 240 };
            _cbLeftClickAction.Items.Add("Open Taskbar Flyout Popup");
            _cbLeftClickAction.Items.Add("Open Windows Task Manager");
            _cbLeftClickAction.Items.Add("None (Do Nothing)");
            _cbLeftClickAction.SelectedIndexChanged += (s, e) =>
            {
                LeftClickAction[] actions = new LeftClickAction[] {
                    LeftClickAction.Flyout,
                    LeftClickAction.TaskManager,
                    LeftClickAction.None
                };
                int idx = _cbLeftClickAction.SelectedIndex;
                if (idx >= 0 && idx < actions.Length) _workingConfig.LeftClickAction = actions[idx];
            };
            page.Controls.Add(new FluentSettingCard("Taskbar Left-Click Action", "Action triggered when clicking the meter on your taskbar", _cbLeftClickAction) { Location = new Point(0, y) });
            y += 72;

            // Card 4: Monthly Data Quota
            _stepperDataQuota = new FluentNumericStepper { Minimum = 0, Maximum = 5000, Increment = 10, Suffix = " GB", Size = new Size(120, 28) };
            _stepperDataQuota.ValueChanged += (s, e) =>
            {
                _workingConfig.MonthlyDataQuotaGb = (double)_stepperDataQuota.Value;
            };
            page.Controls.Add(new FluentSettingCard("Monthly Data Quota", "Display visual quota usage bar in flyout (0 = Unlimited)", _stepperDataQuota) { Location = new Point(0, y) });
            y += 72;

            // Card 5: Start with Windows
            _toggleStartWithWindows = new FluentToggleSwitch();
            _toggleStartWithWindows.CheckedChanged += (s, e) =>
            {
                _workingConfig.StartWithWindows = _toggleStartWithWindows.Checked;
            };
            page.Controls.Add(new FluentSettingCard("Start with Windows", "Automatically launch metriX when you log in", _toggleStartWithWindows) { Location = new Point(0, y) });
            y += 72;

            // Card 6: Application Updates
            var btnCheckUpdates = new Button
            {
                Text = "Check for Updates",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 215),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(130, 28),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnCheckUpdates.FlatAppearance.BorderSize = 0;

            var cardUpdates = new FluentSettingCard("Application Updates", "metriX v2.8.0 • Up to date", btnCheckUpdates) { Location = new Point(0, y) };
            btnCheckUpdates.Click += (s, e) =>
            {
                if (_updateService == null) return;
                btnCheckUpdates.Enabled = false;
                btnCheckUpdates.Text = "Checking...";
                _updateService.CheckForUpdatesAsync(info =>
                {
                    btnCheckUpdates.Enabled = true;
                    btnCheckUpdates.Text = "Check for Updates";

                    if (info != null && info.IsUpdateAvailable)
                    {
                        var dlg = new UpdateDialog(info, _updateService, _configService);
                        dlg.ShowDialog(this);
                    }
                    else if (info != null && !string.IsNullOrEmpty(info.ErrorMessage))
                    {
                        MessageBox.Show(this, "Could not check for updates: " + info.ErrorMessage, "metriX Update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show(this, "You are already using the latest version of metriX (v" + UpdateService.GetCurrentAssemblyVersion() + ").", "metriX Up to Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });
            };
            page.Controls.Add(cardUpdates);
            y += 72;

            // Card 7: Silent Auto-Update
            _toggleSilentUpdate = new FluentToggleSwitch();
            _toggleSilentUpdate.CheckedChanged += (s, e) =>
            {
                _workingConfig.EnableSilentAutoUpdate = _toggleSilentUpdate.Checked;
            };
            page.Controls.Add(new FluentSettingCard("Silent Auto-Update", "Automatically download and apply updates without prompting", _toggleSilentUpdate) { Location = new Point(0, y) });
            y += 72;
        }

        private Panel CreateColorChip(int x, int width, string label, Action<string> onColorPicked)
        {
            var pnl = new Panel
            {
                Location = new Point(x, 2),
                Size = new Size(width, 28),
                BackColor = Color.FromArgb(76, 194, 255),
                Cursor = Cursors.Hand
            };

            pnl.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                int w = pnl.Width - 1;
                int h = pnl.Height - 1;

                using (var path = GetRoundedRect(0, 0, w, h, 4))
                {
                    using (var fillBrush = new SolidBrush(pnl.BackColor))
                    {
                        g.FillPath(fillBrush, path);
                    }
                    using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
                    {
                        g.DrawPath(pen, path);
                    }
                }

                // Dynamic contrast text
                double lum = (pnl.BackColor.R * 0.299 + pnl.BackColor.G * 0.587 + pnl.BackColor.B * 0.114);
                Color textCol = lum > 140 ? Color.FromArgb(20, 20, 20) : Color.White;

                using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(textCol))
                {
                    SizeF sz = g.MeasureString(label, font);
                    g.DrawString(label, font, brush, (pnl.Width - sz.Width) / 2, (pnl.Height - sz.Height) / 2);
                }
            };

            pnl.Click += (s, e) =>
            {
                if (_workingConfig.AutoTheme) return;
                using (var cd = new ColorDialog())
                {
                    cd.Color = pnl.BackColor;
                    if (cd.ShowDialog() == DialogResult.OK)
                    {
                        string hex = "#" + cd.Color.R.ToString("X2") + cd.Color.G.ToString("X2") + cd.Color.B.ToString("X2");
                        onColorPicked(hex);
                        pnl.BackColor = cd.Color;
                        pnl.Invalidate();
                        _previewPanel.Invalidate();
                    }
                }
            };

            return pnl;
        }

        private static GraphicsPath GetRoundedRect(int x, int y, int width, int height, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(x, y, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = x + width - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = y + height - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = x;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Button CreatePresetButton(string text, int x, int width, string uHex, string dHex, string lHex)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(220, 220, 220),
                BackColor = Color.FromArgb(42, 42, 46),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(width, 28),
                Location = new Point(x, 2),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 65);
            btn.Click += (s, e) =>
            {
                _workingConfig.AutoTheme = false;
                _toggleAutoTheme.Checked = false;
                _workingConfig.UploadColor = uHex;
                _workingConfig.DownloadColor = dHex;
                _workingConfig.LabelColor = lHex;
                _chipUploadColor.BackColor = ColorTranslator.FromHtml(uHex);
                _chipDownloadColor.BackColor = ColorTranslator.FromHtml(dHex);
                _chipLabelColor.BackColor = ColorTranslator.FromHtml(lHex);
                _chipUploadColor.Invalidate();
                _chipDownloadColor.Invalidate();
                _chipLabelColor.Invalidate();
                UpdateColorChipStates();
                _previewPanel.Invalidate();
            };
            return btn;
        }

        private void UpdateColorChipStates()
        {
            bool custom = !_workingConfig.AutoTheme;
            _chipUploadColor.Enabled = custom;
            _chipDownloadColor.Enabled = custom;
            _chipLabelColor.Enabled = custom;
        }

        private void BuildBottomActionBar()
        {
            var pnlBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.FromArgb(24, 24, 26)
            };

            pnlBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(42, 42, 46), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 0, pnlBar.Width, 0);
                }
            };

            var btnReset = new Button
            {
                Text = "Reset to Defaults",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(170, 170, 175),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(130, 32),
                Location = new Point(14, 10),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.Click += (s, e) =>
            {
                if (MessageBox.Show("Reset all settings to Windows 11 defaults?", "metriX", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _workingConfig = new AppConfig();
                    PopulateValues();
                    _previewPanel.Invalidate();
                }
            };

            var btnSave = new Button
            {
                Text = "Save Changes",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 215),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(125, 32),
                Location = new Point(pnlBar.Width - 245, 10),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => ApplyAndClose();

            var btnCancel = new Button
            {
                Text = "Cancel",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(220, 220, 220),
                BackColor = Color.FromArgb(42, 42, 46),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(95, 32),
                Location = new Point(pnlBar.Width - 110, 10),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 65);
            btnCancel.Click += (s, e) => Close();

            pnlBar.Controls.Add(btnReset);
            pnlBar.Controls.Add(btnSave);
            pnlBar.Controls.Add(btnCancel);
            Controls.Add(pnlBar);
        }

        private void PopulateValues()
        {
            _cbPosition.SelectedIndex = (int)_workingConfig.Position;
            _cbLayout.SelectedIndex = (int)_workingConfig.Layout;
            _stepperOffsetX.Value = _workingConfig.OffsetX;
            _stepperOffsetY.Value = _workingConfig.OffsetY;
            _toggleAllMonitors.Checked = _workingConfig.ShowOnAllMonitors;

            int fIdx = _cbFont.FindStringExact(_workingConfig.FontFamily);
            _cbFont.SelectedIndex = fIdx >= 0 ? fIdx : 0;
            _stepperFontSize.Value = (decimal)_workingConfig.FontSize;
            _stepperUnitFontSize.Value = (decimal)_workingConfig.UnitFontSize;
            _toggleFontBold.Checked = _workingConfig.FontBold;
            _toggleAutoTheme.Checked = _workingConfig.AutoTheme;

            _chipUploadColor.BackColor = ColorTranslator.FromHtml(_workingConfig.UploadColor);
            _chipDownloadColor.BackColor = ColorTranslator.FromHtml(_workingConfig.DownloadColor);
            _chipLabelColor.BackColor = ColorTranslator.FromHtml(_workingConfig.LabelColor);
            UpdateColorChipStates();

            _toggleHoverHighlight.Checked = _workingConfig.ShowHoverHighlight;

            _cbArrowStyle.SelectedIndex = (int)_workingConfig.ArrowStyle;
            _cbUnitMode.SelectedIndex = (int)_workingConfig.UnitMode;
            _stepperDecimals.Value = _workingConfig.DecimalPlaces;
            _stepperPaddingX.Value = _workingConfig.PaddingX;
            _stepperPaddingY.Value = _workingConfig.PaddingY;
            _stepperSpacing.Value = _workingConfig.ItemSpacing;
            _stepperOverallOpacity.Value = _workingConfig.OverallOpacityPercent;

            _toggleIdleDimming.Checked = _workingConfig.IdleDimming;
            _stepperIdleOpacity.Value = _workingConfig.IdleOpacityPercent;
            _stepperIdleOpacity.Enabled = _workingConfig.IdleDimming;

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

            if (_workingConfig.LeftClickAction == LeftClickAction.Flyout) _cbLeftClickAction.SelectedIndex = 0;
            else if (_workingConfig.LeftClickAction == LeftClickAction.TaskManager) _cbLeftClickAction.SelectedIndex = 1;
            else _cbLeftClickAction.SelectedIndex = 2;

            _stepperDataQuota.Value = (decimal)_workingConfig.MonthlyDataQuotaGb;
            _toggleStartWithWindows.Checked = _workingConfig.StartWithWindows;
            if (_toggleSilentUpdate != null) _toggleSilentUpdate.Checked = _workingConfig.EnableSilentAutoUpdate;
            if (_toggleFloatingMode != null) _toggleFloatingMode.Checked = _workingConfig.IsFloatingMode;
        }

        private void OnPreviewPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            g.Clear(Color.FromArgb(20, 20, 22));

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
                    int y1 = 3;
                    int y2 = 21;

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
                    x += g.MeasureString(sampleUnit, unitFont).Width + _workingConfig.ItemSpacing + 12;

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
            if (_stepperDataQuota != null)
            {
                _workingConfig.MonthlyDataQuotaGb = (double)_stepperDataQuota.Value;
            }
            if (_toggleSilentUpdate != null)
            {
                _workingConfig.EnableSilentAutoUpdate = _toggleSilentUpdate.Checked;
            }
            if (_toggleFloatingMode != null)
            {
                _workingConfig.IsFloatingMode = _toggleFloatingMode.Checked;
            }
            _configService.Save(_workingConfig);
            if (_onSettingsUpdated != null) _onSettingsUpdated();
            Close();
        }
    }
}
