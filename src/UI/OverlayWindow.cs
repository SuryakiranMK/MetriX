using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MetriX.Model;
using MetriX.Native;
using MetriX.Services;

namespace MetriX.UI
{
    public class OverlayWindow : Form
    {
        private readonly ConfigService _configService;
        private readonly NetworkMonitor _networkMonitor;
        private readonly TaskbarTracker _taskbarTracker;
        private readonly IntPtr _secondaryTaskbarHandle;

        private uint _msgTaskbarCreated;
        private bool _isHovered;
        private bool _isDragging;
        private Point _dragStartPoint;
        private Point _initialWindowLocation;
        private int _initialOffsetX;
        private ContextMenuStrip _contextMenu;
        private NativeMethods.HookProc _mouseHookProc;
        private IntPtr _hMouseHook = IntPtr.Zero;
        private Rectangle _lastRenderedBounds = Rectangle.Empty;

        public event Action RequestOpenSettings;
        public event Action RequestToggleFlyout;
        public event Action RequestCheckUpdates;
        public event Action RequestExit;

        public IntPtr SecondaryTaskbarHandle { get { return _secondaryTaskbarHandle; } }

        public OverlayWindow(ConfigService configService, NetworkMonitor networkMonitor, TaskbarTracker taskbarTracker)
            : this(configService, networkMonitor, taskbarTracker, IntPtr.Zero)
        {
        }

        public OverlayWindow(ConfigService configService, NetworkMonitor networkMonitor, TaskbarTracker taskbarTracker, IntPtr secondaryTaskbar)
        {
            _configService = configService;
            _networkMonitor = networkMonitor;
            _taskbarTracker = taskbarTracker;
            _secondaryTaskbarHandle = secondaryTaskbar;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;

            // Transparent and double-buffered
            BackColor = Color.Black;

            _msgTaskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");

            BuildContextMenu();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style = unchecked((int)NativeMethods.WS_POPUP);
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
                cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE;
                cp.ExStyle |= NativeMethods.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            EnsureTaskbarOwnership();
        }

        public void EnsureTaskbarOwnership()
        {
            try
            {
                if (!IsHandleCreated) return;
                var cfg = _configService.Config;
                if (cfg.IsFloatingMode)
                {
                    IntPtr currentOwner = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GWLP_HWNDPARENT);
                    if (currentOwner != IntPtr.Zero)
                    {
                        NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GWLP_HWNDPARENT, IntPtr.Zero);
                    }
                    return;
                }

                IntPtr hTaskbar = _secondaryTaskbarHandle != IntPtr.Zero
                    ? _secondaryTaskbarHandle
                    : (_taskbarTracker != null ? _taskbarTracker.TaskbarHandle : IntPtr.Zero);

