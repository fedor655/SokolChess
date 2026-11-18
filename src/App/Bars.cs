using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Sokol.Engine;

namespace Sokol.App
{
    /// <summary>Name plate above/below the board: avatar, name, rating, captured pieces, "thinking" indicator.</summary>
    sealed class PlayerBar : PaintedControl
    {
        public string PlayerName = "";
        public string Subtitle = "";
        public bool IsBot;
        public int Color = Piece.White;
        public bool Thinking;
        public bool ToMove;
        /// <summary>Enemy pieces captured by this player (piece codes).</summary>
        public readonly List<int> Captured = new List<int>();
        public int MaterialLead;
        readonly Timer timer = new Timer { Interval = 300 };
        int dots;

        public PlayerBar()
        {
            BackColor = Theme.WindowBg;
            timer.Tick += (s, e) => { dots = (dots + 1) % 4; Invalidate(); };
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.WindowBg);
            int h = Height;
            int av = h - Theme.S(4);
            var avRect = new RectangleF(0, (h - av) / 2f, av, av);
            Theme.FillRound(g, IsBot ? System.Drawing.Color.FromArgb(70, 110, 160) : System.Drawing.Color.FromArgb(95, 140, 60), avRect, Theme.S(5));
            var img = PieceRenderer.Get(Piece.Make(Color, IsBot ? Piece.Knight : Piece.Pawn), (int)(av * 0.9f));
            g.DrawImage(img, avRect.X + av * 0.05f, avRect.Y + av * 0.05f, av * 0.9f, av * 0.9f);

            float x = av + Theme.S(10);
            var left = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            SizeF ns = g.MeasureString(PlayerName, Theme.UiBold);
            using (var b = new SolidBrush(Theme.Text))
                g.DrawString(PlayerName, Theme.UiBold, b, new RectangleF(x, 0, ns.Width + 4, h * 0.55f), left);
            if (Subtitle.Length > 0)
                using (var b = new SolidBrush(Theme.TextMuted))
                    g.DrawString(Subtitle, Theme.UiSmall, b, new RectangleF(x + ns.Width + Theme.S(2), 0, Theme.S(260), h * 0.55f), left);

            // Captured pieces, grouped by type, slightly overlapping.
            float cx = x, cy = h * 0.52f;
            int ps = (int)(h * 0.42f);
            int prevType = -1;
            foreach (int p in Captured)
            {
                int t = Piece.TypeOf(p);
                if (prevType >= 0) cx += t == prevType ? ps * 0.45f : ps * 0.85f;
                g.DrawImage(PieceRenderer.Get(p, ps), cx, cy, ps, ps);
                prevType = t;
            }
            if (Captured.Count > 0) cx += ps;
            if (MaterialLead > 0)
                using (var b = new SolidBrush(Theme.TextDim))
                    g.DrawString("+" + MaterialLead, Theme.UiSmallBold, b, new RectangleF(cx + Theme.S(3), cy, Theme.S(40), ps), left);

            if (Thinking)
            {
                string t = "думает" + new string('.', dots);
                using (var b = new SolidBrush(Theme.TextDim))
                    g.DrawString(t, Theme.UiSmall, b, new RectangleF(Width - Theme.S(110), 0, Theme.S(110), h),
                        new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
            }
            else if (ToMove)
            {
                float d = Theme.S(8);
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillEllipse(b, Width - d - Theme.S(4), (h - d) / 2f, d, d);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>End-of-game card shown over the board.</summary>
    sealed class ResultOverlay : PaintedControl
    {
        public string Title = "", Subtitle = "";
        public Color TitleColor = Theme.Text;
        public readonly FlatButton ReviewButton = new FlatButton { Text = "Анализ партии", Primary = true };
        public readonly FlatButton NewGameButton = new FlatButton { Text = "Новая игра" };
        public readonly FlatButton CloseButton = new FlatButton { Text = "✕" };

        public ResultOverlay()
        {
            BackColor = Theme.PanelBg;
            Controls.Add(ReviewButton);
            Controls.Add(NewGameButton);
            Controls.Add(CloseButton);
            CloseButton.Click += (s, e) => Visible = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int pad = Theme.S(18);
            CloseButton.SetBounds(Width - Theme.S(36) - Theme.S(6), Theme.S(6), Theme.S(36), Theme.S(32));
            ReviewButton.SetBounds(pad, Height - pad - Theme.S(44) * 2 - Theme.S(10), Width - pad * 2, Theme.S(44));
            NewGameButton.SetBounds(pad, Height - pad - Theme.S(44), Width - pad * 2, Theme.S(44));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.PanelBg);
            using (var p = new Pen(Theme.Border, 2)) g.DrawRectangle(p, 1, 1, Width - 2, Height - 2);
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var b = new SolidBrush(TitleColor))
                g.DrawString(Title, Theme.UiTitle, b, new RectangleF(0, Theme.S(26), Width, Theme.S(40)), center);
            using (var b = new SolidBrush(Theme.TextDim))
                g.DrawString(Subtitle, Theme.UiFont, b, new RectangleF(Theme.S(12), Theme.S(66), Width - Theme.S(24), Theme.S(44)), center);
        }
    }
}
