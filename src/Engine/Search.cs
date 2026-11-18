using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sokol.Engine
{
    public sealed class SearchLimits
    {
        public int MaxDepth = 64;
        public long MaxNodes = long.MaxValue;
        /// <summary>Time budget in milliseconds; 0 = unlimited (until depth/nodes/Stop).</summary>
        public int TimeMs;
        public int MultiPV = 1;
        /// <summary>Optional restriction of the root moves.</summary>
        public List<int> SearchMoves;
    }

    public sealed class PvLine
    {
        /// <summary>Score from the point of view of the side to move at the root.</summary>
        public int Score;
        public int[] Moves = new int[0];
        public int FirstMove => Moves.Length > 0 ? Moves[0] : Move.None;
    }

    public sealed class SearchInfo
    {
        public int Depth;
        public long Nodes;
        public long TimeMs;
        public List<PvLine> Lines = new List<PvLine>();
        public int BestMove => Lines.Count > 0 ? Lines[0].FirstMove : Move.None;
        public int Score => Lines.Count > 0 ? Lines[0].Score : 0;
    }

    /// <summary>Alpha-beta (PVS) searcher with iterative deepening, aspiration windows and MultiPV.</summary>
    public sealed class Searcher
    {
        const int MaxPly = 120;
        const int Inf = Score.Infinite;

        readonly TranspositionTable tt;
        Board board;
        volatile bool stop;
        bool canStop;
        long nodes, maxNodes;
        long hardLimitMs;
        Stopwatch sw;

        readonly int[][] moveBuf = new int[MaxPly + 2][];
        readonly int[][] scoreBuf = new int[MaxPly + 2][];
        readonly int[][] quietBuf = new int[MaxPly + 2][];
        readonly int[] killers = new int[(MaxPly + 2) * 2];
        readonly int[] history = new int[2 * 64 * 64];
        readonly int[] evalStack = new int[MaxPly + 2];
        readonly int[] pvTable = new int[(MaxPly + 2) * (MaxPly + 2)];
        readonly int[] pvLength = new int[MaxPly + 2];
        static readonly int[] LmrTable = new int[64 * 64];

        sealed class RootMove
        {
            public int Move;
            public int Score = -Inf;
            public int PrevScore = -Inf;
            public int[] Pv;
        }

        List<RootMove> rootMoves;

        static Searcher()
        {
            for (int d = 1; d < 64; d++)
                for (int m = 1; m < 64; m++)
                    LmrTable[d * 64 + m] = (int)(0.75 + Math.Log(d) * Math.Log(m) / 2.25);
        }

        public Searcher(int hashMb = 32)
        {
            tt = new TranspositionTable(hashMb);
            for (int i = 0; i < moveBuf.Length; i++)
            {
                moveBuf[i] = new int[256];
                scoreBuf[i] = new int[256];
                quietBuf[i] = new int[256];
            }
        }

        public long Nodes => nodes;
        public void Stop() => stop = true;

        public void Clear()
        {
            tt.Clear();
            Array.Clear(history, 0, history.Length);
            Array.Clear(killers, 0, killers.Length);
        }

        // ================================================================== public API

        public SearchInfo Search(Board position, SearchLimits limits, Action<SearchInfo> onInfo = null)
        {
            Prepare(position, limits);
            var result = new SearchInfo();

            rootMoves = new List<RootMove>();
            foreach (int m in board.LegalMoves())
                if (limits.SearchMoves == null || limits.SearchMoves.Contains(m))
                    rootMoves.Add(new RootMove { Move = m });
            if (rootMoves.Count == 0) return result;

            int multiPv = Math.Max(1, Math.Min(limits.MultiPV, rootMoves.Count));
            for (int depth = 1; depth <= Math.Min(limits.MaxDepth, MaxPly - 10); depth++)
            {
                for (int pvIdx = 0; pvIdx < multiPv && !stop; pvIdx++)
                {
                    int prev = rootMoves[pvIdx].PrevScore;
                    int delta = 20;
                    int alpha = -Inf, beta = Inf;
                    if (depth >= 4 && prev != -Inf && !Score.IsMate(prev))
                    {
                        alpha = Math.Max(prev - delta, -Inf);
                        beta = Math.Min(prev + delta, Inf);
                    }
                    while (true)
                    {
                        int s = RootSearch(depth, alpha, beta, pvIdx);
                        SortRoot(pvIdx);
                        if (stop) break;
                        if (s <= alpha && alpha > -Inf)
                        {
                            beta = (alpha + beta) / 2;
                            alpha = Math.Max(s - delta, -Inf);
                        }
                        else if (s >= beta && beta < Inf)
                        {
                            beta = Math.Min(s + delta, Inf);
                        }
                        else break;
                        delta += delta / 2 + 10;
                        if (delta > 1000) { alpha = -Inf; beta = Inf; }
                    }
                }
                if (stop && result.Lines.Count > 0) break;

                foreach (var rm in rootMoves) rm.PrevScore = rm.Score;
                result = Snapshot(depth, multiPv);
                canStop = true;
                onInfo?.Invoke(result);
                if (stop) break;

                long elapsed = sw.ElapsedMilliseconds;
                if (limits.TimeMs > 0 && elapsed > limits.TimeMs * 55 / 100) break;
                if (nodes >= maxNodes) break;
                int best = rootMoves[0].Score;
                if (Score.IsMate(best) && depth >= (Score.Mate - Math.Abs(best)) + 4 && multiPv == 1) break;
            }
            return result;
        }

        /// <summary>
        /// Exact score (side to move POV) of every legal move at a fixed shallow depth.
        /// Used by the weaker playing levels to pick "human-like" moves.
        /// </summary>
        public List<KeyValuePair<int, int>> ScoreAllMoves(Board position, int depth, int timeMs = 0)
        {
            var limits = new SearchLimits { MaxDepth = depth, TimeMs = timeMs };
            Prepare(position, limits);
            canStop = true;
            var res = new List<KeyValuePair<int, int>>();
            var moves = board.LegalMoves();
            // Order by a quick 1-ply estimate first so the TT gets useful entries.
            foreach (int m in moves)
            {
                board.MakeMove(m);
                int s = -Negamax(depth - 1, 1, -Inf, Inf, true, true);
                board.UnmakeMove();
                if (stop && res.Count > 0) break;
                res.Add(new KeyValuePair<int, int>(m, s));
            }
            res.Sort((a, b) => b.Value.CompareTo(a.Value));
            return res;
        }

        void Prepare(Board position, SearchLimits limits)
        {
            board = position.Clone();
            stop = false;
            canStop = false;
            nodes = 0;
            maxNodes = limits.MaxNodes;
            hardLimitMs = limits.TimeMs;
            sw = Stopwatch.StartNew();
            tt.NewSearch();
            Array.Clear(killers, 0, killers.Length);
            for (int i = 0; i < history.Length; i++) history[i] /= 4;
            evalStack[0] = board.InCheck() ? -Inf : Eval.Evaluate(board);
        }

        SearchInfo Snapshot(int depth, int multiPv)
        {
            var info = new SearchInfo { Depth = depth, Nodes = nodes, TimeMs = sw.ElapsedMilliseconds };
            for (int i = 0; i < multiPv && i < rootMoves.Count; i++)
            {
                var rm = rootMoves[i];
                info.Lines.Add(new PvLine { Score = rm.Score, Moves = rm.Pv ?? new[] { rm.Move } });
            }
            // Search instability can leave a later line slightly above the first one.
            info.Lines.Sort((a, b) => b.Score.CompareTo(a.Score));
            return info;
        }

        void SortRoot(int from)
        {
            // Stable insertion sort by score, descending.
            for (int i = from + 1; i < rootMoves.Count; i++)
            {
                var x = rootMoves[i];
                int j = i - 1;
                while (j >= from && rootMoves[j].Score < x.Score)
                {
                    rootMoves[j + 1] = rootMoves[j];
                    j--;
                }
                rootMoves[j + 1] = x;
            }
        }

        int[] BuildPv(int first)
        {
            int len = Math.Max(0, pvLength[1] - 1);
            var pv = new int[len + 1];
            pv[0] = first;
            for (int i = 0; i < len; i++) pv[i + 1] = pvTable[1 * (MaxPly + 2) + 1 + i];
            return pv;
        }

        void CheckLimits()
        {
            if (!canStop) return; // always finish depth 1 so there is a move to play
            if (nodes >= maxNodes) stop = true;
            else if (hardLimitMs > 0 && sw.ElapsedMilliseconds >= hardLimitMs) stop = true;
        }

        // ================================================================== root

        int RootSearch(int depth, int alpha, int beta, int pvIdx)
        {
            int best = -Inf;
            for (int i = pvIdx; i < rootMoves.Count; i++)
            {
                var rm = rootMoves[i];
                board.MakeMove(rm.Move);
                nodes++;
                bool givesCheck = board.InCheck();
                int score;
                if (i == pvIdx)
                {
                    score = -Negamax(depth - 1, 1, -beta, -alpha, true, true);
                }
                else
                {
                    int r = (depth >= 3 && i - pvIdx >= 4 && Move.IsQuiet(rm.Move) && !givesCheck) ? 1 : 0;
                    score = -Negamax(depth - 1 - r, 1, -alpha - 1, -alpha, false, true);
                    if (score > alpha && r > 0)
                        score = -Negamax(depth - 1, 1, -alpha - 1, -alpha, false, true);
                    if (score > alpha && score < beta)
                        score = -Negamax(depth - 1, 1, -beta, -alpha, true, true);
                }
                board.UnmakeMove();
                if (stop) return best;

                if (i == pvIdx || score > alpha)
                {
                    rm.Score = score;
                    rm.Pv = BuildPv(rm.Move);
                    if (score > alpha) alpha = score;
                }
                else rm.Score = -Inf;

                if (score > best) best = score;
                if (score >= beta) return score;
            }
            return best;
        }

        // ================================================================== main search

        int Negamax(int depth, int ply, int alpha, int beta, bool pvNode, bool allowNull)
        {
            pvLength[ply] = ply;
            if (depth <= 0) return Quiesce(ply, alpha, beta);

            nodes++;
            if ((nodes & 1023) == 0) CheckLimits();
            if (stop) return 0;
            if (ply >= MaxPly) return Eval.Evaluate(board);

            if (board.IsRepetitionOrFifty() || board.IsInsufficientMaterial()) return 0;

            // Mate distance pruning.
            alpha = Math.Max(alpha, -Score.Mate + ply);
            beta = Math.Min(beta, Score.Mate - ply - 1);
            if (alpha >= beta) return alpha;

            bool inCheck = board.InCheck();
            if (inCheck) depth++;

            ulong key = board.Hash;
            int ttMove = Move.None;
            int staticEval;
            if (tt.Probe(key, out TTEntry e))
            {
                ttMove = e.Move;
                if (!pvNode && e.Depth >= depth)
                {
                    int s = TranspositionTable.ScoreFromTT(e.Score, ply);
                    if (e.Bound == Bound.Exact || (e.Bound == Bound.Lower && s >= beta) || (e.Bound == Bound.Upper && s <= alpha))
                        return s;
                }
                staticEval = inCheck ? -Inf : e.StaticEval;
            }
            else staticEval = inCheck ? -Inf : Eval.Evaluate(board);

            evalStack[ply] = staticEval;
            bool improving = !inCheck && ply >= 2 && evalStack[ply - 2] != -Inf && staticEval > evalStack[ply - 2];
            int us = board.SideToMove;

            if (!pvNode && !inCheck)
            {
                // Reverse futility pruning.
                if (depth <= 7 && staticEval - 80 * (depth - (improving ? 1 : 0)) >= beta && Math.Abs(beta) < Score.MateBound)
                    return staticEval;

                // Null move pruning.
                if (allowNull && depth >= 3 && staticEval >= beta && board.HasNonPawnMaterial(us))
                {
                    int r = 3 + depth / 4 + Math.Min((staticEval - beta) / 200, 3);
                    board.MakeNullMove();
                    int s = -Negamax(depth - 1 - r, ply + 1, -beta, -beta + 1, false, false);
                    board.UnmakeNullMove();
                    if (stop) return 0;
                    if (s >= beta) return s >= Score.MateBound ? beta : s;
                }

                // Razoring.
                if (depth <= 2 && staticEval + 250 * depth < alpha)
                {
                    int s = Quiesce(ply, alpha - 1, alpha);
                    if (s < alpha) return s;
                }
            }

            // Internal iterative reduction.
            if (depth >= 4 && ttMove == Move.None) depth--;

            int[] moves = moveBuf[ply];
            int[] scores = scoreBuf[ply];
            int[] quiets = quietBuf[ply];
            int n = board.GenerateMoves(moves, false);
            ScoreMoves(moves, scores, n, ttMove, ply);

            int bestScore = -Inf, bestMove = Move.None, legal = 0, quietCount = 0, origAlpha = alpha;
            int lmpLimit = (3 + depth * depth) / (improving ? 1 : 2);

            for (int i = 0; i < n; i++)
            {
                int m = PickNext(moves, scores, i, n);
                bool quiet = Move.IsQuiet(m);

                if (!pvNode && !inCheck && legal > 0 && bestScore > -Score.MateBound)
                {
                    if (quiet)
                    {
                        if (depth <= 7 && quietCount >= lmpLimit) continue;
                        if (depth <= 6 && staticEval + 100 + 90 * depth <= alpha) continue;
                    }
                    else if (depth <= 5 && scores[i] < 0 && See(m) < -90 * depth) continue;
                }

                if (!board.MakeMove(m)) continue;
                legal++;
                nodes++;
                bool givesCheck = board.InCheck();
                int newDepth = depth - 1;
                int score;

                if (legal == 1)
                {
                    score = -Negamax(newDepth, ply + 1, -beta, -alpha, pvNode, true);
                }
                else
                {
                    int r = 0;
                    if (depth >= 3 && quiet && !inCheck && !givesCheck && legal > (pvNode ? 3 : 2))
                    {
                        r = LmrTable[Math.Min(depth, 63) * 64 + Math.Min(legal, 63)];
                        if (pvNode) r--;
                        if (!improving) r++;
                        if (m == killers[ply * 2] || m == killers[ply * 2 + 1]) r--;
                        r -= history[us * 4096 + (m & 4095)] / 6000;
                        r = Math.Max(0, Math.Min(r, newDepth - 1));
                    }
                    score = -Negamax(newDepth - r, ply + 1, -alpha - 1, -alpha, false, true);
                    if (score > alpha && r > 0)
                        score = -Negamax(newDepth, ply + 1, -alpha - 1, -alpha, false, true);
                    if (pvNode && score > alpha && score < beta)
                        score = -Negamax(newDepth, ply + 1, -beta, -alpha, true, true);
                }
                board.UnmakeMove();
                if (stop) return 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    if (score > alpha)
                    {
                        bestMove = m;
                        alpha = score;
                        if (pvNode) UpdatePv(ply, m);
                        if (score >= beta)
                        {
                            if (quiet) OnQuietCutoff(m, ply, depth, quiets, quietCount, us);
                            break;
                        }
                    }
                }
                if (quiet && quietCount < 256) quiets[quietCount++] = m;
            }

            if (legal == 0) return inCheck ? -Score.Mate + ply : 0;

            byte bound = bestScore >= beta ? Bound.Lower : (bestScore > origAlpha ? Bound.Exact : Bound.Upper);
            tt.Store(key, bestMove, bestScore, staticEval == -Inf ? 0 : staticEval, depth, bound, ply);
            return bestScore;
        }

        void UpdatePv(int ply, int m)
        {
            int row = ply * (MaxPly + 2), next = (ply + 1) * (MaxPly + 2);
            pvTable[row + ply] = m;
            int len = pvLength[ply + 1];
            for (int j = ply + 1; j < len; j++) pvTable[row + j] = pvTable[next + j];
            pvLength[ply] = Math.Max(len, ply + 1);
        }

        void OnQuietCutoff(int m, int ply, int depth, int[] quiets, int quietCount, int us)
        {
            if (killers[ply * 2] != m)
            {
                killers[ply * 2 + 1] = killers[ply * 2];
                killers[ply * 2] = m;
            }
            int bonus = Math.Min(depth * depth, 400);
            UpdateHistory(us, m, bonus);
            for (int i = 0; i < quietCount; i++)
                if (quiets[i] != m) UpdateHistory(us, quiets[i], -bonus);
        }

        void UpdateHistory(int us, int m, int bonus)
        {
            int idx = us * 4096 + (m & 4095);
            history[idx] += bonus * 32 - history[idx] * Math.Abs(bonus) / 512;
        }

        // ================================================================== quiescence

        int Quiesce(int ply, int alpha, int beta)
        {
            pvLength[ply] = ply;
            nodes++;
            if ((nodes & 1023) == 0) CheckLimits();
            if (stop) return 0;
            if (ply >= MaxPly) return Eval.Evaluate(board);

            bool inCheck = board.InCheck();
            ulong key = board.Hash;
            int ttMove = Move.None;
            if (tt.Probe(key, out TTEntry e))
            {
                int s = TranspositionTable.ScoreFromTT(e.Score, ply);
                if (e.Bound == Bound.Exact || (e.Bound == Bound.Lower && s >= beta) || (e.Bound == Bound.Upper && s <= alpha))
                    return s;
                ttMove = e.Move;
            }

            int standPat = -Inf, bestScore = -Inf;
            if (!inCheck)
            {
                standPat = Eval.Evaluate(board);
                if (standPat >= beta) return standPat;
                if (standPat > alpha) alpha = standPat;
                bestScore = standPat;
            }

            int[] moves = moveBuf[ply];
            int[] scores = scoreBuf[ply];
            int n = board.GenerateMoves(moves, !inCheck);
            ScoreMoves(moves, scores, n, ttMove, ply);
            int legal = 0, bestMove = Move.None, origAlpha = alpha;

            for (int i = 0; i < n; i++)
            {
                int m = PickNext(moves, scores, i, n);
                if (!inCheck)
                {
                    if (!Move.IsPromotion(m))
                    {
                        int victim = Move.IsEnPassant(m) ? Piece.Pawn : Piece.TypeOf(board.Squares[Move.To(m)]);
                        if (standPat + Eval.SeeValue[victim] + 200 < alpha) continue;
                    }
                    if (scores[i] < 0) continue; // losing capture by SEE
                }
                if (!board.MakeMove(m)) continue;
                legal++;
                int score = -Quiesce(ply + 1, -beta, -alpha);
                board.UnmakeMove();
                if (stop) return 0;
                if (score > bestScore)
                {
                    bestScore = score;
                    if (score > alpha)
                    {
                        alpha = score;
                        bestMove = m;
                        if (score >= beta) break;
                    }
                }
            }

            if (inCheck && legal == 0) return -Score.Mate + ply;
            byte bound = bestScore >= beta ? Bound.Lower : (bestScore > origAlpha ? Bound.Exact : Bound.Upper);
            tt.Store(key, bestMove, bestScore, standPat == -Inf ? 0 : standPat, 0, bound, ply);
            return bestScore;
        }

        // ================================================================== move ordering

        void ScoreMoves(int[] moves, int[] scores, int n, int ttMove, int ply)
        {
            int us = board.SideToMove;
            int k1 = killers[ply * 2], k2 = killers[ply * 2 + 1];
            for (int i = 0; i < n; i++)
            {
                int m = moves[i];
                int s;
                if (m == ttMove) s = 3_000_000;
                else if (Move.IsCapture(m))
                {
                    int victim = Move.IsEnPassant(m) ? Piece.Pawn : Piece.TypeOf(board.Squares[Move.To(m)]);
                    int attacker = Piece.TypeOf(board.Squares[Move.From(m)]);
                    int mvvLva = victim * 100 - attacker;
                    if (Move.IsPromotion(m)) mvvLva += 500;
                    if (Eval.SeeValue[attacker] > Eval.SeeValue[victim] && !Move.IsPromotion(m) && See(m) < 0)
                        s = -1_000_000 + mvvLva;
                    else
                        s = 2_000_000 + mvvLva;
                }
                else if (Move.IsPromotion(m)) s = Move.PromotionType(m) == Piece.Queen ? 1_900_000 : -2_000_000;
                else if (m == k1) s = 1_500_000;
                else if (m == k2) s = 1_400_000;
                else s = history[us * 4096 + (m & 4095)];
                scores[i] = s;
            }
        }

        static int PickNext(int[] moves, int[] scores, int start, int n)
        {
            int best = start;
            for (int j = start + 1; j < n; j++)
                if (scores[j] > scores[best]) best = j;
            if (best != start)
            {
                int tm = moves[start]; moves[start] = moves[best]; moves[best] = tm;
                int ts = scores[start]; scores[start] = scores[best]; scores[best] = ts;
            }
            return moves[start];
        }

        readonly int[] seeGain = new int[40];

        /// <summary>Static exchange evaluation of a move (material gain for the mover).</summary>
        public int See(int m)
        {
            return StaticExchange(board, m, seeGain);
        }

        public static int StaticExchange(Board b, int m, int[] gain = null)
        {
            if (Move.IsCastle(m)) return 0;
            if (gain == null) gain = new int[40];
            int from = Move.From(m), to = Move.To(m);
            int us = b.SideToMove;
            int attackerType = Piece.TypeOf(b.Squares[from]);
            int capturedType = Move.IsEnPassant(m) ? Piece.Pawn : (b.Squares[to] != Piece.None ? Piece.TypeOf(b.Squares[to]) : -1);

            ulong occ = b.Occupied;
            int d = 0;
            gain[0] = capturedType >= 0 ? Eval.SeeValue[capturedType] : 0;
            if (Move.IsPromotion(m))
            {
                gain[0] += Eval.SeeValue[Piece.Queen] - Eval.SeeValue[Piece.Pawn];
                attackerType = Piece.Queen;
            }
            occ ^= 1UL << from;
            if (Move.IsEnPassant(m)) occ ^= 1UL << (us == Piece.White ? to - 8 : to + 8);

            ulong bq = b.Pieces[2] | b.Pieces[8] | b.Pieces[4] | b.Pieces[10];
            ulong rq = b.Pieces[3] | b.Pieces[9] | b.Pieces[4] | b.Pieces[10];
            ulong attackers = b.AttackersTo(to, occ) & occ;
            int side = us ^ 1;

            while (true)
            {
                ulong sideAtt = attackers & b.Colors[side];
                if (sideAtt == 0) break;
                int pt, sq = -1;
                for (pt = Piece.Pawn; pt <= Piece.King; pt++)
                {
                    ulong x = sideAtt & b.Pieces[side * 6 + pt];
                    if (x != 0) { sq = BB.Lsb(x); break; }
                }
                d++;
                gain[d] = Eval.SeeValue[attackerType] - gain[d - 1];
                if (Math.Max(-gain[d - 1], gain[d]) < 0) break;
                attackerType = pt;
                occ ^= 1UL << sq;
                attackers |= (BB.BishopAttacks(to, occ) & bq) | (BB.RookAttacks(to, occ) & rq);
                attackers &= occ;
                side ^= 1;
                if (d >= 38) break;
            }
            // Every stored gain[d] is a capture that really happens, so all of them take part in the minimax.
            for (; d > 0; d--) gain[d - 1] = -Math.Max(-gain[d - 1], gain[d]);
            return gain[0];
        }
    }
}
