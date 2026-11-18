using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Sokol.Engine;
using Mv = Sokol.Engine.Move;

namespace Sokol.App
{
    sealed partial class MainForm
    {
        // ---------------------------------------------------------------- state
        readonly Bot bot = new Bot(32);
        readonly AnalysisService analysis;
        readonly Dictionary<(int, ulong), PositionAnalysis> analysisCache = new Dictionary<(int, ulong), PositionAnalysis>();
        readonly Random rng = new Random();

        Game game = new Game();
        readonly List<string> displaySans = new List<string>();
        int viewPly;
        int userColor = Piece.White;
        bool flipped;
        bool reviewMode;
        bool botThinking;
        volatile int botGeneration;
        readonly object botLock = new object();
        int hintMove = Mv.None;
        bool hintPending;
        PositionAnalysis liveAnalysis;
        int livePly = -1;

        int botElo = 1200;
        int colorChoice;
        NotationStyle style = NotationStyle.User;
        bool showEvalBar = true, showFeedback = true;

        // ---------------------------------------------------------------- wiring

        void WireEvents()
        {
            board.MoveMade += (m, animate) => OnUserMove(m, animate);
            newGameButton.Click += (s, e) => AskNewGame();
            reviewNewGameButton.Click += (s, e) => StartNewGame();
            overlay.NewGameButton.Click += (s, e) => StartNewGame();
            overlay.ReviewButton.Click += (s, e) => EnterReview();
            analyzeButton.Click += (s, e) => EnterReview();
            closeReviewButton.Click += (s, e) => ExitReview();
            undoButton.Click += (s, e) => Undo();
            hintButton.Click += (s, e) => Hint();
            flipButton.Click += (s, e) => { flipped = !flipped; RefreshBoard(false); };
            resignButton.Click += (s, e) => Resign();
            pgnButton.Click += (s, e) => CopyPgn();
            navFirst.Click += (s, e) => Navigate(0);
            navPrev.Click += (s, e) => Navigate(viewPly - 1);
            navNext.Click += (s, e) => Navigate(viewPly + 1);
            navLast.Click += (s, e) => Navigate(game.Ply);
            moveList.MoveClicked += i => Navigate(i + 1);
            graph.PlyClicked += p => Navigate(p);

            eloSlider.ValueChanged += (s, e) => { botElo = eloSlider.Value; RefreshSide(); RefreshBars(ViewBoard()); };
            colorPicker.SelectedIndexChanged += (s, e) => colorChoice = colorPicker.SelectedIndex;
            evalToggle.CheckedChanged += (s, e) => { showEvalBar = evalToggle.Checked; LayoutUi(); RefreshEval(); };
            feedbackToggle.CheckedChanged += (s, e) => { showFeedback = feedbackToggle.Checked; RefreshDecorations(); RefreshSide(); };
            soundToggle.CheckedChanged += (s, e) => Sounds.Enabled = soundToggle.Checked;
            notationPicker.SelectedIndexChanged += (s, e) =>
            {
                style = (NotationStyle)notationPicker.SelectedIndex;
                RebuildSans();
                for (int i = 0; i < game.Reviews.Count; i++) game.Reviews[i] = null;
                ClassifyAll();
                RefreshSide();
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutUi();
            if (selfTestPath != null) { RunSelfTest(); return; }
            if (snapshotPath != null) { RunSnapshot(); return; }
            StartNewGame();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (selfTestPath == null && snapshotPath == null) SaveSettings();
            botGeneration++;
            bot.Stop();
            analysis.Dispose();
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left: Navigate(viewPly - 1); return true;
                case Keys.Right: Navigate(viewPly + 1); return true;
                case Keys.Home: Navigate(0); return true;
                case Keys.End: Navigate(game.Ply); return true;
                case Keys.Control | Keys.Z: Undo(); return true;
                case Keys.F: flipped = !flipped; RefreshBoard(false); return true;
                case Keys.H: Hint(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------------------------------------------------------- game flow

        bool CanUserMove() =>
            !reviewMode && game.Result == GameResult.None && !botThinking && viewPly == game.Ply && game.Current.SideToMove == userColor;

        void AskNewGame()
        {
            if (game.Ply > 1 && game.Result == GameResult.None)
            {
                var r = MessageBox.Show(this, "Начать новую партию? Текущая партия будет прервана.", "Новая игра",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
            }
            StartNewGame();
        }

        void StartNewGame()
        {
            botGeneration++;
            bot.Stop();
            botThinking = false;
            game = new Game();
            displaySans.Clear();
            viewPly = 0;
            reviewMode = false;
            hintMove = Mv.None;
            hintPending = false;
            liveAnalysis = null;
            livePly = -1;
            overlay.Visible = false;
            userColor = colorChoice == 2 ? rng.Next(2) : colorChoice;
            flipped = userColor == Piece.Black;
            lock (botLock) bot.NewGame();
            analysis.WorkerCount = 1;
            analysis.Update(game, 0);
            LayoutSide();
            RefreshAll();
            evalBar.SetScore(0);
            if (game.Current.SideToMove != userColor) StartBotMove();
        }

        void OnUserMove(int move, bool animate)
        {
            if (!CanUserMove() || !game.Current.IsLegal(move)) return;
            ApplyMove(move, animate);
            if (!CheckGameEnd()) StartBotMove();
        }

        void ApplyMove(int move, bool animate)
        {
            bool capture = Mv.IsCapture(move);
            displaySans.Add(Notation.ToSan(game.Current, move, style));
            game.Push(move);
            if (analysisCache.TryGetValue((game.Ply, game.Hashes[game.Ply]), out var cached)) game.Analyses[game.Ply] = cached;
            viewPly = game.Ply;
            hintMove = Mv.None;
            hintPending = false;
            if (game.Current.InCheck()) Sounds.Check();
            else if (capture) Sounds.Capture();
            else Sounds.Move();
            ClassifyAll();
            analysis.Update(game, viewPly);
            RefreshBoard(animate);
            RefreshSide();
        }

        void StartBotMove()
        {
            if (game.Result != GameResult.None) return;
            botThinking = true;
            int gen = ++botGeneration;
            var pos = game.Current.Clone();
            int elo = botElo;
            int minDelay = 350 + rng.Next(450); // a short natural pause so the bot does not reply instantly
            board.Interactive = false;
            board.RefreshInteractivity();
            RefreshBars(ViewBoard());
            RefreshSide();
            var t = new Thread(() =>
            {
                var sw = Stopwatch.StartNew();
                int m = Mv.None;
                // One search at a time: a previous (cancelled) search must finish before the bot is reused.
                lock (botLock)
                {
                    if (gen != botGeneration) return;
                    try { m = bot.ChooseMove(pos, elo); } catch { }
                }
                int left = minDelay - (int)sw.ElapsedMilliseconds;
                if (left > 0) Thread.Sleep(left);
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (gen != botGeneration) return;
                        botThinking = false;
                        if (m == Mv.None || !game.Current.IsLegal(m)) { RefreshAll(); return; }
                        ApplyMove(m, true);
                        CheckGameEnd();
                        RefreshSide();
                    }));
                }
                catch (InvalidOperationException) { }
            }) { IsBackground = true, Name = "bot" };
            t.Start();
        }

