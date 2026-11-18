using System.Collections.Generic;

namespace Sokol.Engine
{
    public sealed partial class Board
    {
        /// <summary>
        /// Generates pseudo-legal moves into list (starting at index 0) and returns the count.
        /// noisyOnly: captures and queen promotions only (for quiescence search).
        /// </summary>
        public int GenerateMoves(int[] list, bool noisyOnly)
        {
            int n = 0;
            int us = SideToMove, them = us ^ 1;
            ulong own = Colors[us], enemy = Colors[them], occ = Occupied, empty = ~occ;
            int o = us * 6;

            // ---------------- pawns
            ulong pawns = Pieces[o];
            ulong promoRank = us == Piece.White ? BB.Rank8 : BB.Rank1;
            ulong push1, push2, capL, capR;
            int up, upL, upR;
            if (us == Piece.White)
            {
                push1 = (pawns << 8) & empty;
                push2 = ((push1 & BB.Rank3) << 8) & empty;
                capL = ((pawns & ~BB.FileA) << 7) & enemy;
                capR = ((pawns & ~BB.FileH) << 9) & enemy;
                up = 8; upL = 7; upR = 9;
            }
            else
            {
                push1 = (pawns >> 8) & empty;
                push2 = ((push1 & BB.Rank6) >> 8) & empty;
                capL = ((pawns & ~BB.FileA) >> 9) & enemy;
                capR = ((pawns & ~BB.FileH) >> 7) & enemy;
                up = -8; upL = -9; upR = -7;
            }

            ulong b = push1 & promoRank;
            while (b != 0)
            {
                int to = BB.PopLsb(ref b);
                n = AddPromotions(list, n, to - up, to, false, noisyOnly);
            }
            b = capL & promoRank;
            while (b != 0)
            {
                int to = BB.PopLsb(ref b);
                n = AddPromotions(list, n, to - upL, to, true, noisyOnly);
            }
            b = capR & promoRank;
            while (b != 0)
            {
                int to = BB.PopLsb(ref b);
                n = AddPromotions(list, n, to - upR, to, true, noisyOnly);
            }
            b = capL & ~promoRank;
            while (b != 0)
            {
                int to = BB.PopLsb(ref b);
                list[n++] = Move.Create(to - upL, to, Move.Capture);
            }
            b = capR & ~promoRank;
            while (b != 0)
            {
                int to = BB.PopLsb(ref b);
                list[n++] = Move.Create(to - upR, to, Move.Capture);
            }
            if (EpSquare >= 0)
            {
                b = BB.PawnAttacks[them * 64 + EpSquare] & pawns;
                while (b != 0)
                {
                    int from = BB.PopLsb(ref b);
                    list[n++] = Move.Create(from, EpSquare, Move.EpCapture);
                }
            }
            if (!noisyOnly)
            {
                b = push1 & ~promoRank;
                while (b != 0)
                {
                    int to = BB.PopLsb(ref b);
                    list[n++] = Move.Create(to - up, to, Move.Quiet);
                }
                b = push2;
                while (b != 0)
                {
                    int to = BB.PopLsb(ref b);
                    list[n++] = Move.Create(to - 2 * up, to, Move.DoublePush);
                }
            }

            ulong targets = noisyOnly ? enemy : ~own;

            // ---------------- knights
            b = Pieces[o + Piece.Knight];
            while (b != 0)
            {
                int from = BB.PopLsb(ref b);
                n = AddPieceMoves(list, n, from, BB.KnightAttacks[from] & targets, enemy);
            }
            // ---------------- bishops
            b = Pieces[o + Piece.Bishop];
            while (b != 0)
            {
                int from = BB.PopLsb(ref b);
                n = AddPieceMoves(list, n, from, BB.BishopAttacks(from, occ) & targets, enemy);
            }
            // ---------------- rooks
            b = Pieces[o + Piece.Rook];
            while (b != 0)
            {
                int from = BB.PopLsb(ref b);
                n = AddPieceMoves(list, n, from, BB.RookAttacks(from, occ) & targets, enemy);
            }
            // ---------------- queens
            b = Pieces[o + Piece.Queen];
            while (b != 0)
            {
                int from = BB.PopLsb(ref b);
                n = AddPieceMoves(list, n, from, BB.QueenAttacks(from, occ) & targets, enemy);
            }
            // ---------------- king
            int ksq = BB.Lsb(Pieces[o + Piece.King]);
            n = AddPieceMoves(list, n, ksq, BB.KingAttacks[ksq] & targets, enemy);

            // ---------------- castling
            if (!noisyOnly && CastleRights != 0)
            {
                if (us == Piece.White)
                {
                    if ((CastleRights & 1) != 0 && (occ & 0x60UL) == 0 &&
                        !IsSquareAttacked(4, them) && !IsSquareAttacked(5, them) && !IsSquareAttacked(6, them))
                        list[n++] = Move.Create(4, 6, Move.KingCastle);
                    if ((CastleRights & 2) != 0 && (occ & 0x0EUL) == 0 &&
                        !IsSquareAttacked(4, them) && !IsSquareAttacked(3, them) && !IsSquareAttacked(2, them))
                        list[n++] = Move.Create(4, 2, Move.QueenCastle);
                }
                else
                {
                    if ((CastleRights & 4) != 0 && (occ & (0x60UL << 56)) == 0 &&
                        !IsSquareAttacked(60, them) && !IsSquareAttacked(61, them) && !IsSquareAttacked(62, them))
                        list[n++] = Move.Create(60, 62, Move.KingCastle);
                    if ((CastleRights & 8) != 0 && (occ & (0x0EUL << 56)) == 0 &&
                        !IsSquareAttacked(60, them) && !IsSquareAttacked(59, them) && !IsSquareAttacked(58, them))
                        list[n++] = Move.Create(60, 58, Move.QueenCastle);
                }
            }
            return n;
        }

