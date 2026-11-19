using System;
using System.Collections.Generic;
using System.Globalization;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using Sokol.Engine;

namespace Sokol.Droid
{
    /// <summary>Thin vertical evaluation bar next to the board.</summary>
    sealed class EvalBarView : View
    {
        public bool Flipped;
        float shown = 0.5f, target = 0.5f, from = 0.5f;
        long start;
        string label = "0.0";
        bool whiteBetter = true;
        readonly Paint text = Ui.TextPaint(Color.White, 8, true);

        public EvalBarView(Context c) : base(c) { }

        public void SetScore(int whiteScore)
        {
            float v = (float)(0.5 + 0.5 * Review.WinChance(whiteScore));
            if (!Score.IsMate(whiteScore)) v = Math.Max(0.04f, Math.Min(0.96f, v));
            label = Score.IsMate(whiteScore) ? "M" + Math.Abs(Score.MateInMoves(whiteScore))
                : (Math.Abs(whiteScore) / 100.0).ToString("0.0", CultureInfo.InvariantCulture);
            whiteBetter = whiteScore >= 0;
            from = shown;
            target = v;
            start = SystemClock.UptimeMillis();
            Invalidate();
        }

        protected override void OnDraw(Canvas canvas)
        {
            float t = Math.Min(1f, (SystemClock.UptimeMillis() - start) / 350f);
            shown = from + (target - from) * (float)(1 - Math.Pow(1 - t, 3));
            float r = Ui.Dpf(3);
            canvas.DrawRoundRect(new RectF(0, 0, Width, Height), r, r, Ui.Fill(Color.Rgb(64, 61, 57)));
            float wh = Height * shown;
            var wr = Flipped ? new RectF(0, 0, Width, wh) : new RectF(0, Height - wh, Width, Height);
            canvas.Save();
            canvas.ClipRect(wr);
            canvas.DrawRoundRect(new RectF(0, 0, Width, Height), r, r, Ui.Fill(Color.Rgb(240, 240, 240)));
            canvas.Restore();
            bool atBottom = whiteBetter != Flipped;
            text.Color = whiteBetter ? Color.Rgb(50, 50, 50) : Color.Rgb(235, 235, 235);
            text.TextAlign = Paint.Align.Center;
            text.TextSize = Math.Min(Ui.Dpf(8), Width * 0.42f);
            float y = atBottom ? Height - Ui.Dpf(4) : Ui.Dpf(4) + text.TextSize;
            canvas.DrawText(label, Width / 2f, y, text);
            if (t < 1f) PostInvalidateOnAnimation();
        }
    }

    /// <summary>Name plate: avatar, name, rating, captured pieces, "thinking" indicator.</summary>
    sealed class PlayerBarView : View
    {
        public string PlayerName = "", Subtitle = "";
        public bool IsBot, Thinking, ToMove;
        public int PieceColor = Piece.White;
        public readonly List<int> Captured = new List<int>();
        public int MaterialLead;
        readonly Paint name = Ui.TextPaint(Ui.Text, 14, true);
        readonly Paint sub = Ui.TextPaint(Ui.TextMuted, 12);
        readonly Paint small = Ui.TextPaint(Ui.TextDim, 12, true);

        public PlayerBarView(Context c) : base(c) { }

        protected override void OnMeasure(int w, int h) => SetMeasuredDimension(MeasureSpec.GetSize(w), Ui.Dp(40));