                if (hTaskbar != IntPtr.Zero)
                {
                    IntPtr currentOwner = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GWLP_HWNDPARENT);
                    if (currentOwner != hTaskbar)
                    {
                        NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GWLP_HWNDPARENT, hTaskbar);
                    }
                }
            }
            catch { }
        }

        private void BuildContextMenu()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Renderer = new Windows11MenuRenderer();

            var itemFlyout = new ToolStripMenuItem("📈  Network Graph & Speed Test...", null, (s, e) =>
            {
                if (RequestToggleFlyout != null) RequestToggleFlyout();
            });
            itemFlyout.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);

            var itemSettings = new ToolStripMenuItem("⚙  Settings & Customization...", null, (s, e) =>
            {
                if (RequestOpenSettings != null) RequestOpenSettings();
            });

            var itemUpdates = new ToolStripMenuItem("🔄  Check for Updates...", null, (s, e) =>
            {
                if (RequestCheckUpdates != null) RequestCheckUpdates();
            });

            var itemFloating = new ToolStripMenuItem("📌  Detach to Floating Widget", null, (s, e) =>
            {
                var cfg = _configService.Config;
                cfg.IsFloatingMode = !cfg.IsFloatingMode;
                _configService.Save(cfg);
                EnsureTaskbarOwnership();
                RenderFrame();
            });

            // Floating Customization Submenus
            var itemFloatingSize = new ToolStripMenuItem("📐  Widget Size");
            itemFloatingSize.DropDown.Renderer = new Windows11MenuRenderer();
            var itemSizeCompact = new ToolStripMenuItem("Compact (80%)", null, (s, e) => SetFloatingScale(0.8));
            var itemSizeNormal = new ToolStripMenuItem("Standard (100%)", null, (s, e) => SetFloatingScale(1.0));
            var itemSizeLarge = new ToolStripMenuItem("Large (125%)", null, (s, e) => SetFloatingScale(1.25));
            var itemSizeXLarge = new ToolStripMenuItem("Extra Large (150%)", null, (s, e) => SetFloatingScale(1.5));
            itemFloatingSize.DropDownItems.AddRange(new ToolStripItem[] { itemSizeCompact, itemSizeNormal, itemSizeLarge, itemSizeXLarge });

            var itemFloatingLock = new ToolStripMenuItem("🔒  Lock Position", null, (s, e) =>
            {
                var cfg = _configService.Config;
                cfg.FloatingLocked = !cfg.FloatingLocked;
                _configService.Save(cfg);
            });

            var itemFloatingSnap = new ToolStripMenuItem("📍  Snap to Corner");
            itemFloatingSnap.DropDown.Renderer = new Windows11MenuRenderer();
            var itemSnapBR = new ToolStripMenuItem("Bottom-Right", null, (s, e) => SnapFloatingToCorner(2));
            var itemSnapTR = new ToolStripMenuItem("Top-Right", null, (s, e) => SnapFloatingToCorner(1));
            var itemSnapBL = new ToolStripMenuItem("Bottom-Left", null, (s, e) => SnapFloatingToCorner(4));
            var itemSnapTL = new ToolStripMenuItem("Top-Left", null, (s, e) => SnapFloatingToCorner(3));
            itemFloatingSnap.DropDownItems.AddRange(new ToolStripItem[] { itemSnapBR, itemSnapTR, itemSnapBL, itemSnapTL });

            var itemLayout = new ToolStripMenuItem("🔄  Display Layout");
            itemLayout.DropDown.Renderer = new Windows11MenuRenderer();
            var itemStacked = new ToolStripMenuItem("Stacked (2 Rows)", null, (s, e) => ToggleLayout(LayoutMode.Stacked));
            var itemHorizontal = new ToolStripMenuItem("Horizontal (Single Row)", null, (s, e) => ToggleLayout(LayoutMode.Horizontal));
            itemLayout.DropDownItems.Add(itemStacked);
            itemLayout.DropDownItems.Add(itemHorizontal);

            var itemPosition = new ToolStripMenuItem("📍  Taskbar Position");
            itemPosition.DropDown.Renderer = new Windows11MenuRenderer();
            var itemTray = new ToolStripMenuItem("Right (Left of System Tray)", null, (s, e) => SetPosition(PositionMode.LeftOfTray));
            var itemLeft = new ToolStripMenuItem("Left (Near Start / Widgets)", null, (s, e) => SetPosition(PositionMode.FarLeft));
            var itemApps = new ToolStripMenuItem("Center (Right of Open Apps)", null, (s, e) => SetPosition(PositionMode.RightOfApps));
            itemPosition.DropDownItems.AddRange(new ToolStripItem[] { itemTray, itemLeft, itemApps });

            var itemStartup = new ToolStripMenuItem("🚀  Start with Windows", null, (s, e) =>
            {
                _configService.Config.StartWithWindows = !_configService.Config.StartWithWindows;
                _configService.Save(_configService.Config);
            });

            var itemExit = new ToolStripMenuItem("❌  Exit metriX", null, (s, e) =>
            {
                if (RequestExit != null) RequestExit();
            });

            _contextMenu.Opening += (s, e) =>
            {
                var cfg = _configService.Config;
                itemStacked.Checked = (cfg.Layout == LayoutMode.Stacked);
                itemHorizontal.Checked = (cfg.Layout == LayoutMode.Horizontal);
                itemTray.Checked = (cfg.Position == PositionMode.LeftOfTray);
                itemLeft.Checked = (cfg.Position == PositionMode.FarLeft);
                itemApps.Checked = (cfg.Position == PositionMode.RightOfApps);
                itemPosition.Enabled = !cfg.IsFloatingMode;
                itemStartup.Checked = cfg.StartWithWindows;
                itemFloating.Text = cfg.IsFloatingMode ? "📌  Dock back to Taskbar" : "📌  Detach to Floating Widget";

                itemFloatingSize.Visible = cfg.IsFloatingMode;
                itemFloatingLock.Visible = cfg.IsFloatingMode;
                itemFloatingSnap.Visible = cfg.IsFloatingMode;

                if (cfg.IsFloatingMode)
                {
                    itemFloatingLock.Text = cfg.FloatingLocked ? "🔓  Unlock Position" : "🔒  Lock Position";
                    itemSizeCompact.Checked = Math.Abs(cfg.FloatingScale - 0.8) < 0.05;
                    itemSizeNormal.Checked = Math.Abs(cfg.FloatingScale - 1.0) < 0.05;
                    itemSizeLarge.Checked = Math.Abs(cfg.FloatingScale - 1.25) < 0.05;
                    itemSizeXLarge.Checked = Math.Abs(cfg.FloatingScale - 1.5) < 0.05;
                }
            };

            _contextMenu.Opened += OnContextMenuOpened;
            _contextMenu.Closed += OnContextMenuClosed;

            _contextMenu.Items.Add(itemFlyout);
            _contextMenu.Items.Add(itemSettings);
            _contextMenu.Items.Add(itemUpdates);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(itemFloating);
            _contextMenu.Items.Add(itemFloatingSize);
            _contextMenu.Items.Add(itemFloatingLock);
            _contextMenu.Items.Add(itemFloatingSnap);
            _contextMenu.Items.Add(itemLayout);
            _contextMenu.Items.Add(itemPosition);
            _contextMenu.Items.Add(itemStartup);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(itemExit);
        }

        private void SetFloatingScale(double scale)
        {
            var cfg = _configService.Config;
            cfg.FloatingScale = scale;
            _configService.Save(cfg);
            RenderFrame();
        }

        private void SnapFloatingToCorner(int corner)
        {
            var cfg = _configService.Config;
            Screen scr = Screen.FromPoint(Location);
            Rectangle wa = scr.WorkingArea;
            int margin = 20;
            switch (corner)
            {
                case 1: // Top-Right
                    cfg.FloatingX = wa.Right - Width - margin;
                    cfg.FloatingY = wa.Top + margin;
                    break;
                case 2: // Bottom-Right
                    cfg.FloatingX = wa.Right - Width - margin;
                    cfg.FloatingY = wa.Bottom - Height - margin;
                    break;
                case 3: // Top-Left
                    cfg.FloatingX = wa.Left + margin;
                    cfg.FloatingY = wa.Top + margin;
                    break;
                case 4: // Bottom-Left
                    cfg.FloatingX = wa.Left + margin;
                    cfg.FloatingY = wa.Bottom - Height - margin;
                    break;
            }
            _configService.Save(cfg);
            RenderFrame();
        }

        private void OnContextMenuOpened(object sender, EventArgs e)
        {
            StartOutsideClickWatcher();
        }

        private void OnContextMenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            StopOutsideClickWatcher();
        }

        private void StartOutsideClickWatcher()
        {
            if (_hMouseHook != IntPtr.Zero) return;
            try
            {
                _mouseHookProc = MouseHookCallback;
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    _hMouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseHookProc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
                }
            }
            catch { }
        }

        private void StopOutsideClickWatcher()
        {
            if (_hMouseHook != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.UnhookWindowsHookEx(_hMouseHook);
                }
                catch { }
                _hMouseHook = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == NativeMethods.WM_LBUTTONDOWN || msg == NativeMethods.WM_RBUTTONDOWN ||
                    msg == NativeMethods.WM_NCLBUTTONDOWN || msg == NativeMethods.WM_NCRBUTTONDOWN)
                {
                    Point pt = Cursor.Position;
                    if (_contextMenu != null && _contextMenu.Visible && !_contextMenu.Bounds.Contains(pt))
                    {
                        // Check if cursor is inside any open dropdown submenu as well
                        bool insideChild = false;
                        foreach (ToolStripItem item in _contextMenu.Items)
                        {
                            ToolStripDropDownItem dropDownItem = item as ToolStripDropDownItem;
                            if (dropDownItem != null && dropDownItem.DropDown.Visible && dropDownItem.DropDown.Bounds.Contains(pt))
                            {
                                insideChild = true;
                                break;
                            }
                        }

                        if (!insideChild)
                        {
                            try
                            {
                                BeginInvoke(new Action(() =>
                                {
                                    if (_contextMenu != null && _contextMenu.Visible)
                                    {
                                        _contextMenu.Close();
                                    }
                                }));
                            }
                            catch { }
                        }
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_hMouseHook, nCode, wParam, lParam);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopOutsideClickWatcher();
            }
            base.Dispose(disposing);
        }

        private void ToggleLayout(LayoutMode mode)
        {
            _configService.Config.Layout = mode;
            _configService.Save(_configService.Config);
            RenderFrame();
        }

        private void SetPosition(PositionMode pos)
        {
            _configService.Config.Position = pos;
            _configService.Config.OffsetX = 0;
            _configService.Config.OffsetY = 0;
            _configService.Save(_configService.Config);
            RenderFrame();
        }

        private void LaunchTaskManager()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "taskmgr.exe",
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        public void RenderFrame()
        {
            if (IsDisposed || !IsHandleCreated) return;

            EnsureTaskbarOwnership();

            var cfg = _configService.Config;
            var stats = _networkMonitor.CurrentStats;
            float dpiScale = DpiHelper.GetDpiScale(Handle);

            // Determine dimensions
            int width, height;
            MeasureDimensions(cfg, stats, dpiScale, out width, out height);

            // Calculate coordinates
            Rectangle bounds;
            if (cfg.IsFloatingMode)
            {
                if (_secondaryTaskbarHandle != IntPtr.Zero)
                {
                    if (Visible) Visible = false;
                    return;
                }

                if (cfg.FloatingX < 0 || cfg.FloatingY < 0)
                {
                    Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
                    cfg.FloatingX = workArea.Right - width - 24;
                    cfg.FloatingY = workArea.Bottom - height - 24;
                }
                bounds = new Rectangle(cfg.FloatingX, cfg.FloatingY, width, height);
            }
            else if (_secondaryTaskbarHandle != IntPtr.Zero)
            {
                bounds = _taskbarTracker.CalculateSecondaryBounds(_secondaryTaskbarHandle, width, height);
                if (bounds.IsEmpty)
                {
                    if (!_lastRenderedBounds.IsEmpty) bounds = _lastRenderedBounds;
                    else return;
                }
            }
            else
            {
                bounds = _taskbarTracker.CalculateBounds(cfg, width, height);
                if (bounds.IsEmpty)
                {
                    if (!_lastRenderedBounds.IsEmpty)
                    {
                        bounds = _lastRenderedBounds;
                    }
                    else
                    {
                        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                        Rectangle boundsScr = Screen.PrimaryScreen.Bounds;
                        int tbHeight = boundsScr.Bottom - wa.Bottom;
                        if (tbHeight <= 0) tbHeight = 48;
                        bounds = new Rectangle(wa.Right - width - 120, boundsScr.Bottom - tbHeight + ((tbHeight - height) / 2), width, height);
                    }
                }
            }

            if (!Visible)
            {
                Visible = true;
            }

            // Draw to in-memory 32-bit ARGB DIB Section
            DrawLayeredFrame(bounds, width, height, cfg, stats, dpiScale);
        }

        private void MeasureDimensions(AppConfig cfg, NetworkStats stats, float dpiScale, out int width, out int height)
        {
            double scale = cfg.IsFloatingMode ? Math.Max(0.5, Math.Min(2.5, cfg.FloatingScale > 0 ? cfg.FloatingScale : 1.0)) : 1.0;
            int baseHeight = cfg.IsFloatingMode ? (int)(DpiHelper.Scale(36, dpiScale) * scale) : DpiHelper.Scale(40, dpiScale); // Taskbar height is 48px
            int padX = (int)(DpiHelper.Scale(cfg.PaddingX, dpiScale) * scale);
            int padY = (int)(DpiHelper.Scale(cfg.PaddingY, dpiScale) * scale);

            string fontName = string.IsNullOrEmpty(cfg.FontFamily) ? "Segoe UI Variable Text" : cfg.FontFamily;
            FontStyle style = cfg.FontBold ? FontStyle.Bold : FontStyle.Regular;
            float scaledFontSize = (float)(cfg.FontSize * scale);
            float unitSizePt = (float)((cfg.UnitFontSize > 0 ? cfg.UnitFontSize : Math.Max(6.0f, cfg.FontSize * 0.8f)) * scale);

            using (Font font = CreateFontSafely(fontName, scaledFontSize * dpiScale, style))
            using (Font unitFont = CreateFontSafely(fontName, unitSizePt * dpiScale, FontStyle.Regular))
            using (Bitmap dummyBmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(dummyBmp))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                string upArrow = GetArrowGlyph(cfg.ArrowStyle, true);
                string dnArrow = GetArrowGlyph(cfg.ArrowStyle, false);

                // Sample standard numbers to ensure width remains stable
                string sampleVal = "888.8";
                string sampleUnit = "MB/s";

                SizeF arrowSize = g.MeasureString(upArrow, font);
                SizeF valSize = g.MeasureString(sampleVal, font);
                SizeF unitSize = g.MeasureString(sampleUnit, unitFont);

                if (cfg.Layout == LayoutMode.Stacked)
                {
                    float rowWidth = arrowSize.Width + valSize.Width + unitSize.Width;
                    width = (int)Math.Ceiling(rowWidth) + (padX * 2);
                    height = baseHeight;
                }
                else
                {
                    float totalContentWidth = (arrowSize.Width + valSize.Width + unitSize.Width) * 2 + DpiHelper.Scale(cfg.ItemSpacing + 6, dpiScale);
                    width = (int)Math.Ceiling(totalContentWidth) + (padX * 2);
                    height = baseHeight;
                }
            }

            if (cfg.IsFloatingMode)
            {
                width += (int)(DpiHelper.Scale(12, dpiScale) * scale);
            }

            // Minimum width safeguard
            width = Math.Max(width, (int)(DpiHelper.Scale(50, dpiScale) * scale));
        }

        private void DrawLayeredFrame(Rectangle bounds, int width, int height, AppConfig cfg, NetworkStats stats, float dpiScale)
        {
            IntPtr screenDC = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr memDC = NativeMethods.CreateCompatibleDC(screenDC);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBmp = IntPtr.Zero;
            IntPtr pBits;

            try
            {
                NativeMethods.BITMAPINFO bmi = new NativeMethods.BITMAPINFO();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height; // Top-down DIB
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = NativeMethods.BI_RGB;

                hBitmap = NativeMethods.CreateDIBSection(memDC, ref bmi, NativeMethods.DIB_RGB_COLORS, out pBits, IntPtr.Zero, 0);
                hOldBmp = NativeMethods.SelectObject(memDC, hBitmap);

                using (Graphics g = Graphics.FromHdc(memDC))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.Transparent);

                    if (cfg.IsFloatingMode)
                    {
                        Rectangle widgetRect = new Rectangle(1, 1, width - 2, height - 2);
                        using (GraphicsPath path = CreateRoundedRect(widgetRect, 8))
                        {
                            int bgAlpha = (int)(255 * (cfg.OverallOpacityPercent / 100f));
                            using (Brush widgetBg = new SolidBrush(Color.FromArgb(Math.Min(255, Math.Max(180, bgAlpha)), 24, 24, 28)))
                            {
                                g.FillPath(widgetBg, path);
                            }
                            Color borderColor = _isHovered ? Color.FromArgb(0, 120, 215) : Color.FromArgb(60, 60, 68);
                            using (Pen widgetPen = new Pen(borderColor, 1))
                            {
                                g.DrawPath(widgetPen, path);
                            }
                        }
                    }
                    else
                    {
                        // Fill entire bounds with imperceptible Alpha=1 floor.
                        // This prevents the cursor from falling through Alpha=0 pixels to Shell_TrayWnd,
                        // which causes Windows 11 taskbar to fight for focus and flicker/vanish.
                        using (Brush hitTestBrush = new SolidBrush(Color.FromArgb(1, 0, 0, 0)))
                        {
                            g.FillRectangle(hitTestBrush, 0, 0, width, height);
                        }

                        // Hover highlight (Windows 11 subtle rounded pill)
                        if (_isHovered && cfg.ShowHoverHighlight)
                        {
                            using (GraphicsPath path = CreateRoundedRect(new Rectangle(2, 4, width - 4, height - 8), 5))
                            using (Brush hoverBrush = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                            using (Pen hoverPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1))
                            {
                                g.FillPath(hoverBrush, path);
                                g.DrawPath(hoverPen, path);
                            }
                        }
                    }

                    double scale = cfg.IsFloatingMode ? Math.Max(0.5, Math.Min(2.5, cfg.FloatingScale > 0 ? cfg.FloatingScale : 1.0)) : 1.0;
                    string fontName = string.IsNullOrEmpty(cfg.FontFamily) ? "Segoe UI Variable Text" : cfg.FontFamily;
                    FontStyle style = cfg.FontBold ? FontStyle.Bold : FontStyle.Regular;
                    float scaledFontSize = (float)(cfg.FontSize * scale);
                    float unitSizePt = (float)((cfg.UnitFontSize > 0 ? cfg.UnitFontSize : Math.Max(6.0f, cfg.FontSize * 0.8f)) * scale);

                    using (Font font = CreateFontSafely(fontName, scaledFontSize * dpiScale, style))
                    using (Font unitFont = CreateFontSafely(fontName, unitSizePt * dpiScale, FontStyle.Regular))
                    {
                        Color upColor, dnColor, lblColor;
                        if (cfg.AutoTheme)
                        {
                            string uHex, dHex, lHex;
                            ThemeHelper.GetThemeColors(ThemeHelper.IsSystemLightTheme(), out uHex, out dHex, out lHex);
                            upColor = ParseColor(uHex, Color.FromArgb(76, 194, 255));
                            dnColor = ParseColor(dHex, Color.FromArgb(108, 203, 95));
                            lblColor = ParseColor(lHex, Color.FromArgb(208, 208, 208));
                        }
                        else
                        {
                            upColor = ParseColor(cfg.UploadColor, Color.FromArgb(76, 194, 255));
                            dnColor = ParseColor(cfg.DownloadColor, Color.FromArgb(108, 203, 95));
                            lblColor = ParseColor(cfg.LabelColor, Color.FromArgb(208, 208, 208));
                        }

                        string upArrow = GetArrowGlyph(cfg.ArrowStyle, true);
                        string dnArrow = GetArrowGlyph(cfg.ArrowStyle, false);

                        int padX = (int)(DpiHelper.Scale(cfg.PaddingX, dpiScale) * scale);

                        if (cfg.Layout == LayoutMode.Stacked)
                        {
                            DrawStackedLayout(g, font, unitFont, width, height, padX, upArrow, dnArrow, stats, upColor, dnColor, lblColor, dpiScale);
                        }
                        else
                        {
                            DrawHorizontalLayout(g, font, unitFont, width, height, padX, upArrow, dnArrow, stats, upColor, dnColor, lblColor, cfg.ItemSpacing, dpiScale);
                        }
                    }
                }

                NativeMethods.POINT ptDst = new NativeMethods.POINT(bounds.X, bounds.Y);
                NativeMethods.SIZE sizeDst = new NativeMethods.SIZE(width, height);
                NativeMethods.POINT ptSrc = new NativeMethods.POINT(0, 0);

                NativeMethods.BLENDFUNCTION blend = new NativeMethods.BLENDFUNCTION();
                blend.BlendOp = NativeMethods.AC_SRC_OVER;
                blend.BlendFlags = 0;

                bool isIdle = cfg.IdleDimming && !_isHovered &&
                              (stats.DownloadSpeedBytesPerSec < (cfg.IdleThresholdKb * 1024.0)) &&
                              (stats.UploadSpeedBytesPerSec < (cfg.IdleThresholdKb * 1024.0));

                float baseOpacity = Math.Max(0.2f, Math.Min(1.0f, cfg.OverallOpacityPercent / 100.0f));
                float idleMultiplier = isIdle ? (cfg.IdleOpacityPercent / 100.0f) : 1.0f;
                byte sourceAlpha = (byte)Math.Max(15, Math.Min(255, (int)(baseOpacity * idleMultiplier * 255)));

                blend.SourceConstantAlpha = sourceAlpha;
                blend.AlphaFormat = NativeMethods.AC_SRC_ALPHA;

                NativeMethods.UpdateLayeredWindow(
                    Handle,
                    screenDC,
                    ref ptDst,
                    ref sizeDst,
                    memDC,
                    ref ptSrc,
                    0,
                    ref blend,
                    NativeMethods.ULW_ALPHA);

                // Ensure it stays at topmost layer on taskbar without stealing focus
                NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, bounds.X, bounds.Y, width, height,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
                _lastRenderedBounds = bounds;
            }
            finally
            {
                if (hOldBmp != IntPtr.Zero) NativeMethods.SelectObject(memDC, hOldBmp);
                if (hBitmap != IntPtr.Zero) NativeMethods.DeleteObject(hBitmap);
                if (memDC != IntPtr.Zero) NativeMethods.DeleteDC(memDC);
                if (screenDC != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, screenDC);
            }
        }

        private void DrawStackedLayout(Graphics g, Font font, Font unitFont, int width, int height, int padX,
            string upArrow, string dnArrow, NetworkStats stats, Color upColor, Color dnColor, Color lblColor, float dpiScale)
        {
            int rowHeight = height / 2;
            int yRow1 = 2;
            int yRow2 = rowHeight - 2;

            float baselineOffset = (font.GetHeight(g) - unitFont.GetHeight(g)) * 0.75f;

            using (Brush upBrush = new SolidBrush(upColor))
            using (Brush dnBrush = new SolidBrush(dnColor))
            using (Brush lblBrush = new SolidBrush(lblColor))
            {
                // Measure widths for right-aligned tabular presentation
                SizeF upArrowSz = g.MeasureString(upArrow, font);
                SizeF dnArrowSz = g.MeasureString(dnArrow, font);
                SizeF upValSz = g.MeasureString(stats.UploadFormatted, font);
                SizeF dnValSz = g.MeasureString(stats.DownloadFormatted, font);

                float maxArrowW = Math.Max(upArrowSz.Width, dnArrowSz.Width);
                float maxValW = Math.Max(upValSz.Width, dnValSz.Width);

                // Row 1 (Upload)
                float x1 = padX;
                if (!string.IsNullOrEmpty(upArrow))
                {
                    g.DrawString(upArrow, font, upBrush, x1, yRow1);
                    x1 += maxArrowW + 2;
                }
                // Right-align number
                float numX1 = x1 + (maxValW - upValSz.Width);
                g.DrawString(stats.UploadFormatted, font, upBrush, numX1, yRow1);
                x1 += maxValW + 2;
                g.DrawString(stats.UploadUnit, unitFont, lblBrush, x1, yRow1 + baselineOffset);

                // Row 2 (Download)
                float x2 = padX;
                if (!string.IsNullOrEmpty(dnArrow))
                {
                    g.DrawString(dnArrow, font, dnBrush, x2, yRow2);
                    x2 += maxArrowW + 2;
                }
                float numX2 = x2 + (maxValW - dnValSz.Width);
                g.DrawString(stats.DownloadFormatted, font, dnBrush, numX2, yRow2);
                x2 += maxValW + 2;
                g.DrawString(stats.DownloadUnit, unitFont, lblBrush, x2, yRow2 + baselineOffset);
            }
        }

        private void DrawHorizontalLayout(Graphics g, Font font, Font unitFont, int width, int height, int padX,
            string upArrow, string dnArrow, NetworkStats stats, Color upColor, Color dnColor, Color lblColor, int itemSpacing, float dpiScale)
        {
            float yPos = (height - font.GetHeight(g)) / 2f;
            float baselineOffset = (font.GetHeight(g) - unitFont.GetHeight(g)) * 0.75f;

            using (Brush upBrush = new SolidBrush(upColor))
            using (Brush dnBrush = new SolidBrush(dnColor))
            using (Brush lblBrush = new SolidBrush(lblColor))
            {
                float x = padX;

                // Upload block
                if (!string.IsNullOrEmpty(upArrow))
                {
                    g.DrawString(upArrow, font, upBrush, x, yPos);
                    x += g.MeasureString(upArrow, font).Width + 1;
                }
                g.DrawString(stats.UploadFormatted, font, upBrush, x, yPos);
                x += g.MeasureString(stats.UploadFormatted, font).Width + 1;
                g.DrawString(stats.UploadUnit, unitFont, lblBrush, x, yPos + baselineOffset);
                x += g.MeasureString(stats.UploadUnit, unitFont).Width + DpiHelper.Scale(itemSpacing + 6, dpiScale);

                // Download block
                if (!string.IsNullOrEmpty(dnArrow))
                {
                    g.DrawString(dnArrow, font, dnBrush, x, yPos);
                    x += g.MeasureString(dnArrow, font).Width + 1;
                }
                g.DrawString(stats.DownloadFormatted, font, dnBrush, x, yPos);
                x += g.MeasureString(stats.DownloadFormatted, font).Width + 1;
                g.DrawString(stats.DownloadUnit, unitFont, lblBrush, x, yPos + baselineOffset);
            }
        }

        private static string GetArrowGlyph(ArrowStyle style, bool isUpload)
        {
            switch (style)
            {
                case ArrowStyle.Triangle: return isUpload ? "▲" : "▼";
                case ArrowStyle.Arrow: return isUpload ? "↑" : "↓";
                case ArrowStyle.Letter: return isUpload ? "U:" : "D:";
                case ArrowStyle.None: return "";
                default: return isUpload ? "▲" : "▼";
            }
        }

        private static Font CreateFontSafely(string fontName, float size, FontStyle style)
        {
            try
            {
                return new Font(fontName, size, style, GraphicsUnit.Point);
            }
            catch
            {
                return new Font("Segoe UI", size, style, GraphicsUnit.Point);
            }
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            try
            {
                return ColorTranslator.FromHtml(hex);
            }
            catch
            {
                return fallback;
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!_isHovered)
            {
                _isHovered = true;
                RenderFrame();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isHovered)
            {
                _isHovered = false;
                RenderFrame();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Right)
            {
                NativeMethods.SetForegroundWindow(Handle);
                var cfg = _configService.Config;
                if (cfg.IsFloatingMode)
                {
                    Screen scr = Screen.FromPoint(Location);
                    Rectangle wa = scr.WorkingArea;
                    int menuW = Math.Max(220, _contextMenu.PreferredSize.Width);
                    int menuH = Math.Max(260, _contextMenu.PreferredSize.Height);

                    int posX = Location.X;
                    if (posX + menuW > wa.Right) posX = wa.Right - menuW;
                    if (posX < wa.Left) posX = wa.Left;

                    int posY = Location.Y + Height + 4;
                    if (posY + menuH > wa.Bottom)
                    {
                        posY = Location.Y - menuH - 4;
                        if (posY < wa.Top) posY = wa.Bottom - menuH;
                    }

                    _contextMenu.Show(new Point(posX, posY));
                }
                else
                {
                    _contextMenu.Show(Cursor.Position);
                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                var cfg = _configService.Config;
                if (cfg.IsFloatingMode)
                {
                    if (!cfg.FloatingLocked)
                    {
                        _isDragging = true;
                        _dragStartPoint = Cursor.Position;
                        _initialWindowLocation = new Point(cfg.FloatingX, cfg.FloatingY);
                    }
                }
                else if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                {
                    _isDragging = true;
                    _dragStartPoint = Cursor.Position;
                    _initialOffsetX = cfg.OffsetX;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                var cfg = _configService.Config;
                if (cfg.IsFloatingMode)
                {
                    int dx = Cursor.Position.X - _dragStartPoint.X;
                    int dy = Cursor.Position.Y - _dragStartPoint.Y;
                    int newX = _initialWindowLocation.X + dx;
                    int newY = _initialWindowLocation.Y + dy;

                    // Screen edge snapping (16px snap threshold)
                    Screen screen = Screen.FromPoint(new Point(newX, newY));
                    Rectangle workArea = screen.WorkingArea;
                    const int SNAP = 16;
                    if (Math.Abs(newX - workArea.Left) <= SNAP) newX = workArea.Left + 8;
                    else if (Math.Abs((newX + Width) - workArea.Right) <= SNAP) newX = workArea.Right - Width - 8;
                    if (Math.Abs(newY - workArea.Top) <= SNAP) newY = workArea.Top + 8;
                    else if (Math.Abs((newY + Height) - workArea.Bottom) <= SNAP) newY = workArea.Bottom - Height - 8;

                    cfg.FloatingX = newX;
                    cfg.FloatingY = newY;
                    RenderFrame();
                }
                else
                {
                    int deltaX = Cursor.Position.X - _dragStartPoint.X;
                    cfg.OffsetX = _initialOffsetX + deltaX;
                    RenderFrame();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            int dist = Math.Abs(Cursor.Position.X - _dragStartPoint.X) + Math.Abs(Cursor.Position.Y - _dragStartPoint.Y);
            if (_isDragging && dist > 4)
            {
                _isDragging = false;
                _configService.Save(_configService.Config);
            }
            else
            {
                _isDragging = false;
                if (e.Button == MouseButtons.Left)
                {
                    var action = _configService.Config.LeftClickAction;
                    if (action == LeftClickAction.Flyout)
                    {
                        if (RequestToggleFlyout != null) RequestToggleFlyout();
                    }
                    else if (action == LeftClickAction.TaskManager)
                    {
                        LaunchTaskManager();
                    }
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            const int MA_NOACTIVATE = 3;

            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }

            if (m.Msg == NativeMethods.WM_WINDOWPOSCHANGING)
            {
                try
                {
                    NativeMethods.WINDOWPOS wp = (NativeMethods.WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.WINDOWPOS));

                    // Show Desktop Anti-Flicker Immunity:
                    // Windows shell sends SWP_HIDEWINDOW when clicking the far-right taskbar line ("Show Desktop") or Win+D.
                    // If docked on a taskbar, strip SWP_HIDEWINDOW and ensure SWP_SHOWWINDOW so the meter NEVER disappears.
                    if (!_configService.Config.IsFloatingMode)
                    {
                        if ((wp.flags & NativeMethods.SWP_HIDEWINDOW) != 0)
                        {
                            wp.flags &= ~NativeMethods.SWP_HIDEWINDOW;
                            wp.flags |= NativeMethods.SWP_SHOWWINDOW;
                        }

                        // Always force HWND_TOPMOST and clear SWP_NOZORDER so Windows Shell
                        // cannot demote the docked indicator below the taskbar or tab previews.
                        wp.flags &= ~NativeMethods.SWP_NOZORDER;
                        wp.hwndInsertAfter = NativeMethods.HWND_TOPMOST;
                    }
                    else
                    {
                        if ((wp.flags & NativeMethods.SWP_NOZORDER) == 0 && wp.hwndInsertAfter != NativeMethods.HWND_TOPMOST)
                        {
                            wp.hwndInsertAfter = NativeMethods.HWND_TOPMOST;
                        }
                    }
                    Marshal.StructureToPtr(wp, m.LParam, false);
                }
                catch { }
            }

            if (m.Msg == NativeMethods.WM_SHOWWINDOW)
            {
                // Suppress hide request dispatched by Show Desktop / Win+D
                if (m.WParam == IntPtr.Zero && !_configService.Config.IsFloatingMode)
                {
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            if (m.Msg == NativeMethods.WM_SYSCOMMAND && (m.WParam.ToInt32() & 0xFFF0) == NativeMethods.SC_MINIMIZE)
            {
                if (!_configService.Config.IsFloatingMode)
                {
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            if (m.Msg == (int)_msgTaskbarCreated)
            {
                _taskbarTracker.RefreshHandles();
                EnsureTaskbarOwnership();
                RenderFrame();
                return;
            }

            const int WM_DISPLAYCHANGE = 0x007E;
            const int WM_DPICHANGED = 0x02E0;
            const int WM_SETTINGCHANGE = 0x001A;
            const int WM_THEMECHANGED = 0x031A;

            if (m.Msg == WM_DISPLAYCHANGE || m.Msg == WM_DPICHANGED)
            {
                _taskbarTracker.RefreshHandles();
                EnsureTaskbarOwnership();
                RenderFrame();
            }
            else if (m.Msg == WM_SETTINGCHANGE || m.Msg == WM_THEMECHANGED)
            {
                if (_configService.Config.AutoTheme)
                {
                    RenderFrame();
                }
            }

            base.WndProc(ref m);
        }
    }
}
