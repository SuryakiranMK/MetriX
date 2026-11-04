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
        private int _initialOffsetX;
        private ContextMenuStrip _contextMenu;

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

            var itemTaskMgr = new ToolStripMenuItem("📊  Open Task Manager", null, (s, e) => LaunchTaskManager());

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
                itemStartup.Checked = cfg.StartWithWindows;
            };

            _contextMenu.Items.Add(itemFlyout);
            _contextMenu.Items.Add(itemSettings);
            _contextMenu.Items.Add(itemUpdates);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(itemLayout);
            _contextMenu.Items.Add(itemPosition);
            _contextMenu.Items.Add(itemTaskMgr);
            _contextMenu.Items.Add(itemStartup);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(itemExit);
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

            // Calculate Taskbar coordinates
            Rectangle bounds;
            if (_secondaryTaskbarHandle != IntPtr.Zero)
            {
                bounds = _taskbarTracker.CalculateSecondaryBounds(_secondaryTaskbarHandle, width, height);
                if (bounds.IsEmpty) return;
            }
            else
            {
                bounds = _taskbarTracker.CalculateBounds(cfg, width, height);
            }

            // Draw to in-memory 32-bit ARGB DIB Section
            DrawLayeredFrame(bounds, width, height, cfg, stats, dpiScale);
        }

        private void MeasureDimensions(AppConfig cfg, NetworkStats stats, float dpiScale, out int width, out int height)
        {
            int baseHeight = DpiHelper.Scale(40, dpiScale); // Taskbar height is 48px
            int padX = DpiHelper.Scale(cfg.PaddingX, dpiScale);
            int padY = DpiHelper.Scale(cfg.PaddingY, dpiScale);

            string fontName = string.IsNullOrEmpty(cfg.FontFamily) ? "Segoe UI Variable Text" : cfg.FontFamily;
            FontStyle style = cfg.FontBold ? FontStyle.Bold : FontStyle.Regular;
            float unitSizePt = cfg.UnitFontSize > 0 ? cfg.UnitFontSize : Math.Max(6.0f, cfg.FontSize * 0.8f);

            using (Font font = CreateFontSafely(fontName, cfg.FontSize * dpiScale, style))
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

            // Minimum width safeguard
            width = Math.Max(width, DpiHelper.Scale(50, dpiScale));
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

                    string fontName = string.IsNullOrEmpty(cfg.FontFamily) ? "Segoe UI Variable Text" : cfg.FontFamily;
                    FontStyle style = cfg.FontBold ? FontStyle.Bold : FontStyle.Regular;
                    float unitSizePt = cfg.UnitFontSize > 0 ? cfg.UnitFontSize : Math.Max(6.0f, cfg.FontSize * 0.8f);

                    using (Font font = CreateFontSafely(fontName, cfg.FontSize * dpiScale, style))
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

                        int padX = DpiHelper.Scale(cfg.PaddingX, dpiScale);

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

                // Ensure it stays at topmost taskbar layer without stealing focus
                NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, bounds.X, bounds.Y, width, height,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
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
                _contextMenu.Show(Cursor.Position);
            }
            else if (e.Button == MouseButtons.Left)
            {
                if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                {
                    _isDragging = true;
                    _dragStartPoint = Cursor.Position;
                    _initialOffsetX = _configService.Config.OffsetX;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                int deltaX = Cursor.Position.X - _dragStartPoint.X;
                _configService.Config.OffsetX = _initialOffsetX + deltaX;
                RenderFrame();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_isDragging)
            {
                _isDragging = false;
                _configService.Save(_configService.Config);
            }
            else if (e.Button == MouseButtons.Left)
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
                    if ((wp.flags & NativeMethods.SWP_NOZORDER) == 0 && wp.hwndInsertAfter != NativeMethods.HWND_TOPMOST)
                    {
                        wp.hwndInsertAfter = NativeMethods.HWND_TOPMOST;
                        Marshal.StructureToPtr(wp, m.LParam, false);
                    }
                }
                catch { }
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

            if (m.Msg == WM_DISPLAYCHANGE || m.Msg == WM_DPICHANGED)
            {
                _taskbarTracker.RefreshHandles();
                EnsureTaskbarOwnership();
                RenderFrame();
            }

            base.WndProc(ref m);
        }
    }
}
