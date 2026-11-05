using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MetriX.Model;
using MetriX.Native;

namespace MetriX.Services
{
    public class TaskbarTracker
    {
        private IntPtr _hTaskbar;
        private IntPtr _hTrayNotify;
        private IntPtr _hTaskList;
        private readonly List<IntPtr> _secondaryTaskbars = new List<IntPtr>();

        private NativeMethods.RECT _lastValidTaskbarRect;
        private NativeMethods.RECT _lastValidTrayRect;
        private NativeMethods.RECT _lastValidTaskListRect;
        private Rectangle _lastValidCalculatedBounds = Rectangle.Empty;
        private DateTime _lastRefresh = DateTime.MinValue;
        private bool _isTaskbarHidden;

        public bool IsTaskbarFound { get { return _hTaskbar != IntPtr.Zero; } }
        public bool IsTaskbarHidden { get { return _isTaskbarHidden; } }
        public List<IntPtr> SecondaryTaskbars { get { return _secondaryTaskbars; } }

        public IntPtr TaskbarHandle
        {
            get
            {
                if (_hTaskbar == IntPtr.Zero) RefreshHandles();
                return _hTaskbar;
            }
        }

        public TaskbarTracker()
        {
            RefreshHandles();
        }

        public void RefreshHandlesIfNeeded()
        {
            if ((DateTime.Now - _lastRefresh).TotalSeconds >= 2.5)
            {
                RefreshHandles();
            }
        }

