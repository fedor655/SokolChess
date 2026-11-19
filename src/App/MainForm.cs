using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Sokol.Engine;
using Sokol.Shared;
using Mv = Sokol.Engine.Move;

namespace Sokol.App
{
    /// <summary>Main window: layout and display refresh. Game flow lives in MainForm.Logic.cs.</summary>
    sealed partial class MainForm : Form
    {
        const string BotName = "Sokol";

        // ---------------------------------------------------------------- controls
        readonly BoardControl board = new BoardControl();
        readonly EvalBar evalBar = new EvalBar();
        readonly PlayerBar topBar = new PlayerBar();
        readonly PlayerBar bottomBar = new PlayerBar();
        readonly ResultOverlay overlay = new ResultOverlay { Visible = false };
        readonly Panel side = new Panel();

        readonly TextBlock title = new TextBlock { Wrap = false };
        readonly TextBlock subtitle = new TextBlock { Wrap = false };
        readonly TextBlock eloLabel = new TextBlock { Wrap = false };
        readonly TextBlock eloValue = new TextBlock { Wrap = false, Align = ContentAlignment.MiddleRight };
        readonly Slider eloSlider = new Slider();
        readonly TextBlock colorLabel = new TextBlock { Wrap = false };
        readonly SegmentedControl colorPicker = new SegmentedControl { Items = new[] { "Белыми", "Чёрными", "Случайно" } };
        readonly FlatButton newGameButton = new FlatButton { Text = "Новая игра", Primary = true };

        readonly TextBlock reviewTitle = new TextBlock { Wrap = false };
        readonly FlatButton closeReviewButton = new FlatButton { Text = "Назад к игре" };
        readonly ReviewSummary summary = new ReviewSummary();
        readonly EvalGraph graph = new EvalGraph();

        readonly TextBlock openingLabel = new TextBlock { Wrap = false };
        readonly MoveListControl moveList = new MoveListControl();
        readonly FeedbackCard feedback = new FeedbackCard();

        readonly FlatButton undoButton = new FlatButton { Icon = "↶", Text = "Назад" };
        readonly FlatButton hintButton = new FlatButton { Icon = "✦", Text = "Подсказка" };
        readonly FlatButton flipButton = new FlatButton { Icon = "⇅", Text = "Повернуть" };
        readonly FlatButton resignButton = new FlatButton { Icon = "⚑", Text = "Сдаться" };
        readonly FlatButton pgnButton = new FlatButton { Icon = "⎘", Text = "Копировать PGN" };
        readonly FlatButton reviewNewGameButton = new FlatButton { Text = "Новая игра", Primary = true };

        readonly FlatButton navFirst = new FlatButton { Text = "«" };
        readonly FlatButton navPrev = new FlatButton { Text = "‹" };
        readonly FlatButton navNext = new FlatButton { Text = "›" };
        readonly FlatButton navLast = new FlatButton { Text = "»" };
        readonly FlatButton analyzeButton = new FlatButton { Text = "Анализ партии" };

        readonly ToggleSwitch evalToggle = new ToggleSwitch { Text = "Шкала оценки" };
        readonly ToggleSwitch feedbackToggle = new ToggleSwitch { Text = "Оценка ходов" };
        readonly ToggleSwitch soundToggle = new ToggleSwitch { Text = "Звук" };
        readonly TextBlock notationLabel = new TextBlock { Wrap = false, Text = "Нотация" };
        readonly SegmentedControl notationPicker = new SegmentedControl { Items = new[] { "Ваша (K, K^)", "Межд. (N, K)", "Русская" } };

        readonly ToolTip tips = new ToolTip();
        int lastScrollPly = -1, lastScrollCount = -1;

        public MainForm()
        {
            Text = "Sokol Chess — шахматы с движком Sokol";
            BackColor = Theme.WindowBg;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            KeyPreview = true;
            MinimumSize = new Size(Theme.S(980), Theme.S(660));
            StartPosition = FormStartPosition.CenterScreen;
            var wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(Theme.S(1300), wa.Width - 40), Math.Min(Theme.S(860), wa.Height - 40));
            try { Icon = AppIcon.Create(); } catch { }

