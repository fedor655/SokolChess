using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Sokol.Engine;
using Sokol.Shared;
using Mv = Sokol.Engine.Move;

namespace Sokol.Droid
{
    public partial class MainActivity
    {
        readonly Bot bot = new Bot(16);
        readonly object botLock = new object();
        AnalysisService analysis;
        readonly Dictionary<(int, ulong), PositionAnalysis> analysisCache = new Dictionary<(int, ulong), PositionAnalysis>();
        readonly Random rng = new Random();

        Game game = new Game();
        readonly List<string> displaySans = new List<string>();
        int viewPly;
        int userColor = Piece.White;
        bool flipped, reviewMode, botThinking, destroyed;
        volatile int botGeneration;
        int hintMove = Mv.None;
        bool hintPending;
        PositionAnalysis liveAnalysis;
        int livePly = -1;

        int botElo = 1200, colorChoice;
        NotationStyle style = NotationStyle.User;
        bool showEvalBar = true, showFeedback = true;

        // ---------------------------------------------------------------- lifecycle

        protected override void OnPause()
        {
            base.OnPause();
            SaveSettings();
            SaveGame();
        }

        protected override void OnDestroy()
        {
            destroyed = true;
            botGeneration++;
            bot.Stop();
            analysis?.Dispose();
            AndroidSounds.Release();
            base.OnDestroy();
        }

        public override void OnBackPressed()
        {
            if (overlay.Visibility == Android.Views.ViewStates.Visible) { overlay.Visibility = Android.Views.ViewStates.Gone; return; }
            if (reviewMode) { ExitReview(); return; }
            base.OnBackPressed();
        }

        // ---------------------------------------------------------------- game flow

        bool CanUserMove() =>
            !reviewMode && game.Result == GameResult.None && !botThinking && viewPly == game.Ply && game.Current.SideToMove == userColor;

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
            overlay.Visibility = Android.Views.ViewStates.Gone;
            userColor = colorChoice == 2 ? rng.Next(2) : colorChoice;
            flipped = userColor == Piece.Black;
            lock (botLock) bot.NewGame();
            analysis.WorkerCount = 1;
            analysis.Update(game, 0);
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
            if (game.Current.InCheck()) AndroidSounds.Check();
            else if (capture) AndroidSounds.Capture();
            else AndroidSounds.Move();
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
            int minDelay = 350 + rng.Next(450);
            board.Interactive = false;
            board.RefreshInteractivity();
            RefreshBars(ViewBoard());
            RefreshSide();
            var t = new Thread(() =>
            {
                var sw = Stopwatch.StartNew();
                int m = Mv.None;
                lock (botLock)
                {
                    if (gen != botGeneration) return;
                    try { m = bot.ChooseMove(pos, elo); } catch (Exception) { }
                }
                int left = minDelay - (int)sw.ElapsedMilliseconds;
                if (left > 0) Thread.Sleep(left);
                RunOnUiThread(() =>
                {
                    if (destroyed || gen != botGeneration) return;
                    botThinking = false;
                    if (m == Mv.None || !game.Current.IsLegal(m)) { RefreshAll(); return; }
                    ApplyMove(m, true);
                    CheckGameEnd();
                    RefreshSide();
                });
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
            AndroidSounds.End();
            bool userWon = (game.Result == GameResult.WhiteWins && userColor == Piece.White) ||
                           (game.Result == GameResult.BlackWins && userColor == Piece.Black);
            if (game.Result == GameResult.Draw) { overlayTitle.Text = "Ничья"; overlayTitle.SetTextColor(Ui.Text); }
            else if (userWon) { overlayTitle.Text = "Вы победили!"; overlayTitle.SetTextColor(Ui.Accent); }
            else { overlayTitle.Text = $"Победил {BotName}"; overlayTitle.SetTextColor(Android.Graphics.Color.Rgb(250, 120, 100)); }
            overlaySubtitle.Text = $"Причина: {game.ResultReason}. Ходов: {(game.Ply + 1) / 2}. Бот: {botElo} ELO.";
            overlay.Visibility = Android.Views.ViewStates.Visible;
            analysis.WorkerCount = Math.Max(2, Math.Min(3, System.Environment.ProcessorCount / 2));
            analysis.Update(game, viewPly);
            RefreshAll();
        }

        void Resign(bool confirm)
        {
            if (game.Result != GameResult.None || game.Ply == 0) return;
            void DoResign()
            {
                game.Result = userColor == Piece.White ? GameResult.BlackWins : GameResult.WhiteWins;
                game.ResultReason = "вы сдались";
                EndGame();
            }
            if (!confirm) { DoResign(); return; }
            new AlertDialog.Builder(this, Android.Resource.Style.ThemeDeviceDefaultDialogAlert)
                .SetMessage("Сдаться в этой партии?")
                .SetPositiveButton("Сдаться", (s, e) => DoResign())
                .SetNegativeButton("Отмена", (s, e) => { })
                .Show();
        }

        void Undo()
        {
            if (reviewMode || game.Ply == 0) return;
            botGeneration++;
            bot.Stop();
            botThinking = false;
            overlay.Visibility = Android.Views.ViewStates.Gone;
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
            if (a != null && a.BestMove != Mv.None) { hintMove = a.BestMove; hintPending = false; }
            else { hintPending = true; analysis.Update(game, game.Ply); }
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
                if (Mv.IsCapture(game.Moves[ply - 1])) AndroidSounds.Capture(); else AndroidSounds.Move();
            }
            analysis.Update(game, viewPly);
            RefreshBoard(forwardOne);
            RefreshSide();
        }

