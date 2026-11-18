using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Sokol.Engine;

namespace Sokol.App
{
    /// <summary>Vertical evaluation bar (White's share grows from White's side of the board).</summary>
    sealed class EvalBar : PaintedControl
    {
        public bool Flipped;
        float shown = 0.5f, target = 0.5f, from = 0.5f;
        string label = "0.0";
        bool whiteBetter = true;
        DateTime start;
        readonly Timer timer = new Timer { Interval = 15 };

        public EvalBar()
        {
            timer.Tick += (s, e) =>
            {
                double t = Math.Min(1, (DateTime.Now - start).TotalMilliseconds / 350.0);
                shown = (float)(from + (target - from) * (1 - Math.Pow(1 - t, 3)));
                if (t >= 1) timer.Stop();
                Invalidate();
            };
        }

        public void SetScore(int whiteScore)
        {
            float v = (float)(0.5 + 0.5 * Review.WinChance(whiteScore));
            if (!Score.IsMate(whiteScore)) v = Math.Max(0.04f, Math.Min(0.96f, v));
            if (Score.IsMate(whiteScore)) label = "M" + Math.Abs(Score.MateInMoves(whiteScore));
            else label = (Math.Abs(whiteScore) / 100.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            whiteBetter = whiteScore >= 0;
            if (Math.Abs(v - target) < 0.0001f) { Invalidate(); return; }
            from = shown;
            target = v;
            start = DateTime.Now;
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.WindowBg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0, 0, Width, Height);
            using (var clip = Theme.RoundRect(r, Theme.S(3)))
            {
                g.SetClip(clip);
                using (var b = new SolidBrush(Color.FromArgb(64, 61, 57))) g.FillRectangle(b, r);
                float wh = Height * shown;
                var wr = Flipped ? new RectangleF(0, 0, Width, wh) : new RectangleF(0, Height - wh, Width, wh);
                using (var b = new SolidBrush(Color.FromArgb(240, 240, 240))) g.FillRectangle(b, wr);
                using (var p = new Pen(Color.FromArgb(120, 200, 80, 60), 1)) g.DrawLine(p, 0, Height / 2f, Width, Height / 2f);
                g.ResetClip();
            }
            // White's number sits on White's end of the bar.
            bool atBottom = whiteBetter != Flipped;
            using (var f = new Font("Segoe UI Semibold", Math.Max(6f, Width * 0.30f), GraphicsUnit.Pixel))
            using (var b = new SolidBrush(whiteBetter ? Color.FromArgb(50, 50, 50) : Color.FromArgb(235, 235, 235)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = atBottom ? StringAlignment.Far : StringAlignment.Near };
                g.DrawString(label, f, b, new RectangleF(-4, Theme.S(4), Width + 8, Height - Theme.S(8)), sf);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Scrollable two-column move list (№ | White | Black) with classification badges.</summary>
    sealed class MoveListControl : PaintedControl
    {
        public readonly List<string> Sans = new List<string>();
        public readonly List<MoveClass> Classes = new List<MoveClass>();
        /// <summary>Index of the highlighted move (-1 = none).</summary>
        public int SelectedMove = -1;
        public event Action<int> MoveClicked;
        int scroll;
        int hoverMove = -1;

        int RowH => Theme.S(30);
        int Rows => (Sans.Count + 1) / 2;
        int MaxScroll => Math.Max(0, Rows * RowH - Height);

        public void ScrollToMove(int index)
        {
            if (index < 0) { scroll = 0; Invalidate(); return; }
            int row = index / 2;
            int top = row * RowH, bottom = top + RowH;
            if (top < scroll) scroll = top;
            else if (bottom > scroll + Height) scroll = bottom - Height;
            scroll = Math.Max(0, Math.Min(MaxScroll, scroll));
            Invalidate();
        }

        public void ScrollToEnd()
        {
            scroll = MaxScroll;
            Invalidate();
        }

        Rectangle CellRect(int moveIndex)
        {
            int row = moveIndex / 2;
            int numW = Theme.S(46);
            int colW = (Width - numW - Theme.S(10)) / 2;
            int x = numW + (moveIndex % 2) * colW;
            return new Rectangle(x, row * RowH - scroll, colW, RowH);
        }

        int MoveAt(Point p)
        {
            for (int i = 0; i < Sans.Count; i++)
                if (CellRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.CardBg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int rowH = RowH;
            var sfLeft = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
            if (Sans.Count == 0)
            {
                using (var b = new SolidBrush(Theme.TextMuted))
                    g.DrawString("Ходы появятся здесь", Theme.UiSmall, b, new RectangleF(0, 0, Width, Theme.S(40)),
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                return;
            }
            for (int row = 0; row < Rows; row++)
            {
                int y = row * rowH - scroll;
                if (y + rowH < 0 || y > Height) continue;
                if (row % 2 == 1) using (var b = new SolidBrush(Theme.RowAlt)) g.FillRectangle(b, 0, y, Width, rowH);
                using (var b = new SolidBrush(Theme.TextMuted))
                    g.DrawString((row + 1) + ".", Theme.MoveFont, b, new RectangleF(Theme.S(8), y, Theme.S(40), rowH), sfLeft);
                for (int k = 0; k < 2; k++)
                {
                    int i = row * 2 + k;
                    if (i >= Sans.Count) break;
                    var cell = CellRect(i);
                    bool sel = i == SelectedMove;
                    if (sel) Theme.FillRound(g, Theme.RowHighlight, new RectangleF(cell.X, cell.Y + 3, cell.Width - Theme.S(6), cell.Height - 6), Theme.S(4));
                    else if (i == hoverMove) Theme.FillRound(g, Color.FromArgb(55, 53, 50), new RectangleF(cell.X, cell.Y + 3, cell.Width - Theme.S(6), cell.Height - 6), Theme.S(4));
                    float tx = cell.X + Theme.S(6);
                    MoveClass c = i < Classes.Count ? Classes[i] : MoveClass.None;
                    if (c != MoveClass.None)
                    {
                        float d = rowH * 0.56f;
                        Theme.DrawBadge(g, c, tx + d / 2, cell.Y + rowH / 2f, d);
                        tx += d + Theme.S(5);
                    }
                    using (var b = new SolidBrush(sel ? Color.White : Theme.Text))
                        g.DrawString(Sans[i], sel ? Theme.MoveFontBold : Theme.MoveFont, b, new RectangleF(tx, cell.Y, cell.Right - tx, rowH), sfLeft);
                }
            }
            // Thin scrollbar.
            if (MaxScroll > 0)
            {
                float total = Rows * rowH;
                float h = Math.Max(Theme.S(20), Height * Height / total);
                float y = (Height - h) * scroll / MaxScroll;
                Theme.FillRound(g, Color.FromArgb(90, 255, 255, 255), new RectangleF(Width - Theme.S(5), y, Theme.S(3), h), Theme.S(1.5f));
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll = Math.Max(0, Math.Min(MaxScroll, scroll - Math.Sign(e.Delta) * RowH * 2));
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int m = MoveAt(e.Location);
            if (m != hoverMove) { hoverMove = m; Cursor = m >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverMove = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Focus();
            int m = MoveAt(e.Location);
            if (m >= 0) MoveClicked?.Invoke(m);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            scroll = Math.Max(0, Math.Min(MaxScroll, scroll));
        }
    }

    /// <summary>Evaluation graph over the whole game (White area from the bottom).</summary>
    sealed class EvalGraph : PaintedControl
    {
        /// <summary>White-POV score per ply (null = not analyzed yet).</summary>
        public readonly List<int?> Scores = new List<int?>();
        public readonly List<MoveClass> Classes = new List<MoveClass>();
        public int CurrentPly;
        public event Action<int> PlyClicked;

        float X(int ply) => Scores.Count <= 1 ? 0 : (float)ply * (Width - 1) / (Scores.Count - 1);
        float Y(int score) => (float)(Height * (0.5 - 0.5 * Review.WinChance(score)) * 0.92 + Height * 0.04);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.CardBg);
            using (var clip = Theme.RoundRect(new RectangleF(0, 0, Width, Height), Theme.S(6)))
            {
                g.SetClip(clip);
                using (var b = new SolidBrush(Color.FromArgb(64, 61, 57))) g.FillRectangle(b, 0, 0, Width, Height);
                if (Scores.Count >= 2)
                {
                    var pts = new List<PointF> { new PointF(0, Height) };
                    int last = 0;
                    for (int i = 0; i < Scores.Count; i++)
                    {
                        int s = Scores[i] ?? last;
                        last = s;
                        pts.Add(new PointF(X(i), Y(s)));
                    }
                    pts.Add(new PointF(X(Scores.Count - 1), Height));
                    using (var b = new SolidBrush(Color.FromArgb(235, 235, 235))) g.FillPolygon(b, pts.ToArray());
                }
                using (var p = new Pen(Color.FromArgb(110, 128, 128, 128), 1)) g.DrawLine(p, 0, Height / 2f, Width, Height / 2f);
                if (Scores.Count >= 2)
                {
                    float cx = X(Math.Max(0, Math.Min(Scores.Count - 1, CurrentPly)));
                    using (var p = new Pen(Color.FromArgb(200, Theme.Accent), Theme.S(2))) g.DrawLine(p, cx, 0, cx, Height);
                    for (int i = 0; i < Classes.Count && i + 1 < Scores.Count; i++)
                    {
                        var c = Classes[i];
                        if (c != MoveClass.Blunder && c != MoveClass.Mistake && c != MoveClass.Miss && c != MoveClass.Brilliant && c != MoveClass.Great) continue;
                        int s = Scores[i + 1] ?? 0;
                        float d = Theme.S(7);
                        using (var b = new SolidBrush(Theme.ClassColor(c))) g.FillEllipse(b, X(i + 1) - d / 2, Y(s) - d / 2, d, d);
                    }
                }
                g.ResetClip();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (Scores.Count < 2) return;
            int ply = (int)Math.Round(e.X * (Scores.Count - 1) / (double)Math.Max(1, Width - 1));
            PlyClicked?.Invoke(Math.Max(0, Math.Min(Scores.Count - 1, ply)));
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Cursor = Cursors.Hand; }
    }

    /// <summary>Card that explains the selected move: badge, verdict and comment.</summary>
    sealed class FeedbackCard : PaintedControl
    {
        public MoveClass Class = MoveClass.None;
        public string Title = "";
        public string Body = "";
        public string Eval = "";

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent?.BackColor ?? Theme.PanelBg);
            Theme.FillRound(g, Theme.CardBg, new RectangleF(0, 0, Width - 1, Height - 1), Theme.S(8));
            float pad = Theme.S(12);
            float x = pad;
            if (Class != MoveClass.None)
            {
                float d = Theme.S(30);
                Theme.DrawBadge(g, Class, pad + d / 2, pad + d / 2, d);
                x += d + Theme.S(10);
            }
            Color titleColor = Class == MoveClass.None ? Theme.Text : Theme.ClassColor(Class);
            float evalW = 0;
            if (Eval.Length > 0)
            {
                SizeF es = g.MeasureString(Eval, Theme.UiBold);
                evalW = es.Width + Theme.S(12);
                var er = new RectangleF(Width - pad - evalW, pad, evalW, Theme.S(26));
                bool whiteAdv = !Eval.StartsWith("−") && !Eval.StartsWith("-");
                Theme.FillRound(g, whiteAdv ? Color.FromArgb(235, 235, 235) : Color.FromArgb(20, 20, 20), er, Theme.S(4));
                using (var b = new SolidBrush(whiteAdv ? Color.FromArgb(30, 30, 30) : Color.White))
                    g.DrawString(Eval, Theme.UiBold, b, er, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
            using (var b = new SolidBrush(titleColor))
                g.DrawString(Title, Theme.UiLarge, b, new RectangleF(x, pad - Theme.S(2), Width - x - pad - evalW, Theme.S(32)),
                    new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter });
            using (var b = new SolidBrush(Theme.TextDim))
                g.DrawString(Body, Theme.UiFont, b, new RectangleF(pad, pad + Theme.S(36), Width - pad * 2, Height - pad * 2 - Theme.S(34)),
                    new StringFormat { Trimming = StringTrimming.EllipsisWord });
        }
    }

    /// <summary>Game summary: accuracy of both sides and counts of move categories.</summary>
    sealed class ReviewSummary : PaintedControl
    {
        public string LeftName = "Вы", RightName = "Sokol";
        public double? LeftAccuracy, RightAccuracy;
        public readonly Dictionary<MoveClass, int> LeftCounts = new Dictionary<MoveClass, int>();
        public readonly Dictionary<MoveClass, int> RightCounts = new Dictionary<MoveClass, int>();
        public string Status = "";

        public static readonly MoveClass[] Rows =
        {
            MoveClass.Brilliant, MoveClass.Great, MoveClass.Best, MoveClass.Excellent, MoveClass.Good,
            MoveClass.Book, MoveClass.Inaccuracy, MoveClass.Mistake, MoveClass.Miss, MoveClass.Blunder,
        };

        static string ShortName(MoveClass c)
        {
            switch (c)
            {
                case MoveClass.Brilliant: return "Блестящий";
                case MoveClass.Great: return "Отличный";
                case MoveClass.Best: return "Лучший";
                case MoveClass.Good: return "Хороший";
                default: return Review.ClassName(c);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent?.BackColor ?? Theme.PanelBg);
            Theme.FillRound(g, Theme.CardBg, new RectangleF(0, 0, Width - 1, Height - 1), Theme.S(8));
            float pad = Theme.S(12);
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var left = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
            var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

            using (var dim = new SolidBrush(Theme.TextDim))
            using (var txt = new SolidBrush(Theme.Text))
            {
                // Header: accuracy pills for both players.
                float y = pad;
                float pillW = Theme.S(64), pillH = Theme.S(30);
                float rx = Width - pad - pillW, lx = rx - pillW - Theme.S(10);
                g.DrawString("Точность", Theme.UiBold, txt, new RectangleF(pad, y, lx - pad, Theme.S(18)), left);
                if (Status.Length > 0)
                    g.DrawString(Status, Theme.UiSmall, dim, new RectangleF(pad, y + Theme.S(18), lx - pad, Theme.S(18)), left);
                g.DrawString(LeftName, Theme.UiSmall, dim, new RectangleF(lx, y - Theme.S(2), pillW, Theme.S(14)), center);
                g.DrawString(RightName, Theme.UiSmall, dim, new RectangleF(rx, y - Theme.S(2), pillW, Theme.S(14)), center);
                DrawAccuracy(g, LeftAccuracy, new RectangleF(lx, y + Theme.S(13), pillW, pillH), true);
                DrawAccuracy(g, RightAccuracy, new RectangleF(rx, y + Theme.S(13), pillW, pillH), false);
                y += Theme.S(52);
                using (var p = new Pen(Theme.Border, 1)) g.DrawLine(p, pad, y, Width - pad, y);
                y += Theme.S(6);

                // Category grid: 2 columns x 5 rows, "you / bot" counts in each cell.
                float colGap = Theme.S(14);
                float colW = (Width - pad * 2 - colGap) / 2;
                float rowH = Math.Max(Theme.S(18), (Height - y - pad) / 5);
                for (int i = 0; i < Rows.Length; i++)
                {
                    var c = Rows[i];
                    float cx = pad + (i / 5) * (colW + colGap);
                    float cy = y + (i % 5) * rowH;
                    float d = Math.Min(rowH * 0.8f, Theme.S(20));
                    Theme.DrawBadge(g, c, cx + d / 2, cy + rowH / 2, d);
                    float countW = Theme.S(48);
                    g.DrawString(ShortName(c), Theme.UiSmall, txt, new RectangleF(cx + d + Theme.S(6), cy, colW - d - Theme.S(6) - countW, rowH), left);
                    LeftCounts.TryGetValue(c, out int lc);
                    RightCounts.TryGetValue(c, out int rc);
                    using (var cb = new SolidBrush(Theme.ClassColor(c)))
                    {
                        var r1 = new RectangleF(cx + colW - countW, cy, countW / 2, rowH);
                        var r2 = new RectangleF(cx + colW - countW / 2, cy, countW / 2, rowH);
                        g.DrawString(lc.ToString(), Theme.UiSmallBold, lc > 0 ? cb : dim, r1, center);
                        g.DrawString(rc.ToString(), Theme.UiSmallBold, rc > 0 ? cb : dim, r2, center);
                    }
                }
            }
        }

        static void DrawAccuracy(Graphics g, double? acc, RectangleF r, bool white)
        {
            Theme.FillRound(g, white ? Color.FromArgb(235, 235, 235) : Color.FromArgb(64, 61, 57), r, Theme.S(5));
            string s = acc.HasValue ? acc.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "—";
            using (var b = new SolidBrush(white ? Color.FromArgb(30, 30, 30) : Color.White))
                g.DrawString(s, Theme.UiBold, b, r, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
    }
}
