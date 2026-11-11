using System;
using System.Collections.Generic;

namespace ChessCards
{
    // Bounded two-ply search. It models whether the next move belongs to the same army.
    public sealed class ChessBot
    {
        readonly Random random;
        static readonly int[] Values = { 0, 100, 320, 330, 500, 900, 20000, 100, 320 };
        public BotDifficulty Difficulty { get; }

        public ChessBot(int seed, BotDifficulty difficulty = BotDifficulty.Normal)
        {
            random = new Random(seed);
            Difficulty = difficulty;
        }
        public int ChooseCard(CardMatch match)
        {
            var playable = match.PlayableCards();
            if (Difficulty == BotDifficulty.Easy)
                return playable.Count == 0 ? -1 : playable[random.Next(playable.Count)];
            int best = -1; double bestScore = double.NegativeInfinity;
            Side side = match.ActiveSide;
            foreach (int index in playable)
            {
                Card c = match.Hands[match.Actor][index];
                double score = random.NextDouble() * 15;
                if (c.Kind == CardKind.Number) score += c.Number * 60;
                if (c.Kind == CardKind.Skip) score += 160;
                if (c.Kind == CardKind.Swap) score += -2 * PositionValue(match, side) + 40;
                if (c.Kind == CardKind.Undo && match.Board != null) score += Evaluate(match.UndoPreview(), side) - Evaluate(match.Board, side) + 30;
                if (score > bestScore) { bestScore = score; best = index; }
            }
            return best;
        }
        static int PositionValue(CardMatch match, Side side)
        {
            Side opponent = ChessPosition.Other(side);
            if (match.Go != null) return 24 * match.Go.StoneBalance(side);
            if (match.Domino != null)
                return 22 * (match.Domino.Rack(opponent).Count - match.Domino.Rack(side).Count)
                    + match.Domino.PipTotal(opponent) - match.Domino.PipTotal(side);
            return Evaluate(match.Board, side);
        }

        public bool PlayBoardAction(CardMatch match)
        {
            Side side = match.ActiveSide;
            if (match.Go != null)
            {
                int point = match.Go.ChoosePoint(side, random, Difficulty);
                return point < 0 ? match.PassGo() : match.PlaceStone(point);
            }
            if (match.Domino == null) return match.Move(ChooseMove(match));

            var domino = match.Domino;
            var rack = domino.Rack(side);
            int bestIndex = -1;
            bool bestLeft = false;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < rack.Count; i++)
            {
                for (int end = 0; end < 2; end++)
                {
                    bool left = end == 0;
                    if (!domino.CanPlace(side, i, left)) continue;
                    DominoTile tile = rack[i];
                    int matchingEnd = left ? domino.LeftEnd : domino.RightEnd;
                    int openEnd = tile.Left == matchingEnd ? tile.Right : tile.Left;
                    int support = 0;
                    for (int j = 0; j < rack.Count; j++)
                    {
                        if (i != j && rack[j].Matches(openEnd)) support++;
                    }
                    double score = Difficulty == BotDifficulty.Easy ? random.NextDouble() : tile.Pips * 2 + support * 3 + random.NextDouble();
                    if (Difficulty == BotDifficulty.Hard)
                    {
                        int exposed = 0;
                        foreach (DominoTile played in domino.Chain)
                        {
                            if (played.Left == openEnd) exposed++;
                            if (played.Right == openEnd) exposed++;
                        }
                        // A number already seen often is harder for the other player to match.
                        score += (exposed + support) * (domino.StockCount == 0 ? 4 : 2);
                    }
                    if (score <= bestScore) continue;
                    bestIndex = i;
                    bestLeft = left;
                    bestScore = score;
                }
            }
            if (bestIndex >= 0) return match.PlaceDomino(bestIndex, bestLeft);
            return domino.StockCount > 0 ? match.DrawDomino() : match.PassDomino();
        }

