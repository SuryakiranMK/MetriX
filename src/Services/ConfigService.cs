using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using MetriX.Model;

namespace MetriX.Services
{
    public class ConfigService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "MetriX";
        private readonly string _configFilePath;

        public AppConfig Config { get; private set; }

        public ConfigService()
        {
            string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(appDir))
            {
                appDir = AppDomain.CurrentDomain.BaseDirectory;
            }
            _configFilePath = Path.Combine(appDir, "config.json");
            Config = Load();
        }

        public AppConfig Load()
        {
            var config = new AppConfig();
            if (!File.Exists(_configFilePath))
            {
                Save(config);
                return config;
            }

            try
            {
                string[] lines = File.ReadAllLines(_configFilePath);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line == "{" || line == "}")
                        continue;

                    int colonIdx = line.IndexOf(':');
                    if (colonIdx <= 0) continue;

                    string key = line.Substring(0, colonIdx).Trim().Trim('"', ' ');
                    string val = line.Substring(colonIdx + 1).Trim().TrimEnd(',').Trim().Trim('"', ' ');

                    switch (key)
                    {
                        case "Position":
                            PositionMode pm;
                            if (Enum.TryParse(val, true, out pm)) config.Position = pm;
                            break;
                        case "OffsetX":
                            int ox; if (int.TryParse(val, out ox)) config.OffsetX = ox;
                            break;
                        case "OffsetY":
                            int oy; if (int.TryParse(val, out oy)) config.OffsetY = oy;
                            break;
                        case "Layout":
                            LayoutMode lm;
                            if (Enum.TryParse(val, true, out lm)) config.Layout = lm;
                            break;
                        case "FontFamily":
                            config.FontFamily = val;
                            break;
                        case "FontSize":
                            float fs; if (float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fs)) config.FontSize = fs;
                            break;
                        case "UnitFontSize":
                            float ufs; if (float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ufs)) config.UnitFontSize = ufs;
                            break;
                        case "FontBold":
                            bool fb; if (bool.TryParse(val, out fb)) config.FontBold = fb;
                            break;
                        case "AutoTheme":
                            bool at; if (bool.TryParse(val, out at)) config.AutoTheme = at;
                            break;
                        case "UploadColor":
                            config.UploadColor = val;
                            break;
                        case "DownloadColor":
                            config.DownloadColor = val;
                            break;
                        case "LabelColor":
                            config.LabelColor = val;
                            break;
                        case "ArrowStyle":
                            ArrowStyle ast;
                            if (Enum.TryParse(val, true, out ast)) config.ArrowStyle = ast;
                            break;
                        case "UnitMode":
                            UnitMode um;
                            if (Enum.TryParse(val, true, out um)) config.UnitMode = um;
                            break;
                        case "DecimalPlaces":
                            int dp; if (int.TryParse(val, out dp)) config.DecimalPlaces = dp;
                            break;
                        case "PaddingX":
                            int px; if (int.TryParse(val, out px)) config.PaddingX = px;
                            break;
                        case "PaddingY":
                            int py; if (int.TryParse(val, out py)) config.PaddingY = py;
                            break;
                        case "ItemSpacing":
                            int ispc; if (int.TryParse(val, out ispc)) config.ItemSpacing = ispc;
                            break;
                        case "ShowHoverHighlight":
                            bool hh; if (bool.TryParse(val, out hh)) config.ShowHoverHighlight = hh;
                            break;
                        case "IdleDimming":
                            bool idm; if (bool.TryParse(val, out idm)) config.IdleDimming = idm;
                            break;
                        case "IdleThresholdKb":
                            double itk; if (double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out itk)) config.IdleThresholdKb = itk;
                            break;
                        case "IdleOpacityPercent":
                            int iop; if (int.TryParse(val, out iop)) config.IdleOpacityPercent = Math.Max(10, Math.Min(100, iop));
                            break;
                        case "OverallOpacityPercent":
                            int oop; if (int.TryParse(val, out oop)) config.OverallOpacityPercent = Math.Max(20, Math.Min(100, oop));
                            break;
                        case "UpdateIntervalMs":
                            int ui; if (int.TryParse(val, out ui)) config.UpdateIntervalMs = Math.Max(250, ui);
                            break;
                        case "SelectedAdapterAlias":
                            config.SelectedAdapterAlias = val;
                            break;
                        case "StartWithWindows":
                            bool sww; if (bool.TryParse(val, out sww)) config.StartWithWindows = sww;
                            break;
                        case "LeftClickAction":
                            LeftClickAction lca;
                            if (Enum.TryParse(val, true, out lca)) config.LeftClickAction = lca;
                            break;
                        case "ShowOnAllMonitors":
                            bool sam; if (bool.TryParse(val, out sam)) config.ShowOnAllMonitors = sam;
                            break;
                        case "MonthlyDataQuotaGb":
                            double mdq; if (double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mdq)) config.MonthlyDataQuotaGb = mdq;
                            break;
                        case "EnableAutoUpdateCheck":
                            bool eau; if (bool.TryParse(val, out eau)) config.EnableAutoUpdateCheck = eau;
                            break;
                        case "EnableSilentAutoUpdate":
                            bool esu; if (bool.TryParse(val, out esu)) config.EnableSilentAutoUpdate = esu;
                            break;
                        case "IsFloatingMode":
                            bool ifm; if (bool.TryParse(val, out ifm)) config.IsFloatingMode = ifm;
                            break;
                        case "FloatingX":
                            int fx; if (int.TryParse(val, out fx)) config.FloatingX = fx;
                            break;
                        case "FloatingY":
                            int fy; if (int.TryParse(val, out fy)) config.FloatingY = fy;
                            break;
                        case "FloatingAlwaysOnTop":
                            bool faot; if (bool.TryParse(val, out faot)) config.FloatingAlwaysOnTop = faot;
                            break;
                        case "FloatingScale":
                            double fsc; if (double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fsc)) config.FloatingScale = fsc;
                            break;
                        case "FloatingLocked":
                            bool flk; if (bool.TryParse(val, out flk)) config.FloatingLocked = flk;
                            break;
                        case "TopAppsLimit":
                            int tal; if (int.TryParse(val, out tal)) config.TopAppsLimit = tal;
                            break;
                        case "SpeedTestDurationSeconds":
                            int std; if (int.TryParse(val, out std)) config.SpeedTestDurationSeconds = std;
                            break;
                        case "DisplayOnlyMode":
                            bool dom; if (bool.TryParse(val, out dom)) config.DisplayOnlyMode = dom;
                            break;
                        case "DisplayOnlyHotkey":
                            config.DisplayOnlyHotkey = val;
                            break;
                    }
                }
            }
            catch
            {
                // In case of parsing error, return defaults
            }

            return config;
        }

        public void Save()
        {
            Save(Config);
        }

        public void Save(AppConfig config)
        {
            if (config == null) return;
            Config = config;

            try
            {
                using (var sw = new StreamWriter(_configFilePath, false, System.Text.Encoding.UTF8))
                {
                    sw.WriteLine("{");
                    sw.WriteLine("  \"Position\": \"" + config.Position + "\",");
                    sw.WriteLine("  \"OffsetX\": " + config.OffsetX + ",");
                    sw.WriteLine("  \"OffsetY\": " + config.OffsetY + ",");
                    sw.WriteLine("  \"Layout\": \"" + config.Layout + "\",");
                    sw.WriteLine("  \"FontFamily\": \"" + config.FontFamily + "\",");
                    sw.WriteLine("  \"FontSize\": " + config.FontSize.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ",");
                    sw.WriteLine("  \"UnitFontSize\": " + config.UnitFontSize.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ",");
                    sw.WriteLine("  \"FontBold\": " + config.FontBold.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"AutoTheme\": " + config.AutoTheme.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"UploadColor\": \"" + config.UploadColor + "\",");
                    sw.WriteLine("  \"DownloadColor\": \"" + config.DownloadColor + "\",");
                    sw.WriteLine("  \"LabelColor\": \"" + config.LabelColor + "\",");
                    sw.WriteLine("  \"ArrowStyle\": \"" + config.ArrowStyle + "\",");
                    sw.WriteLine("  \"UnitMode\": \"" + config.UnitMode + "\",");
                    sw.WriteLine("  \"DecimalPlaces\": " + config.DecimalPlaces + ",");
                    sw.WriteLine("  \"PaddingX\": " + config.PaddingX + ",");
                    sw.WriteLine("  \"PaddingY\": " + config.PaddingY + ",");
                    sw.WriteLine("  \"ItemSpacing\": " + config.ItemSpacing + ",");
                    sw.WriteLine("  \"ShowHoverHighlight\": " + config.ShowHoverHighlight.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"IdleDimming\": " + config.IdleDimming.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"IdleThresholdKb\": " + config.IdleThresholdKb.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ",");
                    sw.WriteLine("  \"IdleOpacityPercent\": " + config.IdleOpacityPercent + ",");
                    sw.WriteLine("  \"OverallOpacityPercent\": " + config.OverallOpacityPercent + ",");
                    sw.WriteLine("  \"UpdateIntervalMs\": " + config.UpdateIntervalMs + ",");
                    sw.WriteLine("  \"SelectedAdapterAlias\": \"" + (config.SelectedAdapterAlias ?? "") + "\",");
                    sw.WriteLine("  \"StartWithWindows\": " + config.StartWithWindows.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"LeftClickAction\": \"" + config.LeftClickAction + "\",");
                    sw.WriteLine("  \"ShowOnAllMonitors\": " + config.ShowOnAllMonitors.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"MonthlyDataQuotaGb\": " + config.MonthlyDataQuotaGb.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ",");
                    sw.WriteLine("  \"EnableAutoUpdateCheck\": " + config.EnableAutoUpdateCheck.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"EnableSilentAutoUpdate\": " + config.EnableSilentAutoUpdate.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"IsFloatingMode\": " + config.IsFloatingMode.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"FloatingX\": " + config.FloatingX + ",");
                    sw.WriteLine("  \"FloatingY\": " + config.FloatingY + ",");
                    sw.WriteLine("  \"FloatingAlwaysOnTop\": " + config.FloatingAlwaysOnTop.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"FloatingScale\": " + config.FloatingScale.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",");
                    sw.WriteLine("  \"FloatingLocked\": " + config.FloatingLocked.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"TopAppsLimit\": " + config.TopAppsLimit + ",");
                    sw.WriteLine("  \"SpeedTestDurationSeconds\": " + config.SpeedTestDurationSeconds + ",");
                    sw.WriteLine("  \"DisplayOnlyMode\": " + config.DisplayOnlyMode.ToString().ToLowerInvariant() + ",");
                    sw.WriteLine("  \"DisplayOnlyHotkey\": \"" + (config.DisplayOnlyHotkey ?? "Ctrl + `") + "\"");
                    sw.WriteLine("}");
                }

                UpdateStartupRegistry(config.StartWithWindows);
            }
            catch
            {
            }
        }

        public void UpdateStartupRegistry(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Application.ExecutablePath;
                            key.SetValue(AppName, "\"" + exePath + "\"");
                        }
                        else
                        {
                            if (key.GetValue(AppName) != null)
                            {
                                key.DeleteValue(AppName, false);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
