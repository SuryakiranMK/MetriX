using System;
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

        private NativeMethods.RECT _lastTaskbarRect;
        private NativeMethods.RECT _lastTrayRect;
        private NativeMethods.RECT _lastTaskListRect;

        public bool IsTaskbarFound { get { return _hTaskbar != IntPtr.Zero; } }

        public TaskbarTracker()
        {
            RefreshHandles();
        }

        public void RefreshHandles()
        {
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
        }

        public Rectangle CalculateBounds(AppConfig config, int widgetWidth, int widgetHeight)
        {
            if (_hTaskbar == IntPtr.Zero)
            {
                RefreshHandles();
            }

            NativeMethods.RECT tbRect = new NativeMethods.RECT();
            if (_hTaskbar != IntPtr.Zero && NativeMethods.GetWindowRect(_hTaskbar, out tbRect))
            {
                _lastTaskbarRect = tbRect;
            }
            else
            {
                // Fallback to Primary Screen working area taskbar calculation
                var scr = Screen.PrimaryScreen;
                tbRect = new NativeMethods.RECT
                {
                    Left = 0,
                    Top = scr.WorkingArea.Bottom,
                    Right = scr.Bounds.Width,
                    Bottom = scr.Bounds.Height
                };
            }

            NativeMethods.RECT trayRect = new NativeMethods.RECT();
            bool hasTray = _hTrayNotify != IntPtr.Zero && NativeMethods.GetWindowRect(_hTrayNotify, out trayRect);
            if (hasTray) _lastTrayRect = trayRect;

            NativeMethods.RECT taskListRect = new NativeMethods.RECT();
            bool hasTaskList = _hTaskList != IntPtr.Zero && NativeMethods.GetWindowRect(_hTaskList, out taskListRect);
            if (hasTaskList) _lastTaskListRect = taskListRect;

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
                    return new Rectangle(targetX, targetY, widgetWidth, widgetHeight);

                case PositionMode.LeftOfTray:
                default:
                    if (hasTray && trayRect.Left > 0)
                    {
                        targetX = trayRect.Left - widgetWidth - 4 + (config != null ? config.OffsetX : 0);
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

            return new Rectangle(targetX, targetY, widgetWidth, widgetHeight);
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
                if (curTb.Left != _lastTaskbarRect.Left || curTb.Top != _lastTaskbarRect.Top ||
                    curTb.Right != _lastTaskbarRect.Right || curTb.Bottom != _lastTaskbarRect.Bottom)
                {
                    return true;
                }
            }

            if (_hTrayNotify != IntPtr.Zero)
            {
                NativeMethods.RECT curTray;
                if (NativeMethods.GetWindowRect(_hTrayNotify, out curTray))
                {
                    if (curTray.Left != _lastTrayRect.Left || curTray.Top != _lastTrayRect.Top ||
                        curTray.Right != _lastTrayRect.Right || curTray.Bottom != _lastTrayRect.Bottom)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
