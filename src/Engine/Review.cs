using System;
using System.Collections.Generic;

namespace Sokol.Engine
{
    public enum MoveClass { None, Brilliant, Great, Best, Excellent, Good, Book, Inaccuracy, Mistake, Miss, Blunder, Forced }

    /// <summary>Engine verdict for one position (scores from the side to move's point of view).</summary>
    public sealed class PositionAnalysis
    {
        public ulong Hash;
        public int Depth;
        public List<PvLine> Lines = new List<PvLine>();
        /// <summary>No legal moves or a draw by rule: the score is exact.</summary>
        public bool Terminal;
        public int TerminalScore;

        public int Score => Terminal ? TerminalScore : (Lines.Count > 0 ? Lines[0].Score : 0);
        public int BestMove => Terminal || Lines.Count == 0 ? Move.None : Lines[0].FirstMove;
    }

    public sealed class MoveReview
    {
        public MoveClass Class;
        public int Move;
        public int BestMove;
        public int[] BestLine = new int[0];
        /// <summary>Opponent's best continuation after the played move.</summary>
        public int[] Refutation = new int[0];
        /// <summary>Evaluations from White's point of view, before and after the move.</summary>
        public int WhiteScoreBefore, WhiteScoreAfter;
        public double Accuracy;
        public double Loss;
        public string Comment = "";
    }

    public static class Review
    {
        /// <summary>Winning chances in [-1, 1] for the side whose score this is (lichess model).</summary>
        public static double WinChance(int score)
        {
            if (Score.IsMate(score)) return score > 0 ? 1 : -1;
            double cp = Math.Max(-1500, Math.Min(1500, score));
            return 2.0 / (1.0 + Math.Exp(-0.00368208 * cp)) - 1.0;
        }

        public static double ExpectedPoints(int score) => (1 + WinChance(score)) / 2;

        public static string ClassName(MoveClass c)
        {
            switch (c)
            {
                case MoveClass.Brilliant: return "Блестящий ход";
                case MoveClass.Great: return "Отличный ход";
                case MoveClass.Best: return "Лучший ход";
                case MoveClass.Excellent: return "Превосходно";
                case MoveClass.Good: return "Хороший ход";
                case MoveClass.Book: return "Теория";
                case MoveClass.Inaccuracy: return "Неточность";
                case MoveClass.Mistake: return "Ошибка";
                case MoveClass.Miss: return "Промах";
                case MoveClass.Blunder: return "Зевок";
                case MoveClass.Forced: return "Вынужденный ход";
                default: return "";
            }
        }

        public static string ClassSymbol(MoveClass c)
        {
            switch (c)
            {
                case MoveClass.Brilliant: return "!!";
                case MoveClass.Great: return "!";
                case MoveClass.Best: return "★";
                case MoveClass.Excellent: return "👍";
                case MoveClass.Good: return "✓";
                case MoveClass.Book: return "📖";
                case MoveClass.Inaccuracy: return "?!";
                case MoveClass.Mistake: return "?";
                case MoveClass.Miss: return "✗";
                case MoveClass.Blunder: return "??";
                case MoveClass.Forced: return "□";
                default: return "";
            }
        }

