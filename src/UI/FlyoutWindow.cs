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
    public class FlyoutWindow : Form
    {
        private readonly ConfigService _configService;
        private readonly NetworkMonitor _networkMonitor;
        private readonly SpeedTestService _speedTestService;

        public event Action RequestOpenSettings;

        // View Tabs
        private bool _isSpeedTestView = false;
        private Button _tabBtnMonitor;
        private Button _tabBtnSpeedTest;
        private Panel _panelMonitor;
        private Panel _panelSpeedTest;

        // Live Monitor Controls
        private Panel _graphPanel;
        private Label _lblDownloadStats;
        private Label _lblUploadStats;
        private Label _lblAdapterInfo;
        private Label _lblSessionTotals;
        private Button _btn30s;
        private Button _btn60s;
        private Button _btn5m;

        // Live Monitor State
        private int _timeframeSeconds = 60;
        private int _hoverMouseX = -1;
        private int _hoverMouseY = -1;
        private bool _isHoveringGraph = false;

        // Speed Test Controls
        private Panel _gaugePanel;
        private Button _btnStartSpeedTest;
        private Label _lblSpeedTestStatus;
        private Label _lblTestPing;
        private Label _lblTestDownload;
        private Label _lblTestUpload;
        private Label _lblTestHistory;

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

        private void InitializeUI()
        {
            Width = 384;
            Height = 460;

            // Header bar
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(34, 34, 38)
            };

            var lblTitle = new Label
            {
                Text = "metriX",
                Font = new Font("Segoe UI", 11.0f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(12, 11),
                AutoSize = true
            };

            // Header View Switcher Tabs
            _tabBtnMonitor = new Button
            {
                Text = "Live Graph",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                Size = new Size(82, 26),
                Location = new Point(80, 9),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _tabBtnMonitor.FlatAppearance.BorderSize = 0;
            _tabBtnMonitor.Click += (s, e) => SwitchView(false);

            _tabBtnSpeedTest = new Button
            {
                Text = "Speed Test",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                Size = new Size(82, 26),
                Location = new Point(168, 9),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _tabBtnSpeedTest.FlatAppearance.BorderSize = 0;
            _tabBtnSpeedTest.Click += (s, e) => SwitchView(true);

            var btnClose = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(32, 28),
                Location = new Point(Width - 38, 8),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 64);
            btnClose.Click += (s, e) => Hide();

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(_tabBtnMonitor);
            headerPanel.Controls.Add(_tabBtnSpeedTest);
            headerPanel.Controls.Add(btnClose);
            Controls.Add(headerPanel);

            // Container 1: Live Monitor View
            _panelMonitor = new Panel
            {
                Location = new Point(0, 44),
                Size = new Size(Width, 366),
                BackColor = Color.FromArgb(28, 28, 30)
            };
            BuildMonitorView(_panelMonitor);
            Controls.Add(_panelMonitor);

            // Container 2: Speed Test View
            _panelSpeedTest = new Panel
            {
                Location = new Point(0, 44),
                Size = new Size(Width, 366),
                BackColor = Color.FromArgb(28, 28, 30),
                Visible = false
            };
            BuildSpeedTestView(_panelSpeedTest);
            Controls.Add(_panelSpeedTest);

            UpdateViewTabs();

            // Footer action bar (common to both views)
            int footY = 415;
            var btnSettings = new Button
            {
                Text = "⚙ Settings",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(42, 42, 46),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 32),
                Location = new Point(14, footY),
                Cursor = Cursors.Hand
            };
            btnSettings.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 64);
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
                BackColor = Color.FromArgb(42, 42, 46),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(130, 32),
                Location = new Point(122, footY),
                Cursor = Cursors.Hand
            };
            btnTaskMgr.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 64);
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
                BackColor = Color.FromArgb(42, 42, 46),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(95, 32),
                Location = new Point(260, footY),
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 64);
            btnReset.Click += (s, e) =>
            {
                _networkMonitor.ResetSessionStats();
                UpdateData();
            };

            Controls.Add(btnSettings);
            Controls.Add(btnTaskMgr);
            Controls.Add(btnReset);
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
        }

        private void BuildMonitorView(Panel container)
        {
            // Timeframe selection buttons
            int btnY = 8;
            _btn30s = CreateTimeframeButton("30s", 30, 14, btnY);
            _btn60s = CreateTimeframeButton("60s", 60, 66, btnY);
            _btn5m = CreateTimeframeButton("5m", 300, 118, btnY);
            UpdateTimeframeButtons();

            container.Controls.Add(_btn30s);
            container.Controls.Add(_btn60s);
            container.Controls.Add(_btn5m);

            // Live rate indicators right of timeframe buttons
            _lblDownloadStats = new Label
            {
                Text = "▼ 0.0 KB/s",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 194, 255),
                Location = new Point(175, btnY + 4),
                AutoSize = true
            };
            _lblUploadStats = new Label
            {
                Text = "▲ 0.0 KB/s",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(108, 203, 95),
                Location = new Point(275, btnY + 4),
                AutoSize = true
            };
            container.Controls.Add(_lblDownloadStats);
            container.Controls.Add(_lblUploadStats);

            // Interactive Graph Canvas
            _graphPanel = new Panel
            {
                Location = new Point(14, 38),
                Size = new Size(356, 185),
                BackColor = Color.FromArgb(20, 20, 22)
            };
            _graphPanel.Paint += OnGraphPaint;
            _graphPanel.MouseMove += (s, e) =>
            {
                _hoverMouseX = e.X;
                _hoverMouseY = e.Y;
                _isHoveringGraph = true;
                _graphPanel.Invalidate();
            };
            _graphPanel.MouseLeave += (s, e) =>
            {
                _isHoveringGraph = false;
                _hoverMouseX = -1;
                _hoverMouseY = -1;
                _graphPanel.Invalidate();
            };
            container.Controls.Add(_graphPanel);

            // Session Stats Card
            int cardY = 232;
            var cardSession = new Panel
            {
                Location = new Point(14, cardY),
                Size = new Size(356, 125),
                BackColor = Color.FromArgb(34, 34, 38)
            };
            cardSession.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(50, 50, 54), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, cardSession.Width - 1, cardSession.Height - 1);
                }
            };

            var lblCardTitle = new Label
            {
                Text = "ACTIVE CONNECTION & SESSION TELEMETRY",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 160, 160),
                Location = new Point(12, 10),
                AutoSize = true
            };

            _lblAdapterInfo = new Label
            {
                Text = "Adapter: Detecting...",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.White,
                Location = new Point(12, 32),
                Size = new Size(330, 42)
            };

            _lblSessionTotals = new Label
            {
                Text = "Session Data: 0 B Total",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(210, 210, 210),
                Location = new Point(12, 78),
                Size = new Size(330, 40)
            };

            cardSession.Controls.Add(lblCardTitle);
            cardSession.Controls.Add(_lblAdapterInfo);
            cardSession.Controls.Add(_lblSessionTotals);
            container.Controls.Add(cardSession);
        }

        private void BuildSpeedTestView(Panel container)
        {
            // Speedometer Gauge Arc Canvas
            _gaugePanel = new Panel
            {
                Location = new Point(14, 8),
                Size = new Size(356, 175),
                BackColor = Color.FromArgb(20, 20, 22)
            };
            _gaugePanel.Paint += OnGaugePaint;
            container.Controls.Add(_gaugePanel);

            // Status message label
            _lblSpeedTestStatus = new Label
            {
                Text = "Ready to benchmark your network",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(14, 188),
                Size = new Size(356, 20)
            };
            container.Controls.Add(_lblSpeedTestStatus);

            // 3 Metrics Badges Card
            var cardMetrics = new Panel
            {
                Location = new Point(14, 212),
                Size = new Size(356, 56),
                BackColor = Color.FromArgb(34, 34, 38)
            };
            cardMetrics.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(50, 50, 54), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, cardMetrics.Width - 1, cardMetrics.Height - 1);
                }
            };

            _lblTestPing = new Label
            {
                Text = "PING\n-- ms",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 220, 220),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(10, 8),
                Size = new Size(100, 40)
            };

            _lblTestDownload = new Label
            {
                Text = "DOWNLOAD\n-- Mbps",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 194, 255),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(125, 8),
                Size = new Size(110, 40)
            };

            _lblTestUpload = new Label
            {
                Text = "UPLOAD\n-- Mbps",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(108, 203, 95),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(245, 8),
                Size = new Size(100, 40)
            };

            cardMetrics.Controls.Add(_lblTestPing);
            cardMetrics.Controls.Add(_lblTestDownload);
            cardMetrics.Controls.Add(_lblTestUpload);
            container.Controls.Add(cardMetrics);

            // Start / Cancel Test Button
            _btnStartSpeedTest = new Button
            {
                Text = "⚡  START SPEED TEST",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 215),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(356, 36),
                Location = new Point(14, 276),
                Cursor = Cursors.Hand
            };
            _btnStartSpeedTest.FlatAppearance.BorderSize = 0;
            _btnStartSpeedTest.Click += OnStartOrCancelSpeedTest;
            container.Controls.Add(_btnStartSpeedTest);

            // Recent Benchmark History List
            _lblTestHistory = new Label
            {
                Text = "Recent tests will appear here.",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 150, 155),
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(16, 318),
                Size = new Size(352, 42)
            };
            container.Controls.Add(_lblTestHistory);
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

                _speedTestService.StartTest();
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
            int toShow = Math.Min(2, history.Count);
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
            int gaugeDiameter = 220;
            int arcX = (w - gaugeDiameter) / 2;
            int arcY = 22;
            int penWidth = 14;

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

            using (var valFont = new Font("Segoe UI", 26.0f, FontStyle.Bold))
            using (var unitFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var phaseFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var valBrush = new SolidBrush(Color.White))
            using (var unitBrush = new SolidBrush(Color.FromArgb(180, 180, 185)))
            using (var phaseBrush = new SolidBrush(Color.FromArgb(76, 194, 255)))
            {
                SizeF valSize = g.MeasureString(speedText, valFont);
                float valX = (w - valSize.Width) / 2;
                float valY = arcY + 50;
                g.DrawString(speedText, valFont, valBrush, valX, valY);

                SizeF unitSize = g.MeasureString(unitText, unitFont);
                g.DrawString(unitText, unitFont, unitBrush, (w - unitSize.Width) / 2, valY + valSize.Height - 8);

                SizeF phaseSize = g.MeasureString(phaseText, phaseFont);
                g.DrawString(phaseText, phaseFont, phaseBrush, (w - phaseSize.Width) / 2, valY + valSize.Height + 14);
            }

            // Scale min / max labels
            using (var scaleFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (var scaleBrush = new SolidBrush(Color.FromArgb(120, 120, 125)))
            {
                g.DrawString("0", scaleFont, scaleBrush, arcX - 2, arcY + (gaugeDiameter / 2) + 6);
                string maxStr = _maxGaugeScale.ToString("F0");
                SizeF maxSz = g.MeasureString(maxStr, scaleFont);
                g.DrawString(maxStr, scaleFont, scaleBrush, arcX + gaugeDiameter - maxSz.Width + 2, arcY + (gaugeDiameter / 2) + 6);
            }
        }

        private Button CreateTimeframeButton(string text, int seconds, int x, int y)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                Size = new Size(46, 24),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
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
            int w = DpiHelper.Scale(384, dpi);
            int h = DpiHelper.Scale(460, dpi);
            Size = new Size(w, h);

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

            // Adapter info
            string alias = _networkMonitor.ActiveAdapterName;
            string desc = _networkMonitor.ActiveAdapterDescription;
            ulong speedBps = _networkMonitor.ActiveLinkSpeed;
            string speedStr = speedBps > 0 ? FormatLinkSpeed(speedBps) : "Active";
            _lblAdapterInfo.Text = string.Format("Adapter: {0}\n{1} • {2}", alias, desc, speedStr);

            // Session data
            ulong dlBytes = _networkMonitor.SessionDownloadedBytes;
            ulong ulBytes = _networkMonitor.SessionUploadedBytes;
            TimeSpan duration = DateTime.Now - _networkMonitor.SessionStartTime;
            string timeStr = string.Format("{0:00}h {1:00}m {2:00}s", (int)duration.TotalHours, duration.Minutes, duration.Seconds);
            _lblSessionTotals.Text = string.Format("Downloaded: {0}  |  Uploaded: {1}\nSession Duration: {2}",
                FormatBytes(dlBytes), FormatBytes(ulBytes), timeStr);

            if (_panelMonitor.Visible)
            {
                _graphPanel.Invalidate();
            }
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
    }
}