        protected override void OnDraw(Canvas canvas)
        {
            float h = Height, av = h - Ui.Dpf(6), top = (h - av) / 2;
            canvas.DrawRoundRect(new RectF(0, top, av, top + av), Ui.Dpf(5), Ui.Dpf(5),
                Ui.Fill(IsBot ? Color.Rgb(70, 110, 160) : Color.Rgb(95, 140, 60)));
            Ui.DrawBitmap(canvas, Ui.PieceBitmap(Piece.Make(PieceColor, IsBot ? Piece.Knight : Piece.Pawn)),
                new RectF(av * 0.05f, top + av * 0.05f, av * 0.95f, top + av * 0.95f));
            float x = av + Ui.Dpf(10);
            canvas.DrawText(PlayerName, x, h * 0.45f, name);
            float nx = x + name.MeasureText(PlayerName) + Ui.Dpf(6);
            if (Subtitle.Length > 0) canvas.DrawText(Subtitle, nx, h * 0.45f, sub);

            float ps = h * 0.42f, cx = x, cy = h * 0.52f;
            int prevType = -1;
            foreach (int p in Captured)
            {
                int t = Piece.TypeOf(p);
                if (prevType >= 0) cx += t == prevType ? ps * 0.45f : ps * 0.85f;
                Ui.DrawBitmap(canvas, Ui.PieceBitmap(p), new RectF(cx, cy, cx + ps, cy + ps));
                prevType = t;
            }
            if (Captured.Count > 0) cx += ps;
            if (MaterialLead > 0) canvas.DrawText("+" + MaterialLead, cx + Ui.Dpf(3), cy + ps * 0.8f, small);

            if (Thinking)
            {
                int dots = (int)(SystemClock.UptimeMillis() / 300 % 4);
                small.TextAlign = Paint.Align.Right;
                canvas.DrawText("думает" + new string('.', dots), Width - Ui.Dpf(4), h * 0.62f, small);
                small.TextAlign = Paint.Align.Left;
                PostInvalidateDelayed(300);
            }
            else if (ToMove)
                canvas.DrawCircle(Width - Ui.Dpf(8), h / 2, Ui.Dpf(4), Ui.Fill(Ui.Accent));
        }
    }

    /// <summary>Card that explains the selected move: badge, verdict, eval pill and comment.</summary>
    sealed class FeedbackView : View
    {
        public MoveClass Class = MoveClass.None;
        public string Title = "", Body = "", Eval = "";
        readonly TextPaint body = new TextPaint(PaintFlags.AntiAlias);
        readonly Paint title = Ui.TextPaint(Ui.Text, 17, true);
        readonly Paint evalText = Ui.TextPaint(Color.White, 14, true);
        StaticLayout layout;
        int layoutWidth;
        string layoutText;

        public FeedbackView(Context c) : base(c)
        {
            body.Color = Ui.TextDim;
            body.TextSize = Ui.Dpf(14);
        }

        public void SetContent(MoveClass c, string t, string b, string e)
        {
            Class = c; Title = t; Body = b; Eval = e;
            RequestLayout();
            Invalidate();
        }

        StaticLayout Layout(int width)
        {
            width = Math.Max(10, width);
            if (layout == null || layoutWidth != width || layoutText != Body)
            {
                layout = StaticLayout.Builder.Obtain(Body, 0, Body.Length, body, width).SetLineSpacing(0, 1.1f).Build();
                layoutWidth = width;
                layoutText = Body;
            }
            return layout;
        }

        float Pad => Ui.Dpf(12);

        protected override void OnMeasure(int w, int h)
        {
            int width = MeasureSpec.GetSize(w);
            var l = Layout(width - (int)(Pad * 2));
            SetMeasuredDimension(width, (int)(Pad * 2 + Ui.Dpf(40) + l.Height));
        }

