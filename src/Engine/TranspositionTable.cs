using System;

namespace Sokol.Engine
{
    public static class Bound
    {
        public const byte None = 0, Upper = 1, Lower = 2, Exact = 3;
    }

    public struct TTEntry
    {
        public ulong Key;
        public int Move;
        public short Score;
        public short StaticEval;
        public sbyte Depth;
        public byte Bound;
        public byte Age;
    }

    /// <summary>Single-slot transposition table with depth/age-based replacement.</summary>
    public sealed class TranspositionTable
    {
        readonly TTEntry[] table;
        readonly ulong mask;
        byte age;

        public TranspositionTable(int megabytes)
        {
            long bytes = (long)megabytes * 1024 * 1024;
            long count = 1;
            while (count * 2 * 24 <= bytes) count *= 2;
            table = new TTEntry[count];
            mask = (ulong)(count - 1);
        }

        public void Clear()
        {
            Array.Clear(table, 0, table.Length);
            age = 0;
        }

        public void NewSearch() => age++;

        public bool Probe(ulong key, out TTEntry entry)
        {
            entry = table[key & mask];
            return entry.Key == key && entry.Bound != Bound.None;
        }

        public void Store(ulong key, int move, int score, int staticEval, int depth, byte bound, int ply)
        {
            ref TTEntry e = ref table[key & mask];
            if (e.Key == key && move == Move.None) move = e.Move;
            if (e.Key != key || bound == Bound.Exact || depth + 2 >= e.Depth || e.Age != age)
            {
                if (score >= Score.MateBound) score += ply;
                else if (score <= -Score.MateBound) score -= ply;
                e.Key = key;
                e.Move = move;
                e.Score = (short)score;
                e.StaticEval = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, staticEval));
                e.Depth = (sbyte)Math.Max(-1, Math.Min(127, depth));
                e.Bound = bound;
                e.Age = age;
            }
        }

        public static int ScoreFromTT(int score, int ply)
        {
            if (score >= Score.MateBound) return score - ply;
            if (score <= -Score.MateBound) return score + ply;
            return score;
        }
    }
}
