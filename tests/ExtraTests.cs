using System;
using System.Diagnostics;
using Sokol.Engine;

namespace Sokol.Tests
{
    static class ExtraTests
    {
        static int fails;

        static void Check(bool cond, string what)
        {
            if (!cond) { fails++; Console.WriteLine("  FAIL: " + what); }
        }

        public static void Run(ref int failures)
        {
            fails = 0;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Book();
            San();
            Bots();
            Classification();
            failures += fails;
        }

        static void Book()
        {
            Console.WriteLine("== Opening book");
            var sw = Stopwatch.StartNew();
            int n = OpeningBook.LoadedPositions;
            Console.WriteLine($"  positions: {n}, load {sw.ElapsedMilliseconds} ms");
            foreach (var e in OpeningBook.Errors) Console.WriteLine("  " + e);
            Check(OpeningBook.Errors.Count == 0, "book has illegal moves");
            var b = new Board();
            foreach (var san in new[] { "e4", "c5", "Nf3", "d6", "d4", "cxd4", "Nxd4", "Nf6", "Nc3", "a6" })
                b.MakeMove(Notation.ParseSan(b, san));
            Check(OpeningBook.NameOf(b) == "Сицилианская защита: Вариант Найдорфа", "Najdorf name: " + OpeningBook.NameOf(b));
        }