        static int AddPromotions(int[] list, int n, int from, int to, bool capture, bool queenOnly)
        {
            int baseFlag = capture ? Move.PromoCapN : Move.PromoN;
            list[n++] = Move.Create(from, to, baseFlag + 3); // queen first
            if (!queenOnly)
            {
                list[n++] = Move.Create(from, to, baseFlag);     // knight
                list[n++] = Move.Create(from, to, baseFlag + 2); // rook
                list[n++] = Move.Create(from, to, baseFlag + 1); // bishop
            }
            return n;
        }

        static int AddPieceMoves(int[] list, int n, int from, ulong targets, ulong enemy)
        {
            while (targets != 0)
            {
                int to = BB.PopLsb(ref targets);
                list[n++] = Move.Create(from, to, (enemy & (1UL << to)) != 0 ? Move.Capture : Move.Quiet);
            }
            return n;
        }

        /// <summary>All legal moves in the current position.</summary>
        public List<int> LegalMoves()
        {
            var buf = new int[256];
            int n = GenerateMoves(buf, false);
            var res = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                if (MakeMove(buf[i]))
                {
                    UnmakeMove();
                    res.Add(buf[i]);
                }
            }
            return res;
        }

        public bool HasLegalMove()
        {
            var buf = new int[256];
            int n = GenerateMoves(buf, false);
            for (int i = 0; i < n; i++)
            {
                if (MakeMove(buf[i]))
                {
                    UnmakeMove();
                    return true;
                }
            }
            return false;
        }

        public bool IsLegal(int move)
        {
            foreach (int m in LegalMoves()) if (m == move) return true;
            return false;
        }

        /// <summary>Finds the legal move with the given from/to (and promotion type, if any).</summary>
        public int FindMove(int from, int to, int promotionType = Piece.Queen)
        {
            foreach (int m in LegalMoves())
            {
                if (Move.From(m) != from || Move.To(m) != to) continue;
                if (Move.IsPromotion(m) && Move.PromotionType(m) != promotionType) continue;
                return m;
            }
            return Move.None;
        }

        public int ParseUci(string uci)
        {
            if (string.IsNullOrEmpty(uci) || uci.Length < 4) return Move.None;
            int from = Sq.Parse(uci.Substring(0, 2)), to = Sq.Parse(uci.Substring(2, 2));
            int promo = Piece.Queen;
            if (uci.Length > 4) promo = "pnbrqk".IndexOf(char.ToLowerInvariant(uci[4]));
            return FindMove(from, to, promo);
        }

        public long Perft(int depth)
        {
            if (depth == 0) return 1;
            var buf = new int[256];
            int n = GenerateMoves(buf, false);
            long total = 0;
            for (int i = 0; i < n; i++)
            {
                if (!MakeMove(buf[i])) continue;
                total += depth == 1 ? 1 : Perft(depth - 1);
                UnmakeMove();
            }
            return total;
        }
    }
}
