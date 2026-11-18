using System;

namespace Sokol.Engine
{
    /// <summary>
    /// Hand-crafted tapered evaluation: PeSTO material/piece-square tables plus pawn structure,
    /// mobility, king safety, rook files, bishop pair, endgame scaling and mop-up.
    /// </summary>
    public static class Eval
    {
        public static readonly int[] MgTable = new int[12 * 64];
        public static readonly int[] EgTable = new int[12 * 64];
        public static readonly int[] PhaseInc = { 0, 1, 1, 2, 4, 0 };
        /// <summary>Simple piece values used for SEE / move ordering.</summary>
        public static readonly int[] SeeValue = { 100, 320, 330, 500, 950, 20000 };

        static readonly int[] MgValue = { 82, 337, 365, 477, 1025, 0 };
        static readonly int[] EgValue = { 94, 281, 297, 512, 936, 0 };

        // Tables are written from White's point of view with a8 first (as published for PeSTO).
        static readonly int[][] MgPst =
        {
            new[] {
                  0,   0,   0,   0,   0,   0,  0,   0,
                 98, 134,  61,  95,  68, 126, 34, -11,
                 -6,   7,  26,  31,  65,  56, 25, -20,
                -14,  13,   6,  21,  23,  12, 17, -23,
                -27,  -2,  -5,  12,  17,   6, 10, -25,
                -26,  -4,  -4, -10,   3,   3, 33, -12,
                -35,  -1, -20, -23, -15,  24, 38, -22,
                  0,   0,   0,   0,   0,   0,  0,   0 },
            new[] {
                -167, -89, -34, -49,  61, -97, -15, -107,
                 -73, -41,  72,  36,  23,  62,   7,  -17,
                 -47,  60,  37,  65,  84, 129,  73,   44,
                  -9,  17,  19,  53,  37,  69,  18,   22,
                 -13,   4,  16,  13,  28,  19,  21,   -8,
                 -23,  -9,  12,  10,  19,  17,  25,  -16,
                 -29, -53, -12,  -3,  -1,  18, -14,  -19,
                -105, -21, -58, -33, -17, -28, -19,  -23 },
            new[] {
                -29,   4, -82, -37, -25, -42,   7,  -8,
                -26,  16, -18, -13,  30,  59,  18, -47,
                -16,  37,  43,  40,  35,  50,  37,  -2,
                 -4,   5,  19,  50,  37,  37,   7,  -2,
                 -6,  13,  13,  26,  34,  12,  10,   4,
                  0,  15,  15,  15,  14,  27,  18,  10,
                  4,  15,  16,   0,   7,  21,  33,   1,
                -33,  -3, -14, -21, -13, -12, -39, -21 },
            new[] {
                 32,  42,  32,  51, 63,  9,  31,  43,
                 27,  32,  58,  62, 80, 67,  26,  44,
                 -5,  19,  26,  36, 17, 45,  61,  16,
                -24, -11,   7,  26, 24, 35,  -8, -20,
                -36, -26, -12,  -1,  9, -7,   6, -23,
                -45, -25, -16, -17,  3,  0,  -5, -33,
                -44, -16, -20,  -9, -1, 11,  -6, -71,
                -19, -13,   1,  17, 16,  7, -37, -26 },
            new[] {
                -28,   0,  29,  12,  59,  44,  43,  45,
                -24, -39,  -5,   1, -16,  57,  28,  54,
                -13, -17,   7,   8,  29,  56,  47,  57,
                -27, -27, -16, -16,  -1,  17,  -2,   1,
                 -9, -26,  -9, -10,  -2,  -4,   3,  -3,
                -14,   2, -11,  -2,  -5,   2,  14,   5,
                -35,  -8,  11,   2,   8,  15,  -3,   1,
                 -1, -18,  -9,  10, -15, -25, -31, -50 },
            new[] {
                -65,  23,  16, -15, -56, -34,   2,  13,
                 29,  -1, -20,  -7,  -8,  -4, -38, -29,
                 -9,  24,   2, -16, -20,   6,  22, -22,
                -17, -20, -12, -27, -30, -25, -14, -36,
                -49,  -1, -27, -39, -46, -44, -33, -51,
                -14, -14, -22, -46, -44, -30, -15, -27,
                  1,   7,  -8, -64, -43, -16,   9,   8,
                -15,  36,  12, -54,   8, -28,  24,  14 },
        };

