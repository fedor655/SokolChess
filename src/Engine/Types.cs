using System;

namespace Sokol.Engine
{
    /// <summary>Piece codes: color * 6 + type. Empty square = Piece.None.</summary>
    public static class Piece
    {
        public const int Pawn = 0, Knight = 1, Bishop = 2, Rook = 3, Queen = 4, King = 5;
        public const int White = 0, Black = 1;
        public const int None = 12;

        public static int Make(int color, int type) => color * 6 + type;
        public static int ColorOf(int piece) => piece >= 6 ? 1 : 0;
        public static int TypeOf(int piece) => piece >= 6 ? piece - 6 : piece;
    }

    /// <summary>Squares: a1 = 0, b1 = 1, ..., h8 = 63.</summary>
    public static class Sq
    {
        public static int File(int sq) => sq & 7;
        public static int Rank(int sq) => sq >> 3;
        public static int Make(int file, int rank) => rank * 8 + file;
        public static string Name(int sq) => new string(new[] { (char)('a' + (sq & 7)), (char)('1' + (sq >> 3)) });

        public static int Parse(string s)
        {
            if (s == null || s.Length < 2) return -1;
            int f = s[0] - 'a', r = s[1] - '1';
            if (f < 0 || f > 7 || r < 0 || r > 7) return -1;
            return r * 8 + f;
        }

        public static int Distance(int a, int b) => Math.Max(Math.Abs(File(a) - File(b)), Math.Abs(Rank(a) - Rank(b)));
    }

    /// <summary>
    /// Move encoding (int): bits 0-5 from, 6-11 to, 12-15 flags.
    /// Flags follow the chessprogramming.org scheme.
    /// </summary>
    public static class Move
    {
        public const int None = 0;
        public const int Quiet = 0, DoublePush = 1, KingCastle = 2, QueenCastle = 3, Capture = 4, EpCapture = 5,
            PromoN = 8, PromoB = 9, PromoR = 10, PromoQ = 11,
            PromoCapN = 12, PromoCapB = 13, PromoCapR = 14, PromoCapQ = 15;

        public static int Create(int from, int to, int flags) => from | (to << 6) | (flags << 12);
        public static int From(int m) => m & 63;
        public static int To(int m) => (m >> 6) & 63;
        public static int Flags(int m) => (m >> 12) & 15;
        public static bool IsCapture(int m) => (m & (4 << 12)) != 0;
        public static bool IsPromotion(int m) => (m & (8 << 12)) != 0;
        /// <summary>Promotion piece type (Knight..Queen).</summary>
        public static int PromotionType(int m) => ((m >> 12) & 3) + 1;
        public static bool IsCastle(int m) { int f = Flags(m); return f == KingCastle || f == QueenCastle; }
        public static bool IsEnPassant(int m) => Flags(m) == EpCapture;
        public static bool IsQuiet(int m) => (m & (12 << 12)) == 0;

        public static string ToUci(int m)
        {
            if (m == None) return "0000";
            string s = Sq.Name(From(m)) + Sq.Name(To(m));
            if (IsPromotion(m)) s += "nbrq"[PromotionType(m) - 1];
            return s;
        }
    }

    public static class Score
    {
        public const int Infinite = 32500;
        public const int Mate = 32000;
        /// <summary>Scores beyond this are mate scores.</summary>
        public const int MateBound = Mate - 1000;
        public const int None = -32700;

        public static bool IsMate(int s) => Math.Abs(s) >= MateBound;
        /// <summary>Moves (not plies) to mate, positive if the side to move mates.</summary>
        public static int MateInMoves(int s) => s > 0 ? (Mate - s + 1) / 2 : -(Mate + s + 1) / 2;
    }
}
