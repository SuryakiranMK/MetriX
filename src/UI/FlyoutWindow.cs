using System;
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

        public event Action RequestOpenSettings;

        // UI sub-controls
        private Panel _graphPanel;
        private Label _lblDownloadStats;
        private Label _lblUploadStats;
        private Label _lblAdapterInfo;
        private Label _lblSessionTotals;
        private Button _btn30s;
        private Button _btn60s;
        private Button _btn5m;

        // Interactive graph state
        private int _timeframeSeconds = 60; // 30, 60, or 300
        private int _hoverMouseX = -1;
        private int _hoverMouseY = -1;
        private bool _isHoveringGraph = false;

        public FlyoutWindow(ConfigService configService, NetworkMonitor networkMonitor)
        {
            _configService = configService;
            _networkMonitor = networkMonitor;

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
                // Add drop shadow style in Win32
                const int CS_DROPSHADOW = 0x00020000;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        private void InitializeUI()
        {
            Width = 380;
            Height = 440;

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
                Location = new Point(14, 11),
                AutoSize = true
            };

            var lblSubtitle = new Label
            {
                Text = "LIVE NETWORK MONITOR",
                Font = new Font("Segoe UI", 7.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 194, 255),
                Location = new Point(78, 14),
                AutoSize = true
            };

            var btnClose = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(32, 28),
                Location = new Point(Width - 42, 8),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 64);
            btnClose.Click += (s, e) => Hide();

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(btnClose);
            Controls.Add(headerPanel);

            // Timeframe selection buttons
            int btnY = 52;
            _btn30s = CreateTimeframeButton("30s", 30, 14, btnY);
            _btn60s = CreateTimeframeButton("60s", 60, 66, btnY);
            _btn5m = CreateTimeframeButton("5m", 300, 118, btnY);
            UpdateTimeframeButtons();

            Controls.Add(_btn30s);
            Controls.Add(_btn60s);
            Controls.Add(_btn5m);

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
            Controls.Add(_lblDownloadStats);
            Controls.Add(_lblUploadStats);

            // Interactive Graph Canvas
            _graphPanel = new Panel
            {
                Location = new Point(14, 82),
                Size = new Size(352, 175),
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
            Controls.Add(_graphPanel);

            // Session Stats Card
            int cardY = 265;
            var cardSession = new Panel
            {
                Location = new Point(14, cardY),
                Size = new Size(352, 115),
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
                Text = "ACTIVE CONNECTION & SESSION",
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
                Location = new Point(12, 30),
                Size = new Size(328, 36)
            };

            _lblSessionTotals = new Label
            {
                Text = "Session Data: 0 B Total",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(210, 210, 210),
                Location = new Point(12, 70),
                Size = new Size(328, 36)
            };

            cardSession.Controls.Add(lblCardTitle);
            cardSession.Controls.Add(_lblAdapterInfo);
            cardSession.Controls.Add(_lblSessionTotals);
            Controls.Add(cardSession);

            // Footer action bar
            int footY = 388;
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
                Size = new Size(125, 32),
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
                Location = new Point(255, footY),
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
            int w = DpiHelper.Scale(380, dpi);
            int h = DpiHelper.Scale(440, dpi);
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

            _graphPanel.Invalidate();
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

            // Subtle outer border
            using (var pen = new Pen(Color.FromArgb(60, 60, 65), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            // Light-dismiss: auto-close on clicking outside
            Hide();
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