        static readonly int[][] EgPst =
        {
            new[] {
                  0,   0,   0,   0,   0,   0,   0,   0,
                178, 173, 158, 134, 147, 132, 165, 187,
                 94, 100,  85,  67,  56,  53,  82,  84,
                 32,  24,  13,   5,  -2,   4,  17,  17,
                 13,   9,  -3,  -7,  -7,  -8,   3,  -1,
                  4,   7,  -6,   1,   0,  -5,  -1,  -8,
                 13,   8,   8,  10,  13,   0,   2,  -7,
                  0,   0,   0,   0,   0,   0,   0,   0 },
            new[] {
                -58, -38, -13, -28, -31, -27, -63, -99,
                -25,  -8, -25,  -2,  -9, -25, -24, -52,
                -24, -20,  10,   9,  -1,  -9, -19, -41,
                -17,   3,  22,  22,  22,  11,   8, -18,
                -18,  -6,  16,  25,  16,  17,   4, -18,
                -23,  -3,  -1,  15,  10,  -3, -20, -22,
                -42, -20, -10,  -5,  -2, -20, -23, -44,
                -29, -51, -23, -15, -22, -18, -50, -64 },
            new[] {
                -14, -21, -11,  -8, -7,  -9, -17, -24,
                 -8,  -4,   7, -12, -3, -13,  -4, -14,
                  2,  -8,   0,  -1, -2,   6,   0,   4,
                 -3,   9,  12,   9, 14,  10,   3,   2,
                 -6,   3,  13,  19,  7,  10,  -3,  -9,
                -12,  -3,   8,  10, 13,   3,  -7, -15,
                -14, -18,  -7,  -1,  4,  -9, -15, -27,
                -23,  -9, -23,  -5, -9, -16,  -5, -17 },
            new[] {
                13, 10, 18, 15, 12,  12,   8,   5,
                11, 13, 13, 11, -3,   3,   8,   3,
                 7,  7,  7,  5,  4,  -3,  -5,  -3,
                 4,  3, 13,  1,  2,   1,  -1,   2,
                 3,  5,  8,  4, -5,  -6,  -8, -11,
                -4,  0, -5, -1, -7, -12,  -8, -16,
                -6, -6,  0,  2, -9,  -9, -11,  -3,
                -9,  2,  3, -1, -5, -13,   4, -20 },
            new[] {
                 -9,  22,  22,  27,  27,  19,  10,  20,
                -17,  20,  32,  41,  58,  25,  30,   0,
                -20,   6,   9,  49,  47,  35,  19,   9,
                  3,  22,  24,  45,  57,  40,  57,  36,
                -18,  28,  19,  47,  31,  34,  39,  23,
                -16, -27,  15,   6,   9,  17,  10,   5,
                -22, -23, -30, -16, -16, -23, -36, -32,
                -33, -28, -22, -43,  -5, -32, -20, -41 },
            new[] {
                -74, -35, -18, -18, -11,  15,   4, -17,
                -12,  17,  14,  17,  17,  38,  23,  11,
                 10,  17,  23,  15,  20,  45,  44,  13,
                 -8,  22,  24,  27,  26,  33,  26,   3,
                -18,  -4,  21,  24,  27,  23,   9, -11,
                -19,  -3,  11,  21,  23,  16,   7,  -9,
                -27, -11,   4,  13,  14,   4,  -5, -17,
                -53, -34, -21, -11, -28, -14, -24, -43 },
        };

        static readonly int[] PassedMg = { 0, 0, 5, 10, 18, 30, 50, 0 };
        static readonly int[] PassedEg = { 0, 5, 10, 20, 35, 60, 100, 0 };

        static Eval()
        {
            for (int p = 0; p < 6; p++)
                for (int sq = 0; sq < 64; sq++)
                {
                    MgTable[p * 64 + sq] = MgValue[p] + MgPst[p][sq ^ 56];
                    EgTable[p * 64 + sq] = EgValue[p] + EgPst[p][sq ^ 56];
                    MgTable[(p + 6) * 64 + sq] = MgValue[p] + MgPst[p][sq];
                    EgTable[(p + 6) * 64 + sq] = EgValue[p] + EgPst[p][sq];
                }
        }