            title.Font = Theme.UiTitle;
            title.Text = BotName;
            subtitle.Font = Theme.UiSmall;
            subtitle.TextColor = Theme.TextDim;
            eloLabel.Font = Theme.UiBold;
            eloLabel.Text = "Сила бота";
            eloValue.Font = Theme.UiBold;
            eloValue.TextColor = Theme.Accent;
            colorLabel.Font = Theme.UiBold;
            colorLabel.Text = "Вы играете";
            reviewTitle.Font = Theme.UiTitle;
            reviewTitle.Text = "Анализ партии";
            openingLabel.Font = Theme.UiSmall;
            openingLabel.TextColor = Theme.TextDim;
            notationLabel.Font = Theme.UiSmall;
            notationLabel.TextColor = Theme.TextDim;
            foreach (var b in new[] { undoButton, hintButton, flipButton, resignButton, pgnButton }) b.Font = Theme.UiSmallBold;
            foreach (var b in new[] { navFirst, navPrev, navNext, navLast }) b.Font = Theme.UiLarge;
            analyzeButton.Font = Theme.UiSmallBold;
            closeReviewButton.Font = Theme.UiSmallBold;

            side.BackColor = Theme.PanelBg;
            foreach (Control c in new Control[] {
                title, subtitle, eloLabel, eloValue, eloSlider, colorLabel, colorPicker, newGameButton,
                reviewTitle, closeReviewButton, summary, graph,
                openingLabel, moveList, feedback,
                undoButton, hintButton, flipButton, resignButton, pgnButton, reviewNewGameButton,
                navFirst, navPrev, navNext, navLast, analyzeButton,
                evalToggle, feedbackToggle, soundToggle, notationLabel, notationPicker })
            {
                c.BackColor = Theme.PanelBg;
                side.Controls.Add(c);
            }
            board.Controls.Add(overlay);
            Controls.Add(board);
            Controls.Add(evalBar);
            Controls.Add(topBar);
            Controls.Add(bottomBar);
            Controls.Add(side);

            tips.SetToolTip(undoButton, "Отменить последний ход (Ctrl+Z)");
            tips.SetToolTip(hintButton, "Показать лучший ход (H)");
            tips.SetToolTip(flipButton, "Перевернуть доску (F)");
            tips.SetToolTip(resignButton, "Сдаться");
            tips.SetToolTip(navFirst, "К началу (Home)");
            tips.SetToolTip(navPrev, "Предыдущий ход (←)");
            tips.SetToolTip(navNext, "Следующий ход (→)");
            tips.SetToolTip(navLast, "К последнему ходу (End)");

