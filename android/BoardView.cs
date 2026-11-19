using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Sokol.Engine;
using Mv = Sokol.Engine.Move;

namespace Sokol.Droid
{
    sealed class Arrow
    {
        public int From, To;
        public Color Color;
        public Arrow(int from, int to, Color color) { From = from; To = to; Color = color; }
    }

    /// <summary>Touch chess board: tap-tap and drag moves, promotion picker, arrows, badges, animation.</summary>
    sealed class BoardView : View
    {
        public Board Position { get; private set; } = new Board();
        public bool Flipped;
        public bool Interactive;
        public int UserColor = Piece.White;
        public int LastMove = Mv.None;
        public MoveClass BadgeClass = MoveClass.None;
        public int BadgeSquare = -1;
        public readonly List<Arrow> Arrows = new List<Arrow>();
        /// <summary>(move, animate) - raised when the user completes a legal move.</summary>
        public event Action<int, bool> MoveMade;

        List<int> legal = new List<int>();
        int selected = -1;
        bool deselectOnUp;
        int dragFrom = -1;
        bool dragStarted;
        float downX, downY, dragX, dragY;
        int hoverSquare = -1;
        int promoFrom = -1, promoTo = -1;
        bool promoAnimate;

        sealed class Anim { public int Piece, From, To; }
        readonly List<Anim> anims = new List<Anim>();
        long animStart;
        const float AnimMs = 170f;

        readonly Paint fill = Ui.Fill(Color.White);
        readonly Paint stroke;
        readonly Paint coord;

        public BoardView(Context c) : base(c)
        {
            stroke = new Paint(PaintFlags.AntiAlias);
            stroke.SetStyle(Paint.Style.Stroke);
            coord = Ui.TextPaint(Color.White, 10, true);
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            int w = MeasureSpec.GetSize(widthMeasureSpec);
            SetMeasuredDimension(w, w);
        }

        float Sq => Math.Max(8f, Math.Min(Width, Height) / 8f);

        public void SetPosition(Board b, int lastMove, bool animate)
        {
            Position = b;
            LastMove = lastMove;
            selected = -1;
            dragFrom = -1;
            dragStarted = false;
            promoTo = -1;
            legal = Interactive ? b.LegalMoves() : new List<int>();
            anims.Clear();
            if (animate && lastMove != Mv.None)
            {
                int to = Mv.To(lastMove);
                anims.Add(new Anim { Piece = b.Squares[to], From = Mv.From(lastMove), To = to });
                if (Mv.IsCastle(lastMove))
                {
                    bool kingSide = Mv.Flags(lastMove) == Mv.KingCastle;
                    int rank = to & 56;
                    int rf = rank + (kingSide ? 7 : 0), rt = rank + (kingSide ? 5 : 3);
                    anims.Add(new Anim { Piece = b.Squares[rt], From = rf, To = rt });
                }
                animStart = SystemClock.UptimeMillis();
            }
            Invalidate();
        }

        public void RefreshInteractivity()
        {
            legal = Interactive ? Position.LegalMoves() : new List<int>();
            if (!Interactive) { selected = -1; dragFrom = -1; dragStarted = false; promoTo = -1; }
            Invalidate();
        }

        // ------------------------------------------------------------------ geometry

        PointF Origin(int square)
        {
            int f = square & 7, r = square >> 3;
            int col = Flipped ? 7 - f : f, row = Flipped ? r : 7 - r;
            return new PointF(col * Sq, row * Sq);
        }

        PointF Center(int square)
        {
            var p = Origin(square);
            return new PointF(p.X + Sq / 2, p.Y + Sq / 2);
        }

        int SquareAt(float x, float y)
        {
            if (x < 0 || y < 0) return -1;
            int col = (int)(x / Sq), row = (int)(y / Sq);
            if (col > 7 || row > 7) return -1;
            int f = Flipped ? 7 - col : col, r = Flipped ? row : 7 - row;
            return r * 8 + f;
        }

        bool IsUserPiece(int sq) =>
            sq >= 0 && Position.Squares[sq] != Piece.None && Piece.ColorOf(Position.Squares[sq]) == UserColor && Position.SideToMove == UserColor;

        bool HasMove(int from, int to)
        {
            foreach (int m in legal) if (Mv.From(m) == from && Mv.To(m) == to) return true;
            return false;
        }

        // ------------------------------------------------------------------ drawing

