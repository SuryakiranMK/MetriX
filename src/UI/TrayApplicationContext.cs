using System;
using System.Drawing;
using System.Windows.Forms;
using MetriX.Model;
using MetriX.Services;

namespace MetriX.UI
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly ConfigService _configService;
        private readonly NetworkMonitor _networkMonitor;
        private readonly TaskbarTracker _taskbarTracker;

        private NotifyIcon _notifyIcon;
        private OverlayWindow _overlayWindow;
        private FlyoutWindow _flyoutWindow;
        private Timer _sampleTimer;
        private SettingsForm _settingsForm;

        public TrayApplicationContext(ConfigService configService, NetworkMonitor networkMonitor, TaskbarTracker taskbarTracker)
        {
            _configService = configService;
            _networkMonitor = networkMonitor;
            _taskbarTracker = taskbarTracker;

            InitializeTray();
            InitializeOverlay();
            InitializeTimer();
        }

        private void InitializeTray()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = GetAppIcon(),
                Text = "metriX - Taskbar Speed Indicator",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            menu.Renderer = new ToolStripProfessionalRenderer(new Windows11ColorTable());

            var itemSettings = new ToolStripMenuItem("⚙  Settings & Customization...", null, (s, e) => ShowSettings());
            itemSettings.Font = new Font(itemSettings.Font, FontStyle.Bold);

            var itemExit = new ToolStripMenuItem("❌  Exit", null, (s, e) => ExitThread());

            menu.Items.Add(itemSettings);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemExit);

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, e) => ShowSettings();
        }

        private Icon GetAppIcon()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                Icon assocIcon = Icon.ExtractAssociatedIcon(exePath);
                if (assocIcon != null) return assocIcon;
            }
            catch { }

            try
            {
                if (System.IO.File.Exists("app.ico"))
                {
                    return new Icon("app.ico");
                }
            }
            catch { }

            return GenerateTrayIcon();
        }

        private Icon GenerateTrayIcon()
        {
            // Dynamically generate a clean modern 32x32 icon with up/down arrows
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Rounded background square
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(30, 30, 30)))
                {
                    g.FillEllipse(bgBrush, 1, 1, 30, 30);
                }

                // Up arrow (Cyan)
                using (Brush upBrush = new SolidBrush(Color.FromArgb(76, 194, 255)))
                {
                    PointF[] upTriangle = new PointF[] {
                        new PointF(10, 15),
                        new PointF(16, 6),
                        new PointF(22, 15)
                    };
                    g.FillPolygon(upBrush, upTriangle);
                }

                // Down arrow (Green)
                using (Brush dnBrush = new SolidBrush(Color.FromArgb(108, 203, 95)))
                {
                    PointF[] dnTriangle = new PointF[] {
                        new PointF(10, 18),
                        new PointF(22, 18),
                        new PointF(16, 27)
                    };
                    g.FillPolygon(dnBrush, dnTriangle);
                }

                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private void InitializeOverlay()
        {
            _flyoutWindow = new FlyoutWindow(_configService, _networkMonitor);
            _flyoutWindow.RequestOpenSettings += ShowSettings;

            _overlayWindow = new OverlayWindow(_configService, _networkMonitor, _taskbarTracker);
            _overlayWindow.RequestOpenSettings += ShowSettings;
            _overlayWindow.RequestToggleFlyout += () =>
            {
                if (_flyoutWindow != null && !_flyoutWindow.IsDisposed)
                {
                    _flyoutWindow.Toggle(_overlayWindow.Bounds);
                }
            };
            _overlayWindow.RequestExit += ExitThread;
            _overlayWindow.Show();
        }

        private void InitializeTimer()
        {
            _sampleTimer = new Timer();
            _sampleTimer.Interval = _configService.Config.UpdateIntervalMs;
            _sampleTimer.Tick += OnSampleTick;
            _sampleTimer.Start();

            // Run initial sample
            OnSampleTick(null, EventArgs.Empty);
        }

        private void OnSampleTick(object sender, EventArgs e)
        {
            try
            {
                var stats = _networkMonitor.Sample(_configService.Config);
                _overlayWindow.RenderFrame();

                if (_flyoutWindow != null && _flyoutWindow.Visible)
                {
                    _flyoutWindow.UpdateData();
                }

                if (_notifyIcon != null)
                {
                    string tip = string.Format("metriX: ▲ {0} {1} | ▼ {2} {3}",
                        stats.UploadFormatted, stats.UploadUnit,
                        stats.DownloadFormatted, stats.DownloadUnit);
                    if (tip.Length >= 64) tip = tip.Substring(0, 63);
                    _notifyIcon.Text = tip;
                }
            }
            catch
            {
            }
        }

        public void ShowSettings()
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(_configService, _networkMonitor, () =>
                {
                    _sampleTimer.Interval = _configService.Config.UpdateIntervalMs;
                    _networkMonitor.ResetBaseline();
                    _overlayWindow.RenderFrame();

                if (_flyoutWindow != null && _flyoutWindow.Visible)
                {
                    _flyoutWindow.UpdateData();
                }
                });
                _settingsForm.Show();
            }
            else
            {
                _settingsForm.BringToFront();
                _settingsForm.Activate();
            }
        }

        protected override void ExitThreadCore()
        {
            if (_sampleTimer != null)
            {
                _sampleTimer.Stop();
                _sampleTimer.Dispose();
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            if (_flyoutWindow != null && !_flyoutWindow.IsDisposed)
            {
                _flyoutWindow.Close();
                _flyoutWindow.Dispose();
            }

            if (_overlayWindow != null && !_overlayWindow.IsDisposed)
            {
                _overlayWindow.Close();
                _overlayWindow.Dispose();
            }

            base.ExitThreadCore();
        }
    }
}
