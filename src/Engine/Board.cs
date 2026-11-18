using System;
using System.Text;

namespace Sokol.Engine
{
    /// <summary>Chess position with incremental make/unmake, Zobrist hash and piece-square accumulators.</summary>
    public sealed partial class Board
    {
        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        const int MaxHistory = 4096;

        public readonly ulong[] Pieces = new ulong[12];
        public readonly ulong[] Colors = new ulong[2];
        public ulong Occupied;
        public readonly int[] Squares = new int[64];

        public int SideToMove;
        /// <summary>1 = white O-O, 2 = white O-O-O, 4 = black O-O, 8 = black O-O-O.</summary>
        public int CastleRights;
        /// <summary>En passant target square, or -1. Only set when a capture is actually possible.</summary>
        public int EpSquare = -1;
        public int HalfmoveClock;
        public int FullmoveNumber = 1;
        public ulong Hash;

        /// <summary>Incremental material + piece-square sums, per color.</summary>
        public readonly int[] PsqMg = new int[2], PsqEg = new int[2];
        public int Phase;

        struct Undo
        {
            public int Move, Captured, Castle, Ep, Halfmove;
            public ulong Hash;
        }

        readonly Undo[] undo = new Undo[MaxHistory];
        int undoCount;
        /// <summary>Hashes of all previous positions (for repetition detection).</summary>
        readonly ulong[] hashHistory = new ulong[MaxHistory];
        int historyCount;

        static readonly int[] CastleMask = BuildCastleMask();

        static int[] BuildCastleMask()
        {
            var m = new int[64];
            for (int i = 0; i < 64; i++) m[i] = 15;
            m[0] &= ~2; m[4] &= ~3; m[7] &= ~1;
            m[56] &= ~8; m[60] &= ~12; m[63] &= ~4;
            return m;
        }

        public Board() { SetFen(StartFen); }
        public Board(string fen) { SetFen(fen); }

        public int Ply => undoCount;

        public Board Clone()
        {
            var b = new Board(0);
            Array.Copy(Pieces, b.Pieces, 12);
            Array.Copy(Colors, b.Colors, 2);
            Array.Copy(Squares, b.Squares, 64);
            b.Occupied = Occupied;
            b.SideToMove = SideToMove;
            b.CastleRights = CastleRights;
            b.EpSquare = EpSquare;
            b.HalfmoveClock = HalfmoveClock;
            b.FullmoveNumber = FullmoveNumber;
            b.Hash = Hash;
            b.PsqMg[0] = PsqMg[0]; b.PsqMg[1] = PsqMg[1];
            b.PsqEg[0] = PsqEg[0]; b.PsqEg[1] = PsqEg[1];
            b.Phase = Phase;
            // Keep only the history that matters for repetition detection.
            int keep = Math.Min(historyCount, Math.Min(HalfmoveClock, 200));
            Array.Copy(hashHistory, historyCount - keep, b.hashHistory, 0, keep);
            b.historyCount = keep;
            b.undoCount = 0;
            return b;
        }

        Board(int dummy) { }

        // ------------------------------------------------------------------ FEN

        public void SetFen(string fen)
        {
            Array.Clear(Pieces, 0, 12);
            Colors[0] = Colors[1] = 0;
            Occupied = 0;
            for (int i = 0; i < 64; i++) Squares[i] = Piece.None;
            PsqMg[0] = PsqMg[1] = PsqEg[0] = PsqEg[1] = 0;
            Phase = 0;
            Hash = 0;
            undoCount = 0;
            historyCount = 0;

            string[] parts = fen.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) throw new FormatException("Bad FEN");
            int rank = 7, file = 0;
            foreach (char c in parts[0])
            {
                if (c == '/') { rank--; file = 0; continue; }
                if (char.IsDigit(c)) { file += c - '0'; continue; }
                int color = char.IsUpper(c) ? Piece.White : Piece.Black;
                int type = "pnbrqk".IndexOf(char.ToLowerInvariant(c));
                if (type < 0 || rank < 0 || file > 7) throw new FormatException("Bad FEN");
                AddPiece(rank * 8 + file, Piece.Make(color, type));
                file++;
            }
            SideToMove = parts[1] == "b" ? Piece.Black : Piece.White;
            CastleRights = 0;
            if (parts.Length > 2)
                foreach (char c in parts[2])
                {
                    if (c == 'K') CastleRights |= 1;
                    else if (c == 'Q') CastleRights |= 2;
                    else if (c == 'k') CastleRights |= 4;
                    else if (c == 'q') CastleRights |= 8;
                }
            // Drop rights that are inconsistent with the piece placement.
            if (Squares[4] != Piece.Make(0, Piece.King)) CastleRights &= ~3;
            if (Squares[7] != Piece.Make(0, Piece.Rook)) CastleRights &= ~1;
            if (Squares[0] != Piece.Make(0, Piece.Rook)) CastleRights &= ~2;
            if (Squares[60] != Piece.Make(1, Piece.King)) CastleRights &= ~12;
            if (Squares[63] != Piece.Make(1, Piece.Rook)) CastleRights &= ~4;
            if (Squares[56] != Piece.Make(1, Piece.Rook)) CastleRights &= ~8;

