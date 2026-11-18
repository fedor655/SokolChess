using System.Collections.Generic;
using System.Text;

namespace Sokol.Engine
{
    public enum NotationStyle
    {
        /// <summary>The user's notation: K = knight, K^ = king, B, R, Q.</summary>
        User = 0,
        /// <summary>International (English) algebraic notation: N, B, R, Q, K.</summary>
        International = 1,
        /// <summary>Russian notation: К, С, Л, Ф, Кр.</summary>
        Russian = 2,
    }

    public static class Notation
    {
        static readonly string[][] Letters =
        {
            new[] { "", "K", "B", "R", "Q", "K^" },
            new[] { "", "N", "B", "R", "Q", "K" },
            new[] { "", "К", "С", "Л", "Ф", "Кр" },
        };

        public static string PieceLetter(int type, NotationStyle style) => Letters[(int)style][type];

        /// <summary>Algebraic notation of a legal move in position b (b is restored afterwards).</summary>
        public static string ToSan(Board b, int m, NotationStyle style, bool withCheck = true)
        {
            string s;
            int from = Move.From(m), to = Move.To(m);
            if (Move.IsCastle(m))
            {
                s = Move.Flags(m) == Move.KingCastle ? "O-O" : "O-O-O";
            }
            else
            {
                int piece = b.Squares[from];
                int type = Piece.TypeOf(piece);
                var sb = new StringBuilder();
                if (type == Piece.Pawn)
                {
                    if (Move.IsCapture(m)) sb.Append((char)('a' + Sq.File(from))).Append('x');
                    sb.Append(Sq.Name(to));
                    if (Move.IsPromotion(m)) sb.Append('=').Append(PieceLetter(Move.PromotionType(m), style));
                }
                else
                {
                    sb.Append(PieceLetter(type, style));
                    bool ambiguous = false, sameFile = false, sameRank = false;
                    foreach (int o in b.LegalMoves())
                    {
                        if (o == m || Move.To(o) != to || b.Squares[Move.From(o)] != piece) continue;
                        ambiguous = true;
                        if (Sq.File(Move.From(o)) == Sq.File(from)) sameFile = true;
                        if (Sq.Rank(Move.From(o)) == Sq.Rank(from)) sameRank = true;
                    }
                    if (ambiguous)
                    {
                        if (!sameFile) sb.Append((char)('a' + Sq.File(from)));
                        else if (!sameRank) sb.Append((char)('1' + Sq.Rank(from)));
                        else sb.Append(Sq.Name(from));
                    }
                    if (Move.IsCapture(m)) sb.Append('x');
                    sb.Append(Sq.Name(to));
                }
                s = sb.ToString();
            }
            if (withCheck && b.MakeMove(m))
            {
                if (b.InCheck()) s += b.HasLegalMove() ? "+" : "#";
                b.UnmakeMove();
            }
            return s;
        }

        /// <summary>Parses international SAN ("Nf3", "exd5", "O-O", "e8=Q+"). Returns Move.None if not legal.</summary>
        public static int ParseSan(Board b, string san)
        {
            string clean = Normalize(san);
            if (clean.Length == 0) return Move.None;
            foreach (int m in b.LegalMoves())
                if (Normalize(ToSan(b, m, NotationStyle.International, false)) == clean) return m;
            return Move.None;
        }

        static string Normalize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '+' || c == '#' || c == '!' || c == '?' || c == '=') continue;
                sb.Append(c == '0' ? 'O' : c);
            }
            return sb.ToString();
        }

        /// <summary>Formats a sequence of moves from a position: "12. d4 exd4 13. Kxd4".</summary>
        public static string FormatLine(Board start, IList<int> moves, NotationStyle style, int maxPlies = 10)
        {
            var b = start.Clone();
            var sb = new StringBuilder();
            bool first = true;
            int count = 0;
            foreach (int m in moves)
            {
                if (count >= maxPlies || m == Move.None || !b.IsLegal(m)) break;
                if (b.SideToMove == Piece.White) sb.Append(b.FullmoveNumber).Append(". ");
                else if (first) sb.Append(b.FullmoveNumber).Append("... ");
                sb.Append(ToSan(b, m, style)).Append(' ');
                b.MakeMove(m);
                first = false;
                count++;
            }
            return sb.ToString().TrimEnd();
        }

        public static string PieceName(int type)
        {
            switch (type)
            {
                case Piece.Pawn: return "пешка";
                case Piece.Knight: return "конь";
                case Piece.Bishop: return "слон";
                case Piece.Rook: return "ладья";
                case Piece.Queen: return "ферзь";
                default: return "король";
            }
        }

        /// <summary>Accusative case: "отдаёт коня", "теряет ладью".</summary>
        public static string PieceNameAcc(int type)
        {
            switch (type)
            {
                case Piece.Pawn: return "пешку";
                case Piece.Knight: return "коня";
                case Piece.Bishop: return "слона";
                case Piece.Rook: return "ладью";
                case Piece.Queen: return "ферзя";
                default: return "короля";
            }
        }
    }
}
