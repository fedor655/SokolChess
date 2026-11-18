using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sokol.App
{
    /// <summary>Base for custom-painted, double-buffered controls.</summary>
    class PaintedControl : Control
    {
        protected bool hover, pressed;

        public PaintedControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.PanelBg;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        }
    }

    /// <summary>Flat rounded button with a darker bottom edge.</summary>
    class FlatButton : PaintedControl
    {
        public bool Primary;
        /// <summary>Optional symbol drawn before the text (Segoe UI Symbol).</summary>
        public string Icon = "";
        public string Tooltip = "";

        public FlatButton()
        {
            Cursor = Cursors.Hand;
            Height = Theme.S(40);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Smooth(g);
            g.Clear(Parent?.BackColor ?? Theme.PanelBg);
            Color face = Primary ? (hover && Enabled ? Theme.AccentHover : Theme.Accent) : (hover && Enabled ? Theme.ButtonHover : Theme.Button);
            Color shadow = Primary ? Theme.AccentShadow : Theme.ButtonShadow;
            if (!Enabled) { face = Theme.Blend(face, Theme.PanelBg, 0.55f); shadow = Theme.Blend(shadow, Theme.PanelBg, 0.55f); }
            float r = Theme.S(6);
            float off = pressed ? Theme.S(2) : 0;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.FillRound(g, shadow, rect, r);
            Theme.FillRound(g, face, new RectangleF(rect.X, rect.Y + off, rect.Width, rect.Height - Theme.S(3)), r);

            Color fg = Enabled ? (Primary ? Color.White : Theme.Text) : Theme.TextMuted;
            var textRect = new RectangleF(0, off, Width, Height - Theme.S(3));
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            using (var brush = new SolidBrush(fg))
            {
                if (Icon.Length > 0)
                {
                    using (var iconFont = new Font("Segoe UI Symbol", Font.Size * 1.1f, FontStyle.Regular))
                    {
                        SizeF ts = Text.Length > 0 ? g.MeasureString(Text, Font) : SizeF.Empty;
                        SizeF isz = g.MeasureString(Icon, iconFont);
                        float gap = -Theme.S(1);
                        float total = isz.Width + (Text.Length > 0 ? ts.Width + gap : 0);
                        float x = (Width - total) / 2;
                        g.DrawString(Icon, iconFont, brush, new RectangleF(x, textRect.Y, isz.Width, textRect.Height),
                            new StringFormat { LineAlignment = StringAlignment.Center });
                        if (Text.Length > 0)
                            g.DrawString(Text, Font, brush, new RectangleF(x + isz.Width + gap, textRect.Y, ts.Width + 2, textRect.Height),
                                new StringFormat { LineAlignment = StringAlignment.Center });
                    }
                }
                else g.DrawString(Text, Font, brush, textRect, sf);
            }
        }
    }

    /// <summary>Horizontal slider with rounded track and a round thumb.</summary>
    class Slider : PaintedControl
    {
        int value = 1200;
        public int Minimum = 200, Maximum = 3500, Step = 50;
        public event EventHandler ValueChanged;
        bool dragging;

        public Slider()
        {
            Height = Theme.S(28);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public int Value
        {
            get => value;
            set
            {
                int v = Math.Max(Minimum, Math.Min(Maximum, (int)Math.Round((double)value / Step) * Step));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        float TrackLeft => Theme.S(10);
        float TrackRight => Width - Theme.S(10);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Smooth(g);
            g.Clear(Parent?.BackColor ?? Theme.PanelBg);
            float cy = Height / 2f, th = Theme.S(6);
            float t = (float)(value - Minimum) / (Maximum - Minimum);
            float x = TrackLeft + (TrackRight - TrackLeft) * t;
            Theme.FillRound(g, Theme.Button, new RectangleF(TrackLeft, cy - th / 2, TrackRight - TrackLeft, th), th / 2);
            Theme.FillRound(g, Theme.Accent, new RectangleF(TrackLeft, cy - th / 2, x - TrackLeft, th), th / 2);
            float rad = Theme.S(hover || dragging ? 10 : 9);
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, x - rad, cy - rad, rad * 2, rad * 2);
            using (var p = new Pen(Theme.AccentShadow, Theme.S(2)))
                g.DrawEllipse(p, x - rad, cy - rad, rad * 2, rad * 2);
        }

        void SetFromX(int x)
        {
            float t = (x - TrackLeft) / Math.Max(1f, TrackRight - TrackLeft);
            t = Math.Max(0, Math.Min(1, t));
            Value = (int)(Minimum + t * (Maximum - Minimum));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button == MouseButtons.Left) { dragging = true; SetFromX(e.X); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value = value + Math.Sign(e.Delta) * Step;
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value = value - Step;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value = value + Step;
        }
    }

    /// <summary>On/off switch with a caption.</summary>
    class ToggleSwitch : PaintedControl
    {
        bool isChecked = true;
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            Height = Theme.S(28);
            Cursor = Cursors.Hand;
            Font = Theme.UiSmall;
        }

        public bool Checked
        {
            get => isChecked;
            set { if (isChecked == value) return; isChecked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Smooth(g);
            g.Clear(Parent?.BackColor ?? Theme.PanelBg);
            float w = Theme.S(34), h = Theme.S(18), y = (Height - h) / 2f;
            Theme.FillRound(g, isChecked ? Theme.Accent : Theme.Button, new RectangleF(0, y, w, h), h / 2);
            float knob = h - Theme.S(4);
            float kx = isChecked ? w - knob - Theme.S(2) : Theme.S(2);
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, kx, y + Theme.S(2), knob, knob);
            using (var b = new SolidBrush(hover ? Theme.Text : Theme.TextDim))
                g.DrawString(Text, Font, b, new RectangleF(w + Theme.S(8), 0, Width - w - Theme.S(8), Height),
                    new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
        }
    }

    /// <summary>Row of mutually exclusive options.</summary>
    class SegmentedControl : PaintedControl
    {
        public string[] Items = new string[0];
        int selected;
        int hoverIndex = -1;
        public event EventHandler SelectedIndexChanged;

        public SegmentedControl()
        {
            Height = Theme.S(34);
            Cursor = Cursors.Hand;
            Font = Theme.UiSmallBold;
        }

        public int SelectedIndex
        {
            get => selected;
            set { if (selected == value) return; selected = value; Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); }
        }

        int IndexAt(int x) => Items.Length == 0 ? -1 : Math.Max(0, Math.Min(Items.Length - 1, x * Items.Length / Math.Max(1, Width)));

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int i = IndexAt(e.X); if (i != hoverIndex) { hoverIndex = i; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { hoverIndex = -1; base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); if (Enabled) SelectedIndex = IndexAt(e.X); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Smooth(g);
            g.Clear(Parent?.BackColor ?? Theme.PanelBg);
            Theme.FillRound(g, Theme.CardBg, new RectangleF(0, 0, Width - 1, Height - 1), Theme.S(6));
            if (Items.Length == 0) return;
            float w = (float)Width / Items.Length;
            for (int i = 0; i < Items.Length; i++)
            {
                var r = new RectangleF(i * w + Theme.S(3), Theme.S(3), w - Theme.S(6), Height - Theme.S(7));
                if (i == selected) Theme.FillRound(g, Enabled ? Theme.Accent : Theme.Button, r, Theme.S(5));
                else if (i == hoverIndex && Enabled) Theme.FillRound(g, Theme.Button, r, Theme.S(5));
                Color fg = i == selected ? Color.White : (Enabled ? Theme.TextDim : Theme.TextMuted);
                using (var b = new SolidBrush(fg))
                    g.DrawString(Items[i], Font, b, r, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
            }
        }
    }

    /// <summary>Static text block that wraps and paints on the panel background.</summary>
    class TextBlock : PaintedControl
    {
        public Color TextColor = Theme.Text;
        public ContentAlignment Align = ContentAlignment.MiddleLeft;
        public bool Wrap = true;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            var sf = new StringFormat { Trimming = StringTrimming.EllipsisWord };
            if (!Wrap) sf.FormatFlags = StringFormatFlags.NoWrap;
            sf.Alignment = Align == ContentAlignment.MiddleCenter || Align == ContentAlignment.TopCenter ? StringAlignment.Center :
                           Align == ContentAlignment.MiddleRight || Align == ContentAlignment.TopRight ? StringAlignment.Far : StringAlignment.Near;
            sf.LineAlignment = Align == ContentAlignment.TopLeft || Align == ContentAlignment.TopCenter || Align == ContentAlignment.TopRight
                ? StringAlignment.Near : StringAlignment.Center;
            using (var b = new SolidBrush(TextColor))
                g.DrawString(Text, Font, b, new RectangleF(0, 0, Width, Height), sf);
        }
    }
}