            EpSquare = -1;
            if (parts.Length > 3 && parts[3] != "-")
            {
                int ep = Sq.Parse(parts[3]);
                int them = SideToMove ^ 1;
                if (ep >= 0 && (BB.PawnAttacks[them * 64 + ep] & Pieces[Piece.Make(SideToMove, Piece.Pawn)]) != 0)
                    EpSquare = ep;
            }
            HalfmoveClock = parts.Length > 4 ? int.Parse(parts[4]) : 0;
            FullmoveNumber = parts.Length > 5 ? Math.Max(1, int.Parse(parts[5])) : 1;

            Hash ^= Zobrist.Castle[CastleRights];
            if (EpSquare >= 0) Hash ^= Zobrist.EpFile[EpSquare & 7];
            if (SideToMove == Piece.Black) Hash ^= Zobrist.Side;
        }

        public string ToFen()
        {
            var sb = new StringBuilder();
            for (int r = 7; r >= 0; r--)
            {
                int empty = 0;
                for (int f = 0; f < 8; f++)
                {
                    int p = Squares[r * 8 + f];
                    if (p == Piece.None) { empty++; continue; }
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    char c = "pnbrqk"[Piece.TypeOf(p)];
                    sb.Append(Piece.ColorOf(p) == Piece.White ? char.ToUpperInvariant(c) : c);
                }
                if (empty > 0) sb.Append(empty);
                if (r > 0) sb.Append('/');
            }
            sb.Append(SideToMove == Piece.White ? " w " : " b ");
            string cr = "";
            if ((CastleRights & 1) != 0) cr += "K";
            if ((CastleRights & 2) != 0) cr += "Q";
            if ((CastleRights & 4) != 0) cr += "k";
            if ((CastleRights & 8) != 0) cr += "q";
            sb.Append(cr.Length > 0 ? cr : "-");
            sb.Append(' ').Append(EpSquare >= 0 ? Sq.Name(EpSquare) : "-");
            sb.Append(' ').Append(HalfmoveClock).Append(' ').Append(FullmoveNumber);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ piece placement

        void AddPiece(int sq, int piece)
        {
            ulong b = 1UL << sq;
            Pieces[piece] |= b;
            int c = piece >= 6 ? 1 : 0;
            Colors[c] |= b;
            Occupied |= b;
            Squares[sq] = piece;
            Hash ^= Zobrist.PieceSquare[piece * 64 + sq];
            PsqMg[c] += Eval.MgTable[piece * 64 + sq];
            PsqEg[c] += Eval.EgTable[piece * 64 + sq];
            Phase += Eval.PhaseInc[piece >= 6 ? piece - 6 : piece];
        }

        void RemovePiece(int sq)
        {
            int piece = Squares[sq];
            ulong b = 1UL << sq;
            Pieces[piece] ^= b;
            int c = piece >= 6 ? 1 : 0;
            Colors[c] ^= b;
            Occupied ^= b;
            Squares[sq] = Piece.None;
            Hash ^= Zobrist.PieceSquare[piece * 64 + sq];
            PsqMg[c] -= Eval.MgTable[piece * 64 + sq];
            PsqEg[c] -= Eval.EgTable[piece * 64 + sq];
            Phase -= Eval.PhaseInc[piece >= 6 ? piece - 6 : piece];
        }

        void MovePiece(int from, int to)
        {
            int piece = Squares[from];
            ulong b = (1UL << from) | (1UL << to);
            Pieces[piece] ^= b;
            int c = piece >= 6 ? 1 : 0;
            Colors[c] ^= b;
            Occupied ^= b;
            Squares[from] = Piece.None;
            Squares[to] = piece;
            Hash ^= Zobrist.PieceSquare[piece * 64 + from] ^ Zobrist.PieceSquare[piece * 64 + to];
            PsqMg[c] += Eval.MgTable[piece * 64 + to] - Eval.MgTable[piece * 64 + from];
            PsqEg[c] += Eval.EgTable[piece * 64 + to] - Eval.EgTable[piece * 64 + from];
        }

        // ------------------------------------------------------------------ make / unmake

        /// <summary>Makes a pseudo-legal move. Returns false (and undoes it) if it leaves the own king in check.</summary>
        public bool MakeMove(int m)
        {
            int from = m & 63, to = (m >> 6) & 63, flags = (m >> 12) & 15;
            int us = SideToMove, them = us ^ 1;
            int piece = Squares[from];

            ref Undo u = ref undo[undoCount++];
            u.Move = m;
            u.Castle = CastleRights;
            u.Ep = EpSquare;
            u.Halfmove = HalfmoveClock;
            u.Hash = Hash;
            u.Captured = Piece.None;
            hashHistory[historyCount++] = Hash;

            if (EpSquare >= 0) { Hash ^= Zobrist.EpFile[EpSquare & 7]; EpSquare = -1; }
            HalfmoveClock++;

            if (flags == Move.EpCapture)
            {
                int capSq = us == Piece.White ? to - 8 : to + 8;
                u.Captured = Squares[capSq];
                RemovePiece(capSq);
            }
            else if ((flags & Move.Capture) != 0)
            {
                u.Captured = Squares[to];
                RemovePiece(to);
                HalfmoveClock = 0;
            }

            MovePiece(from, to);

            if (piece == Piece.Make(us, Piece.Pawn))
            {
                HalfmoveClock = 0;
                if (flags == Move.DoublePush)
                {
                    int ep = (from + to) >> 1;
                    if ((BB.PawnAttacks[us * 64 + ep] & Pieces[Piece.Make(them, Piece.Pawn)]) != 0)
                    {
                        EpSquare = ep;
                        Hash ^= Zobrist.EpFile[ep & 7];
                    }
                }
                else if ((flags & 8) != 0)
                {
                    RemovePiece(to);
                    AddPiece(to, Piece.Make(us, ((flags & 3) + 1)));
                }
            }
            else if (flags == Move.KingCastle)
            {
                if (us == Piece.White) MovePiece(7, 5); else MovePiece(63, 61);
            }
            else if (flags == Move.QueenCastle)
            {
                if (us == Piece.White) MovePiece(0, 3); else MovePiece(56, 59);
            }

            int newRights = CastleRights & CastleMask[from] & CastleMask[to];
            if (newRights != CastleRights)
            {
                Hash ^= Zobrist.Castle[CastleRights] ^ Zobrist.Castle[newRights];
                CastleRights = newRights;
            }

            SideToMove = them;
            Hash ^= Zobrist.Side;
            if (us == Piece.Black) FullmoveNumber++;

            if (IsSquareAttacked(BB.Lsb(Pieces[Piece.Make(us, Piece.King)]), them))
            {
                UnmakeMove();
                return false;
            }
            return true;
        }

        public void UnmakeMove()
        {
            ref Undo u = ref undo[--undoCount];
            historyCount--;
            int m = u.Move;
            int from = m & 63, to = (m >> 6) & 63, flags = (m >> 12) & 15;
            SideToMove ^= 1;
            int us = SideToMove;
            if (us == Piece.Black) FullmoveNumber--;

            if ((flags & 8) != 0)
            {
                RemovePiece(to);
                AddPiece(to, Piece.Make(us, Piece.Pawn));
            }
            MovePiece(to, from);
            if (flags == Move.KingCastle)
            {
                if (us == Piece.White) MovePiece(5, 7); else MovePiece(61, 63);
            }
            else if (flags == Move.QueenCastle)
            {
                if (us == Piece.White) MovePiece(3, 0); else MovePiece(59, 56);
            }
            if (u.Captured != Piece.None)
            {
                int capSq = flags == Move.EpCapture ? (us == Piece.White ? to - 8 : to + 8) : to;
                AddPiece(capSq, u.Captured);
            }
            CastleRights = u.Castle;
            EpSquare = u.Ep;
            HalfmoveClock = u.Halfmove;
            Hash = u.Hash;
        }

        public void MakeNullMove()
        {
            ref Undo u = ref undo[undoCount++];
            u.Move = Move.None;
            u.Ep = EpSquare;
            u.Halfmove = HalfmoveClock;
            u.Hash = Hash;
            u.Castle = CastleRights;
            u.Captured = Piece.None;
            hashHistory[historyCount++] = Hash;
            if (EpSquare >= 0) { Hash ^= Zobrist.EpFile[EpSquare & 7]; EpSquare = -1; }
            HalfmoveClock = 0; // repetition detection must not cross a null move
            SideToMove ^= 1;
            Hash ^= Zobrist.Side;
        }

        public void UnmakeNullMove()
        {
            ref Undo u = ref undo[--undoCount];
            historyCount--;
            SideToMove ^= 1;
            EpSquare = u.Ep;
            HalfmoveClock = u.Halfmove;
            Hash = u.Hash;
        }

        /// <summary>The last move made on this board (or Move.None).</summary>
        public int LastMove => undoCount > 0 ? undo[undoCount - 1].Move : Move.None;

        // ------------------------------------------------------------------ attacks

        public bool IsSquareAttacked(int sq, int byColor)
        {
            int o = byColor * 6;
            if ((BB.PawnAttacks[(byColor ^ 1) * 64 + sq] & Pieces[o]) != 0) return true;
            if ((BB.KnightAttacks[sq] & Pieces[o + 1]) != 0) return true;
            if ((BB.KingAttacks[sq] & Pieces[o + 5]) != 0) return true;
            ulong q = Pieces[o + 4];
            if ((BB.BishopAttacks(sq, Occupied) & (Pieces[o + 2] | q)) != 0) return true;
            if ((BB.RookAttacks(sq, Occupied) & (Pieces[o + 3] | q)) != 0) return true;
            return false;
        }

        /// <summary>All pieces of both colors attacking sq with the given occupancy.</summary>
        public ulong AttackersTo(int sq, ulong occ)
        {
            return (BB.PawnAttacks[64 + sq] & Pieces[0])
                 | (BB.PawnAttacks[sq] & Pieces[6])
                 | (BB.KnightAttacks[sq] & (Pieces[1] | Pieces[7]))
                 | (BB.KingAttacks[sq] & (Pieces[5] | Pieces[11]))
                 | (BB.BishopAttacks(sq, occ) & (Pieces[2] | Pieces[8] | Pieces[4] | Pieces[10]))
                 | (BB.RookAttacks(sq, occ) & (Pieces[3] | Pieces[9] | Pieces[4] | Pieces[10]));
        }

        public int KingSquare(int color) => BB.Lsb(Pieces[color * 6 + Piece.King]);

        public bool InCheck() => IsSquareAttacked(KingSquare(SideToMove), SideToMove ^ 1);

        public bool HasNonPawnMaterial(int color)
        {
            int o = color * 6;
            return (Pieces[o + 1] | Pieces[o + 2] | Pieces[o + 3] | Pieces[o + 4]) != 0;
        }

        // ------------------------------------------------------------------ draws

        /// <summary>Repetition (twofold, used inside search) or 50-move rule.</summary>
        public bool IsRepetitionOrFifty()
        {
            if (HalfmoveClock >= 100) return true;
            int limit = Math.Min(HalfmoveClock, historyCount);
            for (int i = 2; i <= limit; i += 2)
                if (hashHistory[historyCount - i] == Hash) return true;
            return false;
        }

        /// <summary>How many times the current position occurred before (for threefold repetition).</summary>
        public int RepetitionCount()
        {
            int count = 0;
            int limit = Math.Min(HalfmoveClock, historyCount);
            for (int i = 2; i <= limit; i += 2)
                if (hashHistory[historyCount - i] == Hash) count++;
            return count;
        }

        public bool IsInsufficientMaterial()
        {
            if ((Pieces[0] | Pieces[6] | Pieces[3] | Pieces[9] | Pieces[4] | Pieces[10]) != 0) return false;
            ulong knights = Pieces[1] | Pieces[7];
            ulong bishops = Pieces[2] | Pieces[8];
            int minors = BB.PopCount(knights | bishops);
            if (minors <= 1) return true;
            // Only bishops, all on the same square color.
            if (knights == 0 && ((bishops & BB.LightSquares) == 0 || (bishops & BB.DarkSquares) == 0)) return true;
            return false;
        }
    }
}
