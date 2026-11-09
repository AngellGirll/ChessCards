using System;
using System.Collections.Generic;

namespace ChessCards
{
    public enum Side { White, Black }
    public enum PieceType { None, Pawn, Knight, Bishop, Rook, Queen, King }

    public struct Piece
    {
        public PieceType Type;
        public Side Side;
        public Piece(PieceType type, Side side) { Type = type; Side = side; }
    }

    public struct ChessMove
    {
        public int From, To;
        public PieceType Promotion;
        public ChessMove(int from, int to, PieceType promotion = PieceType.None)
        { From = from; To = to; Promotion = promotion; }
        public override string ToString()
        { return ChessPosition.SquareName(From) + "–" + ChessPosition.SquareName(To) + (Promotion == PieceType.None ? "" : "=" + Promotion); }
    }

    // No implicit side-to-move: cards can authorize several moves by the same army.
    public sealed class ChessPosition
    {
        public Piece[] Squares = new Piece[64];
        // white short/long, black short/long.
        public int CastleRights = 15;
        public int EnPassantSquare = -1;
        public Side EnPassantCapturer;
        public int QuietMoves;
        static readonly int[] KnightX = { 1, 2, 2, 1, -1, -2, -2, -1 };
        static readonly int[] KnightY = { 2, 1, -1, -2, -2, -1, 1, 2 };
        static readonly int[] RayX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] RayY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly PieceType[] Promotions = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