        /// <summary>Formats a score for display: "+1.25", "-0.40", "M3", "-M2".</summary>
        public static string FormatScore(int score)
        {
            if (Score.IsMate(score))
            {
                int n = Math.Abs(Score.MateInMoves(score));
                return (score > 0 ? "M" : "-M") + n;
            }
            double v = score / 100.0;
            return (v > 0 ? "+" : v < 0 ? "−" : "") + Math.Abs(v).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        static int Material(Board b, int color)
        {
            int o = color * 6;
            return BB.PopCount(b.Pieces[o]) + 3 * BB.PopCount(b.Pieces[o + 1] | b.Pieces[o + 2])
                 + 5 * BB.PopCount(b.Pieces[o + 3]) + 9 * BB.PopCount(b.Pieces[o + 4]);
        }

        /// <summary>
        /// Classifies the move played in position "before" (chess.com-like categories) and writes an explanation.
        /// </summary>
        public static MoveReview Classify(Board before, int move, PositionAnalysis aBefore, PositionAnalysis aAfter,
                                          MoveClass prevOpponentClass, NotationStyle style, string openingName = null)
        {
            var r = new MoveReview { Move = move };
            int mover = before.SideToMove;
            int sign = mover == Piece.White ? 1 : -1;
            var legal = before.LegalMoves();

            var after = before.Clone();
            after.MakeMove(move);

            int bestScore = aBefore.Score;
            int playedScore = -aAfter.Score;
            r.BestMove = aBefore.BestMove;
            if (aBefore.Lines.Count > 0) r.BestLine = aBefore.Lines[0].Moves;
            if (aAfter.Lines.Count > 0) r.Refutation = aAfter.Lines[0].Moves;
            if (move == r.BestMove || playedScore > bestScore) bestScore = Math.Max(bestScore, playedScore);
            r.WhiteScoreBefore = sign * bestScore;
            r.WhiteScoreAfter = sign * playedScore;

            double epBest = ExpectedPoints(bestScore), epPlayed = ExpectedPoints(playedScore);
            double loss = Math.Max(0, epBest - epPlayed);
            if (move == r.BestMove) loss = 0;
            r.Loss = loss;

            double wpBefore = 50 + 50 * WinChance(bestScore), wpAfter = 50 + 50 * WinChance(playedScore);
            r.Accuracy = Math.Max(0, Math.Min(100, 103.1668 * Math.Exp(-0.04354 * Math.Max(0, wpBefore - wpAfter)) - 3.1669));

            string san = Notation.ToSan(before, move, style);
            string bestSan = r.BestMove != Move.None && before.IsLegal(r.BestMove) ? Notation.ToSan(before, r.BestMove, style) : "";
            string bestLine = r.BestLine.Length > 0 ? Notation.FormatLine(before, r.BestLine, style, 8) : "";
            string refLine = r.Refutation.Length > 0 ? Notation.FormatLine(after, r.Refutation, style, 6) : "";

            // ------------------------------------------------ forced / book
            if (legal.Count == 1)
            {
                r.Class = MoveClass.Forced;
                r.Accuracy = 100;
                r.Comment = $"{san} — единственный возможный ход.";
                return r;
            }
            if (OpeningBook.IsBookMove(before, move))
            {
                r.Class = MoveClass.Book;
                r.Accuracy = 100;
                string name = OpeningBook.NameOf(after) ?? openingName;
                r.Comment = $"{san} — теоретический ход" + (name != null ? $" ({name})." : ".");
                return r;
            }

            // ------------------------------------------------ base category by expected points lost
            MoveClass c;
            if (move == r.BestMove || playedScore >= bestScore) c = MoveClass.Best;
            else if (loss < 0.02) c = MoveClass.Excellent;
            else if (loss < 0.05) c = MoveClass.Good;
            else if (loss < 0.10) c = MoveClass.Inaccuracy;
            else if (loss < 0.20) c = MoveClass.Mistake;
            else c = MoveClass.Blunder;

            int see = Searcher.StaticExchange(before, move);
            int movedType = Piece.TypeOf(before.Squares[Move.From(move)]);
            int lastMove = before.LastMove;

            // Missing a short forced mate is a "miss" even if the position stays winning.
            bool hadShortMate = Score.IsMate(bestScore) && bestScore > 0 && Score.MateInMoves(bestScore) <= 3;
            bool keepsMate = Score.IsMate(playedScore) && playedScore > 0;
            if (hadShortMate && !keepsMate && c <= MoveClass.Good) c = MoveClass.Miss;

            // ------------------------------------------------ brilliant: a sound piece sacrifice
            if ((c == MoveClass.Best || c == MoveClass.Excellent) && movedType != Piece.Pawn && movedType != Piece.King &&
                see <= -200 && epPlayed >= 0.45 && (epBest < 0.95 || (Score.IsMate(bestScore) && bestScore > 0)))
            {
                c = MoveClass.Brilliant;
            }
            // ------------------------------------------------ great: the only good move
            else if (c == MoveClass.Best && aBefore.Lines.Count >= 2 && epBest >= 0.40 && epBest < 0.97)
            {
                double epSecond = ExpectedPoints(aBefore.Lines[1].Score);
                bool recapture = lastMove != Move.None && Move.IsCapture(lastMove) && Move.To(lastMove) == Move.To(move);
                if (epBest - epSecond >= 0.15 && !recapture) c = MoveClass.Great;
            }

            // ------------------------------------------------ miss: failing to punish
            if ((c == MoveClass.Mistake || c == MoveClass.Blunder) && epPlayed >= 0.30 && epBest >= 0.65 &&
                (prevOpponentClass == MoveClass.Mistake || prevOpponentClass == MoveClass.Blunder ||
                 prevOpponentClass == MoveClass.Miss || prevOpponentClass == MoveClass.Inaccuracy || hadShortMate))
                c = MoveClass.Miss;

            r.Class = c;

            // ------------------------------------------------ explanation
            var reasons = new List<string>();
            if (Score.IsMate(playedScore) && playedScore < 0 && !(Score.IsMate(bestScore) && bestScore < 0))
                reasons.Add($"Позволяет сопернику поставить мат в {Moves(Math.Abs(Score.MateInMoves(playedScore)))}.");
            if (Score.IsMate(bestScore) && bestScore > 0 && !keepsMate)
                reasons.Add($"Был форсированный мат в {Moves(Score.MateInMoves(bestScore))}.");

            if (c >= MoveClass.Inaccuracy && c != MoveClass.Book)
            {
                if (see <= -150 && movedType != Piece.King)
                    reasons.Add($"{Capitalize(Notation.PieceNameAcc(movedType))} на {Sq.Name(Move.To(move))} можно забрать.");
                // Material lost along the refutation line, measured only at quiet points
                // (both sides moved and the next move is not a capture), so exchanges are complete.
                var tmp = after.Clone();
                int mat0 = Material(tmp, mover) - Material(tmp, mover ^ 1);
                int mat1 = mat0;
                for (int k = 0; k < r.Refutation.Length && k < 10; k++)
                {
                    int m = r.Refutation[k];
                    if (!tmp.IsLegal(m)) break;
                    tmp.MakeMove(m);
                    bool nextCapture = k + 1 < r.Refutation.Length && Move.IsCapture(r.Refutation[k + 1]);
                    if ((k + 1) % 2 == 0 && !nextCapture)
                    {
                        mat1 = Material(tmp, mover) - Material(tmp, mover ^ 1);
                        if (k + 1 >= 4) break;
                    }
                }
                if (mat0 - mat1 >= 2 && reasons.Count == 0)
                    reasons.Add($"Теряет материал (≈ {Pawns(mat0 - mat1)}) после {refLine}.");
                else if (refLine.Length > 0 && reasons.Count == 0)
                    reasons.Add($"Сильный ответ соперника: {refLine}.");
            }

            string head;
            switch (c)
            {
                case MoveClass.Brilliant:
                    head = $"{san} — блестящий ход! Жертва ({Notation.PieceName(movedType)}) оправдана."; break;
                case MoveClass.Great:
                    head = $"{san} — отличный ход: единственный, который удерживает позицию."; break;
                case MoveClass.Best:
                    head = $"{san} — лучший ход."; break;
                case MoveClass.Excellent:
                    head = $"{san} — превосходно. Лучший ход: {bestSan}."; break;
                case MoveClass.Good:
                    head = $"{san} — хороший ход, но точнее {bestSan}."; break;
                case MoveClass.Inaccuracy:
                    head = $"{san} — неточность. Лучше было {bestSan}."; break;
                case MoveClass.Mistake:
                    head = $"{san} — ошибка. Лучше было {bestSan}."; break;
                case MoveClass.Miss:
                    head = $"{san} — промах: упущена возможность {bestSan}."; break;
                default:
                    head = $"{san} — зевок! Лучше было {bestSan}."; break;
            }
            string tail = reasons.Count > 0 ? " " + string.Join(" ", reasons) : "";
            string lineInfo = c > MoveClass.Best && bestLine.Length > 0 ? $" Лучшее продолжение: {bestLine}." : "";
            r.Comment = head + tail + lineInfo;
            return r;
        }

        static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s.Substring(1);

        /// <summary>Russian plural: 1 ход, 2 хода, 5 ходов.</summary>
        static string Plural(int n, string one, string few, string many)
        {
            int m10 = n % 10, m100 = n % 100;
            if (m10 == 1 && m100 != 11) return $"{n} {one}";
            if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return $"{n} {few}";
            return $"{n} {many}";
        }

        static string Moves(int n) => Plural(n, "ход", "хода", "ходов");
        static string Pawns(int n) => Plural(n, "пешку", "пешки", "пешек");

        /// <summary>Game accuracy from per-move accuracies (mix of arithmetic and harmonic mean).</summary>
        public static double GameAccuracy(IList<double> moves)
        {
            if (moves.Count == 0) return 0;
            double sum = 0, inv = 0;
            foreach (double a in moves)
            {
                sum += a;
                inv += 1.0 / Math.Max(1, a);
            }
            double mean = sum / moves.Count;
            double harmonic = moves.Count / inv;
            return (mean + harmonic) / 2;
        }
    }
}
