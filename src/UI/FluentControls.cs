using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace MetriX.UI
{
    /// <summary>
    /// Modern Windows 11 Pill Toggle Switch.
    /// </summary>
    public class FluentToggleSwitch : Control
    {
        private bool _checked;
        private bool _isHovered;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public FluentToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(42, 22);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width - 2;
            int h = Height - 2;
            int radius = h / 2;

            using (var path = GetCapsulePath(1, 1, w, h, radius))
            {
                if (_checked)
                {
                    Color fillColor = _isHovered ? Color.FromArgb(16, 137, 245) : Color.FromArgb(0, 120, 215);
                    using (var brush = new SolidBrush(fillColor))
                    {
                        g.FillPath(brush, path);
                    }

                    // White knob on right
                    int knobDiam = h - 6;
                    int knobX = 1 + w - knobDiam - 3;
                    int knobY = 1 + 3;
                    using (var knobBrush = new SolidBrush(Color.White))
                    {
                        g.FillEllipse(knobBrush, knobX, knobY, knobDiam, knobDiam);
                    }
                }
                else
                {
                    Color fillColor = _isHovered ? Color.FromArgb(46, 46, 48) : Color.FromArgb(36, 36, 38);
                    Color borderColor = _isHovered ? Color.FromArgb(110, 110, 115) : Color.FromArgb(85, 85, 90);

                    using (var brush = new SolidBrush(fillColor))
                    {
                        g.FillPath(brush, path);
                    }

                    using (var pen = new Pen(borderColor, 1.2f))
                    {
                        g.DrawPath(pen, path);
                    }

                    // Grey knob on left
                    int knobDiam = h - 8;
                    int knobX = 1 + 4;
                    int knobY = 1 + 4;
                    Color knobColor = _isHovered ? Color.FromArgb(230, 230, 230) : Color.FromArgb(180, 180, 185);
                    using (var knobBrush = new SolidBrush(knobColor))
                    {
                        g.FillEllipse(knobBrush, knobX, knobY, knobDiam, knobDiam);
                    }
                }
            }
        }

        private static GraphicsPath GetCapsulePath(int x, int y, int width, int height, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(x, y, diameter, diameter);

            path.AddArc(arc, 90, 180);
            arc.X = x + width - diameter;
            path.AddArc(arc, 270, 180);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Modern Windows 11 Dark Numeric Stepper with [-] and [+] pills.
    /// </summary>
    public class FluentNumericStepper : Control
    {
        private decimal _value;
        private decimal _minimum = 0;
        private decimal _maximum = 100;
        private decimal _step = 1;
        private string _suffix = "";
        private int _decimalPlaces = 0;

        private bool _minusHovered;
        private bool _plusHovered;

        public event EventHandler ValueChanged;

        public decimal Value
        {
            get { return _value; }
            set
            {
                decimal clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }
        }

        public decimal Minimum
        {
            get { return _minimum; }
            set { _minimum = value; if (_value < _minimum) Value = _minimum; }
        }

        public decimal Maximum
        {
            get { return _maximum; }
            set { _maximum = value; if (_value > _maximum) Value = _maximum; }
        }

        public decimal Increment
        {
            get { return _step; }
            set { _step = value > 0 ? value : 1; }
        }

        public string Suffix
        {
            get { return _suffix; }
            set { _suffix = value ?? ""; Invalidate(); }
        }

        public int DecimalPlaces
        {
            get { return _decimalPlaces; }
            set { _decimalPlaces = Math.Max(0, value); Invalidate(); }
        }

        public FluentNumericStepper()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(116, 30);
            BackColor = Color.FromArgb(38, 38, 42);
            ForeColor = Color.FromArgb(240, 240, 240);
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.X < 30)
            {
                Value -= _step;
            }
            else if (e.X > Width - 30)
            {
                Value += _step;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool minus = e.X < 30;
            bool plus = e.X > Width - 30;

            if (minus != _minusHovered || plus != _plusHovered)
            {
                _minusHovered = minus;
                _plusHovered = plus;
                Cursor = (minus || plus) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _minusHovered = false;
            _plusHovered = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = Width - 1;
            int h = Height - 1;

            // Draw outer rounded frame
            using (var path = GetRoundedRect(0, 0, w, h, 4))
            {
                using (var bgBrush = new SolidBrush(BackColor))
                {
                    g.FillPath(bgBrush, path);
                }
                using (var borderPen = new Pen(Color.FromArgb(60, 60, 65), 1))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Draw minus button area
            if (_minusHovered)
            {
                using (var hovBrush = new SolidBrush(Color.FromArgb(55, 55, 60)))
                {
                    g.FillRectangle(hovBrush, 1, 1, 28, h - 1);
                }
            }
            using (var symBrush = new SolidBrush(Color.FromArgb(210, 210, 215)))
            using (var symFont = new Font("Segoe UI", 10.0f, FontStyle.Bold))
            {
                g.DrawString("−", symFont, symBrush, 9, 3);
            }

            // Draw plus button area
            if (_plusHovered)
            {
                using (var hovBrush = new SolidBrush(Color.FromArgb(55, 55, 60)))
                {
                    g.FillRectangle(hovBrush, w - 28, 1, 27, h - 1);
                }
            }
            using (var symBrush = new SolidBrush(Color.FromArgb(210, 210, 215)))
            using (var symFont = new Font("Segoe UI", 10.0f, FontStyle.Bold))
            {
                g.DrawString("+", symFont, symBrush, w - 21, 4);
            }

            // Draw dividers
            using (var divPen = new Pen(Color.FromArgb(55, 55, 60), 1))
            {
                g.DrawLine(divPen, 29, 3, 29, h - 3);
                g.DrawLine(divPen, w - 29, 3, w - 29, h - 3);
            }

            // Draw formatted value text
            string format = _decimalPlaces > 0 ? "F" + _decimalPlaces : "F0";
            string text = _value.ToString(format) + _suffix;
            SizeF sz = g.MeasureString(text, Font);
            float tx = (w - sz.Width) / 2;
            float ty = (h - sz.Height) / 2;
            using (var textBrush = new SolidBrush(ForeColor))
            {
                g.DrawString(text, Font, textBrush, tx, ty);
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

    /// <summary>
    /// Modern Windows 11 Card Container for clean vertical rhythm.
    /// </summary>
    public class FluentSettingCard : Panel
    {
        private readonly Label _lblTitle;
        private readonly Label _lblDesc;

        public string CardTitle
        {
            get { return _lblTitle.Text; }
            set { _lblTitle.Text = value; }
        }

        public string CardDescription
        {
            get { return _lblDesc.Text; }
            set { _lblDesc.Text = value; }
        }

        public FluentSettingCard(string title, string description, Control actionControl, int cardHeight = 60)
        {
            Size = new Size(470, cardHeight);
            BackColor = Color.FromArgb(32, 32, 36);

            _lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(14, 10),
                AutoSize = true
            };

            _lblDesc = new Label
            {
                Text = description,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 160, 165),
                Location = new Point(14, 30),
                AutoSize = true
            };

            Controls.Add(_lblTitle);
            Controls.Add(_lblDesc);

            if (actionControl != null)
            {
                actionControl.Location = new Point(Width - actionControl.Width - 14, (cardHeight - actionControl.Height) / 2);
                Controls.Add(actionControl);
            }

            Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int w = Width - 1;
                int h = Height - 1;
                using (var path = GetRoundedRect(0, 0, w, h, 6))
                using (var pen = new Pen(Color.FromArgb(48, 48, 52), 1))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };
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

