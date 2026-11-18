using System;
using System.Diagnostics;
using Sokol.Engine;

namespace Sokol.Tests
{
    static class EngineTests
    {
        static int failures;

        static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : "all";
            var sw = Stopwatch.StartNew();
            ulong touch = BB.RookAttacks(0, 0) ^ (ulong)Eval.MgTable[0];
            Console.WriteLine($"Static init: {sw.ElapsedMilliseconds} ms ({touch & 1})");
            if (mode == "magics") { BB.DumpMagics(); return 0; }
            if (mode == "all" || mode == "perft") Perft(args.Length > 1 && args[1] == "deep");
            if (mode == "all" || mode == "search") SearchTests();
            if (mode == "bench") Bench();
            if (mode == "match") Match(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]));
            if (mode == "all" || mode == "extra") ExtraTests.Run(ref failures);
            Console.WriteLine($"Done in {sw.ElapsedMilliseconds} ms, failures: {failures}");
            return failures == 0 ? 0 : 1;
        }

        static void Check(bool cond, string what)
        {
            if (!cond) { failures++; Console.WriteLine("  FAIL: " + what); }
        }

        static void Perft(bool deep)
        {
            Console.WriteLine("== Perft");
            var cases = new (string fen, long[] counts)[]
            {
                (Board.StartFen, new long[] { 20, 400, 8902, 197281, 4865609 }),
                ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", new long[] { 48, 2039, 97862, 4085603, 193690690 }),
                ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", new long[] { 14, 191, 2812, 43238, 674624 }),
                ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", new long[] { 6, 264, 9467, 422333, 15833292 }),
                ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", new long[] { 44, 1486, 62379, 2103487, 89941194 }),
                ("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", new long[] { 46, 2079, 89890, 3894594, 164075551 }),
            };
            long total = 0;
            var sw = Stopwatch.StartNew();
            foreach (var c in cases)
            {
                var b = new Board(c.fen);
                int maxD = deep ? c.counts.Length : Math.Min(c.counts.Length, 4);
                for (int d = 1; d <= maxD; d++)
                {
                    long n = b.Perft(d);
                    total += n;
                    Check(n == c.counts[d - 1], $"perft {d} of '{c.fen}' = {n}, expected {c.counts[d - 1]}");
                }
                Check(b.ToFen() == c.fen, "FEN roundtrip " + b.ToFen());
            }
            Console.WriteLine($"  nodes {total}, {total * 1000 / Math.Max(1, sw.ElapsedMilliseconds)} nps");
        }

        static void SearchTests()
        {
            Console.WriteLine("== Search");
            var s = new Searcher(32);
            // Mate in 1 / mate in 2 / win material
            var cases = new (string fen, string best, string note)[]
            {
                ("6k1/5ppp/8/8/8/8/5PPP/R5K1 w - - 0 1", "a1a8", "back rank mate in 1"),
                ("r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR w KQkq - 4 4", "h5f7", "scholar's mate"),
                ("2r3k1/5ppp/8/8/8/8/1Q3PPP/6K1 w - - 0 1", "b2g7", "open position (any good move)"),
                ("r2qkb1r/pp2nppp/3p4/2pNN1B1/2BnP3/3P4/PPP2PPP/R2bK2R w KQkq - 1 1", "d5f6", "mate in 2: Nf6+ gxf6 Bxf7#"),
                ("k7/8/8/3q4/8/8/8/K2R4 w - - 0 1", "d1d5", "win the queen"),
            };
            foreach (var c in cases)
            {
                var b = new Board(c.fen);
                var sw = Stopwatch.StartNew();
                var info = s.Search(b, new SearchLimits { TimeMs = 2000, MaxDepth = 30 });
                string got = Move.ToUci(info.BestMove);
                Console.WriteLine($"  {c.note}: best {got} score {info.Score} depth {info.Depth} nodes {info.Nodes} ({info.Nodes * 1000 / Math.Max(1, sw.ElapsedMilliseconds)} nps) pv {Pv(info)}");
                if (c.note.Contains("any")) continue;
                Check(got == c.best, $"{c.note}: expected {c.best}, got {got}");
            }

            // Stalemate / checkmate detection at root
            var mated = new Board("7k/6Q1/6K1/8/8/8/8/8 b - - 0 1");
            Check(mated.LegalMoves().Count == 0 && mated.InCheck(), "checkmate detection");
            var stale = new Board("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
            Check(stale.LegalMoves().Count == 0 && !stale.InCheck(), "stalemate detection");
        }

        static string Pv(SearchInfo info)
        {
            if (info.Lines.Count == 0) return "";
            var parts = new string[Math.Min(8, info.Lines[0].Moves.Length)];
            for (int i = 0; i < parts.Length; i++) parts[i] = Move.ToUci(info.Lines[0].Moves[i]);
            return string.Join(" ", parts);
        }

        /// <summary>Plays games between two playing levels and reports the score of the first one.</summary>
        static void Match(int eloA, int eloB, int games)
        {
            Console.WriteLine($"== Match {eloA} vs {eloB}, {games} games");
            double scoreA = 0;
            int wins = 0, draws = 0, losses = 0; var reasons = new System.Collections.Generic.Dictionary<string, int>();
            object sync = new object();
            var sw = Stopwatch.StartNew();
            System.Threading.Tasks.Parallel.For(0, games, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, g =>
            {
                var a = new Bot(16, 1000 + g);
                var b = new Bot(16, 2000 + g);
                bool aWhite = g % 2 == 0;
                var board = new Board();
                int result = 0; string why = "maxply"; // +1 white wins, -1 black wins, 0 draw
                for (int ply = 0; ply < 300; ply++)
                {
                    if (!board.HasLegalMove()) { result = board.InCheck() ? (board.SideToMove == Piece.White ? -1 : 1) : 0; why = board.InCheck() ? "mate" : "stalemate"; break; }
                    if (board.IsInsufficientMaterial() || board.RepetitionCount() >= 2 || board.HalfmoveClock >= 100) { result = 0; why = board.IsInsufficientMaterial() ? "material" : board.RepetitionCount() >= 2 ? "repetition" : "fifty"; break; }
                    bool whiteToMove = board.SideToMove == Piece.White;
                    var bot = whiteToMove == aWhite ? a : b;
                    int elo = whiteToMove == aWhite ? eloA : eloB;
                    int m = bot.ChooseMove(board, elo);
                    if (!board.IsLegal(m)) { Console.WriteLine("  illegal move from bot!"); result = 0; break; }
                    board.MakeMove(m);
                }
                double sA = result == 0 ? 0.5 : ((result == 1) == aWhite ? 1 : 0);
                lock (sync)
                {
                    scoreA += sA;
                    if (sA == 1) wins++; else if (sA == 0) losses++; else { draws++; reasons.TryGetValue(why, out int c); reasons[why] = c + 1; }
                }
            });
            double pct = scoreA / games;
            double eloDiff = pct <= 0 ? -999 : pct >= 1 ? 999 : -400 * Math.Log10(1 / pct - 1);
            foreach (var kv in reasons) Console.WriteLine($"  draw by {kv.Key}: {kv.Value}"); Console.WriteLine($"  {eloA}: +{wins} ={draws} -{losses}  score {pct * 100:0}%  (~{eloDiff:+0;-0} Elo)  {sw.ElapsedMilliseconds / 1000} s");
        }

        static void Bench()
        {
            Console.WriteLine("== Bench (fixed depth)");
            string[] fens =
            {
                Board.StartFen,
                "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10",
                "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                "r1bq1rk1/pp2bppp/2n1pn2/3p4/2PP4/2N1PN2/PP3PPP/R2QKB1R w KQ - 0 8",
            };
            long nodes = 0;
            var sw = Stopwatch.StartNew();
            foreach (var f in fens)
            {
                var s = new Searcher(32);
                var info = s.Search(new Board(f), new SearchLimits { MaxDepth = 12 });
                nodes += s.Nodes;
                Console.WriteLine($"  d{info.Depth} {Move.ToUci(info.BestMove)} score {info.Score} nodes {s.Nodes} pv {Pv(info)}");
            }
            Console.WriteLine($"  total nodes {nodes}, {sw.ElapsedMilliseconds} ms, {nodes * 1000 / Math.Max(1, sw.ElapsedMilliseconds)} nps");
        }
    }
}
