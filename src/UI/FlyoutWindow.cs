using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using MetriX.Model;
using MetriX.Native;
using MetriX.Services;

namespace MetriX.UI
{
    internal class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();
        }
    }

    public class FlyoutWindow : Form
    {
        private readonly ConfigService _configService;
        private readonly NetworkMonitor _networkMonitor;
        private readonly SpeedTestService _speedTestService;
        private readonly ProcessNetworkTracker _processTracker;

        public event Action RequestOpenSettings;

        // View Tabs
        private bool _isSpeedTestView = false;
        private Button _tabBtnMonitor;
        private Button _tabBtnSpeedTest;
        private Panel _panelMonitor;
        private Panel _panelSpeedTest;

        // Live Monitor Controls
        private DoubleBufferedPanel _graphPanel;
        private Label _lblDownloadStats;
        private Label _lblUploadStats;
        private Label _lblAdapterInfo;
        private Label _lblSessionTotals;
        private Label _lblQuotaStats;
        private Panel _quotaProgressBar;
        private float _quotaPercent = 0f;
        private Panel _cardSession;
        private Panel _monitorContentPanel;
        private FluentScrollBar _monitorScrollBar;
        private DoubleBufferedPanel _pnlTopApps;
        private List<AppNetworkUsage> _currentTopApps = new List<AppNetworkUsage>();
        private Button _btn30s;
        private Button _btn60s;
        private Button _btn5m;

        // Historical Data Usage Controls (Net Used Till Now)
        private Label _lblUsageTodayVal;
        private Label _lblUsageTodaySub;
        private Label _lblUsageWeekVal;
        private Label _lblUsageWeekSub;
        private Label _lblUsageMonthVal;
        private Label _lblUsageMonthSub;

        // Live Monitor State
        private int _timeframeSeconds = 60;
        private int _hoverMouseX = -1;
        private int _hoverMouseY = -1;
        private bool _isHoveringGraph = false;

        // Speed Test Controls
        private DoubleBufferedPanel _gaugePanel;
        private Button _btnStartSpeedTest;
        private Label _lblSpeedTestStatus;
        private Label _lblTestPing;
        private Label _lblTestDownload;
        private Label _lblTestUpload;
        private Label _lblTestHistory;
        private Button _btnDur5s;
        private Button _btnDur10s;
        private Button _btnDur15s;
        private Button _btnDur30s;

        // Speed Test Live State
        private SpeedTestPhase _currentPhase = SpeedTestPhase.Idle;
        private double _currentGaugeSpeedMbps = 0.0;
        private double _maxGaugeScale = 100.0; // Dynamic scale (100, 250, 500, 1000)
        private double _lastResultPing = 0.0;
        private double _lastResultJitter = 0.0;
        private double _lastResultDownload = 0.0;
        private double _lastResultUpload = 0.0;

        public FlyoutWindow(ConfigService configService, NetworkMonitor networkMonitor)
        {
            _configService = configService;
            _networkMonitor = networkMonitor;
            _processTracker = new ProcessNetworkTracker();
            _speedTestService = new SpeedTestService();
            _speedTestService.ProgressUpdated += OnSpeedTestProgress;
            _speedTestService.TestCompleted += OnSpeedTestCompleted;
            _speedTestService.TestFailed += OnSpeedTestFailed;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;

            BackColor = Color.FromArgb(28, 28, 30);
            ForeColor = Color.FromArgb(240, 240, 240);

            InitializeUI();
        }

        protected override bool ShowWithoutActivation
        {
            get { return false; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style = unchecked((int)NativeMethods.WS_POPUP);
                cp.ExStyle |= NativeMethods.WS_EX_TOPMOST;
                const int CS_DROPSHADOW = 0x00020000;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int darkMode = 1;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                int cornerPreference = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
                int backdrop = NativeMethods.DWMSBT_TRANSIENTWINDOW;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
            catch { }
        }

        private void InitializeUI()
        {
            Width = 390;
            Height = 565;

            // Header bar
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Color.FromArgb(32, 32, 36)
            };
            headerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(44, 44, 48), 1))
                {
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
                }
            };

            var lblTitle = new Label
            {
                Text = "metriX",
                Font = new Font("Segoe UI", 11.0f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(14, 12),
                AutoSize = true,
                UseMnemonic = false
            };

            // Segmented View Switcher Pill
            var pnlSegment = new Panel
            {
                Size = new Size(184, 32),
                Location = new Point(106, 7),
                BackColor = Color.FromArgb(22, 22, 26)
            };
            pnlSegment.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(48, 48, 54), 1))
                using (var path = GetRoundedRect(0, 0, pnlSegment.Width - 1, pnlSegment.Height - 1, 4))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };

            _tabBtnMonitor = new Button
            {
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Size = new Size(88, 26),
                Location = new Point(3, 3),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseMnemonic = false,
                Tag = "Live"
            };
            _tabBtnMonitor.FlatAppearance.BorderSize = 0;
            _tabBtnMonitor.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 44, 50);
            _tabBtnMonitor.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 100, 190);
            _tabBtnMonitor.Paint += (s, e) =>
            {
                TextRenderer.DrawText(e.Graphics, (string)_tabBtnMonitor.Tag, _tabBtnMonitor.Font, _tabBtnMonitor.ClientRectangle, _tabBtnMonitor.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            };
            _tabBtnMonitor.Click += (s, e) => SwitchView(false);

            _tabBtnSpeedTest = new Button
            {
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                Size = new Size(88, 26),
                Location = new Point(93, 3),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseMnemonic = false,
                Tag = "Speed"
            };
            _tabBtnSpeedTest.FlatAppearance.BorderSize = 0;
            _tabBtnSpeedTest.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 44, 50);
            _tabBtnSpeedTest.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 100, 190);
            _tabBtnSpeedTest.Paint += (s, e) =>
            {
                TextRenderer.DrawText(e.Graphics, (string)_tabBtnSpeedTest.Tag, _tabBtnSpeedTest.Font, _tabBtnSpeedTest.ClientRectangle, _tabBtnSpeedTest.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            };
            _tabBtnSpeedTest.Click += (s, e) => SwitchView(true);

            pnlSegment.Controls.Add(_tabBtnMonitor);
            pnlSegment.Controls.Add(_tabBtnSpeedTest);

            var btnClose = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 10.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 185),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(46, 46),
                Location = new Point(Width - 46, 0),
                Cursor = Cursors.Hand,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(196, 43, 28);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 35, 20);
            btnClose.MouseEnter += (s, e) => { btnClose.ForeColor = Color.White; };
            btnClose.MouseLeave += (s, e) => { btnClose.ForeColor = Color.FromArgb(180, 180, 185); };
            btnClose.Click += (s, e) => Hide();

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(pnlSegment);
            headerPanel.Controls.Add(btnClose);
            Controls.Add(headerPanel);

            // Container 1: Live Monitor View
            _panelMonitor = new Panel
            {
                Location = new Point(0, 46),
                Size = new Size(Width, 469),
                BackColor = Color.FromArgb(28, 28, 30)
            };
            BuildMonitorView(_panelMonitor);
            Controls.Add(_panelMonitor);

            // Container 2: Speed Test View
            _panelSpeedTest = new Panel
            {
                Location = new Point(0, 46),
                Size = new Size(Width, 469),
                BackColor = Color.FromArgb(28, 28, 30),
                Visible = false
            };
            BuildSpeedTestView(_panelSpeedTest);
            Controls.Add(_panelSpeedTest);

            UpdateViewTabs();

            // Footer action bar (docked to bottom, height 50)
            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(24, 24, 28)
            };
            footerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(45, 45, 50), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
                }
            };

            var btnSettings = new Button
            {
                Text = "⚙ Settings",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(38, 38, 42),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(105, 32),
                Location = new Point(12, 9),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnSettings.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 60);
            btnSettings.Click += (s, e) =>
            {
                Hide();
                if (RequestOpenSettings != null) RequestOpenSettings();
            };

            var btnTaskMgr = new Button
            {
                Text = "📊 Task Manager",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(38, 38, 42),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(135, 32),
                Location = new Point(123, 9),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnTaskMgr.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 60);
            btnTaskMgr.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = "taskmgr.exe", UseShellExecute = true });
                }
                catch { }
            };

            var btnReset = new Button
            {
                Text = "🔄 Reset",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.FromArgb(38, 38, 42),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(98, 32),
                Location = new Point(264, 9),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnReset.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 60);
            btnReset.Click += (s, e) =>
            {
                _networkMonitor.ResetSessionStats();
                UpdateData();
            };

            footerPanel.Controls.Add(btnSettings);
            footerPanel.Controls.Add(btnTaskMgr);
            footerPanel.Controls.Add(btnReset);
            Controls.Add(footerPanel);
        }

        private void SwitchView(bool speedTest)
        {
            _isSpeedTestView = speedTest;
            _panelMonitor.Visible = !_isSpeedTestView;
            _panelSpeedTest.Visible = _isSpeedTestView;
            UpdateViewTabs();

            if (_isSpeedTestView)
            {
                RefreshHistoryDisplay();
            }
        }

        private void UpdateViewTabs()
        {
            if (!_isSpeedTestView)
            {
                _tabBtnMonitor.BackColor = Color.FromArgb(0, 120, 215);
                _tabBtnMonitor.ForeColor = Color.White;
                _tabBtnSpeedTest.BackColor = Color.Transparent;
                _tabBtnSpeedTest.ForeColor = Color.FromArgb(170, 170, 175);
            }
            else
            {
                _tabBtnSpeedTest.BackColor = Color.FromArgb(0, 120, 215);
                _tabBtnSpeedTest.ForeColor = Color.White;
                _tabBtnMonitor.BackColor = Color.Transparent;
                _tabBtnMonitor.ForeColor = Color.FromArgb(170, 170, 175);
            }
            _tabBtnMonitor.Invalidate();
            _tabBtnSpeedTest.Invalidate();
        }

        private void BuildMonitorView(Panel container)
        {
            int w = container.Width - 28; // 362px at 390 width

            _monitorContentPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(container.Width, container.Height),
                BackColor = Color.Transparent,
                AutoScroll = false
            };

            _monitorScrollBar = new FluentScrollBar
            {
                Location = new Point(container.Width - 10, 4),
                Size = new Size(8, container.Height - 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
                ViewportSize = container.Height,
                Visible = false
            };
            _monitorScrollBar.ValueChanged += (s, e) =>
            {
                _monitorContentPanel.Top = -_monitorScrollBar.Value;
            };

            // Timeframe selection buttons
            int btnY = 6;
            _btn30s = CreateTimeframeButton("30s", 30, 14, btnY);
            _btn60s = CreateTimeframeButton("60s", 60, 56, btnY);
            _btn5m = CreateTimeframeButton("5m", 300, 98, btnY);
            UpdateTimeframeButtons();

            _monitorContentPanel.Controls.Add(_btn30s);
            _monitorContentPanel.Controls.Add(_btn60s);
            _monitorContentPanel.Controls.Add(_btn5m);

            // Live rate indicators right of timeframe buttons
            _lblDownloadStats = new Label
            {
                Text = "▼ 0.0 KB/s",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 194, 255),
                Location = new Point(154, btnY + 3),
                AutoSize = true,
                UseMnemonic = false
            };
            _lblUploadStats = new Label
            {
                Text = "▲ 0.0 KB/s",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(108, 203, 95),
                Location = new Point(265, btnY + 3),
                AutoSize = true,
                UseMnemonic = false
            };
            _monitorContentPanel.Controls.Add(_lblDownloadStats);
            _monitorContentPanel.Controls.Add(_lblUploadStats);

            // Interactive Graph Canvas (DoubleBuffered to eliminate hover flicker)
            _graphPanel = new DoubleBufferedPanel
            {
                Location = new Point(14, 34),
                Size = new Size(w, 134),
                BackColor = Color.FromArgb(20, 20, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _graphPanel.Paint += OnGraphPaint;
            _graphPanel.MouseMove += (s, e) =>
            {
                if (_hoverMouseX != e.X || _hoverMouseY != e.Y)
                {
                    _hoverMouseX = e.X;
                    _hoverMouseY = e.Y;
                    _isHoveringGraph = true;
                    _graphPanel.Invalidate();
                }
            };
            _graphPanel.MouseLeave += (s, e) =>
            {
                _isHoveringGraph = false;
                _hoverMouseX = -1;
                _hoverMouseY = -1;
                _graphPanel.Invalidate();
            };
            _monitorContentPanel.Controls.Add(_graphPanel);

            // Card 1: Data Used Till Now (Today / This Week / This Month)
            int usageY = 174;
            var cardUsage = new Panel
            {
                Location = new Point(14, usageY),
                Size = new Size(w, 76),
                BackColor = Color.FromArgb(34, 34, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cardUsage.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(48, 48, 54), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, cardUsage.Width - 1, cardUsage.Height - 1);
                }
                using (var divPen = new Pen(Color.FromArgb(44, 44, 48), 1))
                {
                    int colW = cardUsage.Width / 3;
                    e.Graphics.DrawLine(divPen, colW, 20, colW, cardUsage.Height - 10);
                    e.Graphics.DrawLine(divPen, colW * 2, 20, colW * 2, cardUsage.Height - 10);
                }
            };

            var lblUsageTitle = new Label
            {
                Text = "DATA USED TILL NOW",
                Font = new Font("Segoe UI", 7.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 155),
                Location = new Point(10, 6),
                AutoSize = true,
                UseMnemonic = false
            };

            int colWidth = w / 3;

            // Column 1: Today
            _lblUsageTodayVal = new Label
            {
                Text = "0 B",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 194, 255),
                Location = new Point(10, 22),
                Size = new Size(colWidth - 16, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            _lblUsageTodaySub = new Label
            {
                Text = "📅 Today",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(170, 170, 175),
                Location = new Point(10, 46),
                Size = new Size(colWidth - 16, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };

            // Column 2: This Week
            _lblUsageWeekVal = new Label
            {
                Text = "0 B",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(108, 203, 95),
                Location = new Point(colWidth + 8, 22),
                Size = new Size(colWidth - 16, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            _lblUsageWeekSub = new Label
            {
                Text = "📆 This Week",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(170, 170, 175),
                Location = new Point(colWidth + 8, 46),
                Size = new Size(colWidth - 16, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };

            // Column 3: This Month
            _lblUsageMonthVal = new Label
            {
                Text = "0 B",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 180, 60),
                Location = new Point((colWidth * 2) + 8, 22),
                Size = new Size(colWidth - 16, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            _lblUsageMonthSub = new Label
            {
                Text = "🗓️ This Month",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(170, 170, 175),
                Location = new Point((colWidth * 2) + 8, 46),
                Size = new Size(colWidth - 16, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };

            cardUsage.Controls.Add(lblUsageTitle);
            cardUsage.Controls.Add(_lblUsageTodayVal);
            cardUsage.Controls.Add(_lblUsageTodaySub);
            cardUsage.Controls.Add(_lblUsageWeekVal);
            cardUsage.Controls.Add(_lblUsageWeekSub);
            cardUsage.Controls.Add(_lblUsageMonthVal);
            cardUsage.Controls.Add(_lblUsageMonthSub);
            _monitorContentPanel.Controls.Add(cardUsage);

            // Card 2: Active Connection & Session Telemetry
            int cardY = 256;
            _cardSession = new Panel
            {
                Location = new Point(14, cardY),
                Size = new Size(w, 200),
                BackColor = Color.FromArgb(34, 34, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _cardSession.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(48, 48, 54), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, _cardSession.Width - 1, _cardSession.Height - 1);
                }
            };

            var lblCardTitle = new Label
            {
                Text = "ACTIVE CONNECTION & SESSION TELEMETRY",
                Font = new Font("Segoe UI", 7.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 155),
                Location = new Point(10, 6),
                AutoSize = true,
                UseMnemonic = false
            };

            _lblAdapterInfo = new Label
            {
                Text = "Adapter: Detecting...",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = Color.White,
                Location = new Point(10, 22),
                Size = new Size(w - 20, 16),
                UseMnemonic = false
            };

            _lblSessionTotals = new Label
            {
                Text = "Session: ▲ 0 B  |  ▼ 0 B  (00:00:00)",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(215, 215, 220),
                Location = new Point(10, 38),
                Size = new Size(w - 20, 16),
                UseMnemonic = false
            };

            _lblQuotaStats = new Label
            {
                Text = "Data Quota: Unlimited",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 200, 255),
                Location = new Point(10, 54),
                Size = new Size(w - 20, 16),
                UseMnemonic = false
            };

            _quotaProgressBar = new Panel
            {
                Location = new Point(10, 71),
                Size = new Size(w - 20, 4),
                BackColor = Color.FromArgb(24, 24, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _quotaProgressBar.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int pw = _quotaProgressBar.Width - 1;
                int ph = _quotaProgressBar.Height - 1;

                using (var bgBrush = new SolidBrush(Color.FromArgb(40, 40, 45)))
                using (var path = GetRoundedRect(0, 0, pw, ph, 2))
                {
                    g.FillPath(bgBrush, path);
                }

                if (_quotaPercent > 0f)
                {
                    int fillW = Math.Max(4, (int)(pw * Math.Min(1.0f, _quotaPercent / 100.0f)));
                    Color barCol = _quotaPercent > 95f ? Color.FromArgb(255, 75, 75) :
                                   _quotaPercent > 80f ? Color.FromArgb(255, 170, 40) :
                                                         Color.FromArgb(76, 194, 255);
                    using (var fillBrush = new SolidBrush(barCol))
                    using (var fillPath = GetRoundedRect(0, 0, fillW, ph, 2))
                    {
                        g.FillPath(fillBrush, fillPath);
                    }
                }
            };

            _pnlTopApps = new DoubleBufferedPanel
            {
                Location = new Point(10, 78),
                Size = new Size(w - 20, 126),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _pnlTopApps.Paint += OnTopAppsPaint;

            _cardSession.Controls.Add(lblCardTitle);
            _cardSession.Controls.Add(_lblAdapterInfo);
            _cardSession.Controls.Add(_lblSessionTotals);
            _cardSession.Controls.Add(_lblQuotaStats);
            _cardSession.Controls.Add(_quotaProgressBar);
            _cardSession.Controls.Add(_pnlTopApps);
            _monitorContentPanel.Controls.Add(_cardSession);

            container.Controls.Add(_monitorContentPanel);
            container.Controls.Add(_monitorScrollBar);

            AttachMouseWheelEvents(_monitorContentPanel);
        }

        private void AttachMouseWheelEvents(Control parent)
        {
            if (parent == null) return;
            parent.MouseWheel += OnMonitorMouseWheel;
            foreach (Control child in parent.Controls)
            {
                child.MouseWheel += OnMonitorMouseWheel;
                AttachMouseWheelEvents(child);
            }
        }

        private void OnMonitorMouseWheel(object sender, MouseEventArgs e)
        {
            if (_monitorScrollBar != null && _monitorScrollBar.Visible)
            {
                int step = (e.Delta / 120) * 44;
                _monitorScrollBar.Value -= step;
            }
        }

        private void UpdateMonitorLayout()
        {
            if (_pnlTopApps == null || _cardSession == null || _monitorContentPanel == null || _panelMonitor == null) return;

            int configuredLimit = _configService.Config.TopAppsLimit;
            int maxDisplay = configuredLimit <= 0 ? _currentTopApps.Count : configuredLimit;
            int count = Math.Min(maxDisplay, _currentTopApps.Count);

            int rowH = 20;
            int gap = 2;
            int neededTopAppsH = 16 + Math.Max(1, count) * (rowH + gap) + 4;
            _pnlTopApps.Height = neededTopAppsH;
            _cardSession.Height = 78 + neededTopAppsH + 8;

            int totalH = _cardSession.Bottom + 14;
            _monitorContentPanel.Height = Math.Max(_panelMonitor.Height, totalH);

            if (_monitorScrollBar != null)
            {
                _monitorScrollBar.Maximum = totalH;
                _monitorScrollBar.ViewportSize = _panelMonitor.Height;
                _monitorScrollBar.Visible = totalH > _panelMonitor.Height;
            }
        }

        private void BuildSpeedTestView(Panel container)
        {
            int w = container.Width - 28;

            // Speedometer Gauge Arc Canvas (DoubleBuffered to eliminate flicker)
            _gaugePanel = new DoubleBufferedPanel
            {
                Location = new Point(14, 6),
                Size = new Size(w, 142),
                BackColor = Color.FromArgb(20, 20, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _gaugePanel.Paint += OnGaugePaint;
            container.Controls.Add(_gaugePanel);

            // Status message label
            _lblSpeedTestStatus = new Label
            {
                Text = "Ready to benchmark your network",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(14, 150),
                Size = new Size(w, 18),
                UseMnemonic = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            container.Controls.Add(_lblSpeedTestStatus);

            // 3 Metrics Badges Card
            var cardMetrics = new Panel
            {
                Location = new Point(14, 170),
                Size = new Size(w, 48),
                BackColor = Color.FromArgb(34, 34, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cardMetrics.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(48, 48, 54), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, cardMetrics.Width - 1, cardMetrics.Height - 1);
                }
            };

            int badgeW = w / 3;

            _lblTestPing = new Label
            {
                Text = "PING\n-- ms",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 220, 220),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(6, 4),
                Size = new Size(badgeW - 12, 38),
                UseMnemonic = false
            };

            _lblTestDownload = new Label
            {
                Text = "DOWNLOAD\n-- Mbps",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 194, 255),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(badgeW + 6, 4),
                Size = new Size(badgeW - 12, 38),
                UseMnemonic = false
            };

            _lblTestUpload = new Label
            {
                Text = "UPLOAD\n-- Mbps",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(108, 203, 95),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point((badgeW * 2) + 6, 4),
                Size = new Size(badgeW - 12, 38),
                UseMnemonic = false
            };

            cardMetrics.Controls.Add(_lblTestPing);
            cardMetrics.Controls.Add(_lblTestDownload);
            cardMetrics.Controls.Add(_lblTestUpload);
            container.Controls.Add(cardMetrics);

            // Duration Selector Capsule Pills
            var pnlDuration = new Panel
            {
                Location = new Point(14, 222),
                Size = new Size(w, 28),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblDurTitle = new Label
            {
                Text = "DURATION:",
                Font = new Font("Segoe UI", 7.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 155),
                Location = new Point(4, 7),
                AutoSize = true,
                UseMnemonic = false
            };
            pnlDuration.Controls.Add(lblDurTitle);

            int pillW = 54;
            int pillGap = 6;
            int startPillX = 100;

            _btnDur5s = CreateDurationButton("5s", 5, startPillX, 2, pillW);
            _btnDur10s = CreateDurationButton("10s", 10, startPillX + pillW + pillGap, 2, pillW);
            _btnDur15s = CreateDurationButton("15s", 15, startPillX + (pillW + pillGap) * 2, 2, pillW);
            _btnDur30s = CreateDurationButton("30s", 30, startPillX + (pillW + pillGap) * 3, 2, pillW);

            UpdateDurationButtons();

            pnlDuration.Controls.Add(_btnDur5s);
            pnlDuration.Controls.Add(_btnDur10s);
            pnlDuration.Controls.Add(_btnDur15s);
            pnlDuration.Controls.Add(_btnDur30s);
            container.Controls.Add(pnlDuration);

            // Start / Cancel Test Button
            _btnStartSpeedTest = new Button
            {
                Text = "⚡  START SPEED TEST",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 215),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(w, 34),
                Location = new Point(14, 254),
                Cursor = Cursors.Hand,
                UseMnemonic = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _btnStartSpeedTest.FlatAppearance.BorderSize = 0;
            _btnStartSpeedTest.Click += OnStartOrCancelSpeedTest;
            container.Controls.Add(_btnStartSpeedTest);

            // Recent Benchmark History Card - EXPANDED TO FILL VIEWPORT
            var cardHistory = new Panel
            {
                Location = new Point(14, 296),
                Size = new Size(w, container.Height - 296 - 10),
                BackColor = Color.FromArgb(34, 34, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            cardHistory.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(48, 48, 54), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, cardHistory.Width - 1, cardHistory.Height - 1);
                }
            };

            var lblHistTitle = new Label
            {
                Text = "RECENT SPEED BENCHMARKS & STATS",
                Font = new Font("Segoe UI", 7.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 155),
                Location = new Point(10, 6),
                AutoSize = true,
                UseMnemonic = false
            };

            _lblTestHistory = new Label
            {
                Text = "No previous benchmarks recorded yet.\nClick 'Start Speed Test' above to measure ping, download, and upload speeds.",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 160, 165),
                TextAlign = ContentAlignment.TopLeft,
                Location = new Point(10, 26),
                Size = new Size(w - 20, Math.Max(50, cardHistory.Height - 34)),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                UseMnemonic = false
            };

            cardHistory.Controls.Add(lblHistTitle);
            cardHistory.Controls.Add(_lblTestHistory);
            container.Controls.Add(cardHistory);
        }

        private Button CreateDurationButton(string text, int durationSec, int x, int y, int width)
        {
            var btn = new Button
            {
                Text = text,
                Tag = durationSec,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Size = new Size(width, 24),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) =>
            {
                if (_speedTestService.IsRunning) return;
                _configService.Config.SpeedTestDurationSeconds = durationSec;
                _configService.Save();
                UpdateDurationButtons();
            };
            return btn;
        }

        private void UpdateDurationButtons()
        {
            int current = _configService.Config.SpeedTestDurationSeconds;
            if (current <= 0) current = 10;
            UpdateButtonState(_btnDur5s, current == 5);
            UpdateButtonState(_btnDur10s, current == 10);
            UpdateButtonState(_btnDur15s, current == 15);
            UpdateButtonState(_btnDur30s, current == 30);
        }

        private static void UpdateButtonState(Button btn, bool isSelected)
        {
            if (btn == null) return;
            if (isSelected)
            {
                btn.BackColor = Color.FromArgb(0, 120, 215);
                btn.ForeColor = Color.White;
            }
            else
            {
                btn.BackColor = Color.FromArgb(42, 42, 48);
                btn.ForeColor = Color.FromArgb(180, 180, 185);
            }
        }

        private void OnStartOrCancelSpeedTest(object sender, EventArgs e)
        {
            if (_speedTestService.IsRunning)
            {
                _speedTestService.CancelTest();
                _btnStartSpeedTest.Text = "⚡  START SPEED TEST";
                _btnStartSpeedTest.BackColor = Color.FromArgb(0, 120, 215);
                _lblSpeedTestStatus.Text = "Test cancelled.";
            }
            else
            {
                _btnStartSpeedTest.Text = "✕  CANCEL TEST";
                _btnStartSpeedTest.BackColor = Color.FromArgb(190, 45, 45);
                _currentGaugeSpeedMbps = 0;
                _currentPhase = SpeedTestPhase.Ping;
                _lblTestPing.Text = "PING\n-- ms";
                _lblTestDownload.Text = "DOWNLOAD\n-- Mbps";
                _lblTestUpload.Text = "UPLOAD\n-- Mbps";
                _lblSpeedTestStatus.Text = "Connecting to CDN test node...";
                _gaugePanel.Invalidate();

                int duration = _configService.Config.SpeedTestDurationSeconds;
                if (duration <= 0) duration = 10;
                _speedTestService.StartTest(duration);
            }
        }

        private void OnSpeedTestProgress(SpeedTestPhase phase, double speedVal, string statusMsg)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<SpeedTestPhase, double, string>(OnSpeedTestProgress), phase, speedVal, statusMsg);
                return;
            }

            _currentPhase = phase;
            _lblSpeedTestStatus.Text = statusMsg;

            if (phase == SpeedTestPhase.Download || phase == SpeedTestPhase.Upload)
            {
                _currentGaugeSpeedMbps = speedVal;
                // Dynamically adjust scale
                if (_currentGaugeSpeedMbps > _maxGaugeScale * 0.9)
                {
                    if (_maxGaugeScale <= 100.0) _maxGaugeScale = 250.0;
                    else if (_maxGaugeScale <= 250.0) _maxGaugeScale = 500.0;
                    else if (_maxGaugeScale <= 500.0) _maxGaugeScale = 1000.0;
                }

                if (phase == SpeedTestPhase.Download)
                {
                    _lblTestDownload.Text = string.Format("DOWNLOAD\n{0:F1} Mbps", speedVal);
                }
                else
                {
                    _lblTestUpload.Text = string.Format("UPLOAD\n{0:F1} Mbps", speedVal);
                }
            }
            else if (phase == SpeedTestPhase.Ping)
            {
                _lblTestPing.Text = string.Format("PING\n{0:F0} ms", speedVal);
            }

            _gaugePanel.Invalidate();
        }

        private void OnSpeedTestCompleted(SpeedTestResult res)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<SpeedTestResult>(OnSpeedTestCompleted), res);
                return;
            }

            _currentPhase = SpeedTestPhase.Completed;
            _lastResultPing = res.PingMs;
            _lastResultJitter = res.JitterMs;
            _lastResultDownload = res.DownloadMbps;
            _lastResultUpload = res.UploadMbps;

            _lblTestPing.Text = string.Format("PING\n{0:F0} ms", res.PingMs);
            _lblTestDownload.Text = string.Format("DOWNLOAD\n{0:F1} Mbps", res.DownloadMbps);
            _lblTestUpload.Text = string.Format("UPLOAD\n{0:F1} Mbps", res.UploadMbps);

            _btnStartSpeedTest.Text = "⚡  TEST AGAIN";
            _btnStartSpeedTest.BackColor = Color.FromArgb(0, 120, 215);
            _lblSpeedTestStatus.Text = string.Format("Benchmark completed at {0:HH:mm:ss}!", res.Timestamp);

            _gaugePanel.Invalidate();
            RefreshHistoryDisplay();
        }

        private void OnSpeedTestFailed(string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(OnSpeedTestFailed), error);
                return;
            }

            _currentPhase = SpeedTestPhase.Failed;
            _btnStartSpeedTest.Text = "⚡  START SPEED TEST";
            _btnStartSpeedTest.BackColor = Color.FromArgb(0, 120, 215);
            _lblSpeedTestStatus.Text = "Speed test error: " + error;
            _gaugePanel.Invalidate();
        }

        private void RefreshHistoryDisplay()
        {
            var history = _speedTestService.LoadHistory();
            if (history.Count == 0)
            {
                _lblTestHistory.Text = "No previous benchmarks recorded yet.";
                return;
            }

            var sb = new System.Text.StringBuilder();
            int toShow = Math.Min(3, history.Count);
            for (int i = 0; i < toShow; i++)
            {
                var h = history[i];
                sb.AppendFormat("• {0:MM/dd HH:mm}: {1:F1} Mbps ↓  |  {2:F1} Mbps ↑  ({3:F0} ms)",
                    h.Timestamp, h.DownloadMbps, h.UploadMbps, h.PingMs);
                if (i < toShow - 1) sb.AppendLine();
            }
            _lblTestHistory.Text = sb.ToString();
        }

        private void OnGaugePaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = _gaugePanel.ClientSize.Width;
            int h = _gaugePanel.ClientSize.Height;

            // Border
            using (var borderPen = new Pen(Color.FromArgb(45, 45, 50), 1))
            {
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            }

            // Draw semi-circle speedometer gauge
            int gaugeDiameter = 200;
            int arcX = (w - gaugeDiameter) / 2;
            int arcY = 16;
            int penWidth = 12;

            Rectangle arcRect = new Rectangle(arcX, arcY, gaugeDiameter, gaugeDiameter);

            // Background Track Arc (180 to 360 deg)
            using (var bgPen = new Pen(Color.FromArgb(42, 42, 46), penWidth))
            {
                bgPen.StartCap = LineCap.Round;
                bgPen.EndCap = LineCap.Round;
                g.DrawArc(bgPen, arcRect, 180, 180);
            }

            // Active Progress Arc
            double fraction = Math.Max(0.0, Math.Min(1.0, _currentGaugeSpeedMbps / _maxGaugeScale));
            float sweepAngle = (float)(fraction * 180.0);

            if (sweepAngle > 0.5f)
            {
                Color arcColor = _currentPhase == SpeedTestPhase.Upload
                    ? Color.FromArgb(108, 203, 95)
                    : Color.FromArgb(76, 194, 255);

                using (var activePen = new Pen(arcColor, penWidth))
                {
                    activePen.StartCap = LineCap.Round;
                    activePen.EndCap = LineCap.Round;
                    g.DrawArc(activePen, arcRect, 180, sweepAngle);
                }
            }

            // Center speed reading & units
            string speedText = _currentGaugeSpeedMbps.ToString("F1");
            string unitText = "Mbps";
            string phaseText = "BENCHMARK";

            if (_currentPhase == SpeedTestPhase.Ping)
            {
                speedText = _lastResultPing.ToString("F0");
                unitText = "ms";
                phaseText = "MEASURING LATENCY";
            }
            else if (_currentPhase == SpeedTestPhase.Download)
            {
                phaseText = "TESTING DOWNLOAD";
            }
            else if (_currentPhase == SpeedTestPhase.Upload)
            {
                phaseText = "TESTING UPLOAD";
            }
            else if (_currentPhase == SpeedTestPhase.Completed)
            {
                speedText = _lastResultDownload.ToString("F1");
                unitText = "Mbps";
                phaseText = "COMPLETE";
            }
            else if (_currentPhase == SpeedTestPhase.Idle)
            {
                speedText = "0.0";
                unitText = "Mbps";
                phaseText = "READY";
            }

            using (var valFont = new Font("Segoe UI", 24.0f, FontStyle.Bold))
            using (var unitFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
            using (var phaseFont = new Font("Segoe UI", 7.0f, FontStyle.Bold))
            using (var valBrush = new SolidBrush(Color.White))
            using (var unitBrush = new SolidBrush(Color.FromArgb(180, 180, 185)))
            using (var phaseBrush = new SolidBrush(Color.FromArgb(76, 194, 255)))
            {
                SizeF valSize = g.MeasureString(speedText, valFont);
                float valX = (w - valSize.Width) / 2;
                float valY = arcY + 38;
                g.DrawString(speedText, valFont, valBrush, valX, valY);

                SizeF unitSize = g.MeasureString(unitText, unitFont);
                g.DrawString(unitText, unitFont, unitBrush, (w - unitSize.Width) / 2, valY + valSize.Height - 8);

                SizeF phaseSize = g.MeasureString(phaseText, phaseFont);
                g.DrawString(phaseText, phaseFont, phaseBrush, (w - phaseSize.Width) / 2, valY + valSize.Height + 12);
            }

            // Scale min / max labels
            using (var scaleFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (var scaleBrush = new SolidBrush(Color.FromArgb(120, 120, 125)))
            {
                g.DrawString("0", scaleFont, scaleBrush, arcX - 2, arcY + (gaugeDiameter / 2) + 4);
                string maxStr = _maxGaugeScale.ToString("F0");
                SizeF maxSz = g.MeasureString(maxStr, scaleFont);
                g.DrawString(maxStr, scaleFont, scaleBrush, arcX + gaugeDiameter - maxSz.Width + 2, arcY + (gaugeDiameter / 2) + 4);
            }
        }

        private Button CreateTimeframeButton(string text, int seconds, int x, int y)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                Size = new Size(40, 24),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) =>
            {
                _timeframeSeconds = seconds;
                UpdateTimeframeButtons();
                _graphPanel.Invalidate();
            };
            return btn;
        }

        private void UpdateTimeframeButtons()
        {
            SetBtnStyle(_btn30s, _timeframeSeconds == 30);
            SetBtnStyle(_btn60s, _timeframeSeconds == 60);
            SetBtnStyle(_btn5m, _timeframeSeconds == 300);
        }

        private static void SetBtnStyle(Button btn, bool active)
        {
            if (active)
            {
                btn.BackColor = Color.FromArgb(0, 120, 215);
                btn.ForeColor = Color.White;
            }
            else
            {
                btn.BackColor = Color.FromArgb(42, 42, 46);
                btn.ForeColor = Color.FromArgb(180, 180, 180);
            }
        }

        public void Toggle(Rectangle meterBounds)
        {
            if (Visible)
            {
                Hide();
                return;
            }

            float dpi = DpiHelper.GetDpiScale(Handle);
            int w = DpiHelper.Scale(390, dpi);
            int h = DpiHelper.Scale(565, dpi);
            Size = new Size(w, h);

            int contentW = ClientSize.Width;
            int contentH = ClientSize.Height - 46 - 50;
            if (_panelMonitor != null) _panelMonitor.Size = new Size(contentW, contentH);
            if (_panelSpeedTest != null) _panelSpeedTest.Size = new Size(contentW, contentH);

            Screen scr = Screen.FromRectangle(meterBounds);
            Rectangle workArea = scr.WorkingArea;

            // Horizontally center with the meter
            int x = meterBounds.Left + (meterBounds.Width - w) / 2;
            if (x < workArea.Left + 8) x = workArea.Left + 8;
            if (x + w > workArea.Right - 8) x = workArea.Right - 8;

            // Position vertically directly above the meter if taskbar is at bottom
            int y;
            if (meterBounds.Bottom > workArea.Bottom - 60)
            {
                y = meterBounds.Top - h - 8;
            }
            else
            {
                y = meterBounds.Bottom + 8;
            }

            if (y < workArea.Top + 8) y = workArea.Top + 8;
            if (y + h > workArea.Bottom - 8) y = workArea.Bottom - h - 8;

            Location = new Point(x, y);
            UpdateData();
            Show();
            Activate();
        }

        public void UpdateData()
        {
            if (IsDisposed) return;

            var stats = _networkMonitor.CurrentStats;
            if (stats != null)
            {
                _lblDownloadStats.Text = string.Format("▼ {0} {1}", stats.DownloadFormatted, stats.DownloadUnit);
                _lblUploadStats.Text = string.Format("▲ {0} {1}", stats.UploadFormatted, stats.UploadUnit);
            }

            // Adapter info (concise, clear 1-line presentation)
            string alias = _networkMonitor.ActiveAdapterName;
            string desc = _networkMonitor.ActiveAdapterDescription;
            ulong speedBps = _networkMonitor.ActiveLinkSpeed;
            string speedStr = speedBps > 0 ? FormatLinkSpeed(speedBps) : "Active";
            if (!string.IsNullOrEmpty(desc) && desc != alias)
            {
                _lblAdapterInfo.Text = string.Format("Adapter: {0} • Link: {1} ({2})", alias, speedStr, desc);
            }
            else
            {
                _lblAdapterInfo.Text = string.Format("Adapter: {0} • Link: {1} • Active Telemetry", alias, speedStr);
            }

            // Session data & Quota calculation
            ulong dlBytes = _networkMonitor.SessionDownloadedBytes;
            ulong ulBytes = _networkMonitor.SessionUploadedBytes;
            ulong totalBytes = dlBytes + ulBytes;
            TimeSpan duration = DateTime.Now - _networkMonitor.SessionStartTime;
            string timeStr = string.Format("{0:00}h {1:00}m {2:00}s", (int)duration.TotalHours, duration.Minutes, duration.Seconds);
            _lblSessionTotals.Text = string.Format("Session: ▲ {0}  |  ▼ {1}  ({2})",
                FormatBytes(ulBytes), FormatBytes(dlBytes), timeStr);

            // Update Historical Data Usage (Net Used Till Now: Today, Week, Month)
            if (_networkMonitor.UsageTracker != null)
            {
                var usage = _networkMonitor.UsageTracker.CurrentRecord;
                if (usage != null)
                {
                    if (_lblUsageTodayVal != null) _lblUsageTodayVal.Text = DataUsageTracker.FormatBytes(usage.TodayTotalBytes);
                    if (_lblUsageWeekVal != null) _lblUsageWeekVal.Text = DataUsageTracker.FormatBytes(usage.WeekTotalBytes);
                    if (_lblUsageMonthVal != null) _lblUsageMonthVal.Text = DataUsageTracker.FormatBytes(usage.MonthTotalBytes);
                }
            }

            double quotaGb = _configService.Config.MonthlyDataQuotaGb;
            if (quotaGb > 0)
            {
                double usedGb = totalBytes / 1073741824.0;
                double pct = Math.Min(100.0, (usedGb / quotaGb) * 100.0);
                double remGb = Math.Max(0.0, quotaGb - usedGb);
                _lblQuotaStats.Text = string.Format("Data Quota: {0:F2} GB / {1:F0} GB ({2:F1}%) • {3:F2} GB left",
                    usedGb, quotaGb, pct, remGb);
                _lblQuotaStats.ForeColor = pct > 95.0 ? Color.FromArgb(255, 100, 100) :
                                           pct > 80.0 ? Color.FromArgb(255, 180, 60) :
                                                        Color.FromArgb(100, 200, 255);
                _quotaPercent = (float)pct;
            }
            else
            {
                _lblQuotaStats.Text = string.Format("Data Quota: Unlimited ({0} transferred)", FormatBytes(totalBytes));
                _lblQuotaStats.ForeColor = Color.FromArgb(170, 170, 175);
                _quotaPercent = 0f;
            }
            if (_quotaProgressBar != null) _quotaProgressBar.Invalidate();

            // Active network processes attribution
            try
            {
                int configuredLimit = _configService.Config.TopAppsLimit;
                var topApps = _processTracker.GetTopActiveApps(configuredLimit <= 0 ? 50 : configuredLimit);
                _currentTopApps = topApps ?? new List<AppNetworkUsage>();
            }
            catch
            {
                _currentTopApps = new List<AppNetworkUsage>();
            }
            UpdateMonitorLayout();
            if (_pnlTopApps != null) _pnlTopApps.Invalidate();

            if (_panelMonitor.Visible)
            {
                _graphPanel.Invalidate();
            }
        }

        private void OnTopAppsPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = _pnlTopApps.ClientSize.Width;
            int h = _pnlTopApps.ClientSize.Height;

            // Header line: "TOP NETWORK APPS & LIVE CONSUMPTION"
            using (var titleFont = new Font("Segoe UI", 7.0f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.FromArgb(150, 150, 155)))
            using (var dotBrush = new SolidBrush(Color.FromArgb(76, 194, 255)))
            {
                g.FillEllipse(dotBrush, 0, 3, 5, 5);
                g.DrawString("TOP NETWORK APPS & LIVE CONSUMPTION", titleFont, titleBrush, 9, 0);
            }

            if (_currentTopApps == null || _currentTopApps.Count == 0)
            {
                using (var emptyFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
                using (var emptyBrush = new SolidBrush(Color.FromArgb(140, 140, 145)))
                {
                    g.DrawString("No active network sockets detected", emptyFont, emptyBrush, 0, 24);
                }
                return;
            }

            int configuredLimit = _configService.Config.TopAppsLimit;
            int maxDisplay = configuredLimit <= 0 ? _currentTopApps.Count : configuredLimit;
            int count = Math.Min(maxDisplay, _currentTopApps.Count);

            double totalWeight = 0;
            for (int j = 0; j < count; j++) totalWeight += _currentTopApps[j].AttributionWeight;
            if (totalWeight <= 0) totalWeight = 1.0;

            var stats = _networkMonitor != null ? _networkMonitor.CurrentStats : null;
            double totalBandwidth = stats != null ? (stats.DownloadSpeedBytesPerSec + stats.UploadSpeedBytesPerSec) : 0;

            int rowH = 20;
            int gap = 2;
            int startY = 16;

            using (var nameFont = new Font("Segoe UI", 8.0f, FontStyle.Bold))
            using (var rateFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var badgeFont = new Font("Segoe UI", 7.0f, FontStyle.Regular))
            using (var iconFont = new Font("Segoe UI Emoji", 8.0f, FontStyle.Regular))
            using (var rowBg = new SolidBrush(Color.FromArgb(38, 38, 44)))
            using (var rowBorder = new Pen(Color.FromArgb(52, 52, 60), 1))
            using (var nameBrush = new SolidBrush(Color.FromArgb(235, 235, 240)))
            using (var badgeBg = new SolidBrush(Color.FromArgb(24, 60, 95)))
            using (var badgeText = new SolidBrush(Color.FromArgb(76, 194, 255)))
            using (var rateBrushActive = new SolidBrush(Color.FromArgb(108, 203, 95)))
            using (var rateBrushIdle = new SolidBrush(Color.FromArgb(160, 160, 165)))
            using (var barBrush = new SolidBrush(Color.FromArgb(0, 120, 215)))
            {
                for (int i = 0; i < count; i++)
                {
                    var app = _currentTopApps[i];
                    int py = startY + (i * (rowH + gap));

                    // Full-width row container
                    using (var path = GetRoundedRect(0, py, w - 1, rowH - 1, 3))
                    {
                        g.FillPath(rowBg, path);
                        g.DrawPath(rowBorder, path);
                    }

                    // Activity indicator bar along bottom of row
                    double shareRatio = app.AttributionWeight / totalWeight;
                    int barW = Math.Max(4, (int)((w - 4) * shareRatio));
                    g.FillRectangle(barBrush, 2, py + rowH - 2, barW, 2);

                    // Category Icon (X = 6)
                    string icon = GetAppCategoryIcon(app.ProcessName);
                    g.DrawString(icon, iconFont, nameBrush, 6, py + 2);

                    // Real-time Bandwidth Consumption (right-aligned)
                    string rateText;
                    Brush currentRateBrush;
                    if (totalBandwidth > 512)
                    {
                        double appSpeed = totalBandwidth * shareRatio;
                        rateText = string.Format("{0} ({1:F0}%)", FormatRate(appSpeed), shareRatio * 100.0);
                        currentRateBrush = rateBrushActive;
                    }
                    else
                    {
                        rateText = string.Format("{0:F0}% activity", shareRatio * 100.0);
                        currentRateBrush = rateBrushIdle;
                    }
                    SizeF rateSz = g.MeasureString(rateText, rateFont);
                    float rx = w - rateSz.Width - 8;
                    g.DrawString(rateText, rateFont, currentRateBrush, rx, py + 3);

                    // App Name (between icon and consumption stats)
                    float nameX = 26;
                    int maxNameW = (int)(rx - 12 - nameX);
                    string displayName = app.ProcessCount > 1 ? string.Format("{0} ({1})", app.ProcessName, app.ProcessCount) : app.ProcessName;
                    SizeF nameSz = g.MeasureString(displayName, nameFont);
                    if (maxNameW > 20 && nameSz.Width > maxNameW)
                    {
                        while (displayName.Length > 3 && g.MeasureString(displayName + "…", nameFont).Width > maxNameW)
                        {
                            displayName = displayName.Substring(0, displayName.Length - 1);
                        }
                        displayName += "…";
                        nameSz = g.MeasureString(displayName, nameFont);
                    }
                    g.DrawString(displayName, nameFont, nameBrush, nameX, py + 2);

                    // Subtle, clean socket count text e.g. "(17 conn)" right beside name
                    float connX = nameX + nameSz.Width + 4;
                    if (connX + 44 < rx)
                    {
                        using (var mutedBrush = new SolidBrush(Color.FromArgb(135, 135, 142)))
                        {
                            g.DrawString(string.Format("({0} conn)", app.ActiveConnections), badgeFont, mutedBrush, connX, py + 3);
                        }
                    }
                }
            }
        }

        private static string GetAppCategoryIcon(string name)
        {
            if (string.IsNullOrEmpty(name)) return "📡";
            string lower = name.ToLowerInvariant();
            if (lower.Contains("chrome") || lower.Contains("edge") || lower.Contains("firefox") || lower.Contains("zen") || lower.Contains("brave") || lower.Contains("opera"))
                return "🌐";
            if (lower.Contains("steam") || lower.Contains("epic") || lower.Contains("discord") || lower.Contains("riot") || lower.Contains("game"))
                return "🎮";
            if (lower.Contains("code") || lower.Contains("git") || lower.Contains("agy") || lower.Contains("antigravity") || lower.Contains("devenv") || lower.Contains("cmd") || lower.Contains("node"))
                return "⚡";
            if (lower.Contains("explorer") || lower.Contains("svchost") || lower.Contains("system"))
                return "⚙";
            return "📡";
        }

        private static string FormatLinkSpeed(ulong bps)
        {
            if (bps >= 1000000000UL) return string.Format("{0:F1} Gbps", bps / 1000000000.0);
            if (bps >= 1000000UL) return string.Format("{0:F0} Mbps", bps / 1000000.0);
            if (bps >= 1000UL) return string.Format("{0:F0} Kbps", bps / 1000.0);
            return string.Format("{0} bps", bps);
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes >= 1073741824UL) return string.Format("{0:F2} GB", bytes / 1073741824.0);
            if (bytes >= 1048576UL) return string.Format("{0:F1} MB", bytes / 1048576.0);
            if (bytes >= 1024UL) return string.Format("{0:F0} KB", bytes / 1024.0);
            return string.Format("{0} B", bytes);
        }

        private static string FormatRate(double bytesPerSec)
        {
            if (bytesPerSec >= 1048576.0) return string.Format("{0:F1} MB/s", bytesPerSec / 1048576.0);
            if (bytesPerSec >= 1024.0) return string.Format("{0:F1} KB/s", bytesPerSec / 1024.0);
            return string.Format("{0:F0} B/s", bytesPerSec);
        }

        private void OnGraphPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = _graphPanel.ClientSize.Width;
            int h = _graphPanel.ClientSize.Height;

            // Border
            using (var borderPen = new Pen(Color.FromArgb(45, 45, 50), 1))
            {
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            }

            var snapshot = _networkMonitor.GetHistorySnapshot();
            int count = snapshot.Length;

            // Filter points within selected timeframe
            int pointsToTake = Math.Min(count, _timeframeSeconds);
            if (pointsToTake < 2)
            {
                using (var brush = new SolidBrush(Color.FromArgb(100, 100, 100)))
                {
                    g.DrawString("Accumulating live telemetry data...", Font, brush, 18, h / 2 - 8);
                }
                return;
            }

            NetworkHistoryPoint[] visible = new NetworkHistoryPoint[pointsToTake];
            Array.Copy(snapshot, count - pointsToTake, visible, 0, pointsToTake);

            // Determine max rate for Y auto-scaling
            double maxRate = 1024.0 * 100; // Minimum scale: 100 KB/s
            for (int i = 0; i < pointsToTake; i++)
            {
                if (visible[i].DownloadBytesPerSec > maxRate) maxRate = visible[i].DownloadBytesPerSec;
                if (visible[i].UploadBytesPerSec > maxRate) maxRate = visible[i].UploadBytesPerSec;
            }
            maxRate *= 1.15; // 15% headroom

            // Draw grid lines
            using (var gridPen = new Pen(Color.FromArgb(35, 35, 40), 1) { DashStyle = DashStyle.Dash })
            using (var labelBrush = new SolidBrush(Color.FromArgb(90, 90, 95)))
            using (var labelFont = new Font("Segoe UI", 7.0f))
            {
                int yTop = 15;
                int yMid = (h - 20) / 2 + 10;
                int yBot = h - 15;

                g.DrawLine(gridPen, 5, yTop, w - 5, yTop);
                g.DrawLine(gridPen, 5, yMid, w - 5, yMid);
                g.DrawLine(gridPen, 5, yBot, w - 5, yBot);

                g.DrawString(FormatRate(maxRate), labelFont, labelBrush, 8, yTop - 12);
                g.DrawString(FormatRate(maxRate / 2), labelFont, labelBrush, 8, yMid - 12);
                g.DrawString("0 B/s", labelFont, labelBrush, 8, yBot - 12);
            }

            // Calculate point coordinates
            PointF[] dlPoints = new PointF[pointsToTake];
            PointF[] ulPoints = new PointF[pointsToTake];

            float xStep = (float)(w - 10) / (float)(pointsToTake - 1);
            int plotH = h - 30;

            for (int i = 0; i < pointsToTake; i++)
            {
                float px = 5 + (i * xStep);
                float pyDl = (float)(h - 15 - ((visible[i].DownloadBytesPerSec / maxRate) * plotH));
                float pyUl = (float)(h - 15 - ((visible[i].UploadBytesPerSec / maxRate) * plotH));

                if (pyDl < 15) pyDl = 15;
                if (pyUl < 15) pyUl = 15;

                dlPoints[i] = new PointF(px, pyDl);
                ulPoints[i] = new PointF(px, pyUl);
            }

            // Draw Download area fill & curve (Cyan)
            using (var dlPath = new GraphicsPath())
            {
                dlPath.AddLine(new PointF(dlPoints[0].X, (float)(h - 15)), dlPoints[0]);
                for (int i = 1; i < pointsToTake; i++) dlPath.AddLine(dlPoints[i - 1], dlPoints[i]);
                dlPath.AddLine(dlPoints[pointsToTake - 1], new PointF(dlPoints[pointsToTake - 1].X, (float)(h - 15)));
                dlPath.CloseFigure();

                using (var fillBrush = new LinearGradientBrush(new Point(0, 15), new Point(0, h - 15),
                    Color.FromArgb(45, 76, 194, 255), Color.FromArgb(5, 76, 194, 255)))
                {
                    g.FillPath(fillBrush, dlPath);
                }
            }
            using (var dlPen = new Pen(Color.FromArgb(76, 194, 255), 2.0f))
            {
                g.DrawLines(dlPen, dlPoints);
            }

            // Draw Upload area fill & curve (Green)
            using (var ulPath = new GraphicsPath())
            {
                ulPath.AddLine(new PointF(ulPoints[0].X, (float)(h - 15)), ulPoints[0]);
                for (int i = 1; i < pointsToTake; i++) ulPath.AddLine(ulPoints[i - 1], ulPoints[i]);
                ulPath.AddLine(ulPoints[pointsToTake - 1], new PointF(ulPoints[pointsToTake - 1].X, (float)(h - 15)));
                ulPath.CloseFigure();

                using (var fillBrush = new LinearGradientBrush(new Point(0, 15), new Point(0, h - 15),
                    Color.FromArgb(40, 108, 203, 95), Color.FromArgb(5, 108, 203, 95)))
                {
                    g.FillPath(fillBrush, ulPath);
                }
            }
            using (var ulPen = new Pen(Color.FromArgb(108, 203, 95), 1.8f))
            {
                g.DrawLines(ulPen, ulPoints);
            }

            // Interactive Hover / Scrub Crosshair & Tooltip
            if (_isHoveringGraph && _hoverMouseX >= 5 && _hoverMouseX <= w - 5)
            {
                int nearestIdx = (int)Math.Round((_hoverMouseX - 5) / xStep);
                if (nearestIdx < 0) nearestIdx = 0;
                if (nearestIdx >= pointsToTake) nearestIdx = pointsToTake - 1;

                PointF dlPt = dlPoints[nearestIdx];
                PointF ulPt = ulPoints[nearestIdx];
                var ptData = visible[nearestIdx];

                // Vertical scrub line
                using (var scrubPen = new Pen(Color.FromArgb(160, 255, 255, 255), 1) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(scrubPen, dlPt.X, 15, dlPt.X, h - 15);
                }

                // Glowing highlight dots on curves
                using (var dlDotBrush = new SolidBrush(Color.FromArgb(76, 194, 255)))
                using (var ulDotBrush = new SolidBrush(Color.FromArgb(108, 203, 95)))
                using (var whiteBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(whiteBrush, dlPt.X - 4, dlPt.Y - 4, 8, 8);
                    g.FillEllipse(dlDotBrush, dlPt.X - 3, dlPt.Y - 3, 6, 6);

                    g.FillEllipse(whiteBrush, ulPt.X - 4, ulPt.Y - 4, 8, 8);
                    g.FillEllipse(ulDotBrush, ulPt.X - 3, ulPt.Y - 3, 6, 6);
                }

                // Sleek Floating Tooltip Pill
                string timeStr = ptData.Time.ToString("HH:mm:ss");
                string dlStr = "▼ " + FormatRate(ptData.DownloadBytesPerSec);
                string ulStr = "▲ " + FormatRate(ptData.UploadBytesPerSec);

                int tipW = 125;
                int tipH = 54;
                int tipX = (int)dlPt.X + 10;
                if (tipX + tipW > w - 8) tipX = (int)dlPt.X - tipW - 10;
                int tipY = _hoverMouseY - tipH / 2;
                if (tipY < 12) tipY = 12;
                if (tipY + tipH > h - 12) tipY = h - tipH - 12;

                using (var tipBg = new SolidBrush(Color.FromArgb(235, 18, 18, 20)))
                using (var tipBorder = new Pen(Color.FromArgb(70, 70, 80), 1))
                using (var timeFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                using (var statFont = new Font("Segoe UI", 8.0f, FontStyle.Regular))
                using (var dlBrush = new SolidBrush(Color.FromArgb(76, 194, 255)))
                using (var ulBrush = new SolidBrush(Color.FromArgb(108, 203, 95)))
                using (var timeBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
                {
                    g.FillRectangle(tipBg, tipX, tipY, tipW, tipH);
                    g.DrawRectangle(tipBorder, tipX, tipY, tipW, tipH);

                    g.DrawString(timeStr, timeFont, timeBrush, tipX + 8, tipY + 4);
                    g.DrawString(dlStr, statFont, dlBrush, tipX + 8, tipY + 18);
                    g.DrawString(ulStr, statFont, ulBrush, tipX + 8, tipY + 34);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Outer border
            using (var pen = new Pen(Color.FromArgb(60, 60, 65), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            // Auto close on outside click unless speed test is actively executing
            if (!_speedTestService.IsRunning)
            {
                Hide();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Hide();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
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
    }
}
