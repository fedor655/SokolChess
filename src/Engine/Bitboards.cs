using System;

namespace Sokol.Engine
{
    /// <summary>Bitboard utilities and precomputed attack tables (magic bitboards for sliders).</summary>
    public static class BB
    {
        public const ulong FileA = 0x0101010101010101UL;
        public const ulong FileH = FileA << 7;
        public const ulong Rank1 = 0xFFUL;
        public const ulong Rank2 = Rank1 << 8;
        public const ulong Rank3 = Rank1 << 16;
        public const ulong Rank6 = Rank1 << 40;
        public const ulong Rank7 = Rank1 << 48;
        public const ulong Rank8 = Rank1 << 56;
        public const ulong LightSquares = 0x55AA55AA55AA55AAUL;
        public const ulong DarkSquares = ~LightSquares;

        public static readonly ulong[] KnightAttacks = new ulong[64];
        public static readonly ulong[] KingAttacks = new ulong[64];
        /// <summary>[color * 64 + sq]: squares attacked by a pawn of that color standing on sq.</summary>
        public static readonly ulong[] PawnAttacks = new ulong[128];

        public static readonly ulong[] Files = new ulong[8];
        public static readonly ulong[] Ranks = new ulong[8];
        public static readonly ulong[] AdjacentFiles = new ulong[8];
        /// <summary>[color * 64 + sq]: squares in front of sq on its file and adjacent files (passed pawn mask).</summary>
        public static readonly ulong[] PassedMask = new ulong[128];
        /// <summary>[color * 64 + sq]: squares in front of sq on the same file.</summary>
        public static readonly ulong[] ForwardFile = new ulong[128];

        static readonly ulong[] RookMask = new ulong[64], BishopMask = new ulong[64];
        static readonly ulong[] RookMagic = new ulong[64], BishopMagic = new ulong[64];
        static readonly int[] RookShift = new int[64], BishopShift = new int[64];
        static readonly int[] RookOffset = new int[64], BishopOffset = new int[64];
        static readonly ulong[] RookTable = new ulong[102400];
        static readonly ulong[] BishopTable = new ulong[5248];

        static readonly int[] DeBruijnIndex =
        {
             0, 47,  1, 56, 48, 27,  2, 60, 57, 49, 41, 37, 28, 16,  3, 61,
            54, 58, 35, 52, 50, 42, 21, 44, 38, 32, 29, 23, 17, 11,  4, 62,
            46, 55, 26, 59, 40, 36, 15, 53, 34, 51, 20, 43, 31, 22, 10, 45,
            25, 39, 14, 33, 19, 30,  9, 24, 13, 18,  8, 12,  7,  6,  5, 63
        };
        const ulong DeBruijn64 = 0x03f79d71b4cb0a89UL;

        public static int Lsb(ulong b) => DeBruijnIndex[((b ^ (b - 1)) * DeBruijn64) >> 58];

        public static int PopLsb(ref ulong b)
        {
            int i = DeBruijnIndex[((b ^ (b - 1)) * DeBruijn64) >> 58];
            b &= b - 1;
            return i;
        }