        bool CheckGameEnd()
        {
            var result = game.DetectEnd(out string reason);
            if (result == GameResult.None) return false;
            game.Result = result;
            game.ResultReason = reason;
            EndGame();
            return true;
        }

        void EndGame()
        {
            botGeneration++;
            bot.Stop();
            botThinking = false;
            Sounds.End();
            bool userWon = (game.Result == GameResult.WhiteWins && userColor == Piece.White) ||
                           (game.Result == GameResult.BlackWins && userColor == Piece.Black);
            if (game.Result == GameResult.Draw)
            {
                overlay.Title = "Ничья";
                overlay.TitleColor = Theme.Text;
            }
            else if (userWon)
            {
                overlay.Title = "Вы победили!";
                overlay.TitleColor = Theme.Accent;
            }
            else
            {
                overlay.Title = $"Победил {BotName}";
                overlay.TitleColor = Color.FromArgb(250, 120, 100);
            }
            overlay.Subtitle = $"Причина: {game.ResultReason}. Ходов: {(game.Ply + 1) / 2}. Бот: {botElo} ELO.";
            overlay.Visible = true;
            overlay.BringToFront();
            overlay.Invalidate();
            analysis.WorkerCount = Math.Max(2, Math.Min(6, Environment.ProcessorCount / 2));
            analysis.Update(game, viewPly);
            RefreshAll();
        }