        protected override void OnDraw(Canvas canvas)
        {
            canvas.DrawRoundRect(new RectF(0, 0, Width, Height), Ui.Dpf(10), Ui.Dpf(10), Ui.Fill(Ui.CardBg));
            float pad = Pad, x = pad, d = Ui.Dpf(30);
            if (Class != MoveClass.None)
            {
                Ui.DrawBadge(canvas, Class, pad + d / 2, pad + d / 2, d);
                x += d + Ui.Dpf(10);
            }
            float evalW = 0;
            if (Eval.Length > 0)
            {
                evalW = evalText.MeasureText(Eval) + Ui.Dpf(14);
                var er = new RectF(Width - pad - evalW, pad, Width - pad, pad + Ui.Dpf(28));
                bool whiteAdv = !Eval.StartsWith("−") && !Eval.StartsWith("-");
                canvas.DrawRoundRect(er, Ui.Dpf(5), Ui.Dpf(5), Ui.Fill(whiteAdv ? Color.Rgb(235, 235, 235) : Color.Rgb(15, 15, 15)));
                evalText.Color = whiteAdv ? Color.Rgb(30, 30, 30) : Color.White;
                evalText.TextAlign = Paint.Align.Center;
                canvas.DrawText(Eval, er.CenterX(), er.CenterY() + evalText.TextSize * 0.35f, evalText);
            }
            title.Color = Class == MoveClass.None ? Ui.Text : Ui.ClassColor(Class);
            string t = Title;
            float maxW = Width - x - pad - evalW - Ui.Dpf(6);
            while (t.Length > 1 && title.MeasureText(t) > maxW) t = t.Substring(0, t.Length - 1);
            canvas.DrawText(t, x, pad + d / 2 + title.TextSize * 0.35f, title);
            canvas.Save();
            canvas.Translate(pad, pad + Ui.Dpf(40));
            Layout(Width - (int)(pad * 2)).Draw(canvas);
            canvas.Restore();
        }
    }

    /// <summary>Review summary: accuracy of both sides and counts of move categories.</summary>
    sealed class SummaryView : View
    {
        public string LeftName = "Вы", RightName = "Sokol", Status = "";
        public double? LeftAccuracy, RightAccuracy;
        public readonly Dictionary<MoveClass, int> LeftCounts = new Dictionary<MoveClass, int>();
        public readonly Dictionary<MoveClass, int> RightCounts = new Dictionary<MoveClass, int>();
        static readonly MoveClass[] Rows =
        {
            MoveClass.Brilliant, MoveClass.Great, MoveClass.Best, MoveClass.Excellent, MoveClass.Good,
            MoveClass.Book, MoveClass.Inaccuracy, MoveClass.Mistake, MoveClass.Miss, MoveClass.Blunder,
        };
        readonly Paint text = Ui.TextPaint(Ui.Text, 13);
        readonly Paint bold = Ui.TextPaint(Ui.Text, 15, true);
        readonly Paint dim = Ui.TextPaint(Ui.TextDim, 12);
        readonly Paint count = Ui.TextPaint(Ui.Text, 13, true);

        public SummaryView(Context c) : base(c) { }

        protected override void OnMeasure(int w, int h) => SetMeasuredDimension(MeasureSpec.GetSize(w), Ui.Dp(232));

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