        public void RefreshHandles()
        {
            _lastRefresh = DateTime.Now;
            _hTaskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (_hTaskbar != IntPtr.Zero)
            {
                _hTrayNotify = NativeMethods.FindWindowEx(_hTaskbar, IntPtr.Zero, "TrayNotifyWnd", null);

                IntPtr hRebar = NativeMethods.FindWindowEx(_hTaskbar, IntPtr.Zero, "ReBarWindow32", null);
                if (hRebar != IntPtr.Zero)
                {
                    IntPtr hTaskSw = NativeMethods.FindWindowEx(hRebar, IntPtr.Zero, "MSTaskSwWClass", null);
                    if (hTaskSw != IntPtr.Zero)
                    {
                        _hTaskList = NativeMethods.FindWindowEx(hTaskSw, IntPtr.Zero, "MSTaskListWClass", null);
                    }
                }
            }

            _secondaryTaskbars.Clear();
            IntPtr hSec = IntPtr.Zero;
            while ((hSec = NativeMethods.FindWindowEx(IntPtr.Zero, hSec, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                _secondaryTaskbars.Add(hSec);
            }
        }

        public Rectangle CalculateSecondaryBounds(IntPtr hSecondaryTaskbar, int widgetWidth, int widgetHeight)
        {
            NativeMethods.RECT secRect = new NativeMethods.RECT();
            if (hSecondaryTaskbar != IntPtr.Zero && NativeMethods.GetWindowRect(hSecondaryTaskbar, out secRect) && secRect.Width > 50)
            {
                int targetX = secRect.Right - widgetWidth - 16;
                int targetY = secRect.Top + (secRect.Height - widgetHeight) / 2;
                var result = new Rectangle(targetX, targetY, widgetWidth, widgetHeight);
                _lastValidCalculatedBounds = result;
                return result;
            }
            if (!_lastValidCalculatedBounds.IsEmpty) return _lastValidCalculatedBounds;
            return Rectangle.Empty;
        }

        public Rectangle CalculateBounds(AppConfig config, int widgetWidth, int widgetHeight)
        {
            if (_hTaskbar == IntPtr.Zero)
            {
                RefreshHandles();
            }

            NativeMethods.RECT tbRect = new NativeMethods.RECT();
            bool hasTb = _hTaskbar != IntPtr.Zero && NativeMethods.GetWindowRect(_hTaskbar, out tbRect);

            Screen scr = Screen.PrimaryScreen;

            if (hasTb && tbRect.Width > 100 && tbRect.Height > 10)
            {
                _lastValidTaskbarRect = tbRect;
            }
            else if (_lastValidTaskbarRect.Width > 100)
            {
                tbRect = _lastValidTaskbarRect;
            }
            else
            {
                if (scr != null)
                {
                    tbRect = new NativeMethods.RECT
                    {
                        Left = 0,
                        Top = scr.WorkingArea.Bottom,
                        Right = scr.Bounds.Width,
                        Bottom = scr.Bounds.Height
                    };
                }
                _lastValidTaskbarRect = tbRect;
            }

            NativeMethods.RECT trayRect = new NativeMethods.RECT();
            bool hasTray = _hTrayNotify != IntPtr.Zero && NativeMethods.GetWindowRect(_hTrayNotify, out trayRect);
            if (hasTray && trayRect.Width > 10 && trayRect.Height > 10 && trayRect.Left > 0)
            {
                _lastValidTrayRect = trayRect;
            }
            else if (_lastValidTrayRect.Left > 0)
            {
                trayRect = _lastValidTrayRect;
                hasTray = true;
            }

            NativeMethods.RECT taskListRect = new NativeMethods.RECT();
            bool hasTaskList = _hTaskList != IntPtr.Zero && NativeMethods.GetWindowRect(_hTaskList, out taskListRect);
            if (hasTaskList && taskListRect.Width > 10 && taskListRect.Right > 0)
            {
                _lastValidTaskListRect = taskListRect;
            }
            else if (_lastValidTaskListRect.Right > 0)
            {
                taskListRect = _lastValidTaskListRect;
                hasTaskList = true;
            }

            int targetX = 0;
            int targetY = tbRect.Top + (tbRect.Height - widgetHeight) / 2;

            PositionMode pos = config != null ? config.Position : PositionMode.LeftOfTray;

            switch (pos)
            {
                case PositionMode.FarLeft:
                    targetX = tbRect.Left + 12 + (config != null ? config.OffsetX : 0);
                    break;

                case PositionMode.RightOfApps:
                    if (hasTaskList && taskListRect.Right > 0)
                    {
                        targetX = taskListRect.Right + 12 + (config != null ? config.OffsetX : 0);
                    }
                    else
                    {
                        targetX = tbRect.Left + (tbRect.Width / 2) + 100 + (config != null ? config.OffsetX : 0);
                    }
                    break;

                case PositionMode.Custom:
                    targetX = (config != null ? config.OffsetX : (tbRect.Right - widgetWidth - 200));
                    targetY = tbRect.Top + (config != null ? config.OffsetY : (tbRect.Height - widgetHeight) / 2);
                    _lastValidCalculatedBounds = new Rectangle(targetX, targetY, widgetWidth, widgetHeight);
                    return _lastValidCalculatedBounds;

                case PositionMode.LeftOfTray:
                default:
                    int weatherOffset = DetectWeatherWidgetWidth(_hTaskbar, trayRect);
                    if (hasTray && trayRect.Left > 0)
                    {
                        targetX = trayRect.Left - weatherOffset - widgetWidth - 4 + (config != null ? config.OffsetX : 0);
                    }
                    else if (_lastValidTrayRect.Left > 0)
                    {
                        targetX = _lastValidTrayRect.Left - weatherOffset - widgetWidth - 4 + (config != null ? config.OffsetX : 0);
                    }
                    else
                    {
                        targetX = tbRect.Right - widgetWidth - 180 + (config != null ? config.OffsetX : 0);
                    }
                    break;
            }

            if (config != null)
            {
                targetY += config.OffsetY;
            }

            // Ensure it doesn't spill outside taskbar
            if (targetX < tbRect.Left) targetX = tbRect.Left;
            if (targetX + widgetWidth > tbRect.Right) targetX = tbRect.Right - widgetWidth;

            _lastValidCalculatedBounds = new Rectangle(targetX, targetY, widgetWidth, widgetHeight);
            return _lastValidCalculatedBounds;
        }

        private int DetectWeatherWidgetWidth(IntPtr hTaskbar, NativeMethods.RECT trayRect)
        {
            if (hTaskbar == IntPtr.Zero || trayRect.Left <= 0) return 0;
            try
            {
                int detectedWidth = 0;
                IntPtr hChild = IntPtr.Zero;
                while ((hChild = NativeMethods.FindWindowEx(hTaskbar, hChild, null, null)) != IntPtr.Zero)
                {
                    if (hChild == _hTrayNotify) continue;
                    NativeMethods.RECT r;
                    if (NativeMethods.GetWindowRect(hChild, out r))
                    {
                        // Check if child sits inside taskbar and immediately to the left of the tray (within 16px)
                        if (r.Width > 24 && r.Height > 10 && Math.Abs(r.Right - trayRect.Left) <= 16)
                        {
                            if (r.Width > detectedWidth) detectedWidth = r.Width;
                        }
                    }
                }
                return detectedWidth;
            }
            catch
            {
                return 0;
            }
        }

        public bool HasPositionChanged()
        {
            if (_hTaskbar == IntPtr.Zero)
            {
                RefreshHandles();
                return true;
            }

            NativeMethods.RECT curTb;
            if (NativeMethods.GetWindowRect(_hTaskbar, out curTb))
            {
                Screen scr = Screen.PrimaryScreen;
                bool isRetracted = (curTb.Height <= 6 || curTb.Width <= 6) ||
                    (scr != null && (curTb.Top >= scr.Bounds.Bottom - 4 ||
                                     curTb.Bottom <= scr.Bounds.Top + 4 ||
                                     curTb.Right <= scr.Bounds.Left + 4 ||
                                     curTb.Left >= scr.Bounds.Right - 4));

                if (isRetracted != _isTaskbarHidden)
                {
                    _isTaskbarHidden = isRetracted;
                    return true;
                }

                if (curTb.Width > 100 && !isRetracted)
                {
                    if (curTb.Left != _lastValidTaskbarRect.Left || curTb.Top != _lastValidTaskbarRect.Top ||
                        curTb.Right != _lastValidTaskbarRect.Right || curTb.Bottom != _lastValidTaskbarRect.Bottom)
                    {
                        return true;
                    }
                }
            }

            if (_hTrayNotify != IntPtr.Zero)
            {
                NativeMethods.RECT curTray;
                if (NativeMethods.GetWindowRect(_hTrayNotify, out curTray) && curTray.Width > 10)
                {
                    if (curTray.Left != _lastValidTrayRect.Left || curTray.Top != _lastValidTrayRect.Top ||
                        curTray.Right != _lastValidTrayRect.Right || curTray.Bottom != _lastValidTrayRect.Bottom)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
