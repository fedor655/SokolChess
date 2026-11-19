using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Sokol.Engine;
using Sokol.Shared;
using Mv = Sokol.Engine.Move;

namespace Sokol.Droid
{
    /// <summary>Main screen: layout and display refresh. Game flow lives in MainActivity.Logic.cs.</summary>
    [Activity(Label = "Sokol Chess", MainLauncher = true, Name = "com.sokol.chess.MainActivity",
        Icon = "@mipmap/ic_launcher", Theme = "@android:style/Theme.Material.NoActionBar",
        ScreenOrientation = ScreenOrientation.Portrait, LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden |
                               ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
    public partial class MainActivity : Activity
    {
        const string BotName = "Sokol";

        BoardView board;
        EvalBarView evalBar;
        PlayerBarView topBar, bottomBar;
        TextView titleText, eloChip, openingText;
        MoveStrip moveStrip;
        FeedbackView feedback;
        SummaryView summary;
        GraphView graph;
        LinearLayout actions;
        FrameLayout overlay;
        TextView overlayTitle, overlaySubtitle;
        IconButton undoBtn, hintBtn, prevBtn, nextBtn, flipBtn, resignBtn, newBtn, shareBtn, closeBtn, reviewBtn, settingsBtn;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Ui.Init(this);
            AndroidSounds.Init();
            Window.SetStatusBarColor(Ui.WindowBg);
            Window.SetNavigationBarColor(Ui.PanelBg);
            BuildUi();
            LoadSettings();
            analysis = new AnalysisService(a => { if (!destroyed) RunOnUiThread(() => { if (!destroyed) a(); }); })
            {
                HashMb = 16,
                TimeMs = 1000,
            };
            analysis.Result += OnAnalysisResult;
            analysis.WorkerCount = 1;
            if (Intent?.GetStringExtra("selftest") != null) { RunSelfTest(); return; }
            if (!RestoreGame()) StartNewGame();
        }

        void BuildUi()
        {
            var root = new FrameLayout(this);
            root.SetBackgroundColor(Ui.WindowBg);
            var column = new LinearLayout(this) { Orientation = Orientation.Vertical };
            column.SetPadding(Ui.Dp(8), Ui.Dp(4), Ui.Dp(8), 0);
            root.AddView(column, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            // Header: title + current bot strength (tap opens settings).
            var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            header.SetGravity(GravityFlags.CenterVertical);
            titleText = new TextView(this) { Text = "Sokol Chess" };
            titleText.SetTextColor(Ui.Text);
            titleText.TextSize = 20;
            titleText.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
            header.AddView(titleText, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
            eloChip = new TextView(this);
            eloChip.SetTextColor(Color.White);
            eloChip.TextSize = 13;
            eloChip.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
            eloChip.SetPadding(Ui.Dp(12), Ui.Dp(6), Ui.Dp(12), Ui.Dp(6));
            eloChip.Background = Ui.Rounded(Ui.Accent, 14);
            eloChip.Click += (s, e) => ShowSettings();
            header.AddView(eloChip);
            column.AddView(header, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Ui.Dp(48)));

            topBar = new PlayerBarView(this);
            column.AddView(topBar);

            var boardRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            evalBar = new EvalBarView(this);
            boardRow.AddView(evalBar, new LinearLayout.LayoutParams(Ui.Dp(14), ViewGroup.LayoutParams.MatchParent) { RightMargin = Ui.Dp(4) });
            board = new BoardView(this);
            board.MoveMade += (m, animate) => OnUserMove(m, animate);
            var boardFrame = new FrameLayout(this);
            boardFrame.AddView(board, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            boardRow.AddView(boardFrame, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
            column.AddView(boardRow, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

            bottomBar = new PlayerBarView(this);
            column.AddView(bottomBar);

            // Scrollable middle part: opening, moves, feedback, review summary.
            var scroll = new ScrollView(this) { FillViewport = true };
            var inner = new LinearLayout(this) { Orientation = Orientation.Vertical };
            scroll.AddView(inner);
            openingText = new TextView(this);
            openingText.SetTextColor(Ui.TextDim);
            openingText.TextSize = 12;
            openingText.SetSingleLine(true);
            openingText.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            inner.AddView(openingText, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Ui.Dp(2) });
            moveStrip = new MoveStrip(this);
            moveStrip.Background = Ui.Rounded(Ui.CardBg, 8);
            moveStrip.MoveClicked += i => Navigate(i + 1);
            inner.AddView(moveStrip, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Ui.Dp(44)) { TopMargin = Ui.Dp(4) });
            feedback = new FeedbackView(this);
            inner.AddView(feedback, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Ui.Dp(8) });
            summary = new SummaryView(this);
            inner.AddView(summary, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Ui.Dp(8) });
            graph = new GraphView(this);
            graph.PlyClicked += p => Navigate(p);
            inner.AddView(graph, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Ui.Dp(8), BottomMargin = Ui.Dp(8) });
            column.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));

