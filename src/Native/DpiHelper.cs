using System;
using System.Drawing;

namespace MetriX.Native
{
    internal static class DpiHelper
    {
        public const float DefaultDpi = 96.0f;

        public static float GetDpiScale(IntPtr hWnd)
        {
            try
            {
                uint dpi = NativeMethods.GetDpiForWindow(hWnd);
                if (dpi > 0)
                {
                    return dpi / DefaultDpi;
                }
            }
            catch
            {
                // Fallback for older OS versions
            }

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                return g.DpiX / DefaultDpi;
            }
        }

        public static int Scale(int value, float scale)
        {
            return (int)Math.Round(value * scale);
        }

        public static float Scale(float value, float scale)
        {
            return value * scale;
        }
    }
}