        void EnterReview()
        {
            if (game.Result == GameResult.None) return;
            reviewMode = true;
            overlay.Visibility = Android.Views.ViewStates.Gone;
            viewPly = 0;
            analysis.WorkerCount = Math.Max(2, Math.Min(3, System.Environment.ProcessorCount / 2));
            analysis.Update(game, viewPly);
            RefreshAll();
        }

        void ExitReview()
        {
            reviewMode = false;
            viewPly = game.Ply;
            RefreshAll();
        }

        void SharePgn()
        {
            string white = userColor == Piece.White ? "Игрок" : $"{BotName} ({botElo})";
            string black = userColor == Piece.Black ? "Игрок" : $"{BotName} ({botElo})";
            var intent = new Intent(Intent.ActionSend);
            intent.SetType("text/plain");
            intent.PutExtra(Intent.ExtraText, game.ToPgn(white, black));
            StartActivity(Intent.CreateChooser(intent, "Поделиться партией (PGN)"));
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

        // ---------------------------------------------------------------- analysis

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
            if (hintPending && ply == game.Ply && CanUserMove()) { hintPending = false; hintMove = a.BestMove; }
            ClassifyAll();
            if (ply == viewPly) RefreshEval();
            RefreshDecorations();
            RefreshSide();
        }

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
                catch (Exception) { }
            }
        }

        // ---------------------------------------------------------------- persistence

        ISharedPreferences Prefs => GetSharedPreferences("sokol", FileCreationMode.Private);

        void LoadSettings()
        {
            var p = Prefs;
            botElo = Math.Max(200, Math.Min(3500, p.GetInt("elo", 1200)));
            colorChoice = Math.Max(0, Math.Min(2, p.GetInt("color", 0)));
            showEvalBar = p.GetBoolean("evalbar", true);
            showFeedback = p.GetBoolean("feedback", true);
            AndroidSounds.Enabled = p.GetBoolean("sound", true);
            style = (NotationStyle)Math.Max(0, Math.Min(2, p.GetInt("notation", 0)));
        }

        void SaveSettings()
        {
            var e = Prefs.Edit();
            e.PutInt("elo", botElo);
            e.PutInt("color", colorChoice);
            e.PutBoolean("evalbar", showEvalBar);
            e.PutBoolean("feedback", showFeedback);
            e.PutBoolean("sound", AndroidSounds.Enabled);
            e.PutInt("notation", (int)style);
            e.Apply();
        }

        /// <summary>Phones kill background apps: keep the game so it can be resumed.</summary>
        void SaveGame()
        {
            if (selfTestRunning) return;
            var sb = new StringBuilder();
            foreach (int m in game.Moves) sb.Append(Mv.ToUci(m)).Append(',');
            var e = Prefs.Edit();
            e.PutString("game_moves", sb.ToString());
            e.PutInt("game_color", userColor);
            e.PutInt("game_result", (int)game.Result);
            e.PutString("game_reason", game.ResultReason ?? "");
            e.Apply();
        }

        bool RestoreGame()
        {
            var p = Prefs;
            string moves = p.GetString("game_moves", "");
            if (string.IsNullOrEmpty(moves)) return false;
            game = new Game();
            displaySans.Clear();
            userColor = p.GetInt("game_color", Piece.White);
            flipped = userColor == Piece.Black;
            foreach (var u in moves.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int m = game.Current.ParseUci(u);
                if (m == Mv.None) break;
                displaySans.Add(Notation.ToSan(game.Current, m, style));
                game.Push(m);
            }
            game.Result = (GameResult)p.GetInt("game_result", 0);
            game.ResultReason = p.GetString("game_reason", "");
            viewPly = game.Ply;
            analysis.WorkerCount = game.Result == GameResult.None ? 1 : 2;
            analysis.Update(game, viewPly);
            RefreshAll();
            if (game.Result == GameResult.None && game.Current.SideToMove != userColor) StartBotMove();
            return true;
        }

        // ---------------------------------------------------------------- self-test (adb shell am start ... --es selftest 1)

        bool selfTestRunning;

        void RunSelfTest()
        {
            selfTestRunning = true;
            AndroidSounds.Enabled = false;
            var log = new StringBuilder();
            int failures = 0;
            void Check(bool ok, string what)
            {
                string line = (ok ? "OK   " : "FAIL ") + what;
                Android.Util.Log.Info("SokolTest", line);
                if (!ok) failures++;
            }
            int S(string n) => Sq.Parse(n);
            var steps = new List<Func<bool>>();
            var sw = Stopwatch.StartNew();
            int promoMove = Mv.None;

            steps.Add(() =>
            {
                colorChoice = 0;
                botElo = 800;
                StartNewGame();
                Check(game.Ply == 0 && CanUserMove(), "new game as White");
                board.SimulateTap(S("e2"));
                board.SimulateTap(S("e4"));
                Check(game.Ply == 1 && Mv.ToUci(game.Moves[0]) == "e2e4", "tap-tap move e2-e4");
                return true;
            });
            steps.Add(() => game.Ply >= 2 && !botThinking);
            steps.Add(() =>
            {
                board.SimulateDrag(S("g1"), S("f3"));
                Check(game.Ply == 3 && Mv.ToUci(game.Moves[2]) == "g1f3", "drag move Ng1-f3");
                return true;
            });
            steps.Add(() => game.Ply >= 4 && !botThinking);
            steps.Add(() =>
            {
                int before = game.Ply;
                board.SimulateTap(S("a1"));
                board.SimulateTap(S("a5"));
                Check(game.Ply == before, "rook cannot pass through own pawn");
                board.SimulateDrag(S("c1"), S("h6"));
                Check(game.Ply == before, "bishop cannot jump over a pawn");
                board.SimulateDrag(S("f3"), S("e1"));
                Check(game.Ply == before, "knight cannot land on own king");
                Undo();
                Check(game.Ply == 2 && game.Current.SideToMove == Piece.White, "undo returns to user's move");
                flipped = true;
                RefreshBoard(false);
                board.SimulateDrag(S("d2"), S("d4"));
                Check(game.Ply == 3 && Mv.ToUci(game.Moves[2]) == "d2d4", "drag on flipped board d2-d4");
                return true;
            });
            steps.Add(() => game.Ply >= 4 && !botThinking);
            steps.Add(() => { Hint(); return true; });
            steps.Add(() => hintMove != Mv.None || sw.ElapsedMilliseconds > 90000);
            steps.Add(() =>
            {
                Check(hintMove != Mv.None && game.Current.IsLegal(hintMove), "hint gives a legal move " + Mv.ToUci(hintMove));
                Navigate(1);
                Check(viewPly == 1 && !board.Interactive, "browsing is read-only");
                Navigate(game.Ply);
                Check(board.Interactive, "back to current position");
                var pb = new BoardView(this);
                pb.Layout(0, 0, 800, 800);
                pb.Interactive = true;
                pb.UserColor = Piece.White;
                pb.SetPosition(new Board("8/P6k/8/8/8/8/8/K7 w - - 0 1"), Mv.None, false);
                pb.MoveMade += (m, a) => promoMove = m;
                pb.SimulateTap(S("a7"));
                pb.SimulateTap(S("a8"));
                Check(pb.PromotionPickerOpen, "promotion picker opens");
                pb.SimulatePromotionChoice(1);
                Check(promoMove != Mv.None && Mv.PromotionType(promoMove) == Piece.Knight, "under-promotion to knight " + Mv.ToUci(promoMove));
                return true;
            });
            steps.Add(() =>
            {
                if (game.Ply >= 10 || game.Result != GameResult.None) return true;
                if (botThinking || !CanUserMove()) return false;
                var legal = game.Current.LegalMoves();
                int m = legal[new Random(game.Ply).Next(legal.Count)];
                board.SimulateDrag(Mv.From(m), Mv.To(m));
                if (board.PromotionPickerOpen) board.SimulatePromotionChoice(0);
                return false;
            });
            steps.Add(() => !botThinking);
            steps.Add(() =>
            {
                if (game.Result == GameResult.None) Resign(false);
                Check(game.Result != GameResult.None && overlay.Visibility == Android.Views.ViewStates.Visible, "resign shows result card");
                EnterReview();
                Check(reviewMode, "review mode");
                return true;
            });
            steps.Add(() =>
            {
                foreach (var r in game.Reviews) if (r == null) return sw.ElapsedMilliseconds > 180000;
                return true;
            });
            steps.Add(() =>
            {
                int reviewed = 0;
                foreach (var r in game.Reviews) if (r != null) reviewed++;
                Check(reviewed == game.Ply, $"all {game.Ply} moves classified ({reviewed})");
                foreach (var r in game.Reviews) if (r != null) Android.Util.Log.Info("SokolTest", $"     {Review.ClassName(r.Class)}: {r.Comment}");
                Navigate(5);
                Check(viewPly == 5, "navigate in review");
                Check(displaySans.Count == game.Ply, "notation: " + string.Join(" ", displaySans));
                return true;
            });

            int index = 0;
            var handler = new Handler(Looper.MainLooper);
            void Tick()
            {
                bool finished;
                try { finished = steps[index](); }
                catch (Exception ex) { Check(false, $"step {index} threw {ex}"); finished = true; index = steps.Count - 1; }
                if (finished) index++;
                if (sw.ElapsedMilliseconds > 300000) { Check(false, "timeout at step " + index); index = steps.Count; }
                if (index >= steps.Count)
                {
                    Android.Util.Log.Info("SokolTest", $"FAILURES: {failures}, time {sw.ElapsedMilliseconds} ms");
                    return;
                }
                handler.PostDelayed(Tick, 150);
            }
            handler.PostDelayed(Tick, 500);
        }
    }
}