            // Bottom icon bar.
            actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            actions.SetBackgroundColor(Ui.PanelBg);
            undoBtn = Icon(IconKind.Undo, "Назад", Undo);
            hintBtn = Icon(IconKind.Hint, "Подсказка", Hint);
            prevBtn = Icon(IconKind.Prev, "Ход", () => Navigate(viewPly - 1));
            nextBtn = Icon(IconKind.Next, "Ход", () => Navigate(viewPly + 1));
            flipBtn = Icon(IconKind.Flip, "Повернуть", () => { flipped = !flipped; RefreshBoard(false); });
            resignBtn = Icon(IconKind.Resign, "Сдаться", () => Resign(true));
            reviewBtn = Icon(IconKind.Review, "Анализ", EnterReview);
            settingsBtn = Icon(IconKind.Settings, "Игра", ShowSettings);
            newBtn = Icon(IconKind.NewGame, "Новая", StartNewGame);
            newBtn.Primary = true;
            shareBtn = Icon(IconKind.Share, "PGN", SharePgn);
            closeBtn = Icon(IconKind.Close, "Закрыть", ExitReview);
            root.AddView(actions, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Ui.Dp(58), GravityFlags.Bottom));
            ((FrameLayout.LayoutParams)column.LayoutParameters).BottomMargin = Ui.Dp(58);

            // Result card over the board.
            overlay = new FrameLayout(this) { Visibility = ViewStates.Gone };
            overlay.SetBackgroundColor(Color.Argb(150, 0, 0, 0));
            overlay.Clickable = true;
            var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
            card.SetGravity(GravityFlags.CenterHorizontal);
            card.Background = Ui.Rounded(Ui.PanelBg, 14);
            card.SetPadding(Ui.Dp(20), Ui.Dp(22), Ui.Dp(20), Ui.Dp(18));
            overlayTitle = new TextView(this) { Gravity = GravityFlags.Center };
            overlayTitle.TextSize = 24;
            overlayTitle.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
            card.AddView(overlayTitle);
            overlaySubtitle = new TextView(this) { Gravity = GravityFlags.Center };
            overlaySubtitle.SetTextColor(Ui.TextDim);
            overlaySubtitle.TextSize = 14;
            card.AddView(overlaySubtitle, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Ui.Dp(6), BottomMargin = Ui.Dp(16) });
            card.AddView(TextButton("Анализ партии", true, () => { overlay.Visibility = ViewStates.Gone; EnterReview(); }), ButtonParams());
            card.AddView(TextButton("Новая игра", false, () => { overlay.Visibility = ViewStates.Gone; StartNewGame(); }), ButtonParams());
            card.AddView(TextButton("Закрыть", false, () => overlay.Visibility = ViewStates.Gone), ButtonParams());
            overlay.AddView(card, new FrameLayout.LayoutParams(Ui.Dp(290), ViewGroup.LayoutParams.WrapContent, GravityFlags.Center));
            root.AddView(overlay, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            SetContentView(root);
        }

        IconButton Icon(IconKind kind, string caption, Action onClick)
        {
            var b = new IconButton(this, kind, caption);
            b.Click += (s, e) => { if (b.Enabled) onClick(); };
            return b;
        }

        Button TextButton(string text, bool primary, Action onClick)
        {
            var b = new Button(this) { Text = text };
            b.SetAllCaps(false);
            b.TextSize = 16;
            b.SetTextColor(Color.White);
            b.Background = Ui.Rounded(primary ? Ui.Accent : Ui.Button, 10);
            b.Click += (s, e) => onClick();
            return b;
        }

        static LinearLayout.LayoutParams ButtonParams() =>
            new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Ui.Dp(46)) { TopMargin = Ui.Dp(8) };

        void SetActions(params IconButton[] buttons)
        {
            actions.RemoveAllViews();
            foreach (var b in buttons)
                actions.AddView(b, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1));
        }

        // ---------------------------------------------------------------- settings dialog

        void ShowSettings()
        {
            int elo = botElo, color = colorChoice;
            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(Ui.Dp(22), Ui.Dp(12), Ui.Dp(22), 0);

            var eloLabel = new TextView(this) { TextSize = 16 };
            eloLabel.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
            void UpdateElo() => eloLabel.Text = $"Сила бота: {elo} ELO · {Ui.LevelName(elo)}";
            UpdateElo();
            layout.AddView(eloLabel);
            var seek = new SeekBar(this) { Max = (3500 - 200) / 50, Progress = (elo - 200) / 50 };
            seek.ProgressChanged += (s, e) => { elo = 200 + e.Progress * 50; UpdateElo(); };
            layout.AddView(seek, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Ui.Dp(44)));

            layout.AddView(Label("Вы играете"));
            var colors = Radio(new[] { "Белыми", "Чёрными", "Случайно" }, color, i => color = i);
            layout.AddView(colors);

            layout.AddView(Label("Нотация"));
            var styles = Radio(new[] { "Ваша (K = конь, K^ = король)", "Международная (N, K)", "Русская (К, С, Л, Ф, Кр)" }, (int)style, i => { });
            layout.AddView(styles);

            var evalSw = new Switch(this) { Text = "Шкала оценки", Checked = showEvalBar, TextSize = 15 };
            var fbSw = new Switch(this) { Text = "Оценка ходов во время игры", Checked = showFeedback, TextSize = 15 };
            var soundSw = new Switch(this) { Text = "Звук", Checked = AndroidSounds.Enabled, TextSize = 15 };
            foreach (var sw in new[] { evalSw, fbSw, soundSw })
                layout.AddView(sw, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Ui.Dp(44)));

            var scroll = new ScrollView(this);
            scroll.AddView(layout);

            void Apply()
            {
                botElo = elo;
                colorChoice = color;
                showEvalBar = evalSw.Checked;
                showFeedback = fbSw.Checked;
                AndroidSounds.Enabled = soundSw.Checked;
                var newStyle = (NotationStyle)RadioIndex(styles);
                if (newStyle != style)
                {
                    style = newStyle;
                    RebuildSans();
                    for (int i = 0; i < game.Reviews.Count; i++) game.Reviews[i] = null;
                    ClassifyAll();
                }
                SaveSettings();
                evalBar.Visibility = showEvalBar ? ViewStates.Visible : ViewStates.Invisible;
                RefreshAll();
            }

            new AlertDialog.Builder(this, Android.Resource.Style.ThemeDeviceDefaultDialogAlert)
                .SetTitle("Новая игра и настройки")
                .SetView(scroll)
                .SetPositiveButton("Новая игра", (s, e) => { Apply(); StartNewGame(); })
                .SetNeutralButton("Применить", (s, e) => Apply())
                .SetNegativeButton("Отмена", (s, e) => { })
                .Show();
        }

        TextView Label(string text)
        {
            var t = new TextView(this) { Text = text, TextSize = 14 };
            t.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
            t.SetPadding(0, Ui.Dp(10), 0, Ui.Dp(2));
            return t;
        }

        RadioGroup Radio(string[] items, int selected, Action<int> changed)
        {
            var g = new RadioGroup(this) { Orientation = Orientation.Vertical };
            for (int i = 0; i < items.Length; i++)
            {
                var rb = new RadioButton(this) { Text = items[i], Id = 1000 + i, TextSize = 15 };
                g.AddView(rb);
            }
            g.Check(1000 + selected);
            g.CheckedChange += (s, e) => changed(e.CheckedId - 1000);
            return g;
        }

        static int RadioIndex(RadioGroup g) => Math.Max(0, g.CheckedRadioButtonId - 1000);

        // ---------------------------------------------------------------- refresh

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
                        board.Arrows.Add(new Arrow(Mv.From(r.BestMove), Mv.To(r.BestMove), Color.Argb(190, 129, 182, 76)));
                }
            }
            if (hintMove != Mv.None && viewPly == game.Ply)
                board.Arrows.Add(new Arrow(Mv.From(hintMove), Mv.To(hintMove), Color.Argb(200, 255, 170, 0)));
            board.Invalidate();
        }

        static readonly int[] StartCounts = { 8, 2, 2, 2, 1, 0 };
        static readonly int[] PieceValue = { 1, 3, 3, 5, 9, 0 };

        void RefreshBars(Board b)
        {
            PlayerBarView userBar = flipped == (userColor == Piece.Black) ? bottomBar : topBar;
            PlayerBarView botBar = userBar == bottomBar ? topBar : bottomBar;
            userBar.PlayerName = "Вы";
            userBar.Subtitle = "";
            userBar.IsBot = false;
            userBar.PieceColor = userColor;
            userBar.Thinking = false;
            botBar.PlayerName = BotName;
            botBar.Subtitle = $"({botElo})";
            botBar.IsBot = true;
            botBar.PieceColor = userColor ^ 1;
            botBar.Thinking = botThinking;
            bool playing = game.Result == GameResult.None && !reviewMode;
            userBar.ToMove = playing && b.SideToMove == userColor && viewPly == game.Ply;
            botBar.ToMove = false;
            FillCaptured(b, userBar, userColor);
            FillCaptured(b, botBar, userColor ^ 1);
            topBar.Invalidate();
            bottomBar.Invalidate();
        }

        static void FillCaptured(Board b, PlayerBarView bar, int color)
        {
            bar.Captured.Clear();
            int them = color ^ 1, myMat = 0, theirMat = 0;
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
            int stm = viewPly % 2 == 0 ? Piece.White : Piece.Black;
            evalBar.SetScore(stm == Piece.White ? a.Score : -a.Score);
        }

        void RefreshSide()
        {
            eloChip.Text = $"{botElo} ELO  ⚙";
            evalBar.Visibility = showEvalBar ? ViewStates.Visible : ViewStates.Invisible;
            string opening = game.OpeningName(viewPly);
            openingText.Text = opening != null ? "Дебют: " + opening : (game.Ply == 0 ? "Начальная позиция" : "");

            bool showClasses = showFeedback || reviewMode || game.Result != GameResult.None;
            var classes = new List<MoveClass>();
            for (int i = 0; i < game.Reviews.Count; i++)
                classes.Add(showClasses && game.Reviews[i] != null ? game.Reviews[i].Class : MoveClass.None);
            moveStrip.SetMoves(displaySans, classes, viewPly - 1);

            RefreshFeedback();

            bool over = game.Result != GameResult.None;
            if (reviewMode) SetActions(newBtn, prevBtn, nextBtn, flipBtn, shareBtn, closeBtn);
            else if (over) SetActions(newBtn, undoBtn, prevBtn, nextBtn, flipBtn, reviewBtn);
            else SetActions(settingsBtn, undoBtn, hintBtn, prevBtn, nextBtn, flipBtn, resignBtn);
            undoBtn.Enabled = !reviewMode && game.Ply > 0 && (game.Ply > 1 || userColor == Piece.White);
            hintBtn.Enabled = CanUserMove();
            resignBtn.Enabled = !over && game.Ply > 0;
            prevBtn.Enabled = viewPly > 0;
            nextBtn.Enabled = viewPly < game.Ply;
            foreach (var b in new[] { undoBtn, hintBtn, resignBtn, prevBtn, nextBtn }) b.Invalidate();

            summary.Visibility = reviewMode ? ViewStates.Visible : ViewStates.Gone;
            graph.Visibility = reviewMode ? ViewStates.Visible : ViewStates.Gone;
            if (reviewMode) RefreshReview();
        }

        void RefreshFeedback()
        {
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
                string line = a != null && a.Lines.Count > 0 ? Notation.FormatLine(b, a.Lines[0].Moves, style, 6) : "";
                feedback.SetContent(MoveClass.None, "Подсказка",
                    $"Лучший ход: {Notation.ToSan(b, hintMove, style)}." + (line.Length > 0 ? $" Продолжение: {line}" : ""),
                    a != null ? Review.FormatScore(b.SideToMove == Piece.White ? a.Score : -a.Score) : "");
            }
            else if (!browsing && hintPending)
                feedback.SetContent(MoveClass.None, "Подсказка", "Движок ищет лучший ход…", "");
            else if (moveIndex < 0)
                feedback.SetContent(MoveClass.None, game.Ply == 0 && !reviewMode ? "Ваш ход" : "Начальная позиция",
                    userColor == Piece.White || reviewMode
                        ? "Перетащите фигуру или коснитесь её, затем клетки. Движок оценит каждый ваш ход, как на chess.com."
                        : "Бот играет белыми и ходит первым. После каждого вашего хода движок покажет его оценку.", "");
            else if (!showFeedback && !reviewMode && game.Result == GameResult.None)
                feedback.SetContent(MoveClass.None, "Оценка ходов выключена", "Включите её в настройках или откройте анализ после партии.", "");
            else
            {
                var r = game.Reviews[moveIndex];
                string who = (moveIndex % 2 == 0) == (userColor == Piece.White) ? "Ваш ход" : "Ход бота";
                string num = $"{(moveIndex / 2) + 1}{(moveIndex % 2 == 0 ? "." : "...")}";
                if (r != null)
                    feedback.SetContent(r.Class, Review.ClassName(r.Class), $"{who} {num} {r.Comment}", Review.FormatScore(r.WhiteScoreAfter));
                else
                    feedback.SetContent(MoveClass.None, "Анализ…", $"{who} {displaySans[moveIndex]}: движок оценивает ход на максимальной силе…", "");
            }
        }

        void RefreshReview()
        {
            var userAcc = new List<double>();
            var botAcc = new List<double>();
            summary.LeftCounts.Clear();
            summary.RightCounts.Clear();
            int pending = 0;
            for (int i = 0; i < game.Reviews.Count; i++)
            {
                var r = game.Reviews[i];
                if (r == null) { pending++; continue; }
                bool user = (i % 2 == 0) == (userColor == Piece.White);
                var counts = user ? summary.LeftCounts : summary.RightCounts;
                counts.TryGetValue(r.Class, out int n);
                counts[r.Class] = n + 1;
                if (r.Class != MoveClass.Book && r.Class != MoveClass.Forced) (user ? userAcc : botAcc).Add(r.Accuracy);
            }
            summary.LeftAccuracy = userAcc.Count > 0 ? Review.GameAccuracy(userAcc) : (double?)null;
            summary.RightAccuracy = botAcc.Count > 0 ? Review.GameAccuracy(botAcc) : (double?)null;
            summary.Status = pending > 0 ? $"Анализ… осталось: {pending}" : "";
            summary.Invalidate();

            graph.Scores.Clear();
            for (int ply = 0; ply < game.Analyses.Count; ply++)
            {
                var a = game.Analyses[ply];
                graph.Scores.Add(a == null ? (int?)null : (ply % 2 == 0 ? a.Score : -a.Score));
            }
            graph.Classes.Clear();
            foreach (var r in game.Reviews) graph.Classes.Add(r != null ? r.Class : MoveClass.None);
            graph.CurrentPly = viewPly;
            graph.Invalidate();
        }
    }
}
