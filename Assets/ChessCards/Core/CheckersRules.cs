using System;
using System.Collections.Generic;

namespace ChessCards
{
    // Russian draughts: mandatory captures, flying kings, immediate crowning.
    // Captured pieces stay as obstacles until the complete capture sequence ends.
    public static class CheckersRules
    {
        static readonly int[] DX = { 1, -1, 1, -1 };
        static readonly int[] DY = { 1, 1, -1, -1 };
        static bool Inside(int x, int y) { return x >= 0 && x < 8 && y >= 0 && y < 8; }
        public static bool Pending(ChessPosition p, int square)
        { return (p.PendingCaptured & (1UL << square)) != 0; }
        public static ChessPosition Initial()
        {
            var p = new ChessPosition(GameMode.Checkers) { CastleRights = 0 };
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                if ((x + y) % 2 == 0 && (y < 3 || y > 4))
                    p.Squares[y * 8 + x] = new Piece(PieceType.Checker, y < 3 ? Side.White : Side.Black);
            return p;
        }
        public static List<ChessMove> LegalMoves(ChessPosition p, Side side)
        {
            var captures = new List<ChessMove>();
            if (p.ForcedCaptureFrom >= 0)
            {
                if (p.Squares[p.ForcedCaptureFrom].Side == side) AddCaptures(p, p.ForcedCaptureFrom, captures);
                return captures;
            }
            for (int from = 0; from < 64; from++)
                if (p.Squares[from].Type != PieceType.None && p.Squares[from].Side == side)
                    AddCaptures(p, from, captures);
            if (captures.Count > 0) return captures;
            var moves = new List<ChessMove>();
            for (int from = 0; from < 64; from++)
            {
                Piece piece = p.Squares[from];
                if (piece.Type == PieceType.None || piece.Side != side) continue;
                int x = from % 8, y = from / 8;
                bool king = piece.Type == PieceType.CrownedChecker;
                for (int d = 0; d < 4; d++)
                {
                    if (!king && DY[d] != (side == Side.White ? 1 : -1)) continue;
                    for (int step = 1; step <= (king ? 7 : 1); step++)
                    {
                        int nx = x + step * DX[d], ny = y + step * DY[d];
                        if (!Inside(nx, ny) || p.Squares[ny * 8 + nx].Type != PieceType.None) break;
                        moves.Add(new ChessMove(from, ny * 8 + nx));
                    }
                }
            }
            return moves;
        }
        static void AddCaptures(ChessPosition p, int from, List<ChessMove> result)
        {
            Piece piece = p.Squares[from];
            if (piece.Type == PieceType.None || Pending(p, from)) return;
            bool king = piece.Type == PieceType.CrownedChecker;
            int x = from % 8, y = from / 8;
            for (int d = 0; d < 4; d++)
            {
                int victim = -1;
                for (int step = 1; step < 8; step++)
                {
                    int nx = x + step * DX[d], ny = y + step * DY[d];
                    if (!Inside(nx, ny)) break;
                    int to = ny * 8 + nx;
                    Piece target = p.Squares[to];
                    if (target.Type != PieceType.None)
                    {
                        if (victim >= 0 || target.Side == piece.Side || Pending(p, to)) break;
                        if (!king && step != 1) break;
                        victim = to;
                    }
                    else
                    {
                        if (victim >= 0)
                        {
                            result.Add(new ChessMove(from, to));
                            if (!king) break;
                        }
                        else if (!king) break;
                    }
                }
            }
        }
        public static int CapturedSquare(ChessPosition p, ChessMove move)
        {
            int dx = Math.Sign(move.To % 8 - move.From % 8), dy = Math.Sign(move.To / 8 - move.From / 8);
            int x = move.From % 8 + dx, y = move.From / 8 + dy;
            while (Inside(x, y) && y * 8 + x != move.To)
            {
                int i = y * 8 + x;
                if (p.Squares[i].Type != PieceType.None) return i;
                x += dx; y += dy;
            }
            return -1;
        }
        public static void ApplyStep(ChessPosition p, ChessMove move)
        {
            Piece piece = p.Squares[move.From];
            bool wasMan = piece.Type == PieceType.Checker;
            int victim = CapturedSquare(p, move);
            p.Squares[move.From] = new Piece();
            if (wasMan && move.To / 8 == (piece.Side == Side.White ? 7 : 0)) piece.Type = PieceType.CrownedChecker;
            p.Squares[move.To] = piece;
            if (victim >= 0)
            {
                p.PendingCaptured |= 1UL << victim;
                p.WhiteKingQuietMoves = p.BlackKingQuietMoves = p.QuietMoves = 0;
                var next = new List<ChessMove>(); AddCaptures(p, move.To, next);
                if (next.Count > 0) { p.ForcedCaptureFrom = move.To; return; }
            }
            else if (wasMan) p.WhiteKingQuietMoves = p.BlackKingQuietMoves = p.QuietMoves = 0;
            else
            {
                p.QuietMoves++;
                if (piece.Side == Side.White) p.WhiteKingQuietMoves++; else p.BlackKingQuietMoves++;
            }
            for (int i = 0; i < 64; i++) if (Pending(p, i)) p.Squares[i] = new Piece();
            p.PendingCaptured = 0; p.ForcedCaptureFrom = -1;
        }
    }
}