        public static Side Other(Side side) { return side == Side.White ? Side.Black : Side.White; }
        public static string SquareName(int index) { return "" + (char)('a' + index % 8) + (index / 8 + 1); }
        public static ChessPosition Initial()
        {
            var p = new ChessPosition();
            PieceType[] row = { PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen, PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook };
            for (int x = 0; x < 8; x++)
            {
                p.Squares[x] = new Piece(row[x], Side.White);
                p.Squares[8 + x] = new Piece(PieceType.Pawn, Side.White);
                p.Squares[48 + x] = new Piece(PieceType.Pawn, Side.Black);
                p.Squares[56 + x] = new Piece(row[x], Side.Black);
            }
            return p;
        }
        public ChessPosition Clone()
        {
            return new ChessPosition { Squares = (Piece[])Squares.Clone(), CastleRights = CastleRights,
                EnPassantSquare = EnPassantSquare, EnPassantCapturer = EnPassantCapturer, QuietMoves = QuietMoves };
        }
        public bool InCheck(Side side)
        {
            for (int i = 0; i < 64; i++)
                if (Squares[i].Type == PieceType.King && Squares[i].Side == side)
                    return Attacked(i, Other(side));
            return true; // Malformed positions cannot pass legality validation.
        }
        public bool Attacked(int target, Side by)
        {
            int tx = target % 8, ty = target / 8;
            int pawnY = ty - (by == Side.White ? 1 : -1);
            if (Is(tx - 1, pawnY, PieceType.Pawn, by) || Is(tx + 1, pawnY, PieceType.Pawn, by)) return true;
            for (int k = 0; k < 8; k++)
                if (Is(tx + KnightX[k], ty + KnightY[k], PieceType.Knight, by)) return true;
            for (int d = 0; d < 8; d++)
                for (int step = 1; step < 8; step++)
                {
                    int x = tx + RayX[d] * step, y = ty + RayY[d] * step;
                    if (!Inside(x, y)) break;
                    Piece p = Squares[y * 8 + x];
                    if (p.Type == PieceType.None) continue;
                    if (p.Side == by && (p.Type == PieceType.Queen ||
                        (d < 4 && p.Type == PieceType.Rook) || (d >= 4 && p.Type == PieceType.Bishop) ||
                        (step == 1 && p.Type == PieceType.King))) return true;
                    break;
                }
            return false;
        }
        bool Is(int x, int y, PieceType type, Side side)
        { return Inside(x, y) && Squares[y * 8 + x].Type == type && Squares[y * 8 + x].Side == side; }
        static bool Inside(int x, int y) { return x >= 0 && x < 8 && y >= 0 && y < 8; }
        public List<ChessMove> LegalMoves(Side side)
        {
            var moves = new List<ChessMove>();
            for (int i = 0; i < 64; i++)
            {
                if (Squares[i].Type == PieceType.None || Squares[i].Side != side) continue;
                foreach (ChessMove m in PseudoMoves(i))
                {
                    var next = Clone();
                    next.ApplyUnchecked(m);
                    if (!next.InCheck(side)) moves.Add(m);
                }
            }
            return moves;
        }
        IEnumerable<ChessMove> PawnMove(int from, int to)
        {
            if (to / 8 == 0 || to / 8 == 7)
            { foreach (var type in Promotions) yield return new ChessMove(from, to, type); }
            else yield return new ChessMove(from, to);
        }
        IEnumerable<ChessMove> PseudoMoves(int from)
        {
            Piece piece = Squares[from];
            int x = from % 8, y = from / 8;
            if (piece.Type == PieceType.Pawn)
            {
                int dy = piece.Side == Side.White ? 1 : -1;
                int ny = y + dy;
                if (!Inside(x, ny)) yield break;
                if (Squares[ny * 8 + x].Type == PieceType.None)
                {
                    foreach (var m in PawnMove(from, ny * 8 + x)) yield return m;
                    if (y == (piece.Side == Side.White ? 1 : 6) && Squares[(y + 2 * dy) * 8 + x].Type == PieceType.None)
                        yield return new ChessMove(from, (y + 2 * dy) * 8 + x);
                }
                for (int dx = -1; dx <= 1; dx += 2)
                {
                    if (!Inside(x + dx, ny)) continue;
                    int to = ny * 8 + x + dx;
                    Piece target = Squares[to];
                    bool capture = target.Type != PieceType.None && target.Side != piece.Side && target.Type != PieceType.King;
                    bool ep = to == EnPassantSquare && piece.Side == EnPassantCapturer && target.Type == PieceType.None &&
                        Is(x + dx, y, PieceType.Pawn, Other(piece.Side));
                    if (capture || ep) foreach (var m in PawnMove(from, to)) yield return m;
                }
                yield break;
            }
            if (piece.Type == PieceType.Knight)
            {
                for (int k = 0; k < 8; k++)
                {
                    int nx = x + KnightX[k], ny = y + KnightY[k];
                    if (Inside(nx, ny) && CanLand(ny * 8 + nx, piece.Side)) yield return new ChessMove(from, ny * 8 + nx);
                }
                yield break;
            }
            for (int d = 0; d < 8; d++)
            {
                if (piece.Type == PieceType.Bishop && d < 4 || piece.Type == PieceType.Rook && d >= 4) continue;
                for (int step = 1; step <= (piece.Type == PieceType.King ? 1 : 7); step++)
                {
                    int nx = x + RayX[d] * step, ny = y + RayY[d] * step;
                    if (!Inside(nx, ny)) break;
                    int to = ny * 8 + nx;
                    if (CanLand(to, piece.Side)) yield return new ChessMove(from, to);
                    if (Squares[to].Type != PieceType.None) break;
                }
            }
            if (piece.Type == PieceType.King)
            {
                int row = piece.Side == Side.White ? 0 : 56;
                int shortBit = piece.Side == Side.White ? 1 : 4, longBit = shortBit * 2;
                Side enemy = Other(piece.Side);
                if (from != row + 4 || InCheck(piece.Side)) yield break;
                if ((CastleRights & shortBit) != 0 && Is(7, row / 8, PieceType.Rook, piece.Side) &&
                    Squares[row + 5].Type == PieceType.None && Squares[row + 6].Type == PieceType.None &&
                    !Attacked(row + 5, enemy) && !Attacked(row + 6, enemy)) yield return new ChessMove(from, row + 6);
                if ((CastleRights & longBit) != 0 && Is(0, row / 8, PieceType.Rook, piece.Side) &&
                    Squares[row + 1].Type == PieceType.None && Squares[row + 2].Type == PieceType.None && Squares[row + 3].Type == PieceType.None &&
                    !Attacked(row + 3, enemy) && !Attacked(row + 2, enemy)) yield return new ChessMove(from, row + 2);
            }
        }
        bool CanLand(int square, Side side)
        { return Squares[square].Type == PieceType.None || (Squares[square].Side != side && Squares[square].Type != PieceType.King); }
        // Call only with a move returned by LegalMoves. Search and tests also use this method.
        public void ApplyUnchecked(ChessMove m)
        {
            Piece p = Squares[m.From], captured = Squares[m.To];
            if (p.Type == PieceType.Pawn && m.To == EnPassantSquare && p.Side == EnPassantCapturer && captured.Type == PieceType.None)
            { Squares[m.To + (p.Side == Side.White ? -8 : 8)] = new Piece(); captured = new Piece(PieceType.Pawn, Other(p.Side)); }
            EnPassantSquare = -1;
            if (p.Type == PieceType.Pawn && Math.Abs(m.To - m.From) == 16)
            { EnPassantSquare = (m.From + m.To) / 2; EnPassantCapturer = Other(p.Side); }
            if (p.Type == PieceType.King)
            {
                CastleRights &= p.Side == Side.White ? ~3 : ~12;
                if (Math.Abs(m.To - m.From) == 2)
                {
                    int rookFrom = m.To > m.From ? m.From + 3 : m.From - 4;
                    int rookTo = m.To > m.From ? m.From + 1 : m.From - 1;
                    Squares[rookTo] = Squares[rookFrom]; Squares[rookFrom] = new Piece();
                }
            }
            if (m.From == 0 || m.To == 0) CastleRights &= ~2;
            if (m.From == 7 || m.To == 7) CastleRights &= ~1;
            if (m.From == 56 || m.To == 56) CastleRights &= ~8;
            if (m.From == 63 || m.To == 63) CastleRights &= ~4;
            QuietMoves = p.Type == PieceType.Pawn || captured.Type != PieceType.None ? 0 : QuietMoves + 1;
            Squares[m.To] = m.Promotion == PieceType.None ? p : new Piece(m.Promotion, p.Side);
            Squares[m.From] = new Piece();
        }
        public bool BareKings()
        {
            foreach (var p in Squares) if (p.Type != PieceType.None && p.Type != PieceType.King) return false;
            return true;
        }
    }
}
