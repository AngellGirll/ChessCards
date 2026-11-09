using System;
using System.Collections.Generic;

namespace ChessCards
{
    // Bounded two-ply search. It models whether the next move belongs to the same army.
    public sealed class ChessBot
    {
        readonly Random random;
        static readonly int[] Values = { 0, 100, 320, 330, 500, 900, 20000 };
        public ChessBot(int seed) { random = new Random(seed); }
        public int ChooseCard(CardMatch match)
        {
            var playable = match.PlayableCards();
            int best = -1; double bestScore = double.NegativeInfinity;
            Side side = match.ActiveSide;
            foreach (int index in playable)
            {
                Card c = match.Hands[match.Actor][index];
                double score = random.NextDouble() * 15;
                if (c.Kind == CardKind.Number) score += c.Number * 60;
                if (c.Kind == CardKind.Skip) score += 160;
                if (c.Kind == CardKind.Swap) score += -2 * Evaluate(match.Board, side) + 40;
                if (c.Kind == CardKind.Undo) score += Evaluate(match.UndoPreview(), side) - Evaluate(match.Board, side) + 30;
                if (score > bestScore) { bestScore = score; best = index; }
            }
            return best;
        }
        public ChessMove ChooseMove(CardMatch match)
        {
            Side side = match.ActiveSide;
            var legal = match.Board.LegalMoves(side);
            if (legal.Count == 0) throw new InvalidOperationException("No legal bot move.");
            ChessMove best = legal[0]; double bestScore = double.NegativeInfinity;
            foreach (ChessMove move in legal)
            {
                var next = match.Board.Clone(); next.ApplyUnchecked(move);
                Side enemy = ChessPosition.Other(side);
                bool check = next.InCheck(enemy);
                var enemyMoves = next.LegalMoves(enemy);
                if (check && enemyMoves.Count == 0) return move;
                Side following = (match.Remaining > 1 && !check || match.SkipPending && !check) ? side : enemy;
                List<ChessMove> continuations = following == enemy ? enemyMoves : next.LegalMoves(side);
                double score = Evaluate(next, side);
                if (continuations.Count > 0)
                {
                    double bound = following == side ? double.NegativeInfinity : double.PositiveInfinity;
                    foreach (var reply in continuations)
                    {
                        var end = next.Clone(); end.ApplyUnchecked(reply);
                        double value = Evaluate(end, side);
                        if (following == side) bound = Math.Max(bound, value); else bound = Math.Min(bound, value);
                    }
                    score = bound;
                }
                else if (!check && following == enemy) score = 0; // Stalemate.
                score += random.NextDouble() * 10;
                if (score > bestScore) { bestScore = score; best = move; }
            }
            return best;
        }
        public static int Evaluate(ChessPosition board, Side perspective)
        {
            int result = 0;
            for (int i = 0; i < 64; i++)
            {
                Piece p = board.Squares[i]; if (p.Type == PieceType.None) continue;
                int x = i % 8, y = p.Side == Side.White ? i / 8 : 7 - i / 8;
                int bonus = p.Type == PieceType.Pawn ? y * 7 : p.Type == PieceType.King ? 0 : 3 * (7 - Math.Abs(2 * x - 7) / 2 - Math.Abs(2 * y - 7) / 2);
                result += (p.Side == perspective ? 1 : -1) * (Values[(int)p.Type] + bonus);
            }
            return result;
        }
    }
}