        void Resign(bool confirm = true)
        {
            if (game.Result != GameResult.None || game.Ply == 0) return;
            if (confirm && MessageBox.Show(this, "Сдаться в этой партии?", "Сдаться", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            game.Result = userColor == Piece.White ? GameResult.BlackWins : GameResult.WhiteWins;
            game.ResultReason = "вы сдались";
            EndGame();
        }

        void Undo()
        {
            if (reviewMode || game.Ply == 0) return;
            botGeneration++;
            bot.Stop();
            botThinking = false;
            overlay.Visible = false;
            do
            {
                game.Pop();
                displaySans.RemoveAt(displaySans.Count - 1);
            } while (game.Ply > 0 && game.Current.SideToMove != userColor);
            game.Result = GameResult.None;
            viewPly = game.Ply;
            hintMove = Mv.None;
            hintPending = false;
            analysis.WorkerCount = 1;
            analysis.Update(game, viewPly);
            RefreshAll();
            if (game.Current.SideToMove != userColor) StartBotMove();
        }

        void Hint()
        {
            if (!CanUserMove()) return;
            var a = game.Analyses[game.Ply];
            if (a != null && a.BestMove != Mv.None)
            {
                hintMove = a.BestMove;
                hintPending = false;
            }
            else
            {
                hintPending = true;
                analysis.Update(game, game.Ply);
            }
            RefreshDecorations();
            RefreshFeedback();
        }

        void Navigate(int ply)
        {
            ply = Math.Max(0, Math.Min(game.Ply, ply));
            if (ply == viewPly) return;
            bool forwardOne = ply == viewPly + 1;
            viewPly = ply;
            if (forwardOne)
            {
                int m = game.Moves[ply - 1];
                if (Mv.IsCapture(m)) Sounds.Capture(); else Sounds.Move();
            }
            analysis.Update(game, viewPly);
            RefreshBoard(forwardOne);
            RefreshSide();
        }

        void EnterReview()
        {
            if (game.Result == GameResult.None) return;
            reviewMode = true;
            overlay.Visible = false;
            viewPly = 0;
            analysis.WorkerCount = Math.Max(2, Math.Min(6, Environment.ProcessorCount / 2));
            analysis.Update(game, viewPly);
            LayoutSide();
            RefreshAll();
        }

        void ExitReview()
        {
            reviewMode = false;
            viewPly = game.Ply;
            LayoutSide();
            RefreshAll();
        }

        void CopyPgn()
        {
            string white = userColor == Piece.White ? "Игрок" : $"{BotName} ({botElo})";
            string black = userColor == Piece.Black ? "Игрок" : $"{BotName} ({botElo})";
            try
            {
                Clipboard.SetText(game.ToPgn(white, black));
                pgnButton.Text = "Скопировано!";
                var t = new System.Windows.Forms.Timer { Interval = 1500 };
                t.Tick += (s, e) => { pgnButton.Text = "Копировать PGN"; t.Dispose(); };
                t.Start();
            }
            catch { }
        }

        void RebuildSans()
        {
            displaySans.Clear();
            var b = new Board(game.StartFen);
            foreach (int m in game.Moves)
            {
                displaySans.Add(Notation.ToSan(b, m, style));
                b.MakeMove(m);
            }
        }

        // ---------------------------------------------------------------- analysis results

        void OnAnalysisResult(int ply, ulong hash, PositionAnalysis a, bool final)
        {
            if (ply >= game.Hashes.Count || game.Hashes[ply] != hash) return;
            if (!final)
            {
                if (game.Analyses[ply] != null) return;
                liveAnalysis = a;
                livePly = ply;
                if (ply == viewPly) RefreshEval();
                return;
            }
            analysisCache[(ply, hash)] = a;
            if (game.Analyses[ply] != null) return;
            game.Analyses[ply] = a;
            if (livePly == ply) { liveAnalysis = null; livePly = -1; }
            if (hintPending && ply == game.Ply && CanUserMove())
            {
                hintPending = false;
                hintMove = a.BestMove;
            }
            ClassifyAll();
            if (ply == viewPly) RefreshEval();
            RefreshDecorations();
            RefreshSide();
        }

        /// <summary>Creates reviews for every move whose before/after analyses are both available.</summary>
        void ClassifyAll()
        {
            for (int i = 0; i < game.Moves.Count; i++)
            {
                if (game.Reviews[i] != null || game.Analyses[i] == null || game.Analyses[i + 1] == null) continue;
                var before = game.BoardAt(i);
                var prev = i > 0 && game.Reviews[i - 1] != null ? game.Reviews[i - 1].Class : MoveClass.None;
                try
                {
                    game.Reviews[i] = Review.Classify(before, game.Moves[i], game.Analyses[i], game.Analyses[i + 1], prev, style,
                        game.OpeningName(i + 1));
                }
                catch { }
            }
        }

        // ---------------------------------------------------------------- settings

        static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SokolChess", "settings.txt");

        void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    foreach (var line in File.ReadAllLines(SettingsPath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                        switch (k)
                        {
                            case "elo": if (int.TryParse(v, out int elo)) botElo = Math.Max(200, Math.Min(3500, elo)); break;
                            case "color": if (int.TryParse(v, out int c)) colorChoice = Math.Max(0, Math.Min(2, c)); break;
                            case "evalbar": showEvalBar = v == "1"; break;
                            case "feedback": showFeedback = v == "1"; break;
                            case "sound": Sounds.Enabled = v == "1"; break;
                            case "notation": if (int.TryParse(v, out int n)) style = (NotationStyle)Math.Max(0, Math.Min(2, n)); break;
                        }
                    }
            }
            catch { }
            eloSlider.Value = botElo;
            colorPicker.SelectedIndex = colorChoice;
            evalToggle.Checked = showEvalBar;
            feedbackToggle.Checked = showFeedback;
            soundToggle.Checked = Sounds.Enabled;
            notationPicker.SelectedIndex = (int)style;
        }

        void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllLines(SettingsPath, new[]
                {
                    "elo=" + botElo,
                    "color=" + colorChoice,
                    "evalbar=" + (showEvalBar ? 1 : 0),
                    "feedback=" + (showFeedback ? 1 : 0),
                    "sound=" + (Sounds.Enabled ? 1 : 0),
                    "notation=" + (int)style,
                });
            }
            catch { }
        }