            analysis = new AnalysisService(a => { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); });
            analysis.Result += OnAnalysisResult;
            WireEvents();
            LoadSettings();
            analysis.WorkerCount = 1;
        }

        // ---------------------------------------------------------------- layout

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutUi();
        }

        void LayoutUi()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            int pad = Theme.S(16);
            int sideW = Theme.S(400);
            int barH = Theme.S(40);
            int evalW = showEvalBar ? Theme.S(26) : 0;
            int gap = Theme.S(8);

            int availH = ClientSize.Height - pad * 2 - barH * 2 - gap * 2;
            int availW = ClientSize.Width - sideW - pad * 3 - evalW - (showEvalBar ? gap : 0);
            int size = Math.Max(Theme.S(240), Math.Min(availH, availW)) / 8 * 8;

            int boardX = pad + evalW + (showEvalBar ? gap : 0);
            int boardY = pad + barH + gap + Math.Max(0, (availH - size) / 2);
            board.SetBounds(boardX, boardY, size, size);
            evalBar.Visible = showEvalBar;
            evalBar.SetBounds(pad, boardY, Theme.S(26), size);
            topBar.SetBounds(boardX, boardY - gap - barH, size, barH);
            bottomBar.SetBounds(boardX, boardY + size + gap, size, barH);

            int ow = Math.Min(size - Theme.S(40), Theme.S(320)), oh = Theme.S(250);
            overlay.SetBounds((size - ow) / 2, (size - oh) / 2, ow, oh);

            int sideX = boardX + size + pad;
            side.SetBounds(sideX, pad, ClientSize.Width - sideX - pad, ClientSize.Height - pad * 2);
            LayoutSide();
        }

        void LayoutSide()
        {
            int w = side.ClientSize.Width, h = side.ClientSize.Height;
            int p = Theme.S(14);
            int iw = w - p * 2;
            int y = p;
            bool review = reviewMode;

            foreach (Control c in new Control[] { title, subtitle, eloLabel, eloValue, eloSlider, colorLabel, colorPicker, newGameButton })
                c.Visible = !review;
            foreach (Control c in new Control[] { reviewTitle, closeReviewButton, summary, graph })
                c.Visible = review;
            undoButton.Visible = hintButton.Visible = resignButton.Visible = !review;
            pgnButton.Visible = reviewNewGameButton.Visible = review;

            // Bottom-anchored blocks first.
            int bottom = h - p;
            int rowH = Theme.S(30);
            notationPicker.SetBounds(p + Theme.S(70), bottom - rowH, iw - Theme.S(70), rowH);
            notationLabel.SetBounds(p, bottom - rowH, Theme.S(70), rowH);
            bottom -= rowH + Theme.S(6);
            int tw = iw / 3;
            evalToggle.SetBounds(p, bottom - Theme.S(26), tw + Theme.S(10), Theme.S(26));
            feedbackToggle.SetBounds(p + tw + Theme.S(10), bottom - Theme.S(26), tw + Theme.S(10), Theme.S(26));
            soundToggle.SetBounds(p + tw * 2 + Theme.S(20), bottom - Theme.S(26), iw - tw * 2 - Theme.S(20), Theme.S(26));
            bottom -= Theme.S(26) + Theme.S(10);

            int navH = Theme.S(40);
            int navW = Theme.S(46);
            navFirst.SetBounds(p, bottom - navH, navW, navH);
            navPrev.SetBounds(p + navW + Theme.S(6), bottom - navH, navW, navH);
            navNext.SetBounds(p + (navW + Theme.S(6)) * 2, bottom - navH, navW, navH);
            navLast.SetBounds(p + (navW + Theme.S(6)) * 3, bottom - navH, navW, navH);
            int ax = p + (navW + Theme.S(6)) * 4;
            analyzeButton.Visible = !review;
            analyzeButton.SetBounds(ax, bottom - navH, iw - (ax - p), navH);
            if (review) { navW = (iw - Theme.S(18)) / 4; for (int i = 0; i < 4; i++) new[] { navFirst, navPrev, navNext, navLast }[i].SetBounds(p + i * (navW + Theme.S(6)), bottom - navH, navW, navH); }
            bottom -= navH + Theme.S(8);

            int actH = Theme.S(40);
            if (!review)
            {
                int bw = (iw - Theme.S(18)) / 4;
                undoButton.SetBounds(p, bottom - actH, bw, actH);
                hintButton.SetBounds(p + bw + Theme.S(6), bottom - actH, bw, actH);
                flipButton.SetBounds(p + (bw + Theme.S(6)) * 2, bottom - actH, bw, actH);
                resignButton.SetBounds(p + (bw + Theme.S(6)) * 3, bottom - actH, iw - (bw + Theme.S(6)) * 3, actH);
            }
            else
            {
                int bw = (iw - Theme.S(12)) / 3;
                reviewNewGameButton.SetBounds(p, bottom - actH, bw, actH);
                pgnButton.SetBounds(p + bw + Theme.S(6), bottom - actH, bw, actH);
                flipButton.SetBounds(p + (bw + Theme.S(6)) * 2, bottom - actH, iw - (bw + Theme.S(6)) * 2, actH);
            }
            bottom -= actH + Theme.S(10);

            int fbH = Theme.S(150);
            feedback.SetBounds(p, bottom - fbH, iw, fbH);
            bottom -= fbH + Theme.S(10);

            // Top blocks.
            if (!review)
            {
                title.SetBounds(p, y, iw, Theme.S(30)); y += Theme.S(30);
                subtitle.SetBounds(p, y, iw, Theme.S(20)); y += Theme.S(26);
                eloLabel.SetBounds(p, y, iw / 2, Theme.S(22));
                eloValue.SetBounds(p + iw / 2, y, iw - iw / 2, Theme.S(22)); y += Theme.S(24);
                eloSlider.SetBounds(p - Theme.S(4), y, iw + Theme.S(8), Theme.S(30)); y += Theme.S(36);
                colorLabel.SetBounds(p, y, iw, Theme.S(22)); y += Theme.S(24);
                colorPicker.SetBounds(p, y, iw, Theme.S(34)); y += Theme.S(42);
                newGameButton.SetBounds(p, y, iw, Theme.S(44)); y += Theme.S(52);
            }
            else
            {
                reviewTitle.SetBounds(p, y, iw - Theme.S(130), Theme.S(36));
                closeReviewButton.SetBounds(p + iw - Theme.S(124), y + Theme.S(2), Theme.S(124), Theme.S(34));
                y += Theme.S(44);
                int sumH = Math.Min(Theme.S(290), Math.Max(Theme.S(200), (bottom - y) / 2));
                summary.SetBounds(p, y, iw, sumH); y += sumH + Theme.S(8);
                graph.SetBounds(p, y, iw, Theme.S(64)); y += Theme.S(72);
            }
            openingLabel.SetBounds(p, y, iw, Theme.S(22)); y += Theme.S(24);
            moveList.SetBounds(p, y, iw, Math.Max(Theme.S(60), bottom - y));
        }

        // ---------------------------------------------------------------- refresh

        static string LevelName(int elo)
        {
            if (elo < 600) return "Новичок";
            if (elo < 1000) return "Начинающий";
            if (elo < 1400) return "Любитель";
            if (elo < 1800) return "Клубный игрок";
            if (elo < 2100) return "Кандидат в мастера";
            if (elo < 2400) return "Мастер";
            if (elo < 2700) return "Гроссмейстер";
            if (elo < 3000) return "Элита";
            return "Полная сила движка";
        }

        void RefreshAll()
        {
            RefreshBoard(false);
            RefreshSide();
        }

        Board ViewBoard() => viewPly == game.Ply ? game.Current.Clone() : game.BoardAt(viewPly);

        void RefreshBoard(bool animate)
        {
            var b = ViewBoard();
            board.Flipped = flipped;
            evalBar.Flipped = flipped;
            board.UserColor = userColor;
            board.Interactive = CanUserMove();
            board.SetPosition(b, viewPly > 0 ? game.Moves[viewPly - 1] : Mv.None, animate);
            RefreshDecorations();
            RefreshBars(b);
            RefreshEval();
        }

        void RefreshDecorations()
        {
            board.Arrows.Clear();
            board.BadgeClass = MoveClass.None;
            board.BadgeSquare = -1;
            bool show = showFeedback || reviewMode || game.Result != GameResult.None;
            if (viewPly > 0 && show)
            {
                var r = game.Reviews[viewPly - 1];
                if (r != null)
                {
                    board.BadgeClass = r.Class;
                    board.BadgeSquare = Mv.To(game.Moves[viewPly - 1]);
                    bool good = r.Class == MoveClass.Best || r.Class == MoveClass.Brilliant || r.Class == MoveClass.Great ||
                                r.Class == MoveClass.Book || r.Class == MoveClass.Forced;
                    if (!good && r.BestMove != Mv.None)
                        board.Arrows.Add(new Arrow(Mv.From(r.BestMove), Mv.To(r.BestMove), Color.FromArgb(190, 129, 182, 76)));
                }
            }
            if (hintMove != Mv.None && viewPly == game.Ply)
                board.Arrows.Add(new Arrow(Mv.From(hintMove), Mv.To(hintMove), Color.FromArgb(200, 255, 170, 0)));
            board.Invalidate();
        }

        void RefreshBars(Board b)
        {
            PlayerBar userBar = flipped == (userColor == Piece.Black) ? bottomBar : topBar;
            PlayerBar botBar = userBar == bottomBar ? topBar : bottomBar;
            userBar.PlayerName = "Вы";
            userBar.Subtitle = "";
            userBar.IsBot = false;
            userBar.Color = userColor;
            botBar.PlayerName = BotName;
            botBar.Subtitle = $"({botElo})";
            botBar.IsBot = true;
            botBar.Color = userColor ^ 1;
            botBar.Thinking = botThinking;
            userBar.Thinking = false;
            bool playing = game.Result == GameResult.None && !reviewMode;
            userBar.ToMove = playing && b.SideToMove == userColor && viewPly == game.Ply;
            botBar.ToMove = false;
            FillCaptured(b, userBar, userColor);
            FillCaptured(b, botBar, userColor ^ 1);
            topBar.Invalidate();
            bottomBar.Invalidate();
        }

        static readonly int[] StartCounts = { 8, 2, 2, 2, 1, 0 };
        static readonly int[] PieceValue = { 1, 3, 3, 5, 9, 0 };

        static void FillCaptured(Board b, PlayerBar bar, int color)
        {
            bar.Captured.Clear();
            int them = color ^ 1;
            int myMat = 0, theirMat = 0;
            for (int t = 0; t < 5; t++)
            {
                int theirs = BB.PopCount(b.Pieces[Piece.Make(them, t)]);
                int mine = BB.PopCount(b.Pieces[Piece.Make(color, t)]);
                myMat += mine * PieceValue[t];
                theirMat += theirs * PieceValue[t];
                for (int k = theirs; k < StartCounts[t]; k++) bar.Captured.Add(Piece.Make(them, t));
            }
            bar.MaterialLead = myMat - theirMat;
        }

        void RefreshEval()
        {
            PositionAnalysis a = game.Analyses[viewPly] ?? (livePly == viewPly ? liveAnalysis : null);
            if (a == null) return;
            var b = viewPly == game.Ply ? game.Current : null;
            int stm = b != null ? b.SideToMove : (viewPly % 2 == 0 ? Piece.White : Piece.Black);
            int s = a.Score;
            evalBar.SetScore(stm == Piece.White ? s : -s);
        }

        void RefreshSide()
        {
            subtitle.Text = $"Собственный шахматный движок · {LevelName(botElo)}";
            eloValue.Text = $"{botElo} ELO";
            subtitle.Invalidate();
            eloValue.Invalidate();

            string opening = game.OpeningName(viewPly);
            openingLabel.Text = opening != null ? "Дебют: " + opening : (game.Ply == 0 ? "Начальная позиция" : "");
            openingLabel.Invalidate();

            // Move list.
            bool showClasses = showFeedback || reviewMode || game.Result != GameResult.None;
            moveList.Sans.Clear();
            moveList.Sans.AddRange(displaySans);
            moveList.Classes.Clear();
            for (int i = 0; i < game.Reviews.Count; i++)
                moveList.Classes.Add(showClasses && game.Reviews[i] != null ? game.Reviews[i].Class : MoveClass.None);
            moveList.SelectedMove = viewPly - 1;
            // Auto-scroll only when the selection or the number of moves changes (keeps manual scrolling intact).
            if (viewPly != lastScrollPly || game.Ply != lastScrollCount)
            {
                moveList.ScrollToMove(viewPly - 1);
                lastScrollPly = viewPly;
                lastScrollCount = game.Ply;
            }
            moveList.Invalidate();

            RefreshFeedback();

            bool over = game.Result != GameResult.None;
            undoButton.Enabled = !reviewMode && game.Ply > 0 && (game.Ply > 1 || userColor == Piece.White);
            hintButton.Enabled = CanUserMove();
            resignButton.Enabled = !over && game.Ply > 0;
            analyzeButton.Enabled = over;
            navFirst.Enabled = navPrev.Enabled = viewPly > 0;
            navNext.Enabled = navLast.Enabled = viewPly < game.Ply;
            colorPicker.Enabled = true;

            if (reviewMode) RefreshReview();
        }

        void RefreshFeedback()
        {
            feedback.Class = MoveClass.None;
            feedback.Eval = "";
            int moveIndex;
            bool browsing = reviewMode || viewPly < game.Ply || game.Result != GameResult.None;
            if (browsing) moveIndex = viewPly - 1;
            else
            {
                moveIndex = -1;
                for (int i = game.Ply - 1; i >= 0; i--)
                    if ((i % 2 == 0) == (userColor == Piece.White)) { moveIndex = i; break; }
            }

            if (!browsing && hintMove != Mv.None)
            {
                var b = game.Current;
                var a = game.Analyses[game.Ply];
                feedback.Title = "Подсказка";
                string line = a != null && a.Lines.Count > 0 ? Notation.FormatLine(b, a.Lines[0].Moves, style, 6) : "";
                feedback.Body = $"Лучший ход: {Notation.ToSan(b, hintMove, style)}." + (line.Length > 0 ? $" Продолжение: {line}" : "");
                if (a != null) feedback.Eval = Review.FormatScore(b.SideToMove == Piece.White ? a.Score : -a.Score);
            }
            else if (!browsing && hintPending)
            {
                feedback.Title = "Подсказка";
                feedback.Body = "Движок ищет лучший ход…";
            }
            else if (moveIndex < 0)
            {
                feedback.Title = game.Ply == 0 && !reviewMode ? "Ваш ход" : "Начальная позиция";
                feedback.Body = userColor == Piece.White || reviewMode
                    ? "Сделайте ход: перетащите фигуру или щёлкните по ней, затем по клетке. Движок оценит каждый ваш ход, как на chess.com."
                    : "Бот играет белыми и ходит первым. После каждого вашего хода движок покажет его оценку.";
            }
            else if (!showFeedback && !reviewMode && game.Result == GameResult.None)
            {
                feedback.Title = "Оценка ходов выключена";
                feedback.Body = "Включите переключатель «Оценка ходов» или откройте «Анализ партии» после игры.";
            }
            else
            {
                var r = game.Reviews[moveIndex];
                string who = (moveIndex % 2 == 0) == (userColor == Piece.White) ? "Ваш ход" : "Ход бота";
                if (r != null)
                {
                    feedback.Class = r.Class;
                    feedback.Title = Review.ClassName(r.Class);
                    feedback.Body = $"{who} {(moveIndex / 2) + 1}{(moveIndex % 2 == 0 ? "." : "...")} " + r.Comment;
                    feedback.Eval = Review.FormatScore(r.WhiteScoreAfter);
                }
                else
                {
                    feedback.Title = "Анализ…";
                    feedback.Body = $"{who} {displaySans[moveIndex]}: движок оценивает ход на максимальной силе…";
                }
            }
            feedback.Invalidate();
        }

        void RefreshReview()
        {
            var userAcc = new List<double>();
            var botAcc = new List<double>();
            summary.LeftCounts.Clear();
            summary.RightCounts.Clear();
            summary.LeftName = "Вы";
            summary.RightName = BotName;
            for (int i = 0; i < game.Reviews.Count; i++)
            {
                var r = game.Reviews[i];
                if (r == null) continue;
                bool user = (i % 2 == 0) == (userColor == Piece.White);
                var counts = user ? summary.LeftCounts : summary.RightCounts;
                counts.TryGetValue(r.Class, out int n);
                counts[r.Class] = n + 1;
                if (r.Class != MoveClass.Book && r.Class != MoveClass.Forced) (user ? userAcc : botAcc).Add(r.Accuracy);
            }
            summary.LeftAccuracy = userAcc.Count > 0 ? Review.GameAccuracy(userAcc) : (double?)null;
            summary.RightAccuracy = botAcc.Count > 0 ? Review.GameAccuracy(botAcc) : (double?)null;
            int pending = 0;
            foreach (var r in game.Reviews) if (r == null) pending++;
            summary.Status = pending > 0 ? $"Анализ… осталось ходов: {pending}" : "";
            summary.Invalidate();

            graph.Scores.Clear();
            for (int ply = 0; ply < game.Analyses.Count; ply++)
            {
                var a = game.Analyses[ply];
                if (a == null) { graph.Scores.Add(null); continue; }
                int s = a.Score;
                graph.Scores.Add(ply % 2 == 0 ? s : -s);
            }
            graph.Classes.Clear();
            foreach (var r in game.Reviews) graph.Classes.Add(r != null ? r.Class : MoveClass.None);
            graph.CurrentPly = viewPly;
            graph.Invalidate();
        }
    }
}