        protected override void OnDraw(Canvas canvas)
        {
            canvas.DrawRoundRect(new RectF(0, 0, Width, Height), Ui.Dpf(10), Ui.Dpf(10), Ui.Fill(Ui.CardBg));
            float pad = Ui.Dpf(12), pillW = Ui.Dpf(64), pillH = Ui.Dpf(30);
            float rx = Width - pad - pillW, lx = rx - pillW - Ui.Dpf(10);
            canvas.DrawText("Точность", pad, pad + Ui.Dpf(14), bold);
            if (Status.Length > 0) canvas.DrawText(Status, pad, pad + Ui.Dpf(34), dim);
            dim.TextAlign = Paint.Align.Center;
            canvas.DrawText(LeftName, lx + pillW / 2, pad + Ui.Dpf(8), dim);
            canvas.DrawText(RightName, rx + pillW / 2, pad + Ui.Dpf(8), dim);
            dim.TextAlign = Paint.Align.Left;
            Pill(canvas, LeftAccuracy, new RectF(lx, pad + Ui.Dpf(14), lx + pillW, pad + Ui.Dpf(14) + pillH), true);
            Pill(canvas, RightAccuracy, new RectF(rx, pad + Ui.Dpf(14), rx + pillW, pad + Ui.Dpf(14) + pillH), false);
            float y = pad + Ui.Dpf(56);
            canvas.DrawLine(pad, y, Width - pad, y, Ui.Fill(Ui.Border));
            y += Ui.Dpf(6);
            float colGap = Ui.Dpf(12), colW = (Width - pad * 2 - colGap) / 2, rowH = (Height - y - pad) / 5;
            for (int i = 0; i < Rows.Length; i++)
            {
                var c = Rows[i];
                float cx = pad + (i / 5) * (colW + colGap), cy = y + (i % 5) * rowH;
                float d = Math.Min(rowH * 0.8f, Ui.Dpf(22));
                Ui.DrawBadge(canvas, c, cx + d / 2, cy + rowH / 2, d);
                canvas.DrawText(ShortName(c), cx + d + Ui.Dpf(6), cy + rowH / 2 + text.TextSize * 0.35f, text);
                LeftCounts.TryGetValue(c, out int lc);
                RightCounts.TryGetValue(c, out int rc);
                count.TextAlign = Paint.Align.Center;
                count.Color = lc > 0 ? Ui.ClassColor(c) : Ui.TextMuted;
                canvas.DrawText(lc.ToString(), cx + colW - Ui.Dpf(36), cy + rowH / 2 + count.TextSize * 0.35f, count);
                count.Color = rc > 0 ? Ui.ClassColor(c) : Ui.TextMuted;
                canvas.DrawText(rc.ToString(), cx + colW - Ui.Dpf(12), cy + rowH / 2 + count.TextSize * 0.35f, count);
            }
        }

        void Pill(Canvas canvas, double? acc, RectF r, bool white)
        {
            canvas.DrawRoundRect(r, Ui.Dpf(5), Ui.Dpf(5), Ui.Fill(white ? Color.Rgb(235, 235, 235) : Color.Rgb(64, 61, 57)));
            var p = Ui.TextPaint(white ? Color.Rgb(30, 30, 30) : Color.White, 15, true);
            p.TextAlign = Paint.Align.Center;
            string s = acc.HasValue ? acc.Value.ToString("0.0", CultureInfo.InvariantCulture) : "—";
            canvas.DrawText(s, r.CenterX(), r.CenterY() + p.TextSize * 0.35f, p);
        }
    }

    /// <summary>Evaluation graph over the whole game.</summary>
    sealed class GraphView : View
    {
        public readonly List<int?> Scores = new List<int?>();
        public readonly List<MoveClass> Classes = new List<MoveClass>();
        public int CurrentPly;
        public event Action<int> PlyClicked;

        public GraphView(Context c) : base(c) { }

        protected override void OnMeasure(int w, int h) => SetMeasuredDimension(MeasureSpec.GetSize(w), Ui.Dp(72));

        float X(int ply) => Scores.Count <= 1 ? 0 : (float)ply * (Width - 1) / (Scores.Count - 1);
        float Y(int score) => (float)(Height * (0.5 - 0.5 * Review.WinChance(score)) * 0.92 + Height * 0.04);

        protected override void OnDraw(Canvas canvas)
        {
            float r = Ui.Dpf(8);
            canvas.DrawRoundRect(new RectF(0, 0, Width, Height), r, r, Ui.Fill(Color.Rgb(64, 61, 57)));
            if (Scores.Count >= 2)
            {
                var path = new Path();
                path.MoveTo(0, Height);
                int last = 0;
                for (int i = 0; i < Scores.Count; i++)
                {
                    int s = Scores[i] ?? last;
                    last = s;
                    path.LineTo(X(i), Y(s));
                }
                path.LineTo(X(Scores.Count - 1), Height);
                path.Close();
                canvas.Save();
                var clip = new Path();
                clip.AddRoundRect(new RectF(0, 0, Width, Height), r, r, Path.Direction.Cw);
                canvas.ClipPath(clip);
                canvas.DrawPath(path, Ui.Fill(Color.Rgb(235, 235, 235)));
                canvas.Restore();
                canvas.DrawLine(0, Height / 2f, Width, Height / 2f, Ui.Fill(Color.Argb(110, 128, 128, 128)));
                float cx = X(Math.Max(0, Math.Min(Scores.Count - 1, CurrentPly)));
                var marker = Ui.Fill(Ui.Accent);
                marker.StrokeWidth = Ui.Dpf(2);
                canvas.DrawLine(cx, 0, cx, Height, marker);
                for (int i = 0; i < Classes.Count && i + 1 < Scores.Count; i++)
                {
                    var c = Classes[i];
                    if (c != MoveClass.Blunder && c != MoveClass.Mistake && c != MoveClass.Miss && c != MoveClass.Brilliant && c != MoveClass.Great) continue;
                    canvas.DrawCircle(X(i + 1), Y(Scores[i + 1] ?? 0), Ui.Dpf(4), Ui.Fill(Ui.ClassColor(c)));
                }
            }
        }

