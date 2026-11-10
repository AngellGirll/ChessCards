using System;
using System.Linq;
using ChessCards;

static class CheckersTests
{
    static int checks;
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception("Checkers: " + label); }
    static int S(string name) { return name[0] - 'a' + (name[1] - '1') * 8; }
    static ChessMove M(string from, string to) { return new ChessMove(S(from), S(to)); }
    static bool Legal(ChessPosition p, Side side, string from, string to)
    { return p.LegalMoves(side).Any(m => m.From == S(from) && m.To == S(to)); }
    static ChessPosition P(params string[] entries)
    {
        var p = new ChessPosition(GameMode.Checkers) { CastleRights = 0 };
        foreach (var e in entries) p.Squares[S(e.Substring(2))] = new Piece(e[1] == 'K' ? PieceType.CrownedChecker : PieceType.Checker, e[0] == 'w' ? Side.White : Side.Black);
        return p;
    }
    static void Card(CardMatch m, CardKind kind, int number = 1)
    {
        m.Hands[m.Actor].Clear(); m.Hands[m.Actor].Add(kind == CardKind.Number ? new Card(m.Top.Color, number) : new Card(m.Top.Color, kind));
        Check(m.PlayCard(0), "Could not play " + kind);
    }
    public static int Run()
    {
        checks = 0;
        var p = ChessPosition.Initial(GameMode.Checkers);
        Check(p.Squares.Count(x => x.Type == PieceType.Checker && x.Side == Side.White) == 12, "White setup");
        Check(p.Squares.Count(x => x.Type == PieceType.Checker && x.Side == Side.Black) == 12, "Black setup");
        Check(p.LegalMoves(Side.White).Count == 7, "Seven opening moves");
        Check(!p.InCheck(Side.White) && !p.BareKings(), "Chess end rules leaked into checkers");
        Check(Legal(p, Side.White, "c3", "d4") && !Legal(p, Side.White, "c3", "e5"), "Man quiet move geometry");
        p = P("wMc5", "bMd4");
        Check(Legal(p, Side.White, "c5", "e3"), "Man must capture backwards");
        p = P("wMa3", "wMc3", "bMd4");
        Check(p.LegalMoves(Side.White).Count == 1 && !Legal(p, Side.White, "a3", "b4"), "Capture not mandatory globally");
        p = P("wMc3", "bMb4", "bMd4", "bMf6");
        Check(Legal(p, Side.White, "c3", "a5") && Legal(p, Side.White, "c3", "e5"), "Russian rules must permit short and long capture branches");
        p = P("wKa1", "bMh8");
        Check(Legal(p, Side.White, "a1", "f6"), "King cannot fly");
        p = P("wKa1", "bMc3", "bMe5");
        Check(Legal(p, Side.White, "a1", "d4") && !Legal(p, Side.White, "a1", "f6"), "King jumped two victims in a single leg");
        p = P("wKd4", "bMe5", "bMc3"); p.ApplyUnchecked(M("d4", "f6"));
        Check(p.ForcedCaptureFrom == -1 && p.Squares[S("c3")].Type == PieceType.Checker, "Captured victim must block reversal until chain ends");
        p = P("wMb6", "bMc7", "bMf6"); p.ApplyUnchecked(M("b6", "d8"));
        Check(p.Squares[S("d8")].Type == PieceType.CrownedChecker && p.ForcedCaptureFrom == S("d8"), "Mid-chain promotion");
        Check(Legal(p, Side.White, "d8", "g5") && Legal(p, Side.White, "d8", "h4"), "New king must continue with flying captures");
        p.ApplyUnchecked(M("d8", "g5"));
        Check(p.PendingCaptured == 0 && p.ForcedCaptureFrom == -1 && p.Squares[S("c7")].Type == PieceType.None && p.Squares[S("f6")].Type == PieceType.None, "Finish promoted capture");
        p = P("wMb6", "bMh8"); p.ApplyUnchecked(M("b6", "a7")); p.ApplyUnchecked(M("a7", "b8"));
        Check(p.Squares[S("b8")].Type == PieceType.CrownedChecker, "Quiet promotion");
        Console.WriteLine("PASS checkers setup, mandatory/backward captures, flying kings, capture choice, Turkish strike, crowning");

        var match = new CardMatch(9, P("wMc3", "wMa1", "bMd4", "bMf6", "bMh8"));
        Card(match, CardKind.Number);
        Check(match.Move(M("c3", "e5")), "First jump rejected");
        Check(match.Actor == 0 && match.Remaining == 1 && match.MovesPlayed == 0 && match.HistoryCount == 1, "Chain spent card budget or changed actor");
        Check(CheckersRules.Pending(match.Board, S("d4")) && match.Board.Squares[S("d4")].Type != PieceType.None, "Captured piece lifted before chain finished");
        var clone = match.Board.Clone();
        Check(clone.Mode == GameMode.Checkers && clone.PendingCaptured == match.Board.PendingCaptured && clone.ForcedCaptureFrom == S("e5"), "Clone lost capture state");
        Check(!match.Move(M("a1", "b2")) && !match.Draw() && !match.PlayCard(0), "Changed piece/card mid-chain");
        Check(match.Move(M("e5", "g7")), "Second jump rejected");
        Check(match.Actor == 1 && match.MovesPlayed == 1 && match.HistoryCount == 1 && match.Board.PendingCaptured == 0, "Chain is not one completed move");
        Card(match, CardKind.Undo);
        Check(match.Board.Squares[S("c3")].Type == PieceType.Checker && match.Board.Squares[S("d4")].Type == PieceType.Checker && match.Board.Squares[S("f6")].Type == PieceType.Checker && match.Board.ForcedCaptureFrom == -1, "Undo must restore whole capture chain");
        match = new CardMatch(10, null, GameMode.Checkers); Card(match, CardKind.Number, 3);
        Check(match.Move(M("c3", "d4")) && match.Move(M("d4", "e5")) && match.Actor == 0 && match.Remaining == 1, "Numeric card moves same man repeatedly");
        Check(match.Move(M("a3", "b4")) && match.Actor == 1, "Number card did not hand off");
        var before = match.Board.Clone(); Card(match, CardKind.Swap);
        Check(match.Mode == GameMode.Checkers && match.HumanSide == Side.Black && match.ActiveSide == Side.White, "Checkers ownership swap");
        for (int i = 0; i < 64; i++) Check(match.Board.Squares[i].Type == before.Squares[i].Type && match.Board.Squares[i].Side == before.Squares[i].Side, "Swap moved checkers physically");
        match = new CardMatch(12, null, GameMode.Checkers); Card(match, CardKind.Skip); Check(match.Move(M("a3", "b4")) && match.Actor == 0, "Checkers skip");
        match = new CardMatch(12, P("wMc3", "bMa1")); Card(match, CardKind.Number); Check(match.Move(M("c3", "d4")) && match.Phase == MatchPhase.Finished && match.Winner == 0, "Blocked opponent is a loss, not a draw");
        match = new CardMatch(12, P("wMc3", "bMd4")); Card(match, CardKind.Number); Check(match.Move(M("c3", "e5")) && match.Phase == MatchPhase.Finished && match.Winner == 0, "All enemy checkers captured");
        p = P("wKa1", "bKh8"); p.WhiteKingQuietMoves = 14; p.BlackKingQuietMoves = 15;
        match = new CardMatch(12, p); Card(match, CardKind.Number); Check(match.Move(M("a1", "b2")) && match.Phase == MatchPhase.Finished && match.Winner == -1, "Quiet-king draw");
        Console.WriteLine("PASS checkers card budgets, chain lock, whole-chain undo, swap, skip, wins and draw");

        int actions = 0, completed = 0;
        for (int seed = 1; seed <= 8; seed++)
        {
            match = new CardMatch(seed, null, GameMode.Checkers); var bot = new ChessBot(seed + 400);
            for (int step = 0; step < 500 && match.Phase != MatchPhase.Finished; step++)
            {
                if (match.Phase == MatchPhase.ChooseCard)
                { int card = bot.ChooseCard(match); Check(card < 0 ? match.Draw() : match.PlayCard(card), "Bot card"); }
                else
                {
                    int actor = match.Actor, remaining = match.Remaining;
                    Check(match.Move(bot.ChooseMove(match)), "Bot move");
                    if (match.Board.ForcedCaptureFrom >= 0) Check(match.Actor == actor && match.Remaining == remaining, "Bot handed off in capture");
                }
                for (int i = 0; i < 64; i++)
                    if (match.Board.Squares[i].Type != PieceType.None)
                        Check((i % 8 + i / 8) % 2 == 0 && (match.Board.Squares[i].Type == PieceType.Checker || match.Board.Squares[i].Type == PieceType.CrownedChecker), "Illegal checker/piece square");
                actions++;
            }
            if (match.Phase == MatchPhase.Finished) completed++;
        }
        Check(completed > 0, "No bot game reached an outcome");
        Console.WriteLine("PASS checkers bot simulation: " + actions + " actions, " + completed + "/8 completed games");
        return checks;
    }
}
