using System;
using System.Collections.Generic;
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
            int btnWidth = Math.Min(28, Width / 4);
            if (e.X < btnWidth)
            {
                Value -= _step;
            }
            else if (e.X > Width - btnWidth)
            {
                Value += _step;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int btnWidth = Math.Min(28, Width / 4);
            bool minus = e.X < btnWidth;
            bool plus = e.X > Width - btnWidth;

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

            int btnWidth = Math.Min(28, w / 4);

            // Draw minus button area
            if (_minusHovered)
            {
                using (var hovBrush = new SolidBrush(Color.FromArgb(55, 55, 60)))
                {
                    g.FillRectangle(hovBrush, 1, 1, btnWidth, h - 1);
                }
            }
            Rectangle minusRect = new Rectangle(0, 0, btnWidth, h);
            using (var symFont = new Font("Segoe UI", 10.0f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, "−", symFont, minusRect, Color.FromArgb(210, 210, 215),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }

            // Draw plus button area
            if (_plusHovered)
            {
                using (var hovBrush = new SolidBrush(Color.FromArgb(55, 55, 60)))
                {
                    g.FillRectangle(hovBrush, w - btnWidth, 1, btnWidth, h - 1);
                }
            }
            Rectangle plusRect = new Rectangle(w - btnWidth, 0, btnWidth, h);
            using (var symFont = new Font("Segoe UI", 10.0f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, "+", symFont, plusRect, Color.FromArgb(210, 210, 215),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }

            // Draw dividers
            using (var divPen = new Pen(Color.FromArgb(55, 55, 60), 1))
            {
                g.DrawLine(divPen, btnWidth, 3, btnWidth, h - 3);
                g.DrawLine(divPen, w - btnWidth, 3, w - btnWidth, h - 3);
            }

            // Draw formatted value text
            string format = _decimalPlaces > 0 ? "F" + _decimalPlaces : "F0";
            string text = _value.ToString(format) + _suffix;
            Rectangle valRect = new Rectangle(btnWidth + 2, 0, Math.Max(10, w - (btnWidth * 2) - 4), h);
            TextRenderer.DrawText(g, text, Font, valRect, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
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

        public FluentSettingCard(string title, string description, Control actionControl, int cardHeight = 66)
        {
            Size = new Size(540, cardHeight);
            BackColor = Color.FromArgb(32, 32, 36);

            if (actionControl != null)
            {
                actionControl.Location = new Point(Width - actionControl.Width - 16, (cardHeight - actionControl.Height) / 2);
                actionControl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                Controls.Add(actionControl);
                actionControl.BringToFront();
            }

            int maxTextWidth = (actionControl != null) ? (actionControl.Left - 28) : (Width - 32);
            if (maxTextWidth < 180) maxTextWidth = 180;

            _lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(16, 12),
                AutoSize = true,
                UseMnemonic = false,
                MaximumSize = new Size(maxTextWidth, 0)
            };

            _lblDesc = new Label
            {
                Text = description,
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 160, 165),
                Location = new Point(16, 33),
                AutoSize = true,
                UseMnemonic = false,
                MaximumSize = new Size(maxTextWidth, 0)
            };

            Controls.Add(_lblTitle);
            Controls.Add(_lblDesc);

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

    /// <summary>
    /// Modern Windows 11 Fluent Dropdown Control with acrylic menu popup.
    /// Eliminates legacy Win98/Win32 ComboBox white borders and text clipping.
    /// </summary>
    public class FluentDropdown : Control
    {
        private readonly List<string> _items = new List<string>();
        private int _selectedIndex = -1;
        private bool _isHovered;

        public event EventHandler SelectedIndexChanged;

        public IList<string> Items { get { return _items; } }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int clamped = Math.Max(-1, Math.Min(_items.Count - 1, value));
                if (_selectedIndex != clamped)
                {
                    _selectedIndex = clamped;
                    Invalidate();
                    if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
                }
            }
        }

        public string SelectedItem
        {
            get { return (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : null; }
            set
            {
                int idx = _items.IndexOf(value);
                if (idx >= 0) SelectedIndex = idx;
            }
        }

        public int FindStringExact(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i], s, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        public FluentDropdown()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(240, 30);
            BackColor = Color.FromArgb(36, 36, 40);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            Cursor = Cursors.Hand;
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

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ShowDropdownMenu();
        }

        private void ShowDropdownMenu()
        {
            if (_items.Count == 0) return;

            var menu = new ContextMenuStrip();
            menu.Renderer = new Windows11MenuRenderer();
            menu.ShowImageMargin = false;

            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                string text = _items[i];
                var item = new ToolStripMenuItem(text);
                item.Font = Font;
                if (index == _selectedIndex)
                {
                    item.Font = new Font(Font, FontStyle.Bold);
                    item.Checked = true;
                }
                item.Click += (s, ev) =>
                {
                    SelectedIndex = index;
                };
                menu.Items.Add(item);
            }

            Point pt = new Point(0, Height + 2);
            menu.Show(this, pt);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = Width - 1;
            int h = Height - 1;

            Color bg = _isHovered ? Color.FromArgb(48, 48, 54) : BackColor;
            Color border = _isHovered ? Color.FromArgb(80, 80, 88) : Color.FromArgb(55, 55, 62);

            using (var path = GetRoundedRect(0, 0, w, h, 4))
            {
                using (var brush = new SolidBrush(bg))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(border, 1.2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Draw selected text
            string text = (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : "";
            Rectangle textRect = new Rectangle(10, 0, Math.Max(10, w - 32), h);
            TextRenderer.DrawText(g, text, Font, textRect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // Draw drop chevron (v)
            int cx = w - 14;
            int cy = h / 2;
            using (var pen = new Pen(Color.FromArgb(200, 200, 205), 1.3f))
            {
                g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
                g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
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

