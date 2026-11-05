using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Threading;
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
        private Panel _pnlBarTrack;
        private int _progressPercent = 0;
        private Button _btnUpdate;
        private Button _btnLater;
        private FluentToggleSwitch _toggleSilent;
        private FluentChangelogView _changelogView;

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

        private static Bitmap GetProductIconBitmap()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                if (File.Exists(exePath))
                {
                    using (Icon ico = Icon.ExtractAssociatedIcon(exePath))
                    {
                        if (ico != null) return new Bitmap(ico.ToBitmap(), 36, 36);
                    }
                }

                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath))
                {
                    using (Icon ico = new Icon(icoPath, 36, 36))
                    {
                        return new Bitmap(ico.ToBitmap(), 36, 36);
                    }
                }
            }
            catch { }

            // Clean vector fallback if icon file is not found
            Bitmap bmp = new Bitmap(36, 36);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                {
                    g.FillEllipse(brush, 2, 2, 32, 32);
                }
                using (var pen = new Pen(Color.White, 2.5f))
                {
                    g.DrawLine(pen, 10, 24, 18, 10);
                    g.DrawLine(pen, 18, 10, 26, 24);
                }
            }
            return bmp;
        }

        private void InitializeUI()
        {
            Text = "metriX Update Available";
            Size = new Size(560, 585);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(24, 24, 28);
            ForeColor = Color.White;
            Font = new Font("Segoe UI Variable Text", 9.0f, FontStyle.Regular);

            // 1. Header Banner
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = Color.FromArgb(32, 32, 36)
            };
            headerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(48, 48, 54), 1))
                {
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
                }
            };

            var picIcon = new PictureBox
            {
                Image = GetProductIconBitmap(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Location = new Point(20, 18),
                Size = new Size(36, 36),
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = string.Format("metriX {0} is available!", _updateInfo.VersionTag),
                Font = new Font("Segoe UI", 12.0f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(68, 16),
                AutoSize = true,
                UseMnemonic = false
            };

            string dateStr = _updateInfo.PublishedAt != DateTime.MinValue
                ? _updateInfo.PublishedAt.ToLocalTime().ToString("MMM dd, yyyy")
                : "Latest Release";

            var lblSubtitle = new Label
            {
                Text = string.Format("Current version: v{0}  •  Released {1}", _updateInfo.CurrentVersion, dateStr),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(170, 170, 175),
                Location = new Point(68, 42),
                AutoSize = true,
                UseMnemonic = false
            };

            headerPanel.Controls.Add(picIcon);
            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            Controls.Add(headerPanel);

            int contentW = 504;

            // 2. Changelog / Release Notes Section
            var lblCardHeader = new Label
            {
                Text = "WHAT'S NEW IN THIS UPDATE",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(135, 135, 142),
                Location = new Point(20, 86),
                AutoSize = true,
                UseMnemonic = false
            };
            Controls.Add(lblCardHeader);

            // Large changelog view dominating majority of window height
            _changelogView = new FluentChangelogView
            {
                Location = new Point(20, 106),
                Size = new Size(contentW, 280)
            };
            _changelogView.SetChangelog(_updateInfo.Changelog);
            Controls.Add(_changelogView);

            // 3. Silent Update Opt-In Option (directly below update notes)
            var pnlOption = new Panel
            {
                Location = new Point(20, 396),
                Size = new Size(contentW, 30),
                BackColor = Color.Transparent
            };

            _toggleSilent = new FluentToggleSwitch
            {
                Location = new Point(0, 3),
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
                Location = new Point(48, 5),
                AutoSize = true,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            lblSilentDesc.Click += (s, e) => _toggleSilent.Checked = !_toggleSilent.Checked;

            pnlOption.Controls.Add(_toggleSilent);
            pnlOption.Controls.Add(lblSilentDesc);
            Controls.Add(pnlOption);

            // 4. Progress bar panel (hidden until update starts)
            _progressPanel = new Panel
            {
                Location = new Point(20, 434),
                Size = new Size(contentW, 40),
                BackColor = Color.Transparent,
                Visible = false
            };

            _lblProgressStatus = new Label
            {
                Text = "Downloading update...",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(76, 194, 255),
                Location = new Point(0, 0),
                Size = new Size(contentW, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };

            _pnlBarTrack = new Panel
            {
                Location = new Point(0, 22),
                Size = new Size(contentW, 8),
                BackColor = Color.Transparent
            };
            _pnlBarTrack.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int pw = _pnlBarTrack.Width - 1;
                int ph = _pnlBarTrack.Height - 1;

                using (var trackPath = GetRoundedRect(0, 0, pw, ph, 4))
                using (var trackBrush = new SolidBrush(Color.FromArgb(40, 40, 46)))
                {
                    g.FillPath(trackBrush, trackPath);
                }

                if (_progressPercent > 0)
                {
                    int fillW = Math.Max(8, (int)(pw * Math.Min(1.0f, _progressPercent / 100.0f)));
                    using (var fillPath = GetRoundedRect(0, 0, fillW, ph, 4))
                    using (var fillBrush = new SolidBrush(_progressPercent >= 100 ? Color.FromArgb(108, 203, 95) : Color.FromArgb(0, 120, 215)))
                    {
                        g.FillPath(fillBrush, fillPath);
                    }
                }
            };

            _progressPanel.Controls.Add(_lblProgressStatus);
            _progressPanel.Controls.Add(_pnlBarTrack);
            Controls.Add(_progressPanel);

            // 5. Footer action bar (Buttons on very right bottom)
            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(20, 20, 24)
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
                BackColor = Color.FromArgb(36, 36, 40),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(115, 36),
                Location = new Point(contentW + 20 - 305, 12),
                Cursor = Cursors.Hand,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = Padding.Empty
            };
            _btnLater.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 62);
            _btnLater.Click += (s, e) => Close();

            _btnUpdate = new Button
            {
                Text = "⚡ Download & Install",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 215),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(180, 36),
                Location = new Point(contentW + 20 - 180, 12),
                Cursor = Cursors.Hand,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = Padding.Empty
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
            _progressPercent = 5;
            _lblProgressStatus.Text = "Connecting to GitHub...";
            _lblProgressStatus.ForeColor = Color.FromArgb(76, 194, 255);
            _pnlBarTrack.Invalidate();

            _updateService.DownloadUpdateAsync(_updateInfo,
                (percent, received, total) =>
                {
                    _progressPercent = Math.Max(5, percent);
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
                    _pnlBarTrack.Invalidate();
                },
                (readyExePath, err) =>
                {
                    if (err != null || string.IsNullOrEmpty(readyExePath) || !File.Exists(readyExePath))
                    {
                        _lblProgressStatus.Text = "Update failed: " + (err != null ? err.Message : "Unknown error");
                        _lblProgressStatus.ForeColor = Color.FromArgb(255, 80, 80);
                        _btnLater.Text = "Close";
                        _btnLater.Enabled = true;
                        return;
                    }

                    // Direct synchronous update & restart (Zero unrooted timers, guaranteed transition)
                    _lblProgressStatus.Text = "Applying update & restarting...";
                    _lblProgressStatus.ForeColor = Color.FromArgb(108, 203, 95);
                    _progressPercent = 100;
                    _pnlBarTrack.Invalidate();
                    Update();
                    Application.DoEvents();
                    Thread.Sleep(350);

                    Hide();
                    UpdateService.ApplyUpdateAndRestart(readyExePath);
                });
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

    /// <summary>
    /// Sleek Windows 11 Fluent Changelog View for UpdateDialog.
    /// Completely replaces classic Win32 white TextBox with anti-aliased dark rendering and Fluent dark scrollbar.
    /// </summary>
    public class FluentChangelogView : Panel
    {
        private class ChangelogBlock
        {
            public string Text;
            public Font BlockFont;
            public Color TextColor;
            public int IndentX;
            public int Height;
        }

        private readonly List<ChangelogBlock> _blocks = new List<ChangelogBlock>();
        private readonly FluentScrollBar _scrollBar;
        private int _totalContentHeight = 0;

        public FluentChangelogView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(18, 18, 22);
            AutoScroll = false;

            _scrollBar = new FluentScrollBar
            {
                Size = new Size(8, Height - 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
                ViewportSize = Height,
                Visible = false
            };
            _scrollBar.ValueChanged += (s, e) => Invalidate();
            Controls.Add(_scrollBar);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _scrollBar.Location = new Point(Width - 14, 8);
            _scrollBar.Size = new Size(8, Math.Max(10, Height - 16));
            _scrollBar.ViewportSize = Height;
            RecomputeLayout();
            Invalidate();
        }

        public void SetChangelog(string rawMarkdown)
        {
            _blocks.Clear();
            if (string.IsNullOrEmpty(rawMarkdown))
            {
                rawMarkdown = "• Performance optimizations and stability improvements.\n• Windows 11 Fluent UI visual refinements.";
            }

            string text = rawMarkdown.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] rawLines = text.Split('\n');

            foreach (string rawLine in rawLines)
            {
                string trimmed = rawLine.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("---"))
                {
                    continue;
                }

                ChangelogBlock block = new ChangelogBlock();

                if (trimmed.StartsWith("# "))
                {
                    block.Text = trimmed.TrimStart('#', ' ');
                    block.BlockFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                    block.TextColor = Color.FromArgb(76, 194, 255);
                    block.IndentX = 14;
                }
                else if (trimmed.StartsWith("## "))
                {
                    block.Text = trimmed.TrimStart('#', ' ');
                    block.BlockFont = new Font("Segoe UI", 9.0f, FontStyle.Bold);
                    block.TextColor = Color.White;
                    block.IndentX = 14;
                }
                else if (trimmed.StartsWith("### "))
                {
                    block.Text = trimmed.TrimStart('#', ' ');
                    block.BlockFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    block.TextColor = Color.FromArgb(170, 210, 255);
                    block.IndentX = 18;
                }
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("• "))
                {
                    block.Text = "•  " + trimmed.Substring(2).Trim();
                    block.BlockFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                    block.TextColor = Color.FromArgb(220, 220, 225);
                    block.IndentX = 22;
                }
                else if (rawLine.StartsWith("   ") || rawLine.StartsWith("\t"))
                {
                    string subTrim = trimmed.TrimStart('-', '*', '•', ' ');
                    block.Text = "–  " + subTrim;
                    block.BlockFont = new Font("Segoe UI", 8.25f, FontStyle.Regular);
                    block.TextColor = Color.FromArgb(170, 170, 178);
                    block.IndentX = 36;
                }
                else
                {
                    block.Text = trimmed;
                    block.BlockFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                    block.TextColor = Color.FromArgb(210, 210, 215);
                    block.IndentX = 14;
                }

                _blocks.Add(block);
            }

            RecomputeLayout();
            Invalidate();
        }

        private void RecomputeLayout()
        {
            int availW = Math.Max(100, Width - 36);
            _totalContentHeight = 12;

            foreach (var block in _blocks)
            {
                int maxTextW = availW - block.IndentX;
                Size measured = TextRenderer.MeasureText(block.Text, block.BlockFont, new Size(maxTextW, 0),
                    TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                block.Height = Math.Max(18, measured.Height + 5);
                _totalContentHeight += block.Height;
            }

            _totalContentHeight += 16;
            _scrollBar.Maximum = _totalContentHeight;
            _scrollBar.ViewportSize = Height;
            _scrollBar.Visible = _totalContentHeight > Height;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Draw rounded card border
            int w = Width - 1;
            int h = Height - 1;
            using (var path = GetRoundedRect(0, 0, w, h, 8))
            using (var pen = new Pen(Color.FromArgb(46, 46, 52), 1))
            {
                g.DrawPath(pen, path);
            }

            // Draw text blocks
            int clipW = Width - 24;
            int scrollY = _scrollBar.Visible ? _scrollBar.Value : 0;
            int curY = 12 - scrollY;

            foreach (var block in _blocks)
            {
                if (curY + block.Height >= 0 && curY <= Height)
                {
                    Rectangle textRect = new Rectangle(block.IndentX, curY, clipW - block.IndentX, block.Height);
                    TextRenderer.DrawText(g, block.Text, block.BlockFont, textRect, block.TextColor,
                        TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
                }
                curY += block.Height;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_scrollBar.Visible)
            {
                int step = (e.Delta / 120) * 44;
                _scrollBar.Value -= step;
                Invalidate();
            }
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
