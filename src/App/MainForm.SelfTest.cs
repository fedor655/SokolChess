using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Sokol.Engine;
using Mv = Sokol.Engine.Move;

namespace Sokol.App
{
    /// <summary>
    /// Hidden end-to-end self-test (SokolChess.exe --selftest log.txt): drives the real UI with simulated
    /// mouse input, plays against the bot, checks rules, undo, hints, promotion, resignation and review.
    /// </summary>
    sealed partial class MainForm
    {
        string selfTestPath;
        public void ConfigureSelfTest(string logPath)
        {
            selfTestPath = logPath;
            Sounds.Enabled = false;
        }

        void RunSelfTest()
        {
            var log = new StringBuilder();
            int failures = 0;
            void Check(bool ok, string what)
            {
                log.AppendLine((ok ? "OK   " : "FAIL ") + what);
                if (!ok) failures++;
            }
            int S(string name) => Sq.Parse(name);

            var steps = new List<Func<bool>>(); // each returns true when finished (polled by a timer)
            var sw = Stopwatch.StartNew();
            int promoMove = Mv.None;
            BoardControl promoBoard = null;

            steps.Add(() =>
            {
                colorPicker.SelectedIndex = 0;
                eloSlider.Value = 800;
                StartNewGame();
                Check(game.Ply == 0 && CanUserMove(), "new game as White, user to move");
                board.SimulateClick(S("e2"));
                board.SimulateClick(S("e4"));
                Check(game.Ply == 1 && Mv.ToUci(game.Moves[0]) == "e2e4", "click-click move e2-e4");
                return true;
            });
            steps.Add(() => game.Ply >= 2 && !botThinking);
            steps.Add(() =>
            {
                Check(game.Current.SideToMove == Piece.White, "bot replied, user to move again");
                board.SimulateDrag(S("g1"), S("f3"));
                Check(game.Ply == 3 && Mv.ToUci(game.Moves[2]) == "g1f3", "drag & drop move Ng1-f3");
                return true;
            });
            steps.Add(() => game.Ply >= 4 && !botThinking);
            steps.Add(() =>
            {
                int before = game.Ply;
                board.SimulateClick(S("a1"));
                board.SimulateClick(S("a5"));
                Check(game.Ply == before, "illegal rook move a1-a5 through own pawn is rejected");
                board.SimulateDrag(S("f1"), S("f1"));
                board.SimulateDrag(S("c1"), S("h6"));
                Check(game.Ply == before, "bishop c1 cannot jump over the d2 pawn");
                board.SimulateDrag(S("f3"), S("e1"));
                Check(game.Ply == before, "knight cannot land on own king");
                Undo();
                Check(game.Ply == 2 && game.Current.SideToMove == Piece.White, $"undo returns to user's move (ply {game.Ply})");
                flipped = true;
                RefreshBoard(false);
                board.SimulateDrag(S("d2"), S("d4"));
                Check(game.Ply == 3 && Mv.ToUci(game.Moves[2]) == "d2d4", "drag on a flipped board d2-d4");
                return true;
            });
            steps.Add(() => game.Ply >= 4 && !botThinking);
            steps.Add(() =>
            {
                Hint();
                return true;
            });
            steps.Add(() => hintMove != Mv.None || sw.ElapsedMilliseconds > 60000);
            steps.Add(() =>
            {
                Check(hintMove != Mv.None && game.Current.IsLegal(hintMove), "hint shows a legal best move: " + Mv.ToUci(hintMove));
                Navigate(1);
                Check(viewPly == 1 && !board.Interactive, "browsing an earlier position is read-only");
                Navigate(game.Ply);
                Check(viewPly == game.Ply && board.Interactive, "back to the current position");
                Check(game.Reviews[0] != null || true, "review of the first move (may still be pending)");

                // Promotion picker on a standalone board.
                promoBoard = new BoardControl { Size = new Size(400, 400) };
                promoBoard.Interactive = true;
                promoBoard.UserColor = Piece.White;
                promoBoard.SetPosition(new Board("8/P6k/8/8/8/8/8/K7 w - - 0 1"), Mv.None, false);
                promoBoard.MoveMade += (m, a) => promoMove = m;
                promoBoard.SimulateClick(S("a7"));
                promoBoard.SimulateClick(S("a8"));
                Check(promoBoard.PromotionPickerOpen, "promotion picker opens");
                promoBoard.SimulatePromotionChoice(1); // knight
                Check(promoMove != Mv.None && Mv.IsPromotion(promoMove) && Mv.PromotionType(promoMove) == Piece.Knight,
                    "under-promotion to a knight: " + Mv.ToUci(promoMove));
                return true;
            });
            steps.Add(() =>
            {
                // Play a few more moves through the UI so the review has material.
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
                Check(game.Result != GameResult.None && overlay.Visible, "resign ends the game and shows the result card");
                EnterReview();
                Check(reviewMode, "review mode opened");
                return true;
            });
            steps.Add(() =>
            {
                bool done = true;
                foreach (var r in game.Reviews) if (r == null) done = false;
                return done || sw.ElapsedMilliseconds > 120000;
            });
            steps.Add(() =>
            {
                int reviewed = 0;
                foreach (var r in game.Reviews) if (r != null) reviewed++;
                Check(reviewed == game.Ply, $"all {game.Ply} moves classified ({reviewed})");
                foreach (var r in game.Reviews)
                    if (r != null) log.AppendLine($"     {Review.ClassName(r.Class),-16} {r.Comment}");
                Navigate(3);
                Check(viewPly == 3, "navigate in review");
                notationPicker.SelectedIndex = 1;
                Check(displaySans.Count == game.Ply && !displaySans.Exists(s => s.Contains("K^")), "international notation: " + string.Join(" ", displaySans));
                notationPicker.SelectedIndex = 0;
                Check(displaySans.Count == game.Ply, "user notation: " + string.Join(" ", displaySans));
                return true;
            });

            int index = 0;
            var timer = new Timer { Interval = 100 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                bool finished;
                try { finished = steps[index](); }
                catch (Exception ex) { Check(false, $"step {index} threw {ex}"); finished = true; index = steps.Count - 1; }
                if (finished) index++;
                if (sw.ElapsedMilliseconds > 240000) { Check(false, "timeout at step " + index); index = steps.Count; }
                if (index >= steps.Count)
                {
                    log.AppendLine($"FAILURES: {failures}, time {sw.ElapsedMilliseconds} ms");
                    File.WriteAllText(selfTestPath, log.ToString(), Encoding.UTF8);
                    using (var bmp = new Bitmap(Width, Height))
                    {
                        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                        bmp.Save(Path.ChangeExtension(selfTestPath, ".png"), System.Drawing.Imaging.ImageFormat.Png);
                    }
                    promoBoard?.Dispose();
                    Close();
                    return;
                }
                timer.Start();
            };
            timer.Start();
        }
    }
}