        public static int PopCount(ulong x)
        {
            x -= (x >> 1) & 0x5555555555555555UL;
            x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL);
            x = (x + (x >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((x * 0x0101010101010101UL) >> 56);
        }

        public static ulong Bit(int sq) => 1UL << sq;

        public static ulong RookAttacks(int sq, ulong occ) =>
            RookTable[RookOffset[sq] + (int)(((occ & RookMask[sq]) * RookMagic[sq]) >> RookShift[sq])];

        public static ulong BishopAttacks(int sq, ulong occ) =>
            BishopTable[BishopOffset[sq] + (int)(((occ & BishopMask[sq]) * BishopMagic[sq]) >> BishopShift[sq])];

        public static ulong QueenAttacks(int sq, ulong occ) => RookAttacks(sq, occ) | BishopAttacks(sq, occ);

        static readonly ulong[] KnownRookMagics = { 0x1080004008801020UL, 0x0840092002C03000UL, 0x1900200010400900UL, 0x0880100008000480UL, 0x4200100420080200UL, 0x8100020100080400UL, 0x0200040110886200UL, 0x0200008040220411UL, 0x0404800084400220UL, 0x0000401000402000UL, 0x0086001081220440UL, 0x0408800800100280UL, 0x000A001201040820UL, 0x8848800200840080UL, 0x4001000100040200UL, 0x0442000102105084UL, 0x9080010020804100UL, 0x0040404000201009UL, 0x0000808010002009UL, 0x2200090021D00100UL, 0x0008008008040080UL, 0x0004004002010040UL, 0x0011040008015042UL, 0x00000A0001768104UL, 0x0000800080204009UL, 0x2010004140002001UL, 0x9800200280100080UL, 0x1000100080080080UL, 0x0442000A00049020UL, 0x2100040080020080UL, 0x0800120400900148UL, 0x0010040A00128541UL, 0x2800804000800030UL, 0x1010002000400041UL, 0x4000200011004100UL, 0x0610008410800800UL, 0x0400802402800800UL, 0xC100020080800400UL, 0x0002000802000401UL, 0x0182085882000401UL, 0x0220204000808000UL, 0x2860100040024022UL, 0x0001002004110040UL, 0x99101042000A0020UL, 0x0004080004008080UL, 0x0010040002008080UL, 0x2012004881020004UL, 0x8300842444820011UL, 0x0088403882010200UL, 0x0820400080210100UL, 0x0110910040A00300UL, 0x0801100280080480UL, 0x0242009008200600UL, 0x1002000489500200UL, 0x0040800200010080UL, 0x0091800041000080UL, 0x0000209300488001UL, 0x04C1002414824001UL, 0x020020000B001041UL, 0x7000100004200901UL, 0x8002002004100802UL, 0x30010002084C0007UL, 0x0888221800813004UL, 0x4000002840840112UL };
        static readonly ulong[] KnownBishopMagics = { 0x2048017020910100UL, 0x0044410424008008UL, 0x040828A400900000UL, 0x8002209200022000UL, 0x0002021000540002UL, 0x0021018840000000UL, 0x00009E8420204002UL, 0x00A0920110084480UL, 0x4003062018010110UL, 0x0221046812004E09UL, 0x01E11002958912A0UL, 0x0000044410804000UL, 0x0000821210000080UL, 0x080201102210A800UL, 0x0080040411045004UL, 0x00704A1842021000UL, 0x1005061070322800UL, 0x0018001010410444UL, 0x0010000800401420UL, 0x2204002844000800UL, 0x2052020412022280UL, 0x000A020101008208UL, 0x0040400201042000UL, 0x03E1082040480410UL, 0x1004200004208414UL, 0x08700400984808C8UL, 0x0088080004004410UL, 0x008C0240140100A2UL, 0x0008840001822000UL, 0x0050088001080100UL, 0x98140840040A2200UL, 0x3002020900210110UL, 0x1004040640206000UL, 0x1090909000840400UL, 0x9002444810100020UL, 0x4000020080080080UL, 0x0028020400011010UL, 0x0290808300020100UL, 0x8010020882004410UL, 0x0604010040082C20UL, 0x20040104C0801008UL, 0x6004208424001050UL, 0x1002840041000800UL, 0x0200042018000102UL, 0xA8002000A0821C00UL, 0x0040080802201910UL, 0x0222620444000100UL, 0x0002080041020088UL, 0x1500820110401050UL, 0x0000492090100080UL, 0x0900410041100000UL, 0x0302000420880000UL, 0x0010501202020020UL, 0x0008200490049040UL, 0x0462080214A40120UL, 0x2421310102008100UL, 0x2400420080884060UL, 0x0800804406184208UL, 0x0B0080124A084400UL, 0x082E082300840412UL, 0x6051049040082200UL, 0xC610211002102101UL, 0x0000048808010433UL, 0x0010200804405440UL };

        static readonly int[] RookDf = { 1, -1, 0, 0 }, RookDr = { 0, 0, 1, -1 };
        static readonly int[] BishopDf = { 1, 1, -1, -1 }, BishopDr = { 1, -1, 1, -1 };

        static BB()
        {
            for (int sq = 0; sq < 64; sq++)
            {
                int f = sq & 7, r = sq >> 3;
                ulong k = 0, kn = 0;
                for (int df = -1; df <= 1; df++)
                    for (int dr = -1; dr <= 1; dr++)
                        if ((df != 0 || dr != 0) && OnBoard(f + df, r + dr)) k |= Bit((r + dr) * 8 + f + df);
                int[] nf = { 1, 2, 2, 1, -1, -2, -2, -1 }, nr = { 2, 1, -1, -2, -2, -1, 1, 2 };
                for (int i = 0; i < 8; i++)
                    if (OnBoard(f + nf[i], r + nr[i])) kn |= Bit((r + nr[i]) * 8 + f + nf[i]);
                KingAttacks[sq] = k;
                KnightAttacks[sq] = kn;
                ulong wp = 0, bp = 0;
                if (OnBoard(f - 1, r + 1)) wp |= Bit((r + 1) * 8 + f - 1);
                if (OnBoard(f + 1, r + 1)) wp |= Bit((r + 1) * 8 + f + 1);
                if (OnBoard(f - 1, r - 1)) bp |= Bit((r - 1) * 8 + f - 1);
                if (OnBoard(f + 1, r - 1)) bp |= Bit((r - 1) * 8 + f + 1);
                PawnAttacks[sq] = wp;
                PawnAttacks[64 + sq] = bp;
            }

            for (int i = 0; i < 8; i++)
            {
                Files[i] = FileA << i;
                Ranks[i] = Rank1 << (8 * i);
            }
            for (int i = 0; i < 8; i++)
                AdjacentFiles[i] = (i > 0 ? Files[i - 1] : 0) | (i < 7 ? Files[i + 1] : 0);

            for (int sq = 0; sq < 64; sq++)
            {
                int f = sq & 7, r = sq >> 3;
                ulong wFront = 0, bFront = 0;
                for (int rr = r + 1; rr < 8; rr++) wFront |= Ranks[rr];
                for (int rr = r - 1; rr >= 0; rr--) bFront |= Ranks[rr];
                ForwardFile[sq] = wFront & Files[f];
                ForwardFile[64 + sq] = bFront & Files[f];
                PassedMask[sq] = wFront & (Files[f] | AdjacentFiles[f]);
                PassedMask[64 + sq] = bFront & (Files[f] | AdjacentFiles[f]);
            }

            InitMagics(true, RookMask, RookMagic, RookShift, RookOffset, RookTable);
            InitMagics(false, BishopMask, BishopMagic, BishopShift, BishopOffset, BishopTable);
        }

        static bool OnBoard(int f, int r) => f >= 0 && f < 8 && r >= 0 && r < 8;

        static ulong SlidingAttacks(int sq, ulong occ, bool rook)
        {
            int[] df = rook ? RookDf : BishopDf, dr = rook ? RookDr : BishopDr;
            ulong att = 0;
            for (int d = 0; d < 4; d++)
            {
                int f = (sq & 7) + df[d], r = (sq >> 3) + dr[d];
                while (OnBoard(f, r))
                {
                    ulong b = Bit(r * 8 + f);
                    att |= b;
                    if ((occ & b) != 0) break;
                    f += df[d];
                    r += dr[d];
                }
            }
            return att;
        }

        static ulong RelevantMask(int sq, bool rook)
        {
            int[] df = rook ? RookDf : BishopDf, dr = rook ? RookDr : BishopDr;
            ulong mask = 0;
            for (int d = 0; d < 4; d++)
            {
                int f = (sq & 7) + df[d], r = (sq >> 3) + dr[d];
                while (OnBoard(f + df[d], r + dr[d]))
                {
                    mask |= Bit(r * 8 + f);
                    f += df[d];
                    r += dr[d];
                }
            }
            return mask;
        }

        static void InitMagics(bool rook, ulong[] masks, ulong[] magics, int[] shifts, int[] offsets, ulong[] table)
        {
            ulong seed = rook ? 0x9E3779B97F4A7C15UL : 0xD1B54A32D192ED03UL;
            ulong[] occ = new ulong[4096], refAtt = new ulong[4096];
            int[] epoch = new int[4096];
            int attempt = 0, offset = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                ulong mask = RelevantMask(sq, rook);
                int bits = PopCount(mask);
                int size = 1 << bits;
                masks[sq] = mask;
                shifts[sq] = 64 - bits;
                offsets[sq] = offset;

                // Enumerate all subsets of the mask (Carry-Rippler trick).
                ulong b = 0;
                int n = 0;
                do
                {
                    occ[n] = b;
                    refAtt[n] = SlidingAttacks(sq, b, rook);
                    n++;
                    b = (b - mask) & mask;
                } while (b != 0);

                ulong[] known = rook ? KnownRookMagics : KnownBishopMagics;
                bool first = true;
                while (true)
                {
                    // Precomputed magics make startup instant; random search is only a fallback.
                    ulong magic = first ? known[sq] : Next(ref seed) & Next(ref seed) & Next(ref seed);
                    first = false;
                    if (PopCount((mask * magic) & 0xFF00000000000000UL) < 6) continue;
                    attempt++;
                    bool ok = true;
                    for (int i = 0; i < n; i++)
                    {
                        int idx = (int)((occ[i] * magic) >> (64 - bits));
                        if (epoch[idx] != attempt)
                        {
                            epoch[idx] = attempt;
                            table[offset + idx] = refAtt[i];
                        }
                        else if (table[offset + idx] != refAtt[i])
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok)
                    {
                        magics[sq] = magic;
                        break;
                    }
                }
                offset += size;
            }
        }