        protected override void OnDraw(Canvas canvas)
        {
            float sq = Sq;
            int kingInCheck = Position.InCheck() ? Position.KingSquare(Position.SideToMove) : -1;

            for (int s = 0; s < 64; s++)
            {
                var o = Origin(s);
                bool light = ((s >> 3) + (s & 7)) % 2 == 1;
                bool hl = (LastMove != Mv.None && (Mv.From(LastMove) == s || Mv.To(LastMove) == s)) || s == selected;
                fill.Color = hl ? (light ? Ui.LightHighlight : Ui.DarkHighlight) : (light ? Ui.LightSquare : Ui.DarkSquare);
                fill.SetShader(null);
                canvas.DrawRect(o.X, o.Y, o.X + sq, o.Y + sq, fill);
                if (s == kingInCheck)
                {
                    var glow = Ui.Fill(Color.White);
                    glow.SetShader(new RadialGradient(o.X + sq / 2, o.Y + sq / 2, sq * 0.6f,
                        new[] { Color.Argb(255, 255, 30, 30).ToArgb(), Color.Argb(170, 255, 0, 0).ToArgb(), Color.Argb(0, 255, 0, 0).ToArgb() },
                        new[] { 0f, 0.5f, 1f }, Shader.TileMode.Clamp));
                    canvas.DrawRect(o.X, o.Y, o.X + sq, o.Y + sq, glow);
                }
                if (dragStarted && s == hoverSquare)
                {
                    stroke.Color = Color.Argb(200, 255, 255, 255);
                    stroke.StrokeWidth = Math.Max(2f, sq / 14f);
                    canvas.DrawRect(o.X + sq / 28, o.Y + sq / 28, o.X + sq - sq / 28, o.Y + sq - sq / 28, stroke);
                }
            }
            DrawCoordinates(canvas, sq);

            float t = 1f;
            if (anims.Count > 0)
            {
                t = Math.Min(1f, (SystemClock.UptimeMillis() - animStart) / AnimMs);
                if (t >= 1f) anims.Clear();
            }
            var hidden = new HashSet<int>();
            foreach (var a in anims) hidden.Add(a.To);
            if (dragStarted && dragFrom >= 0) hidden.Add(dragFrom);
            for (int s = 0; s < 64; s++)
            {
                int p = Position.Squares[s];
                if (p == Piece.None || hidden.Contains(s)) continue;
                var o = Origin(s);
                Ui.DrawBitmap(canvas, Ui.PieceBitmap(p), new RectF(o.X, o.Y, o.X + sq, o.Y + sq));
            }

            if (selected >= 0 && promoTo < 0)
            {
                fill.Color = Color.Argb(45, 0, 0, 0);
                stroke.Color = Color.Argb(45, 0, 0, 0);
                stroke.StrokeWidth = sq * 0.09f;
                foreach (int m in legal)
                {
                    if (Mv.From(m) != selected) continue;
                    if (Mv.IsPromotion(m) && Mv.PromotionType(m) != Piece.Queen) continue;
                    var c = Center(Mv.To(m));
                    if (Position.Squares[Mv.To(m)] != Piece.None) canvas.DrawCircle(c.X, c.Y, sq * 0.44f, stroke);
                    else canvas.DrawCircle(c.X, c.Y, sq * 0.16f, fill);
                }
            }

            foreach (var arrow in Arrows) DrawArrow(canvas, arrow, sq);

            double ease = 1 - Math.Pow(1 - t, 3);
            foreach (var a in anims)
            {
                if (a.Piece == Piece.None) continue;
                var p0 = Origin(a.From);
                var p1 = Origin(a.To);
                float x = (float)(p0.X + (p1.X - p0.X) * ease), y = (float)(p0.Y + (p1.Y - p0.Y) * ease);
                Ui.DrawBitmap(canvas, Ui.PieceBitmap(a.Piece), new RectF(x, y, x + sq, y + sq));
            }

            if (BadgeClass != MoveClass.None && BadgeSquare >= 0 && anims.Count == 0)
            {
                var o = Origin(BadgeSquare);
                float d = sq * 0.42f;
                float cx = Math.Min(o.X + sq - d * 0.40f, Width - d / 2f), cy = Math.Max(o.Y + d * 0.40f, d / 2f);
                Ui.DrawBadge(canvas, BadgeClass, cx, cy, d);
            }

            // The dragged piece is lifted above the finger so it stays visible.
            if (dragStarted && dragFrom >= 0 && Position.Squares[dragFrom] != Piece.None)
            {
                float big = sq * 1.5f;
                float cx = dragX, cy = dragY - sq * 0.6f;
                Ui.DrawBitmap(canvas, Ui.PieceBitmap(Position.Squares[dragFrom]), new RectF(cx - big / 2, cy - big / 2, cx + big / 2, cy + big / 2));
            }

            if (promoTo >= 0) DrawPromotionPicker(canvas, sq);
            if (anims.Count > 0) PostInvalidateOnAnimation();
        }

