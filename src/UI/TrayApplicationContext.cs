using System;
using System.Collections.Generic;
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
        private readonly List<OverlayWindow> _secondaryOverlays = new List<OverlayWindow>();
        private FlyoutWindow _flyoutWindow;
        private Timer _sampleTimer;
        private SettingsForm _settingsForm;
        private readonly UpdateService _updateService;
        private int _trimTickCounter = 0;

        public TrayApplicationContext(ConfigService configService, NetworkMonitor networkMonitor, TaskbarTracker taskbarTracker)
        {
            _configService = configService;
            _networkMonitor = networkMonitor;
            _taskbarTracker = taskbarTracker;

            _updateService = new UpdateService(_configService, _networkMonitor);
            _updateService.UpdateAvailable += OnUpdateAvailable;

            InitializeTray();
            InitializeOverlay();
            InitializeTimer();

            // Perform post-startup working set optimization
            TrimProcessMemory();
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
            menu.Renderer = new Windows11MenuRenderer();

            var itemFlyout = new ToolStripMenuItem("📈  Network Graph & Speed Test...", null, (s, e) =>
            {
                if (_flyoutWindow != null && !_flyoutWindow.IsDisposed)
                {
                    _flyoutWindow.Toggle(_overlayWindow.Bounds);
                }
            });
            itemFlyout.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

            var itemSettings = new ToolStripMenuItem("⚙  Settings & Customization...", null, (s, e) => ShowSettings());

            var itemCheckUpdate = new ToolStripMenuItem("🔄  Check for Updates...", null, (s, e) => TriggerManualUpdateCheck());

            var itemDisplayOnly = new ToolStripMenuItem("👁️  Display Only Mode", null, (s, e) =>
            {
                if (_overlayWindow != null && !_overlayWindow.IsDisposed)
                {
                    _overlayWindow.ToggleDisplayOnlyMode();
                }
            });

            var itemExit = new ToolStripMenuItem("❌  Exit metriX", null, (s, e) => ExitThread());

            menu.Items.Add(itemFlyout);
            menu.Items.Add(itemSettings);
            menu.Items.Add(itemCheckUpdate);
            menu.Items.Add(itemDisplayOnly);
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

        private void SynchronizeSecondaryOverlays()
        {
            try
            {
                if (!_configService.Config.ShowOnAllMonitors)
                {
                    if (_secondaryOverlays.Count > 0)
                    {
                        foreach (var sec in _secondaryOverlays)
                        {
                            if (sec != null && !sec.IsDisposed)
                            {
                                sec.Close();
                                sec.Dispose();
                            }
                        }
                        _secondaryOverlays.Clear();
                    }
                    return;
                }

                var currentSecTaskbars = _taskbarTracker.SecondaryTaskbars;

                // Clean up disposed overlays or overlays whose taskbars no longer exist
                for (int i = _secondaryOverlays.Count - 1; i >= 0; i--)
                {
                    var ov = _secondaryOverlays[i];
                    if (ov.IsDisposed || !currentSecTaskbars.Contains(ov.SecondaryTaskbarHandle))
                    {
                        if (!ov.IsDisposed)
                        {
                            ov.Close();
                            ov.Dispose();
                        }
                        _secondaryOverlays.RemoveAt(i);
                    }
                }

                // Create overlay for any newly discovered secondary taskbars
                foreach (IntPtr hSec in currentSecTaskbars)
                {
                    bool exists = false;
                    foreach (var ov in _secondaryOverlays)
                    {
                        if (ov.SecondaryTaskbarHandle == hSec)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists && hSec != IntPtr.Zero)
                    {
                        var secOverlay = new OverlayWindow(_configService, _networkMonitor, _taskbarTracker, hSec);
                        secOverlay.RequestOpenSettings += ShowSettings;
                        secOverlay.RequestToggleFlyout += () =>
                        {
                            if (_flyoutWindow != null && !_flyoutWindow.IsDisposed)
                            {
                                _flyoutWindow.Toggle(secOverlay.Bounds);
                            }
                        };
                        secOverlay.RequestExit += ExitThread;
                        secOverlay.RequestCheckUpdates += TriggerManualUpdateCheck;
                        secOverlay.Show();
                        _secondaryOverlays.Add(secOverlay);
                    }
                }
            }
            catch
            {
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
            _overlayWindow.RequestCheckUpdates += TriggerManualUpdateCheck;
            _overlayWindow.RequestExit += ExitThread;
            _overlayWindow.DisplayOnlyModeToggled += (isDisplayOnly) =>
            {
                foreach (var sec in _secondaryOverlays)
                {
                    if (sec != null && !sec.IsDisposed)
                    {
                        sec.ApplyDisplayOnlyMode();
                    }
                }

                if (_notifyIcon != null)
                {
                    string hk = _configService.Config.DisplayOnlyHotkey ?? "Ctrl + `";
                    if (isDisplayOnly)
                    {
                        _notifyIcon.ShowBalloonTip(3500, "metriX Display-Only Mode", "metriX is now in click-through Display-Only Mode.\nPress " + hk + " to unlock interactivity.", ToolTipIcon.Info);
                    }
                    else
                    {
                        _notifyIcon.ShowBalloonTip(2500, "metriX Interactive Mode", "metriX is now interactive and clickable.", ToolTipIcon.Info);
                    }
                }
            };
            _overlayWindow.Show();

            SynchronizeSecondaryOverlays();
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
                _taskbarTracker.RefreshHandlesIfNeeded();
                SynchronizeSecondaryOverlays();

                var stats = _networkMonitor.Sample(_configService.Config);
                _overlayWindow.RenderFrame();

                foreach (var sec in _secondaryOverlays)
                {
                    if (sec != null && !sec.IsDisposed && sec.Visible)
                    {
                        sec.RenderFrame();
                    }
                }

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
                    if (_notifyIcon.Text != tip)
                    {
                        _notifyIcon.Text = tip;
                    }
                }

                // Periodic CLR working set optimization (locks physical footprint under 6–8 MB)
                _trimTickCounter++;
                if (_trimTickCounter >= 30)
                {
                    _trimTickCounter = 0;
                    TrimProcessMemory();
                }
            }
            catch
            {
            }
        }

        private static void TrimProcessMemory()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced);

                IntPtr hProc = System.Diagnostics.Process.GetCurrentProcess().Handle;
                Native.NativeMethods.EmptyWorkingSet(hProc);
                Native.NativeMethods.SetProcessWorkingSetSize(hProc, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch
            {
            }
        }

        public void ShowSettings()
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(_configService, _networkMonitor, _updateService, () =>
                {
                    _sampleTimer.Interval = _configService.Config.UpdateIntervalMs;
                    _networkMonitor.ResetBaseline();
                    _overlayWindow.RegisterDisplayOnlyHotkey();
                    _overlayWindow.ApplyDisplayOnlyMode();
                    _overlayWindow.InvalidateDimensions();
                    SynchronizeSecondaryOverlays();
                    _overlayWindow.RenderFrame();

                    foreach (var sec in _secondaryOverlays)
                    {
                        if (sec != null && !sec.IsDisposed && sec.Visible)
                        {
                            sec.ApplyDisplayOnlyMode();
                            sec.InvalidateDimensions();
                            sec.RenderFrame();
                        }
                    }

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

        private void OnUpdateAvailable(UpdateInfo info)
        {
            if (info == null || !info.IsUpdateAvailable) return;

            try
            {
                if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.Visible)
                {
                    var dlg = new UpdateDialog(info, _updateService, _configService);
                    dlg.ShowDialog(_settingsForm);
                }
                else
                {
                    var dlg = new UpdateDialog(info, _updateService, _configService);
                    dlg.ShowDialog();
                }
            }
            catch
            {
            }
        }

        public void TriggerManualUpdateCheck()
        {
            if (_updateService.IsChecking) return;

            _updateService.CheckForUpdatesAsync(info =>
            {
                if (info != null && info.IsUpdateAvailable)
                {
                    OnUpdateAvailable(info);
                }
                else if (info != null && !string.IsNullOrEmpty(info.ErrorMessage))
                {
                    MessageBox.Show("Could not check for updates:\n" + info.ErrorMessage, "metriX Update",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show("You are running the latest version of metriX (v" + UpdateService.GetCurrentAssemblyVersion() + ").",
                        "metriX Up to Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });
        }

        protected override void ExitThreadCore()
        {
            if (_updateService != null)
            {
                _updateService.Dispose();
            }

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

            foreach (var sec in _secondaryOverlays)
            {
                if (sec != null && !sec.IsDisposed)
                {
                    sec.Close();
                    sec.Dispose();
                }
            }
            _secondaryOverlays.Clear();

            if (_overlayWindow != null && !_overlayWindow.IsDisposed)
            {
                _overlayWindow.Close();
                _overlayWindow.Dispose();
            }

            base.ExitThreadCore();
        }
    }
}
