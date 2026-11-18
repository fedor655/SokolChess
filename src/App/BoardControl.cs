using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Sokol.Engine;
using Mv = Sokol.Engine.Move;

namespace Sokol.App
{
    sealed class Arrow
    {
        public int From, To;
        public Color Color;
        public Arrow(int from, int to, Color color) { From = from; To = to; Color = color; }
    }

    /// <summary>Chess board: drawing, drag &amp; drop / click moves, promotion picker, arrows, badges and animation.</summary>
    sealed class BoardControl : PaintedControl
    {
        public Board Position { get; private set; } = new Board();
        public bool Flipped;
        public bool Interactive;
        public int UserColor = Piece.White;
        public int LastMove = Mv.None;
        public MoveClass BadgeClass = MoveClass.None;
        public int BadgeSquare = -1;
        public readonly List<Arrow> Arrows = new List<Arrow>();
        /// <summary>Raised when the user completes a legal move. Arg2: animate (click move) or not (drag).</summary>
        public event Action<int, bool> MoveMade;

        List<int> legal = new List<int>();
        int selected = -1;
        bool deselectOnUp;
        int dragFrom = -1;
        bool dragStarted;
        Point mouseDown, dragPos;
        int hoverSquare = -1;
        int promoFrom = -1, promoTo = -1;
        bool promoAnimate;

        sealed class Anim { public int Piece, From, To; }
        readonly List<Anim> anims = new List<Anim>();
        DateTime animStart;
        const double AnimMs = 170;
        readonly Timer timer = new Timer { Interval = 15 };

        public BoardControl()
        {
            timer.Tick += (s, e) =>
            {
                if ((DateTime.Now - animStart).TotalMilliseconds >= AnimMs) { anims.Clear(); timer.Stop(); }
                Invalidate();
            };
        }