        public override bool OnTouchEvent(MotionEvent e)
        {
            if (e.ActionMasked == MotionEventActions.Down || e.ActionMasked == MotionEventActions.Move)
            {
                if (Scores.Count >= 2)
                {
                    int ply = (int)Math.Round(e.GetX() * (Scores.Count - 1) / Math.Max(1f, Width - 1));
                    PlyClicked?.Invoke(Math.Max(0, Math.Min(Scores.Count - 1, ply)));
                }
                Parent?.RequestDisallowInterceptTouchEvent(true);
                return true;
            }
            return base.OnTouchEvent(e);
        }
    }

    enum IconKind { Undo, Hint, Prev, Next, Flip, Resign, NewGame, Share, Close, Review, Settings }

    /// <summary>Bottom bar button: vector icon with a small caption.</summary>
    sealed class IconButton : View
    {
        public IconKind Kind;
        public string Caption;
        public bool Primary;
        bool pressed;
        readonly Paint caption = Ui.TextPaint(Ui.TextDim, 10);
        readonly Paint line;

        public IconButton(Context c, IconKind kind, string text) : base(c)
        {
            Kind = kind;
            Caption = text;
            line = new Paint(PaintFlags.AntiAlias) { StrokeWidth = Ui.Dpf(2.2f), StrokeCap = Paint.Cap.Round, StrokeJoin = Paint.Join.Round };
            line.SetStyle(Paint.Style.Stroke);
            Clickable = true;
        }

        public override bool OnTouchEvent(MotionEvent e)
        {
            if (e.ActionMasked == MotionEventActions.Down) { pressed = true; Invalidate(); }
            else if (e.ActionMasked == MotionEventActions.Up || e.ActionMasked == MotionEventActions.Cancel) { pressed = false; Invalidate(); }
            return base.OnTouchEvent(e);
        }

