using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace MetriX.UI
{
    /// <summary>
    /// Custom 3-column quick-action bar for Windows 11 Fluent Context Menu.
    /// Inherits from ToolStripMenuItem to guarantee edge-to-edge full width stretching (Width = ClientSize.Width)
    /// and perfect horizontal alignment with Float Widget and other menu pills.
    /// </summary>
    public class QuickActionBarItem : ToolStripMenuItem
    {
        private int _hoveredBox = -1; // -1: none, 0: Flyout, 1: Settings, 2: Updates
        private int _lastToolTipBox = -1;
        private ToolTip _toolTip;

        public Action OnFlyoutClick;
        public Action OnSettingsClick;
        public Action OnUpdatesClick;

        public QuickActionBarItem()
        {
            Padding = new Padding(0, 4, 0, 4);
            Margin = Padding.Empty;
            AutoSize = true;
        }

        public override Size GetPreferredSize(Size constrainingSize)
        {
            var baseSize = base.GetPreferredSize(constrainingSize);
            return new Size(baseSize.Width, 36);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = GetBoxIndex(e.X);
            if (idx != _hoveredBox)
            {
                _hoveredBox = idx;
                Invalidate();
                UpdateToolTip(idx);
            }
            if (Owner != null)
            {
                Owner.Cursor = (idx >= 0) ? Cursors.Hand : Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredBox != -1)
            {
                _hoveredBox = -1;
                Invalidate();
                UpdateToolTip(-1);
            }
            if (Owner != null)
            {
                Owner.Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                int idx = GetBoxIndex(e.X);
                if (idx < 0) return;

                var dropDown = Owner as ToolStripDropDown;
                if (dropDown != null) dropDown.Close();

                if (idx == 0 && OnFlyoutClick != null) OnFlyoutClick();
                else if (idx == 1 && OnSettingsClick != null) OnSettingsClick();
                else if (idx == 2 && OnUpdatesClick != null) OnUpdatesClick();
            }
        }

        private void UpdateToolTip(int idx)
        {
            if (idx == _lastToolTipBox) return;
            _lastToolTipBox = idx;

            if (_toolTip == null)
            {
                _toolTip = new ToolTip
                {
                    InitialDelay = 350,
                    ReshowDelay = 100,
                    AutoPopDelay = 3000
                };
            }

            if (Owner != null)
            {
                string tipText = null;
                if (idx == 0) tipText = "Network Graph & Speed Test";
                else if (idx == 1) tipText = "Settings & Customization";
                else if (idx == 2) tipText = "Check for Updates";

                if (tipText != null)
                {
                    _toolTip.SetToolTip(Owner, tipText);
                }
                else
                {
                    _toolTip.SetToolTip(Owner, null);
                }
            }
        }

        private int GetBoxIndex(int x)
        {
            int w = Width;
            int margin = 4;
            int gap = 4;
            int usableW = w - (margin * 2) - (gap * 2);
            int bWidth = usableW / 3;

            int b0Start = margin;
            int b0End = b0Start + bWidth;
            int b1Start = b0End + gap;
            int b1End = b1Start + bWidth;
            int b2Start = b1End + gap;
            int b2End = w - margin;

            if (x >= b0Start && x < b0End) return 0;
            if (x >= b1Start && x < b1End) return 1;
            if (x >= b2Start && x <= b2End) return 2;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;

            // Fill background matching context menu
            using (var bgBrush = new SolidBrush(Color.FromArgb(32, 32, 35)))
            {
                g.FillRectangle(bgBrush, 0, 0, w, h);
            }

            int margin = 4;
            int gap = 4;
            int usableW = w - (margin * 2) - (gap * 2);
            int bWidth = usableW / 3;

            Rectangle[] boxes = new Rectangle[3];
            boxes[0] = new Rectangle(margin, 4, bWidth, h - 8);
            boxes[1] = new Rectangle(margin + bWidth + gap, 4, bWidth, h - 8);
            int b2X = margin + (bWidth + gap) * 2;
            boxes[2] = new Rectangle(b2X, 4, w - margin - b2X, h - 8);

            string[] glyphs = new string[] { "\uD83D\uDCCA", "\u2699", "\uD83D\uDD04" };

            using (var boxBgNormal = new SolidBrush(Color.FromArgb(40, 40, 46)))
            using (var boxBgHover = new SolidBrush(Color.FromArgb(52, 52, 60)))
            using (var borderPenNormal = new Pen(Color.FromArgb(56, 56, 62), 1))
            using (var borderPenHover = new Pen(Color.FromArgb(80, 80, 90), 1))
            using (var iconFont = new Font("Segoe UI Emoji", 9.5f, FontStyle.Regular))
            using (var textBrush = new SolidBrush(Color.FromArgb(235, 235, 240)))
            {
                for (int i = 0; i < 3; i++)
                {
                    Rectangle r = boxes[i];
                    bool isHovered = (_hoveredBox == i);

                    using (var path = GetRoundedRect(r.X, r.Y, r.Width - 1, r.Height - 1, 3))
                    {
                        g.FillPath(isHovered ? boxBgHover : boxBgNormal, path);
                        g.DrawPath(isHovered ? borderPenHover : borderPenNormal, path);
                    }

                    // Draw centered glyph
                    SizeF sz = g.MeasureString(glyphs[i], iconFont);
                    float gx = r.X + (r.Width - sz.Width) / 2.0f;
                    float gy = r.Y + (r.Height - sz.Height) / 2.0f;
                    g.DrawString(glyphs[i], iconFont, textBrush, gx, gy);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_toolTip != null)
                {
                    _toolTip.Dispose();
                    _toolTip = null;
                }
            }
            base.Dispose(disposing);
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
