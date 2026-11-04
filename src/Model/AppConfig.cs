using System;

namespace MetriX.Model
{
    public enum PositionMode
    {
        LeftOfTray = 0,
        FarLeft = 1,
        RightOfApps = 2,
        Custom = 3
    }

    public enum LayoutMode
    {
        Stacked = 0,    // Two rows: Upload over Download (or vice-versa)
        Horizontal = 1  // Single row: Upload and Download side-by-side
    }

    public enum ArrowStyle
    {
        Triangle = 0,   // ▲ / ▼
        Arrow = 1,      // ↑ / ↓
        Letter = 2,     // U: / D:
        None = 3        // No prefix
    }

    public enum UnitMode
    {
        AutoBytes = 0,  // B/s, KB/s, MB/s, GB/s
        AutoBits = 1,   // bps, Kbps, Mbps, Gbps
        FixedKB = 2,    // Always KB/s
        FixedMB = 3     // Always MB/s
    }

    public enum LeftClickAction
    {
        Flyout = 0,
        TaskManager = 1,
        None = 2
    }

    public class AppConfig
    {
        public PositionMode Position { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }

        public LayoutMode Layout { get; set; }
        public string FontFamily { get; set; }
        public float FontSize { get; set; }
        public float UnitFontSize { get; set; }
        public bool FontBold { get; set; }

        public bool AutoTheme { get; set; }
        public string UploadColor { get; set; }
        public string DownloadColor { get; set; }
        public string LabelColor { get; set; }

        public ArrowStyle ArrowStyle { get; set; }
        public UnitMode UnitMode { get; set; }
        public int DecimalPlaces { get; set; }

        public int PaddingX { get; set; }
        public int PaddingY { get; set; }
        public int ItemSpacing { get; set; }
        public bool ShowHoverHighlight { get; set; }

        public bool IdleDimming { get; set; }
        public double IdleThresholdKb { get; set; }
        public int IdleOpacityPercent { get; set; }
        public int OverallOpacityPercent { get; set; }

        public int UpdateIntervalMs { get; set; }
        public string SelectedAdapterAlias { get; set; }
        public bool StartWithWindows { get; set; }
        public LeftClickAction LeftClickAction { get; set; }
        public bool ShowOnAllMonitors { get; set; }

        public AppConfig()
        {
            // Default native Windows 11 styling
            Position = PositionMode.LeftOfTray;
            OffsetX = 0;
            OffsetY = 0;

            Layout = LayoutMode.Stacked;
            FontFamily = "Segoe UI Variable Text";
            FontSize = 9.0f;
            UnitFontSize = 7.5f;
            FontBold = false;

            AutoTheme = true;
            // Fluent accent colors with high contrast on dark taskbar
            UploadColor = "#4CC2FF";    // Soft Windows Blue / Cyan
            DownloadColor = "#6CCB5F";  // Soft Windows Green
            LabelColor = "#D0D0D0";     // Crisp off-white

            ArrowStyle = ArrowStyle.Triangle;
            UnitMode = UnitMode.AutoBytes;
            DecimalPlaces = 1;

            PaddingX = 8;
            PaddingY = 2;
            ItemSpacing = 4;
            ShowHoverHighlight = true;

            IdleDimming = true;
            IdleThresholdKb = 1.0;
            IdleOpacityPercent = 45;
            OverallOpacityPercent = 100;

            UpdateIntervalMs = 1000;
            SelectedAdapterAlias = ""; // All active adapters aggregated
            StartWithWindows = false;
            LeftClickAction = LeftClickAction.Flyout;
            ShowOnAllMonitors = true;
        }
    }
}