        protected override void OnDraw(Canvas canvas)
        {
            Color fg = !Enabled ? Ui.TextMuted : Primary ? Color.White : Ui.Text;
            if (Primary) canvas.DrawRoundRect(new RectF(Ui.Dpf(3), Ui.Dpf(4), Width - Ui.Dpf(3), Height - Ui.Dpf(4)), Ui.Dpf(8), Ui.Dpf(8), Ui.Fill(Enabled ? Ui.Accent : Ui.Button));
            if (pressed && Enabled) canvas.DrawRoundRect(new RectF(Ui.Dpf(3), Ui.Dpf(4), Width - Ui.Dpf(3), Height - Ui.Dpf(4)), Ui.Dpf(8), Ui.Dpf(8), Ui.Fill(Color.Argb(50, 255, 255, 255)));
            line.Color = fg;
            float cx = Width / 2f, cy = Height * 0.40f, s = Ui.Dpf(10);
            var p = new Path();
            switch (Kind)
            {
                case IconKind.Undo:
                    p.AddArc(new RectF(cx - s, cy - s * 0.8f, cx + s, cy + s * 0.9f), 200, 250);
                    canvas.DrawPath(p, line);
                    var head = new Path();
                    head.MoveTo(cx - s * 1.1f, cy - s * 0.75f); head.LineTo(cx - s * 0.9f, cy + s * 0.05f); head.LineTo(cx - s * 0.15f, cy - s * 0.35f);
                    canvas.DrawPath(head, line);
                    break;
                case IconKind.Hint:
                    var star = new Path();
                    for (int i = 0; i < 10; i++)
                    {
                        double a = -Math.PI / 2 + i * Math.PI / 5;
                        float rr = i % 2 == 0 ? s * 1.05f : s * 0.45f;
                        float x = cx + (float)Math.Cos(a) * rr, y = cy + (float)Math.Sin(a) * rr;
                        if (i == 0) star.MoveTo(x, y); else star.LineTo(x, y);
                    }
                    star.Close();
                    canvas.DrawPath(star, line);
                    break;
                case IconKind.Prev:
                case IconKind.Next:
                    float dir = Kind == IconKind.Prev ? 1 : -1;
                    p.MoveTo(cx + s * 0.4f * dir, cy - s * 0.8f); p.LineTo(cx - s * 0.4f * dir, cy); p.LineTo(cx + s * 0.4f * dir, cy + s * 0.8f);
                    canvas.DrawPath(p, line);
                    break;
                case IconKind.Flip:
                    p.MoveTo(cx - s * 0.45f, cy + s * 0.9f); p.LineTo(cx - s * 0.45f, cy - s * 0.9f);
                    p.MoveTo(cx - s * 0.95f, cy - s * 0.4f); p.LineTo(cx - s * 0.45f, cy - s * 0.9f); p.LineTo(cx + s * 0.05f, cy - s * 0.4f);
                    p.MoveTo(cx + s * 0.45f, cy - s * 0.9f); p.LineTo(cx + s * 0.45f, cy + s * 0.9f);
                    p.MoveTo(cx - s * 0.05f, cy + s * 0.4f); p.LineTo(cx + s * 0.45f, cy + s * 0.9f); p.LineTo(cx + s * 0.95f, cy + s * 0.4f);
                    canvas.DrawPath(p, line);
                    break;
                case IconKind.Resign:
                    p.MoveTo(cx - s * 0.7f, cy + s); p.LineTo(cx - s * 0.7f, cy - s);
                    canvas.DrawPath(p, line);
                    var flag = new Path();
                    flag.MoveTo(cx - s * 0.7f, cy - s); flag.LineTo(cx + s * 0.9f, cy - s * 0.55f); flag.LineTo(cx - s * 0.7f, cy - s * 0.05f); flag.Close();
                    canvas.DrawPath(flag, Ui.Fill(fg));
                    break;
                case IconKind.NewGame:
                    p.MoveTo(cx, cy - s); p.LineTo(cx, cy + s); p.MoveTo(cx - s, cy); p.LineTo(cx + s, cy);
                    canvas.DrawPath(p, line);
                    break;
                case IconKind.Share:
                    canvas.DrawCircle(cx + s * 0.7f, cy - s * 0.7f, s * 0.3f, Ui.Fill(fg));
                    canvas.DrawCircle(cx - s * 0.7f, cy, s * 0.3f, Ui.Fill(fg));
                    canvas.DrawCircle(cx + s * 0.7f, cy + s * 0.7f, s * 0.3f, Ui.Fill(fg));
                    p.MoveTo(cx + s * 0.7f, cy - s * 0.7f); p.LineTo(cx - s * 0.7f, cy); p.LineTo(cx + s * 0.7f, cy + s * 0.7f);
                    canvas.DrawPath(p, line);
                    break;
                case IconKind.Close:
                    p.MoveTo(cx - s * 0.75f, cy - s * 0.75f); p.LineTo(cx + s * 0.75f, cy + s * 0.75f);
                    p.MoveTo(cx + s * 0.75f, cy - s * 0.75f); p.LineTo(cx - s * 0.75f, cy + s * 0.75f);
                    canvas.DrawPath(p, line);
                    break;
                case IconKind.Review:
                    p.MoveTo(cx - s, cy + s * 0.8f); p.LineTo(cx - s * 0.3f, cy); p.LineTo(cx + s * 0.2f, cy + s * 0.4f); p.LineTo(cx + s, cy - s * 0.8f);
                    canvas.DrawPath(p, line);
                    break;
                case IconKind.Settings:
                    canvas.DrawCircle(cx, cy, s * 0.4f, line);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        p.MoveTo(cx + (float)Math.Cos(a) * s * 0.65f, cy + (float)Math.Sin(a) * s * 0.65f);
                        p.LineTo(cx + (float)Math.Cos(a) * s * 1.0f, cy + (float)Math.Sin(a) * s * 1.0f);
                    }
                    canvas.DrawPath(p, line);
                    break;
            }
            caption.Color = fg;
            caption.TextAlign = Paint.Align.Center;
            canvas.DrawText(Caption, cx, Height * 0.84f, caption);
        }
    }

    /// <summary>Horizontally scrolling move list: "1. e4 e5 2. Kf3 ..." with badges.</summary>
    sealed class MoveStrip : HorizontalScrollView
    {
        readonly LinearLayout row;
        readonly List<TextView> chips = new List<TextView>();
        public event Action<int> MoveClicked;

        public MoveStrip(Context c) : base(c)
        {
            HorizontalScrollBarEnabled = false;
            row = new LinearLayout(c) { Orientation = Orientation.Horizontal };
            row.SetGravity(GravityFlags.CenterVertical);
            row.SetPadding(Ui.Dp(4), 0, Ui.Dp(4), 0);
            AddView(row, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.WrapContent, Ui.Dp(44)));
        }

        public void SetMoves(IList<string> sans, IList<MoveClass> classes, int selected)
        {
            row.RemoveAllViews();
            chips.Clear();
            if (sans.Count == 0)
            {
                var hint = new TextView(Context) { Text = "Ходы появятся здесь" };
                hint.SetTextColor(Ui.TextMuted);
                hint.TextSize = 13;
                row.AddView(hint);
                return;
            }
            for (int i = 0; i < sans.Count; i++)
            {
                if (i % 2 == 0)
                {
                    var num = new TextView(Context) { Text = (i / 2 + 1) + "." };
                    num.SetTextColor(Ui.TextMuted);
                    num.TextSize = 14;
                    num.SetPadding(Ui.Dp(6), 0, Ui.Dp(2), 0);
                    row.AddView(num);
                }
                var chip = new TextView(Context) { Text = sans[i] };
                chip.SetTextColor(i == selected ? Color.White : Ui.Text);
                chip.TextSize = 15;
                chip.SetTypeface(Typeface.Default, i == selected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                chip.SetPadding(Ui.Dp(7), Ui.Dp(5), Ui.Dp(8), Ui.Dp(5));
                if (i == selected) chip.Background = Ui.Rounded(Ui.RowHighlight, 6);
                var c = i < classes.Count ? classes[i] : MoveClass.None;
                var badge = Ui.BadgeBitmap(c);
                if (badge != null)
                {
                    var d = new Android.Graphics.Drawables.BitmapDrawable(Resources, badge);
                    d.SetBounds(0, 0, Ui.Dp(18), Ui.Dp(18));
                    chip.SetCompoundDrawables(d, null, null, null);
                    chip.CompoundDrawablePadding = Ui.Dp(4);
                }
                int index = i;
                chip.Click += (s, e) => MoveClicked?.Invoke(index);
                row.AddView(chip, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { LeftMargin = Ui.Dp(1), RightMargin = Ui.Dp(1) });
                chips.Add(chip);
            }
            int target = selected;
            Post(() =>
            {
                if (target < 0 || target >= chips.Count) { SmoothScrollTo(0, 0); return; }
                var v = chips[target];
                SmoothScrollTo(Math.Max(0, v.Left - Width / 2 + v.Width / 2), 0);
            });
        }
    }
}