        // ---------------------------------------------------------------- snapshot mode (for automated visual checks)

        string snapshotPath;
        string[] snapshotMoves = new string[0];
        bool snapshotReview;
        int snapshotView = -1;

        public void ConfigureSnapshot(string path, string moves, bool review, int view)
        {
            snapshotPath = path;
            snapshotMoves = string.IsNullOrEmpty(moves) ? new string[0] : moves.Split(',');
            snapshotReview = review;
            snapshotView = view;
            Sounds.Enabled = false;
        }

        void RunSnapshot()
        {
            game = new Game();
            displaySans.Clear();
            userColor = Piece.White;
            flipped = false;
            analysis.WorkerCount = 4;
            foreach (var u in snapshotMoves)
            {
                int m = game.Current.ParseUci(u.Trim());
                if (m == Mv.None) break;
                ApplyMove(m, false);
            }
            if (snapshotReview)
            {
                if (!CheckGameEnd())
                {
                    game.Result = GameResult.BlackWins;
                    game.ResultReason = "вы сдались";
                    EndGame();
                }
            }
            var sw = Stopwatch.StartNew();
            var timer = new System.Windows.Forms.Timer { Interval = 200 };
            timer.Tick += (s, e) =>
            {
                bool done = true;
                foreach (var a in game.Analyses) if (a == null) done = false;
                if (!done && sw.ElapsedMilliseconds < 60000) return;
                timer.Stop();
                if (snapshotReview) EnterReview();
                if (snapshotView >= 0) Navigate(snapshotView);
                var t2 = new System.Windows.Forms.Timer { Interval = 600 };
                t2.Tick += (s2, e2) =>
                {
                    t2.Stop();
                    using (var bmp = new Bitmap(Width, Height))
                    {
                        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                        bmp.Save(snapshotPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    Close();
                };
                t2.Start();
            };
            timer.Start();
        }
    }
}
