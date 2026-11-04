using System;
using Microsoft.Win32;

namespace MetriX.Services
{
    public static class ThemeHelper
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static bool IsSystemLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("SystemUsesLightTheme");
                        if (val is int)
                        {
                            return ((int)val) == 1;
                        }
                    }
                }
            }
            catch
            {
            }
            return false; // Default to dark taskbar
        }

        public static void GetThemeColors(bool isLight, out string upColor, out string dnColor, out string lblColor)
        {
            if (isLight)
            {
                upColor = "#0066B4";    // Deep vibrant blue for light backgrounds
                dnColor = "#107C10";    // Deep emerald green
                lblColor = "#262626";   // Dark charcoal
            }
            else
            {
                upColor = "#4CC2FF";    // Soft vibrant cyan for dark backgrounds
                dnColor = "#6CCB5F";    // Vibrant light green
                lblColor = "#D0D0D0";   // Crisp off-white
            }
        }
    }
}