        void DrawCoordinates(Canvas canvas, float sq)
        {
            coord.TextSize = Math.Max(Ui.Dpf(8), sq * 0.2f);
            for (int i = 0; i < 8; i++)
            {
                int rankSq = Flipped ? (i * 8 + 7) : (i * 8);
                var o = Origin(rankSq);
                bool light = ((rankSq >> 3) + (rankSq & 7)) % 2 == 1;
                coord.Color = light ? Ui.DarkSquare : Ui.LightSquare;
                coord.TextAlign = Paint.Align.Left;
                canvas.DrawText(((char)('1' + i)).ToString(), o.X + sq * 0.05f, o.Y + coord.TextSize * 0.95f, coord);

                int fileSq = Flipped ? (56 + i) : i;
                o = Origin(fileSq);
                light = ((fileSq >> 3) + (fileSq & 7)) % 2 == 1;
                coord.Color = light ? Ui.DarkSquare : Ui.LightSquare;
                coord.TextAlign = Paint.Align.Right;
                canvas.DrawText(((char)('a' + i)).ToString(), o.X + sq - sq * 0.05f, o.Y + sq - sq * 0.06f, coord);
            }
        }

        void DrawArrow(Canvas canvas, Arrow a, float sq)
        {
            PointF p0 = Center(a.From), p1 = Center(a.To);
            var pts = new List<PointF> { p0 };
            int df = (a.To & 7) - (a.From & 7), dr = (a.To >> 3) - (a.From >> 3);
            bool knight = (Math.Abs(df) == 1 && Math.Abs(dr) == 2) || (Math.Abs(df) == 2 && Math.Abs(dr) == 1);
            if (knight) pts.Add(Math.Abs(dr) == 2 ? new PointF(p0.X, p1.Y) : new PointF(p1.X, p0.Y));
            pts.Add(p1);

            float width = sq * 0.18f, headLen = sq * 0.42f, headW = sq * 0.46f;
            PointF last = pts[pts.Count - 2];
            float vx = p1.X - last.X, vy = p1.Y - last.Y;
            float len = (float)Math.Sqrt(vx * vx + vy * vy);
            if (len < 1) return;
            vx /= len; vy /= len;
            if (!knight) pts[0] = new PointF(p0.X + vx * sq * 0.25f, p0.Y + vy * sq * 0.25f);
            var tip = new PointF(p1.X + vx * sq * 0.02f, p1.Y + vy * sq * 0.02f);
            var baseP = new PointF(tip.X - vx * headLen, tip.Y - vy * headLen);
            pts[pts.Count - 1] = baseP;

            var line = new Paint(PaintFlags.AntiAlias) { Color = a.Color, StrokeWidth = width };
            line.SetStyle(Paint.Style.Stroke);
            line.StrokeJoin = Paint.Join.Miter;
            var path = new Path();
            path.MoveTo(pts[0].X, pts[0].Y);
            for (int i = 1; i < pts.Count; i++) path.LineTo(pts[i].X, pts[i].Y);
            canvas.DrawPath(path, line);

            float nx = -vy, ny = vx;
            var head = new Path();
            head.MoveTo(tip.X, tip.Y);
            head.LineTo(baseP.X + nx * headW / 2, baseP.Y + ny * headW / 2);
            head.LineTo(baseP.X - nx * headW / 2, baseP.Y - ny * headW / 2);
            head.Close();
            canvas.DrawPath(head, Ui.Fill(a.Color));
        }

        static readonly int[] PromoOrder = { Piece.Queen, Piece.Knight, Piece.Rook, Piece.Bishop };

        RectF PromoRect(int index, float sq)
        {
            var o = Origin(promoTo);
            bool down = o.Y < 4 * sq;
            float y = down ? o.Y + index * sq : o.Y - index * sq;
            return new RectF(o.X, y, o.X + sq, y + sq);
        }

        void DrawPromotionPicker(Canvas canvas, float sq)
        {
            canvas.DrawRect(0, 0, Width, Height, Ui.Fill(Color.Argb(130, 0, 0, 0)));
            int color = Piece.ColorOf(Position.Squares[promoFrom]);
            var bg = Ui.Fill(Color.Rgb(245, 245, 245));
            for (int i = 0; i < 4; i++)
            {
                var r = PromoRect(i, sq);
                canvas.DrawCircle(r.CenterX(), r.CenterY(), sq * 0.48f, bg);
                Ui.DrawBitmap(canvas, Ui.PieceBitmap(Piece.Make(color, PromoOrder[i])), r);
            }
        }

