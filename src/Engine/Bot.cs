using System;
using System.Collections.Generic;

namespace Sokol.Engine
{
    /// <summary>Playing strength parameters derived from the requested Elo (200..3500).</summary>
    public sealed class StrengthProfile
    {
        public int Elo;
        public int Depth;
        public int TimeMs;
        /// <summary>Weak levels: score every legal move and pick one "humanly".</summary>
        public bool ScoreAll;
        /// <summary>Softmax temperature in centipawns (0 = always the best move).</summary>
        public double Temperature;
        /// <summary>Probability of a random (often bad) move.</summary>
        public double BlunderChance;
        /// <summary>How many plies the bot follows the opening book.</summary>
        public int BookPlies;

        // elo, depth, temperature, blunder, bookPlies, timeMs
        static readonly double[,] Anchors =
        {
            {  200,  1, 260, 0.30,  2,    0 },
            {  400,  1, 190, 0.20,  4,    0 },
            {  600,  2, 140, 0.13,  4,    0 },
            {  800,  2, 100, 0.08,  6,    0 },
            { 1000,  3,  75, 0.05,  6,    0 },
            { 1200,  3,  55, 0.03,  8,    0 },
            { 1400,  4,  40, 0.015, 10,   0 },
            { 1600,  5,  28, 0.007, 12,   0 },
            { 1800,  6,  20, 0.003, 14,   0 },
            { 2000,  7,  14, 0,    16,    0 },
            { 2200,  9,   9, 0,    20,    0 },
            { 2400, 11,   6, 0,    24,    0 },
            { 2600, 13,   3, 0,    30,    0 },
            { 2800, 64,   0, 0,    40,  500 },
            { 3000, 64,   0, 0,    40, 1000 },
            { 3200, 64,   0, 0,    40, 2000 },
            { 3500, 64,   0, 0,    40, 3500 },
        };

        public static StrengthProfile ForElo(int elo)
        {
            elo = Math.Max(200, Math.Min(3500, elo));
            int n = Anchors.GetLength(0);
            int i = 0;
            while (i < n - 2 && elo > Anchors[i + 1, 0]) i++;
            double t = (elo - Anchors[i, 0]) / (Anchors[i + 1, 0] - Anchors[i, 0]);
            t = Math.Max(0, Math.Min(1, t));
            double Lerp(int col) => Anchors[i, col] + (Anchors[i + 1, col] - Anchors[i, col]) * t;

            var p = new StrengthProfile { Elo = elo };
            p.Temperature = Lerp(2);
            p.BlunderChance = Lerp(3);
            p.BookPlies = (int)Math.Round(Lerp(4));
            if (elo >= 2800)
            {
                p.Depth = 64;
                p.TimeMs = (int)Math.Round(Lerp(5));
                p.Temperature = 0;
            }
            else
            {
                p.Depth = (int)Math.Round(Lerp(1));
                p.TimeMs = 0;
            }
            p.ScoreAll = elo <= 2000;
            return p;
        }
    }

    /// <summary>The playing bot: opening book + search + human-like move choice for lower ratings.</summary>
    public sealed class Bot
    {
        readonly Searcher searcher;
        readonly Random rng;

        public Bot(int hashMb = 32, int seed = -1)
        {
            searcher = new Searcher(hashMb);
            rng = seed >= 0 ? new Random(seed) : new Random();
        }

        public void NewGame() => searcher.Clear();
        public void Stop() => searcher.Stop();

        public int ChooseMove(Board position, int elo)
        {
            var p = StrengthProfile.ForElo(elo);
            var legal = position.LegalMoves();
            if (legal.Count == 0) return Move.None;
            if (legal.Count == 1) return legal[0];

            int ply = (position.FullmoveNumber - 1) * 2 + position.SideToMove;
            if (ply < p.BookPlies)
            {
                double useBook = elo < 800 ? 0.6 : 0.95;
                if (rng.NextDouble() < useBook)
                {
                    int bm = OpeningBook.Pick(position, rng);
                    if (bm != Move.None) return bm;
                }
            }

            if (p.ScoreAll)
            {
                // Safety cap so even complicated positions do not take forever.
                var scored = searcher.ScoreAllMoves(position, p.Depth, 4000);
                if (scored.Count == 0) return legal[0];
                // Converting a won position: play more carefully, and never miss a found mate (except beginners).
                int bestScore = scored[0].Value;
                double temperature = p.Temperature, blunder = p.BlunderChance;
                if (Score.IsMate(bestScore) && bestScore > 0)
                {
                    if (elo >= 700) return scored[0].Key;
                    temperature *= 0.3;
                    blunder *= 0.3;
                }
                else if (bestScore >= 300)
                {
                    double k = bestScore >= 800 ? 0.2 : 0.45;
                    temperature *= k;
                    blunder *= k;
                }
                if (rng.NextDouble() < blunder)
                {
                    var candidates = new List<int>();
                    foreach (int m in legal)
                        if (Piece.TypeOf(position.Squares[Move.From(m)]) != Piece.King) candidates.Add(m);
                    if (candidates.Count == 0) candidates = legal;
                    return candidates[rng.Next(candidates.Count)];
                }
                return Softmax(scored, temperature);
            }

            if (p.Temperature > 0)
            {
                var info = searcher.Search(position, new SearchLimits { MaxDepth = p.Depth, MultiPV = 4, TimeMs = 5000 });
                var list = new List<KeyValuePair<int, int>>();
                foreach (var line in info.Lines) list.Add(new KeyValuePair<int, int>(line.FirstMove, line.Score));
                return list.Count > 0 ? Softmax(list, p.Temperature) : legal[0];
            }

            var best = searcher.Search(position, new SearchLimits { MaxDepth = p.Depth, TimeMs = p.TimeMs });
            return best.BestMove != Move.None ? best.BestMove : legal[0];
        }

        int Softmax(List<KeyValuePair<int, int>> scored, double temperature)
        {
            if (temperature <= 0) return scored[0].Key;
            int best = int.MinValue;
            foreach (var kv in scored) best = Math.Max(best, Clamp(kv.Value));
            double total = 0;
            var w = new double[scored.Count];
            for (int i = 0; i < scored.Count; i++)
            {
                w[i] = Math.Exp(-(best - Clamp(scored[i].Value)) / temperature);
                total += w[i];
            }
            double r = rng.NextDouble() * total;
            for (int i = 0; i < scored.Count; i++)
            {
                r -= w[i];
                if (r <= 0) return scored[i].Key;
            }
            return scored[0].Key;
        }

        static int Clamp(int s) => Math.Max(-1500, Math.Min(1500, s));
    }
}