        public static void DumpMagics()
        {
            Console.WriteLine("RookMagic = { " + string.Join(", ", Array.ConvertAll(RookMagic, m => "0x" + m.ToString("X16") + "UL")) + " };");
            Console.WriteLine("BishopMagic = { " + string.Join(", ", Array.ConvertAll(BishopMagic, m => "0x" + m.ToString("X16") + "UL")) + " };");
        }

        static ulong Next(ref ulong s)
        {
            // xorshift64*
            s ^= s >> 12;
            s ^= s << 25;
            s ^= s >> 27;
            return s * 2685821657736338717UL;
        }
    }

    /// <summary>Zobrist hashing keys.</summary>
    public static class Zobrist
    {
        public static readonly ulong[] PieceSquare = new ulong[12 * 64];
        public static readonly ulong[] Castle = new ulong[16];
        public static readonly ulong[] EpFile = new ulong[8];
        public static readonly ulong Side;

        static Zobrist()
        {
            ulong s = 0x1234567890ABCDEFUL;
            for (int i = 0; i < PieceSquare.Length; i++) PieceSquare[i] = Next(ref s);
            for (int i = 0; i < 16; i++) Castle[i] = Next(ref s);
            for (int i = 0; i < 8; i++) EpFile[i] = Next(ref s);
            Side = Next(ref s);
        }

        static ulong Next(ref ulong s)
        {
            // splitmix64
            ulong z = (s += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