        static void San()
        {
            Console.WriteLine("== Notation");
            var b = new Board("r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3");
            int m = b.FindMove(Sq.Parse("b1"), Sq.Parse("d2"));
            Check(m == Move.None, "d2 is occupied by a pawn, Nbd2 must be illegal");
            b = new Board("r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPP2PPP/RNBQKB1R w KQkq - 0 3");
            m = b.FindMove(Sq.Parse("b1"), Sq.Parse("d2"));
            Check(Notation.ToSan(b, m, NotationStyle.User) == "Kbd2", "user style Kbd2: " + Notation.ToSan(b, m, NotationStyle.User));
            Check(Notation.ToSan(b, m, NotationStyle.International) == "Nbd2", "international Nbd2");
            Check(Notation.ToSan(b, m, NotationStyle.Russian) == "Кbd2", "russian Кbd2");

            b = new Board("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2");
            m = b.FindMove(Sq.Parse("e1"), Sq.Parse("e2"));
            Check(Notation.ToSan(b, m, NotationStyle.User) == "K^e2", "user king K^e2: " + Notation.ToSan(b, m, NotationStyle.User));
            m = b.FindMove(Sq.Parse("d1"), Sq.Parse("h5"));
            Check(Notation.ToSan(b, m, NotationStyle.User) == "Qh5", "Qh5");
            m = b.FindMove(Sq.Parse("h2"), Sq.Parse("h3"));
            Check(Notation.ToSan(b, m, NotationStyle.User) == "h3", "pawn h3");

            b = new Board("8/P7/8/8/8/8/8/k6K w - - 0 1");
            m = b.FindMove(Sq.Parse("a7"), Sq.Parse("a8"), Piece.Queen);
            Check(Notation.ToSan(b, m, NotationStyle.User) == "a8=Q+", "promotion a8=Q+: " + Notation.ToSan(b, m, NotationStyle.User));
            m = b.FindMove(Sq.Parse("a7"), Sq.Parse("a8"), Piece.Knight);
            Check(Notation.ToSan(b, m, NotationStyle.User) == "a8=K", "knight promotion a8=K: " + Notation.ToSan(b, m, NotationStyle.User));

            b = new Board("6k1/5ppp/8/8/8/8/5PPP/R5K1 w - - 0 1");
            m = b.FindMove(Sq.Parse("a1"), Sq.Parse("a8"));
            Check(Notation.ToSan(b, m, NotationStyle.User) == "Ra8#", "mate Ra8#");

            b = new Board("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
            foreach (int x in b.LegalMoves())
            {
                string s = Notation.ToSan(b, x, NotationStyle.International);
                Check(Notation.ParseSan(b, s) == x, "SAN roundtrip " + s);
            }
            Console.WriteLine("  sample line: " + Notation.FormatLine(new Board(), new[] {
                new Board().ParseUci("e2e4") }, NotationStyle.User));
        }

        static void Bots()
        {
            Console.WriteLine("== Bot levels");
            string mid = "r1bq1rk1/pp2bppp/2n1pn2/3p4/2PP4/2N1PN2/PP3PPP/R1BQKB1R w KQ - 0 8";
            var bot = new Bot(16, 1);
            foreach (int elo in new[] { 200, 600, 1000, 1500, 2000, 2400, 2800, 3500 })
            {
                var p = StrengthProfile.ForElo(elo);
                var sw = Stopwatch.StartNew();
                var b = new Board(mid);
                int m = bot.ChooseMove(b, elo);
                Check(b.IsLegal(m), $"bot {elo} returned illegal move");
                Console.WriteLine($"  elo {elo}: depth {p.Depth} T {p.Temperature:0} blunder {p.BlunderChance:0.000} time {p.TimeMs} -> {Notation.ToSan(b, m, NotationStyle.User)} in {sw.ElapsedMilliseconds} ms");
            }
        }

        static void Classification()
        {
            Console.WriteLine("== Move classification");
            var s = new Searcher(32);
            string fen = "r1bqkbnr/pppp1ppp/2n5/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR b KQkq - 3 3";
            foreach (var uci in new[] { "g8f6", "g7g6", "d8e7", "a7a6" })
            {
                var before = new Board(fen);
                int m = before.ParseUci(uci);
                var a1 = Analyze(s, before);
                var after = before.Clone();
                after.MakeMove(m);
                var a2 = Analyze(s, after);
                var r = Review.Classify(before, m, a1, a2, MoveClass.None, NotationStyle.User);
                Console.WriteLine($"  {uci}: {r.Class} acc {r.Accuracy:0} | {r.Comment}");
                if (uci == "g8f6") Check(r.Class == MoveClass.Blunder, "Nf6?? must be a blunder");
                if (uci == "g7g6") Check(r.Class <= MoveClass.Good, "g6 must be fine");
            }
            // Mate by a "sacrifice-looking" capture must not be brilliant unless SEE says so.
            var mateB = new Board("r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR w KQkq - 4 4");
            int qx = mateB.ParseUci("h5f7");
            Console.WriteLine($"  SEE(Qxf7#) = {Searcher.StaticExchange(mateB, qx)}");
            var ma1 = Analyze(s, mateB);
            var maAfter = mateB.Clone(); maAfter.MakeMove(qx);
            var ma2 = Analyze(s, maAfter);
            var mr = Review.Classify(mateB, qx, ma1, ma2, MoveClass.Blunder, NotationStyle.User);
            Console.WriteLine($"  Qxf7#: {mr.Class} | {mr.Comment}");
            Check(mr.Class == MoveClass.Best, "Qxf7# should be Best");

            // Brilliant-ish: Greek gift
            string greek = "r1bq1rk1/pppn1ppp/4p3/3pP3/1b1P4/2NB1N2/PPP2PPP/R2QK2R w KQ - 0 1";
            var bg = new Board(greek);
            int bx = bg.ParseUci("d3h7");
            var g1 = Analyze(s, bg);
            var ga = bg.Clone(); ga.MakeMove(bx);
            var g2 = Analyze(s, ga);
            var gr = Review.Classify(bg, bx, g1, g2, MoveClass.None, NotationStyle.User);
            Console.WriteLine($"  Bxh7+: {gr.Class} | best {Move.ToUci(g1.BestMove)} {Review.FormatScore(g1.Score)} | {gr.Comment}");
        }

        static PositionAnalysis Analyze(Searcher s, Board b)
        {
            var a = new PositionAnalysis { Hash = b.Hash };
            if (!b.HasLegalMove())
            {
                a.Terminal = true;
                a.TerminalScore = b.InCheck() ? -Score.Mate : 0;
                return a;
            }
            var info = s.Search(b, new SearchLimits { TimeMs = 1000, MultiPV = 2 });
            a.Depth = info.Depth;
            a.Lines = info.Lines;
            return a;
        }
    }
}
