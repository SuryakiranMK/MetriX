using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MetriX.Model;
using MetriX.Native;
using MetriX.Services;

namespace MetriX.UI
{
    public class UpdateDialog : Form
    {
        private readonly UpdateInfo _updateInfo;
        private readonly UpdateService _updateService;
        private readonly ConfigService _configService;

        private Panel _progressPanel;
        private Label _lblProgressStatus;
        private int _progressPercent = 0;
        private Button _btnUpdate;
        private Button _btnLater;
        private FluentToggleSwitch _toggleSilent;

        public UpdateDialog(UpdateInfo updateInfo, UpdateService updateService, ConfigService configService)
        {
            _updateInfo = updateInfo;
            _updateService = updateService;
            _configService = configService;

            InitializeUI();
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
                int darkMode = 1;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                int cornerPreference = 2; // DWMWCP_ROUND
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
            }
            catch { }
        }

        private void InitializeUI()
        {
            Text = "metriX Update Available";
            Size = new Size(460, 440);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(28, 28, 30);
            ForeColor = Color.White;
            Font = new Font("Segoe UI Variable Text", 9.0f, FontStyle.Regular);

            // Header Banner
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(34, 34, 38)
            };
            headerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(48, 48, 54), 1))
                {
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
                }
            };

            var lblBadge = new Label
            {
                Text = "⚡",
                Font = new Font("Segoe UI", 18.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(76, 194, 255),
                Location = new Point(16, 16),
                AutoSize = true
            };

            var lblTitle = new Label
            {
                Text = string.Format("metriX {0} is available!", _updateInfo.VersionTag),
                Font = new Font("Segoe UI", 12.0f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(54, 14),
                AutoSize = true,
                UseMnemonic = false
            };

            string dateStr = _updateInfo.PublishedAt != DateTime.MinValue
                ? _updateInfo.PublishedAt.ToLocalTime().ToString("MMM dd, yyyy")
                : "Latest Release";

            var lblSubtitle = new Label
            {
                Text = string.Format("Current version: v{0} • Released {1}", _updateInfo.CurrentVersion, dateStr),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(170, 170, 175),
                Location = new Point(55, 38),
                AutoSize = true,
                UseMnemonic = false
            };

            headerPanel.Controls.Add(lblBadge);
            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            Controls.Add(headerPanel);

            // Changelog / Release Highlights Card
            var cardChangelog = new Panel
            {
                Location = new Point(16, 86),
                Size = new Size(412, 190),
                BackColor = Color.FromArgb(20, 20, 22)
            };
            cardChangelog.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(45, 45, 50), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, cardChangelog.Width - 1, cardChangelog.Height - 1);
                }
            };

            var lblCardHeader = new Label
            {
                Text = "WHAT'S NEW IN THIS UPDATE",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 130, 135),
                Location = new Point(12, 8),
                AutoSize = true,
                UseMnemonic = false
            };

            var txtChangelog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(20, 20, 22),
                ForeColor = Color.FromArgb(220, 220, 225),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                Location = new Point(12, 28),
                Size = new Size(388, 150),
                Text = CleanChangelogText(_updateInfo.Changelog)
            };

            cardChangelog.Controls.Add(lblCardHeader);
            cardChangelog.Controls.Add(txtChangelog);
            Controls.Add(cardChangelog);

            // Silent Update Opt-In Option
            var pnlOption = new Panel
            {
                Location = new Point(16, 284),
                Size = new Size(412, 34),
                BackColor = Color.Transparent
            };

            _toggleSilent = new FluentToggleSwitch
            {
                Location = new Point(0, 5),
                Checked = _configService.Config.EnableSilentAutoUpdate
            };
            _toggleSilent.CheckedChanged += (s, e) =>
            {
                _configService.Config.EnableSilentAutoUpdate = _toggleSilent.Checked;
                _configService.Save(_configService.Config);
            };

            var lblSilentDesc = new Label
            {
                Text = "Automatically download and install future updates silently",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 205),
                Location = new Point(48, 8),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            lblSilentDesc.Click += (s, e) => _toggleSilent.Checked = !_toggleSilent.Checked;

            pnlOption.Controls.Add(_toggleSilent);
            pnlOption.Controls.Add(lblSilentDesc);
            Controls.Add(pnlOption);

            // Progress bar panel (hidden initially)
            _progressPanel = new Panel
            {
                Location = new Point(16, 322),
                Size = new Size(412, 26),
                BackColor = Color.Transparent,
                Visible = false
            };

            _lblProgressStatus = new Label
            {
                Text = "Downloading update...",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(76, 194, 255),
                Location = new Point(0, 0),
                Size = new Size(412, 16),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var pnlBarTrack = new Panel
            {
                Location = new Point(0, 18),
                Size = new Size(412, 6),
                BackColor = Color.FromArgb(40, 40, 45)
            };
            pnlBarTrack.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int pw = pnlBarTrack.Width - 1;
                int ph = pnlBarTrack.Height - 1;

                if (_progressPercent > 0)
                {
                    int fillW = Math.Max(4, (int)(pw * Math.Min(1.0f, _progressPercent / 100.0f)));
                    using (var fillBrush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                    {
                        g.FillRectangle(fillBrush, 0, 0, fillW, ph);
                    }
                }
            };

            _progressPanel.Controls.Add(_lblProgressStatus);
            _progressPanel.Controls.Add(pnlBarTrack);
            Controls.Add(_progressPanel);

            // Footer action bar
            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = Color.FromArgb(24, 24, 28)
            };
            footerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(45, 45, 50), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
                }
            };

            _btnLater = new Button
            {
                Text = "Remind Later",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 205),
                BackColor = Color.FromArgb(38, 38, 42),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(115, 34),
                Location = new Point(195, 10),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _btnLater.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 60);
            _btnLater.Click += (s, e) => Close();

            _btnUpdate = new Button
            {
                Text = "⚡ Download & Install",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 215),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(140, 34),
                Location = new Point(318, 10),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            _btnUpdate.FlatAppearance.BorderSize = 0;
            _btnUpdate.Click += OnStartUpdateClick;

            footerPanel.Controls.Add(_btnLater);
            footerPanel.Controls.Add(_btnUpdate);
            Controls.Add(footerPanel);
        }

        private void OnStartUpdateClick(object sender, EventArgs e)
        {
            _btnUpdate.Enabled = false;
            _btnLater.Enabled = false;
            _progressPanel.Visible = true;
            _lblProgressStatus.Text = "Connecting to GitHub...";

            _updateService.DownloadUpdateAsync(_updateInfo,
                (percent, received, total) =>
                {
                    _progressPercent = percent;
                    if (total > 0)
                    {
                        double recvMb = received / 1048576.0;
                        double totMb = total / 1048576.0;
                        _lblProgressStatus.Text = string.Format("Downloading: {0}% ({1:F2} MB / {2:F2} MB)", percent, recvMb, totMb);
                    }
                    else
                    {
                        _lblProgressStatus.Text = string.Format("Downloading: {0}%", percent);
                    }
                    _progressPanel.Controls[1].Invalidate();
                },
                (readyExePath, err) =>
                {
                    if (err != null || string.IsNullOrEmpty(readyExePath))
                    {
                        _lblProgressStatus.Text = "Update failed: " + (err != null ? err.Message : "Unknown error");
                        _lblProgressStatus.ForeColor = Color.FromArgb(255, 80, 80);
                        _btnLater.Text = "Close";
                        _btnLater.Enabled = true;
                        return;
                    }

                    _lblProgressStatus.Text = "Applying update & restarting...";
                    _lblProgressStatus.ForeColor = Color.FromArgb(108, 203, 95);
                    _progressPercent = 100;
                    _progressPanel.Controls[1].Invalidate();

                    var restartTimer = new Timer();
                    restartTimer.Interval = 400;
                    restartTimer.Tick += (ts, te) =>
                    {
                        restartTimer.Stop();
                        restartTimer.Dispose();
                        UpdateService.ApplyUpdateAndRestart(readyExePath);
                    };
                    restartTimer.Start();
                });
        }

        private static string CleanChangelogText(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return "• Performance enhancements and bug fixes.\n• Windows 11 Fluent UI optimizations.";
            }

            // Normalize line breaks
            string text = raw.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = text.Split('\n');
            var cleanLines = new System.Collections.Generic.List<string>();

            foreach (var l in lines)
            {
                string trim = l.Trim();
                if (trim.StartsWith("#"))
                {
                    cleanLines.Add(trim.TrimStart('#').Trim().ToUpperInvariant());
                }
                else if (trim.StartsWith("---"))
                {
                    continue;
                }
                else if (!string.IsNullOrEmpty(trim))
                {
                    cleanLines.Add(trim);
                }
            }

            return string.Join("\r\n", cleanLines.ToArray());
        }
    }
}