        /// <summary>Static evaluation from the side to move's point of view (centipawns).</summary>
        public static int Evaluate(Board b)
        {
            int mg = b.PsqMg[0] - b.PsqMg[1];
            int eg = b.PsqEg[0] - b.PsqEg[1];

            ulong wPawns = b.Pieces[0], bPawns = b.Pieces[6];
            ulong wPawnAtt = ((wPawns & ~BB.FileA) << 7) | ((wPawns & ~BB.FileH) << 9);
            ulong bPawnAtt = ((bPawns & ~BB.FileA) >> 9) | ((bPawns & ~BB.FileH) >> 7);

            EvalSide(b, Piece.White, wPawns, bPawns, bPawnAtt, ref mg, ref eg, 1);
            EvalSide(b, Piece.Black, bPawns, wPawns, wPawnAtt, ref mg, ref eg, -1);

            int phase = Math.Min(b.Phase, 24);
            int score = (mg * phase + eg * (24 - phase)) / 24;

            score = ScaleEndgame(b, score);
            score += b.SideToMove == Piece.White ? 10 : -10; // tempo
            return b.SideToMove == Piece.White ? score : -score;
        }

        static void EvalSide(Board b, int us, ulong ourPawns, ulong theirPawns, ulong theirPawnAtt,
                             ref int mg, ref int eg, int sign)
        {
            int them = us ^ 1;
            int o = us * 6;
            ulong occ = b.Occupied;
            ulong own = b.Colors[us];
            int ourKing = b.KingSquare(us), theirKing = b.KingSquare(them);
            int m = 0, e = 0;

            // ---------------- pawns
            ulong p = ourPawns;
            while (p != 0)
            {
                int sq = BB.PopLsb(ref p);
                int f = sq & 7;
                int relRank = us == Piece.White ? (sq >> 3) : 7 - (sq >> 3);
                if ((BB.AdjacentFiles[f] & ourPawns) == 0) { m -= 10; e -= 14; }
                if ((BB.ForwardFile[us * 64 + sq] & ourPawns) != 0) { m -= 8; e -= 18; }
                if ((BB.PassedMask[us * 64 + sq] & theirPawns) == 0 && (BB.ForwardFile[us * 64 + sq] & ourPawns) == 0)
                {
                    m += PassedMg[relRank];
                    e += PassedEg[relRank];
                    int front = us == Piece.White ? sq + 8 : sq - 8;
                    if (front >= 0 && front < 64)
                    {
                        int w = Math.Max(0, relRank - 2);
                        e += (Sq.Distance(theirKing, front) * 4 - Sq.Distance(ourKing, front) * 2) * w;
                        if (b.Squares[front] != Piece.None) e -= 4 * w;
                    }
                }
            }

            // ---------------- pieces
            ulong mobilityArea = ~own & ~theirPawnAtt;
            ulong kingZone = BB.KingAttacks[theirKing] | (1UL << theirKing);
            int attackers = 0, attackWeight = 0;

            ulong pcs = b.Pieces[o + Piece.Knight];
            while (pcs != 0)
            {
                int sq = BB.PopLsb(ref pcs);
                ulong att = BB.KnightAttacks[sq];
                int mob = BB.PopCount(att & mobilityArea);
                m += 4 * (mob - 4); e += 4 * (mob - 4);
                ulong z = att & kingZone;
                if (z != 0) { attackers++; attackWeight += 2 * BB.PopCount(z); }
            }

            pcs = b.Pieces[o + Piece.Bishop];
            if (BB.PopCount(pcs) >= 2) { m += 25; e += 45; }
            while (pcs != 0)
            {
                int sq = BB.PopLsb(ref pcs);
                ulong att = BB.BishopAttacks(sq, occ);
                int mob = BB.PopCount(att & mobilityArea);
                m += 5 * (mob - 6); e += 5 * (mob - 6);
                ulong z = att & kingZone;
                if (z != 0) { attackers++; attackWeight += 2 * BB.PopCount(z); }
            }

            pcs = b.Pieces[o + Piece.Rook];
            while (pcs != 0)
            {
                int sq = BB.PopLsb(ref pcs);
                ulong att = BB.RookAttacks(sq, occ);
                int mob = BB.PopCount(att & mobilityArea);
                m += 2 * (mob - 7); e += 4 * (mob - 7);
                ulong file = BB.Files[sq & 7];
                if ((file & ourPawns) == 0)
                {
                    if ((file & theirPawns) == 0) { m += 25; e += 10; }
                    else { m += 12; e += 6; }
                }
                ulong z = att & kingZone;
                if (z != 0) { attackers++; attackWeight += 3 * BB.PopCount(z); }
            }

            pcs = b.Pieces[o + Piece.Queen];
            bool hasQueen = pcs != 0;
            while (pcs != 0)
            {
                int sq = BB.PopLsb(ref pcs);
                ulong att = BB.QueenAttacks(sq, occ);
                int mob = BB.PopCount(att & mobilityArea);
                m += 1 * (mob - 13); e += 2 * (mob - 13);
                ulong z = att & kingZone;
                if (z != 0) { attackers++; attackWeight += 5 * BB.PopCount(z); }
            }

            // ---------------- attack on the enemy king (middlegame only)
            if (hasQueen && attackers >= 2)
            {
                int w = Math.Min(attackWeight, 40);
                m += w * w / 4;
            }

            // ---------------- own king shelter
            int kf = ourKing & 7;
            int kRel = us == Piece.White ? (ourKing >> 3) : 7 - (ourKing >> 3);
            if (kRel <= 1)
            {
                int shelter = 0;
                for (int f = Math.Max(0, kf - 1); f <= Math.Min(7, kf + 1); f++)
                {
                    ulong filePawns = ourPawns & BB.Files[f];
                    if (filePawns == 0) { shelter -= 18; continue; }
                    int psq = us == Piece.White ? BB.Lsb(filePawns) : 63 - LeadingIndex(filePawns);
                    int dist = Math.Abs((psq >> 3) - (ourKing >> 3));
                    if (dist == 1) shelter += 12;
                    else if (dist == 2) shelter += 5;
                }
                m += shelter;
            }

            mg += sign * m;
            eg += sign * e;
        }