        public ChessMove ChooseMove(CardMatch match)
        {
            Side side = match.ActiveSide;
            var legal = match.Board.LegalMoves(side);
            if (legal.Count == 0) throw new InvalidOperationException("No legal bot move.");
            if (Difficulty == BotDifficulty.Easy) return legal[random.Next(legal.Count)];
            ChessMove best = legal[0]; double bestScore = double.NegativeInfinity;
            foreach (ChessMove move in legal)
            {
                var next = match.Board.Clone(); next.ApplyUnchecked(move);
                Side enemy = ChessPosition.Other(side);
                bool check = next.InCheck(enemy);
                var enemyMoves = next.LegalMoves(enemy);
                if (check && enemyMoves.Count == 0 || next.Mode == GameMode.Checkers && next.ForcedCaptureFrom < 0 && enemyMoves.Count == 0) return move;
                Side following = (next.ForcedCaptureFrom >= 0 || match.Remaining > 1 && !check || match.SkipPending && !check) ? side : enemy;
                List<ChessMove> continuations = following == enemy ? enemyMoves : next.LegalMoves(side);
                double score = Evaluate(next, side);
                if (continuations.Count > 0)
                {
                    score = ReplyValue(next, continuations, following, side, enemy, match.Remaining);
                }
                else if (!check && following == enemy) score = 0; // Stalemate.
                score += random.NextDouble() * 10;
                if (score > bestScore) { bestScore = score; best = move; }
            }
            return best;
        }
        double ReplyValue(ChessPosition board, List<ChessMove> replies, Side actor, Side perspective, Side enemy, int remaining)
        {
            bool maximize = actor == perspective;
            double bound = maximize ? double.NegativeInfinity : double.PositiveInfinity;
            // Keep the extra tactical pass bounded for mobile and browser builds.
            var candidates = Difficulty == BotDifficulty.Hard ? new List<(ChessPosition Board, int Score)>() : null;
            foreach (ChessMove reply in replies)
            {
                var next = board.Clone();
                next.ApplyUnchecked(reply);
                int value = Evaluate(next, perspective);
                if (candidates != null) candidates.Add((next, value));
                else bound = maximize ? Math.Max(bound, value) : Math.Min(bound, value);
            }

            if (candidates == null) return bound;
            candidates.Sort((first, second) => maximize ? second.Score.CompareTo(first.Score) : first.Score.CompareTo(second.Score));
            for (int i = 0; i < Math.Min(4, candidates.Count); i++)
            {
                var candidate = candidates[i];
                bool stays = candidate.Board.ForcedCaptureFrom >= 0 ||
                    (actor == perspective && remaining > 2 && !candidate.Board.InCheck(enemy));
                Side third = stays ? actor : ChessPosition.Other(actor);
                double value = candidate.Score + .75 * TacticalSwing(candidate.Board, third, perspective);
                bound = maximize ? Math.Max(bound, value) : Math.Min(bound, value);
            }
            return bound;
        }

        static int TacticalSwing(ChessPosition board, Side actor, Side perspective)
        {
            int swing = 0;
            foreach (ChessMove move in board.LegalMoves(actor))
            {
                Piece victim = board.Squares[move.To];
                int gain = victim.Type == PieceType.None ? 0 : Values[(int)victim.Type];
                if (board.Mode == GameMode.Checkers)
                {
                    int captured = CheckersRules.CapturedSquare(board, move);
                    gain = captured >= 0 ? Values[(int)board.Squares[captured].Type] : 0;
                }
                if (move.Promotion != PieceType.None)
                    gain += Values[(int)move.Promotion] - Values[(int)board.Squares[move.From].Type];
                swing = Math.Max(swing, gain);
            }
            return actor == perspective ? swing : -swing;
        }

        public static int Evaluate(ChessPosition board, Side perspective)
        {
            int result = 0;
            for (int i = 0; i < 64; i++)
            {
                Piece p = board.Squares[i]; if (p.Type == PieceType.None || CheckersRules.Pending(board, i)) continue;
                int x = i % 8, y = p.Side == Side.White ? i / 8 : 7 - i / 8;
                int bonus = p.Type == PieceType.Pawn || p.Type == PieceType.Checker ? y * 7 : p.Type == PieceType.King ? 0 : 3 * (7 - Math.Abs(2 * x - 7) / 2 - Math.Abs(2 * y - 7) / 2);
                result += (p.Side == perspective ? 1 : -1) * (Values[(int)p.Type] + bonus);
            }
            return result;
        }
    }
}