        int Sq8 => Math.Max(8, Math.Min(Width, Height) / 8);
        int Left0 => (Width - Sq8 * 8) / 2;
        int Top0 => (Height - Sq8 * 8) / 2;

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
                animStart = DateTime.Now;
                timer.Start();
            }
            Invalidate();
        }

        public void RefreshInteractivity()
        {
            legal = Interactive ? Position.LegalMoves() : new List<int>();
            if (!Interactive) { selected = -1; dragFrom = -1; dragStarted = false; promoTo = -1; }
            Invalidate();
        }

        public void ClearSelection()
        {
            selected = -1;
            dragFrom = -1;
            dragStarted = false;
            promoTo = -1;
            Invalidate();
        }

        // ------------------------------------------------------------------ geometry

        Point Origin(int square)
        {
            int f = square & 7, r = square >> 3;
            int col = Flipped ? 7 - f : f, row = Flipped ? r : 7 - r;
            return new Point(Left0 + col * Sq8, Top0 + row * Sq8);
        }

        PointF Center(int square)
        {
            var p = Origin(square);
            return new PointF(p.X + Sq8 / 2f, p.Y + Sq8 / 2f);
        }

        int SquareAt(Point p)
        {
            int col = (p.X - Left0) / Sq8, row = (p.Y - Top0) / Sq8;
            if (p.X < Left0 || p.Y < Top0 || col < 0 || col > 7 || row < 0 || row > 7) return -1;
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

        // ------------------------------------------------------------------ painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.WindowBg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int sq = Sq8;
            int kingInCheck = Position.InCheck() ? Position.KingSquare(Position.SideToMove) : -1;

            // Squares and highlights.
            for (int s = 0; s < 64; s++)
            {
                var o = Origin(s);
                bool light = ((s >> 3) + (s & 7)) % 2 == 1;
                Color c = light ? Theme.LightSquare : Theme.DarkSquare;
                bool hl = (LastMove != Mv.None && (Mv.From(LastMove) == s || Mv.To(LastMove) == s)) || s == selected;
                if (hl) c = light ? Theme.LightHighlight : Theme.DarkHighlight;
                using (var b = new SolidBrush(c)) g.FillRectangle(b, o.X, o.Y, sq, sq);
                if (s == kingInCheck)
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(o.X - sq * 0.1f, o.Y - sq * 0.1f, sq * 1.2f, sq * 1.2f);
                        using (var pgb = new PathGradientBrush(path)
                        {
                            CenterColor = Color.FromArgb(255, 255, 30, 30),
                            SurroundColors = new[] { Color.FromArgb(0, 255, 0, 0) },
                            FocusScales = new PointF(0.25f, 0.25f),
                        })
                        {
                            g.SetClip(new Rectangle(o.X, o.Y, sq, sq));
                            g.FillPath(pgb, path);
                            g.ResetClip();
                        }
                    }
                }
                if (dragStarted && s == hoverSquare)
                    using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), Math.Max(2, sq / 16f)))
                        g.DrawRectangle(pen, o.X + sq / 32f, o.Y + sq / 32f, sq - sq / 16f, sq - sq / 16f);
            }

            DrawCoordinates(g, sq);

            // Pieces.
            var hidden = new HashSet<int>();
            double t = anims.Count > 0 ? Math.Min(1, (DateTime.Now - animStart).TotalMilliseconds / AnimMs) : 1;
            foreach (var a in anims) hidden.Add(a.To);
            if (dragStarted && dragFrom >= 0) hidden.Add(dragFrom);
            for (int s = 0; s < 64; s++)
            {
                int p = Position.Squares[s];
                if (p == Piece.None || hidden.Contains(s)) continue;
                var o = Origin(s);
                g.DrawImage(PieceRenderer.Get(p, sq), o.X, o.Y, sq, sq);
            }

            // Legal move hints.
            if (selected >= 0 && promoTo < 0)
            {
                using (var dot = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                using (var ring = new Pen(Color.FromArgb(40, 0, 0, 0), sq * 0.09f))
                {
                    foreach (int m in legal)
                    {
                        if (Mv.From(m) != selected) continue;
                        if (Mv.IsPromotion(m) && Mv.PromotionType(m) != Piece.Queen) continue;
                        var o = Origin(Mv.To(m));
                        if (Position.Squares[Mv.To(m)] != Piece.None)
                            g.DrawEllipse(ring, o.X + sq * 0.06f, o.Y + sq * 0.06f, sq * 0.88f, sq * 0.88f);
                        else
                            g.FillEllipse(dot, o.X + sq * 0.34f, o.Y + sq * 0.34f, sq * 0.32f, sq * 0.32f);
                    }
                }
            }

            foreach (var arrow in Arrows) DrawArrow(g, arrow, sq);

            // Animated pieces.
            double ease = 1 - Math.Pow(1 - t, 3);
            foreach (var a in anims)
            {
                if (a.Piece == Piece.None) continue;
                var p0 = Origin(a.From);
                var p1 = Origin(a.To);
                float x = (float)(p0.X + (p1.X - p0.X) * ease), y = (float)(p0.Y + (p1.Y - p0.Y) * ease);
                g.DrawImage(PieceRenderer.Get(a.Piece, sq), x, y, sq, sq);
            }

            if (BadgeClass != MoveClass.None && BadgeSquare >= 0 && anims.Count == 0)
            {
                var o = Origin(BadgeSquare);
                float d = sq * 0.40f;
                Theme.DrawBadge(g, BadgeClass, o.X + sq - d * 0.45f, o.Y + d * 0.45f, d);
            }

            // Dragged piece follows the cursor.
            if (dragStarted && dragFrom >= 0 && Position.Squares[dragFrom] != Piece.None)
            {
                int big = (int)(sq * 1.08);
                g.DrawImage(PieceRenderer.Get(Position.Squares[dragFrom], big), dragPos.X - big / 2, dragPos.Y - big / 2, big, big);
            }

            if (promoTo >= 0) DrawPromotionPicker(g, sq);
        }

        void DrawCoordinates(Graphics g, int sq)
        {
            using (var font = new Font("Segoe UI Semibold", Math.Max(6f, sq * 0.16f), GraphicsUnit.Pixel))
            {
                for (int i = 0; i < 8; i++)
                {
                    // Rank numbers on the left column, file letters on the bottom row.
                    int rankSq = Flipped ? (i * 8 + 7) : (i * 8);
                    var o = Origin(rankSq);
                    bool light = ((rankSq >> 3) + (rankSq & 7)) % 2 == 1;
                    using (var b = new SolidBrush(light ? Theme.DarkSquare : Theme.LightSquare))
                        g.DrawString(((char)('1' + i)).ToString(), font, b, o.X + sq * 0.04f, o.Y + sq * 0.02f);

                    int fileSq = Flipped ? (56 + i) : i;
                    o = Origin(fileSq);
                    light = ((fileSq >> 3) + (fileSq & 7)) % 2 == 1;
                    string letter = ((char)('a' + i)).ToString();
                    SizeF ts = g.MeasureString(letter, font);
                    using (var b = new SolidBrush(light ? Theme.DarkSquare : Theme.LightSquare))
                        g.DrawString(letter, font, b, o.X + sq - ts.Width - sq * 0.02f, o.Y + sq - ts.Height - sq * 0.0f);
                }
            }
        }

        void DrawArrow(Graphics g, Arrow a, int sq)
        {
            PointF p0 = Center(a.From), p1 = Center(a.To);
            var pts = new List<PointF> { p0 };
            int df = (a.To & 7) - (a.From & 7), dr = (a.To >> 3) - (a.From >> 3);
            bool knight = (Math.Abs(df) == 1 && Math.Abs(dr) == 2) || (Math.Abs(df) == 2 && Math.Abs(dr) == 1);
            if (knight)
            {
                // L-shaped arrow: long leg first.
                if (Math.Abs(dr) == 2) pts.Add(new PointF(p0.X, p1.Y));
                else pts.Add(new PointF(p1.X, p0.Y));
            }
            pts.Add(p1);

            float width = sq * 0.18f, headLen = sq * 0.42f, headW = sq * 0.44f;
            PointF last = pts[pts.Count - 2];
            float vx = p1.X - last.X, vy = p1.Y - last.Y;
            float len = (float)Math.Sqrt(vx * vx + vy * vy);
            if (len < 1) return;
            vx /= len; vy /= len;
            // Start a bit away from the piece center.
            if (!knight)
            {
                pts[0] = new PointF(p0.X + vx * sq * 0.25f, p0.Y + vy * sq * 0.25f);
            }
            var tip = new PointF(p1.X + vx * sq * 0.02f, p1.Y + vy * sq * 0.02f);
            var baseP = new PointF(tip.X - vx * headLen, tip.Y - vy * headLen);
            pts[pts.Count - 1] = baseP;
            using (var pen = new Pen(a.Color, width) { LineJoin = LineJoin.Miter, StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                g.DrawLines(pen, pts.ToArray());
            float nx = -vy, ny = vx;
            var head = new[]
            {
                tip,
                new PointF(baseP.X + nx * headW / 2, baseP.Y + ny * headW / 2),
                new PointF(baseP.X - nx * headW / 2, baseP.Y - ny * headW / 2),
            };
            using (var b = new SolidBrush(a.Color)) g.FillPolygon(b, head);
        }

        static readonly int[] PromoOrder = { Piece.Queen, Piece.Knight, Piece.Rook, Piece.Bishop };

        Rectangle PromoRect(int index, int sq)
        {
            var o = Origin(promoTo);
            bool down = o.Y < Top0 + 4 * sq; // picker extends towards the center of the board
            return new Rectangle(o.X, down ? o.Y + index * sq : o.Y - index * sq, sq, sq);
        }

        void DrawPromotionPicker(Graphics g, int sq)
        {
            using (var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillRectangle(dim, Left0, Top0, sq * 8, sq * 8);
            int color = Piece.ColorOf(Position.Squares[promoFrom]);
            for (int i = 0; i < 4; i++)
            {
                var r = PromoRect(i, sq);
                using (var b = new SolidBrush(Color.FromArgb(245, 245, 245))) g.FillEllipse(b, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
                g.DrawImage(PieceRenderer.Get(Piece.Make(color, PromoOrder[i]), sq), r);
            }
        }

        // ------------------------------------------------------------------ input

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int sq = Sq8;
            if (e.Button == MouseButtons.Right) { ClearSelection(); return; }
            if (e.Button != MouseButtons.Left) return;

            if (promoTo >= 0)
            {
                for (int i = 0; i < 4; i++)
                    if (PromoRect(i, sq).Contains(e.Location))
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

            int s = SquareAt(e.Location);
            if (s < 0) { ClearSelection(); return; }
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
                mouseDown = e.Location;
                dragPos = e.Location;
                Invalidate();
            }
            else ClearSelection();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragFrom >= 0 && (e.Button & MouseButtons.Left) != 0)
            {
                if (!dragStarted && (Math.Abs(e.X - mouseDown.X) > 3 || Math.Abs(e.Y - mouseDown.Y) > 3))
                {
                    dragStarted = true;
                    deselectOnUp = false;
                }
                if (dragStarted)
                {
                    dragPos = e.Location;
                    hoverSquare = SquareAt(e.Location);
                    Invalidate();
                }
            }
            Cursor = Interactive && (IsUserPiece(SquareAt(e.Location)) || dragStarted) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            if (dragStarted && dragFrom >= 0)
            {
                int from = dragFrom;
                int to = SquareAt(e.Location);
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
            MoveMade?.Invoke(chosen, animate);
        }

        // ------------------------------------------------------------------ input simulation (self-test)

        Point CenterPoint(int square)
        {
            var o = Origin(square);
            return new Point(o.X + Sq8 / 2, o.Y + Sq8 / 2);
        }

        internal void SimulateClick(int square)
        {
            var p = CenterPoint(square);
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0));
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0));
        }

        internal void SimulateDrag(int from, int to)
        {
            var a = CenterPoint(from);
            var b = CenterPoint(to);
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, a.X, a.Y, 0));
            OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, (a.X + b.X) / 2, (a.Y + b.Y) / 2, 0));
            OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, b.X, b.Y, 0));
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, b.X, b.Y, 0));
        }

        internal bool PromotionPickerOpen => promoTo >= 0;

        internal void SimulatePromotionChoice(int index)
        {
            var r = PromoRect(index, Sq8);
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, r.X + r.Width / 2, r.Y + r.Height / 2, 0));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
