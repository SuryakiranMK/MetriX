using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using MetriX.Native;

namespace MetriX.UI
{
    /// <summary>
    /// Custom Windows 11 Fluent Context Menu Renderer.
    /// Implements rounded selection pills, Fluent checkmark glyphs, DWM rounded corners, and Acrylic-ready styling.
    /// </summary>
    public class Windows11MenuRenderer : ToolStripRenderer
    {
        private static readonly Color BgColor = Color.FromArgb(32, 32, 35);
        private static readonly Color BorderColor = Color.FromArgb(50, 50, 55);
        private static readonly Color HoverPillColor = Color.FromArgb(50, 50, 55);
        private static readonly Color TextColor = Color.FromArgb(240, 240, 245);
        private static readonly Color SubTextColor = Color.FromArgb(160, 160, 165);
        private static readonly Color AccentColor = Color.FromArgb(76, 194, 255);
        private static readonly Color SeparatorColor = Color.FromArgb(46, 46, 50);

        protected override void Initialize(ToolStrip toolStrip)
        {
            base.Initialize(toolStrip);
            toolStrip.BackColor = BgColor;
            toolStrip.ForeColor = TextColor;
            toolStrip.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            var dropDown = toolStrip as ToolStripDropDownMenu;
            if (dropDown != null)
            {
                dropDown.ShowImageMargin = false;
                dropDown.ShowCheckMargin = false;
            }

            toolStrip.HandleCreated += (s, e) =>
            {
                ApplyDwmCornerPreference(toolStrip.Handle);
            };
        }

        public static void ApplyDwmCornerPreference(IntPtr hWnd)
        {
            try
            {
                int cornerPref = NativeMethods.DWMWCP_ROUNDSMALL;
                NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

                int darkMode = 1;
                NativeMethods.DwmSetWindowAttribute(hWnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }
            catch
            {
            }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            using (var brush = new SolidBrush(BgColor))
            {
                g.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            using (var pen = new Pen(BorderColor, 1))
            {
                g.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle pillRect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);

            if (e.Item.Selected && e.Item.Enabled)
            {
                // Draw sleek rounded selection pill
                using (var path = GetRoundedRect(pillRect.X, pillRect.Y, pillRect.Width, pillRect.Height, 4))
                using (var brush = new SolidBrush(HoverPillColor))
                {
                    g.FillPath(brush, path);
                }
            }

            // Custom Item Outlines
            string tag = e.Item.Tag as string;
            if (!string.IsNullOrEmpty(tag))
            {
                Color outlineCol = Color.Empty;
                if (tag == "Exit")
                {
                    outlineCol = Color.FromArgb(232, 17, 35); // Sleek red
                }
                else if (tag == "Docked")
                {
                    outlineCol = Color.FromArgb(0, 150, 255); // Taskbar blue/cyan
                }
                else if (tag == "Detached")
                {
                    outlineCol = Color.FromArgb(108, 203, 95); // Floating green
                }

                if (outlineCol != Color.Empty)
                {
                    using (var path = GetRoundedRect(pillRect.X, pillRect.Y, pillRect.Width, pillRect.Height, 4))
                    using (var pen = new Pen(outlineCol, 1.25f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Indent slightly if checked or if checking is supported
            int leftPad = 26;
            Rectangle textRect = new Rectangle(leftPad, e.TextRectangle.Y, e.Item.Width - leftPad - 16, e.TextRectangle.Height);

            Color col = e.Item.Enabled ? TextColor : SubTextColor;
            using (var brush = new SolidBrush(col))
            {
                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(g, e.Text, e.TextFont, textRect, col, flags);
            }

            // If checked, draw a sleek Fluent checkmark glyph
            var mi = e.Item as ToolStripMenuItem;
            if (mi != null && mi.Checked)
            {
                using (var chkFont = new Font("Segoe UI", 9.0f, FontStyle.Bold))
                using (var chkBrush = new SolidBrush(AccentColor))
                {
                    g.DrawString("✓", chkFont, chkBrush, 8, e.TextRectangle.Y + (e.TextRectangle.Height - 16) / 2);
                }
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            int y = e.Item.Height / 2;
            using (var pen = new Pen(SeparatorColor, 1))
            {
                g.DrawLine(pen, 12, y, e.Item.Width - 12, y);
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw sleek chevron '>'
            using (var pen = new Pen(SubTextColor, 1.6f))
            {
                int x = e.ArrowRectangle.X + 2;
                int y = e.ArrowRectangle.Y + (e.ArrowRectangle.Height / 2);
                g.DrawLine(pen, x, y - 4, x + 4, y);
                g.DrawLine(pen, x + 4, y, x, y + 4);
            }
        }

        private static GraphicsPath GetRoundedRect(int x, int y, int width, int height, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(x, y, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = x + width - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = y + height - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = x;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