        // ------------------------------------------------------------------ touch

        public override bool OnTouchEvent(MotionEvent e)
        {
            float x = e.GetX(), y = e.GetY();
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down: Down(x, y); return true;
                case MotionEventActions.Move: MoveTo(x, y); return true;
                case MotionEventActions.Up: Up(x, y); return true;
                case MotionEventActions.Cancel:
                    dragStarted = false; dragFrom = -1; hoverSquare = -1; Invalidate(); return true;
            }
            return base.OnTouchEvent(e);
        }

        void Down(float x, float y)
        {
            float sq = Sq;
            if (promoTo >= 0)
            {
                for (int i = 0; i < 4; i++)
                    if (PromoRect(i, sq).Contains(x, y))
                    {
                        int m = Position.FindMove(promoFrom, promoTo, PromoOrder[i]);
                        promoTo = -1;
                        selected = -1;
                        Invalidate();
                        if (m != Mv.None) MoveMade?.Invoke(m, promoAnimate);
                        return;
                    }
                promoTo = -1;
                selected = -1;
                Invalidate();
                return;
            }
            if (!Interactive) return;
            int s = SquareAt(x, y);
            if (s < 0) { selected = -1; Invalidate(); return; }
            if (selected >= 0 && s != selected && HasMove(selected, s))
            {
                TryMove(selected, s, true);
                return;
            }
            if (IsUserPiece(s))
            {
                deselectOnUp = s == selected;
                selected = s;
                dragFrom = s;
                dragStarted = false;
                downX = dragX = x;
                downY = dragY = y;
                Invalidate();
            }
            else { selected = -1; Invalidate(); }
        }

        void MoveTo(float x, float y)
        {
            if (dragFrom < 0) return;
            if (!dragStarted && (Math.Abs(x - downX) > Sq * 0.15f || Math.Abs(y - downY) > Sq * 0.15f))
            {
                dragStarted = true;
                deselectOnUp = false;
                Parent?.RequestDisallowInterceptTouchEvent(true);
            }
            if (dragStarted)
            {
                dragX = x;
                dragY = y;
                hoverSquare = SquareAt(x, y - Sq * 0.6f);
                Invalidate();
            }
        }

        void Up(float x, float y)
        {
            if (dragStarted && dragFrom >= 0)
            {
                int from = dragFrom;
                int to = SquareAt(x, y - Sq * 0.6f);
                if (to < 0 || !HasMove(from, to)) to = SquareAt(x, y);
                dragStarted = false;
                dragFrom = -1;
                hoverSquare = -1;
                if (to >= 0 && to != from && HasMove(from, to)) TryMove(from, to, false);
                Invalidate();
                return;
            }
            dragFrom = -1;
            if (deselectOnUp) { selected = -1; deselectOnUp = false; Invalidate(); }
        }

        void TryMove(int from, int to, bool animate)
        {
            bool promotion = false;
            int chosen = Mv.None;
            foreach (int m in legal)
            {
                if (Mv.From(m) != from || Mv.To(m) != to) continue;
                if (Mv.IsPromotion(m)) promotion = true;
                chosen = m;
            }
            if (chosen == Mv.None) return;
            if (promotion)
            {
                promoFrom = from;
                promoTo = to;
                promoAnimate = animate;
                selected = from;
                Invalidate();
                return;
            }
            selected = -1;
            Invalidate();
            PerformHapticFeedback(FeedbackConstants.KeyboardTap);
            MoveMade?.Invoke(chosen, animate);
        }

        // ------------------------------------------------------------------ self-test helpers

        public void SimulateTap(int square)
        {
            var c = Center(square);
            Down(c.X, c.Y);
            Up(c.X, c.Y);
        }

        public void SimulateDrag(int from, int to)
        {
            var a = Center(from);
            var b = Center(to);
            float lift = Sq * 0.6f;
            Down(a.X, a.Y);
            MoveTo((a.X + b.X) / 2, (a.Y + b.Y) / 2 + lift);
            MoveTo(b.X, b.Y + lift);
            Up(b.X, b.Y + lift);
        }

        public bool PromotionPickerOpen => promoTo >= 0;

        public void SimulatePromotionChoice(int index)
        {
            var r = PromoRect(index, Sq);
            Down(r.CenterX(), r.CenterY());
        }
    }
}