        /// <summary>63 - index of the highest set bit (b != 0).</summary>
        static int LeadingIndex(ulong b)
        {
            int msb = 63;
            while ((b & (1UL << msb)) == 0) msb--;
            return 63 - msb;
        }

        static int ScaleEndgame(Board b, int score)
        {
            int strong = score > 0 ? Piece.White : Piece.Black;
            int weak = strong ^ 1;
            int so = strong * 6, wo = weak * 6;

            ulong strongPawns = b.Pieces[so];
            int strongMat = 3 * BB.PopCount(b.Pieces[so + 1] | b.Pieces[so + 2]) + 5 * BB.PopCount(b.Pieces[so + 3]) + 9 * BB.PopCount(b.Pieces[so + 4]);
            int weakMat = 3 * BB.PopCount(b.Pieces[wo + 1] | b.Pieces[wo + 2]) + 5 * BB.PopCount(b.Pieces[wo + 3]) + 9 * BB.PopCount(b.Pieces[wo + 4]);

            if (strongPawns == 0)
            {
                // Cannot win without enough extra material.
                if (strongMat - weakMat <= 3)
                    return score / 8;
                // Two knights cannot force mate.
                if (weakMat == 0 && b.Pieces[wo] == 0 && strongMat == 6 && BB.PopCount(b.Pieces[so + 1]) == 2)
                    return score / 16;
            }

            // Opposite-colored bishops with nothing else.
            if (strongMat == 3 && weakMat == 3)
            {
                ulong sb = b.Pieces[so + 2], wb = b.Pieces[wo + 2];
                if (sb != 0 && wb != 0 && BB.PopCount(sb) == 1 && BB.PopCount(wb) == 1 &&
                    (((sb & BB.LightSquares) != 0) != ((wb & BB.LightSquares) != 0)))
                    score /= 2;
            }

            // Mop-up: drive a lone king to the edge when we have a decisive material advantage.
            if (b.Pieces[wo] == 0 && weakMat <= 3 && strongMat - weakMat >= 5)
            {
                int wk = b.KingSquare(weak), sk = b.KingSquare(strong);
                int wf = wk & 7, wr = wk >> 3;
                int centerDist = Math.Max(3 - wf, wf - 4) + Math.Max(3 - wr, wr - 4);
                int kingsDist = Math.Abs((sk & 7) - wf) + Math.Abs((sk >> 3) - wr);
                int bonus = 12 * centerDist + 6 * (14 - kingsDist) + 200;
                score += strong == Piece.White ? bonus : -bonus;
            }
            return score;
        }
    }
}
