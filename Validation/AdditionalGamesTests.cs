using System;
using ChessCards;

static class AdditionalGamesTests
{
    static int checks;

    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    static int Point(int x, int y) { return x + y * GoPosition.Size; }

    static void GiveCard(CardMatch match, Card card)
    {
        match.Hands[match.Actor].Clear();
        match.Hands[match.Actor].Add(card);
        Check(match.PlayCard(0), "Fixture card must be playable");
    }

    static void GoRules()
    {
        var go = new GoPosition();
        Check(go.Place(Side.White, Point(1, 1), out _), "Ko target");
        foreach (int point in new[] { Point(0, 1), Point(1, 0), Point(2, 1) })
            Check(go.Place(Side.Black, point, out _), "Ko black surround");
        foreach (int point in new[] { Point(0, 2), Point(2, 2), Point(1, 3) })
            Check(go.Place(Side.White, point, out _), "Ko white surround");
        var previous = go.Clone();
        Check(go.Place(Side.Black, Point(1, 2), out int captured) && captured == 1, "Ko capture");
        Check(go.StoneAt(Point(1, 1)) == null && previous.StoneAt(Point(1, 1)) == Side.White, "Capture and snapshot");
        Check(!go.CanPlace(Side.White, Point(1, 1)) && !go.Clone().CanPlace(Side.White, Point(1, 1)), "Ko survives cloning");
        Check(go.Place(Side.White, Point(8, 8), out _) && go.Place(Side.White, Point(1, 1), out captured) && captured == 1, "Recapture after position changes");

        go = new GoPosition();
        foreach (int point in new[] { Point(4, 3), Point(3, 4), Point(5, 4), Point(4, 5) })
            Check(go.Place(Side.Black, point, out _), "Suicide fixture");
        Check(!go.Place(Side.White, Point(4, 4), out _), "Suicide rejected");
        Check(go.Place(Side.White, Point(8, 8), out _), "Place white outside");
        Check(go.Area(Side.Black) == 5 && go.Area(Side.White) == 1, "Surrounded point counts, shared region is neutral");

        var match = new CardMatch(7, mode: GameMode.Go);
        Check(match.ActiveSide == Side.Black && !match.PlaceStone(0), "Black starts and placement requires a card");
        GiveCard(match, new Card(match.Top.Color, 3));
        for (int i = 0; i < 3; i++) Check(match.PlaceStone(i), "Three stones with card 3");
        Check(match.Actor == 1 && match.Go.Occupied == 3, "Third placement hands over");
        GiveCard(match, new Card(match.Top.Color, CardKind.Undo));
        Check(match.Go.Occupied == 2 && match.Go.StoneAt(2) == null && match.Remaining == 1, "Undo restores Go board");
        Check(match.PassGo() && match.PassGo() && match.Phase == MatchPhase.GoScoring, "Two passes open scoring");
        Check(match.ConfirmGoScore() && match.Winner == 0, "Black wins occupied Go board");
        match = new CardMatch(9, mode: GameMode.Go);
        Check(match.PassGo() && match.PassGo() && match.ConfirmGoScore() && match.Winner == 1, "White wins empty Go board by komi");
    }

    static void GoScoring()
    {
        var match = new CardMatch(4, mode: GameMode.Go);
        for (int y = 1; y <= 4; y++)
        for (int x = 1; x <= 4; x++)
            if (x == 1 || x == 4 || y == 1 || y == 4)
                Check(match.Go.Place(Side.Black, Point(x, y), out _), "Scoring fixture black border");
        int first = Point(2, 2), second = Point(2, 3), live = Point(8, 8);
        foreach (int point in new[] { first, second, live })
            Check(match.Go.Place(Side.White, point, out _), "Scoring fixture white group");
        Check(!match.ToggleGoDeadGroup(first) && !match.ConfirmGoScore(), "Scoring actions unavailable in play");
        Check(match.PassGo() && match.Go.AcceptPass(match.ActiveSide) == false && match.PassGo(), "Pass with unsettled groups");
        Check(match.GoScoreText == "Чёрные 12  ·  Белые 10,5", "Score before marking the group");
        Check(!match.PassGo() && !match.PlaceStone(0) && !match.ToggleGoDeadGroup(0), "Scoring guards");
        Check(match.ToggleGoDeadGroup(first) && match.IsGoStoneMarked(first) &&
            match.IsGoStoneMarked(second) && !match.IsGoStoneMarked(live), "Click marks only the connected group");
        Check(match.GoScoreText == "Чёрные 16  ·  Белые 8,5", "Dead group excluded and territory recomputed");
        Check(match.ToggleGoDeadGroup(second) && !match.IsGoStoneMarked(first) &&
            match.GoScoreText == "Чёрные 12  ·  Белые 10,5", "Click again restores the group and score");
        Check(match.ToggleGoDeadGroup(first) && match.ResumeGo() && match.Actor == 0 &&
            match.Phase == MatchPhase.ChooseCard && match.Go.LastPass == null, "Resume with the opponent of the last passer");
        Check(match.Go.StoneAt(first) == Side.White && !match.IsGoStoneMarked(first), "Resume preserves stones and clears marks");
        Check(match.PassGo() && match.Phase == MatchPhase.ChooseCard && match.PassGo(), "Resume resets the pass sequence");
        Check(match.ToggleGoDeadGroup(first) && match.ConfirmGoScore() && match.Phase == MatchPhase.Finished &&
            match.Winner == 0 && match.Result.Contains("Белые 8,5") && !match.ConfirmGoScore(), "Confirmed score determines the winner once");

        match = new CardMatch(5, mode: GameMode.Go);
        Check(match.PassGo(), "Pass before a placement");
        GiveCard(match, new Card(match.Top.Color, 1));
        Check(match.PlaceStone(0), "Placement interrupts the pass streak");
        GiveCard(match, new Card(match.Top.Color, CardKind.Undo));
        Check(match.Go.LastPass == null && match.PassGo() && match.Phase == MatchPhase.ChooseCard,
            "Undo of a placement does not restore an old pass");
    }

    static void DominoRules()
    {
        var match = new CardMatch(9, mode: GameMode.Domino);
        Check(match.Domino.StockCount == 14 && match.Domino.Rack(Side.White).Count == 7 &&
            match.Domino.Rack(Side.Black).Count == 7, "Initial domino deal");
        Check(!match.PlaceDomino(0, true) && !match.DrawDomino() && !match.PassDomino(), "Domino actions require a card");
        GiveCard(match, new Card(match.Top.Color, 1));
        Check(!match.DrawDomino() && match.PlaceDomino(0, true), "Cannot draw when a tile fits");
        GiveCard(match, new Card(match.Top.Color, CardKind.Undo));
        Check(match.Domino.Chain.Count == 0 && match.Domino.Rack(Side.White).Count == 7, "Undo returns the tile to its rack");
        Check(match.PlaceDomino(0, false), "Can open chain from either end");
        Side side = match.HumanSide;
        GiveCard(match, new Card(match.Top.Color, CardKind.Swap));
        Check(match.HumanSide != side && match.Domino.Rack(match.ActiveSide).Count == 6, "Swap exchanges domino racks");
    }

    static void CompleteMatches()
    {
        int stockDraws = 0, blocked = 0, emptyRack = 0;
        foreach (GameMode mode in new[] { GameMode.Go, GameMode.Domino })
        foreach (int seed in new[] { 0, 1, 2, 3, 4, 5, 6, 11 })
        {
            var match = new CardMatch(seed, mode: mode);
            var bot = new ChessBot(seed + 400);
            for (int actions = 0; actions < 900 && match.Phase != MatchPhase.Finished; actions++)
            {
                if (match.Phase == MatchPhase.GoScoring)
                    Check(match.ConfirmGoScore(), "Bot match confirms Go score");
                else if (match.Phase == MatchPhase.ChooseCard)
                {
                    int card = bot.ChooseCard(match);
                    Check(card < 0 ? match.Draw() : match.PlayCard(card), mode + " bot card step");
                }
                else
                {
                    int oldStock = match.Domino?.StockCount ?? 0;
                    int oldRemaining = match.Remaining;
                    Check(bot.PlayBoardAction(match), mode + " bot board step");
                    if (match.Domino != null && match.Domino.StockCount < oldStock)
                    {
                        stockDraws++;
                        Check(match.Remaining == oldRemaining || match.Phase == MatchPhase.Finished, "Domino draw keeps action budget");
                    }
                }

                if (match.Domino == null) continue;
                var domino = match.Domino;
                Check(domino.Chain.Count + domino.StockCount + domino.Rack(Side.White).Count +
                    domino.Rack(Side.Black).Count == 28, "All dominoes remain accounted for");
                for (int i = 1; i < domino.Chain.Count; i++)
                    Check(domino.Chain[i - 1].Right == domino.Chain[i].Left, "Domino ends connect");
            }
            Check(match.Phase == MatchPhase.Finished, mode + " match did not finish, seed " + seed);
            if (mode == GameMode.Go) continue;
            var result = match.Domino;
            if (result.Rack(match.SideFor(0)).Count == 0 || result.Rack(match.SideFor(1)).Count == 0)
            {
                emptyRack++;
                Check(match.Winner >= 0 && result.Rack(match.SideFor(match.Winner)).Count == 0, "Empty rack wins");
            }
            else
            {
                blocked++;
                int humanPips = result.PipTotal(match.SideFor(0)), botPips = result.PipTotal(match.SideFor(1));
                Check(result.StockCount == 0 && !result.HasPlacement(Side.White) && !result.HasPlacement(Side.Black), "Blocked domino finish");
                Check(match.Winner == (humanPips == botPips ? -1 : humanPips < botPips ? 0 : 1), "Fewer pips wins a blocked round");
            }
        }
        Check(stockDraws > 0 && emptyRack > 0 && blocked > 0, "Domino test seeds cover both endings and stock draws");
    }

    public static int Run()
    {
        GoRules();
        GoScoring();
        DominoRules();
        CompleteMatches();
        Console.WriteLine("PASS Go capture, suicide, ko, scoring and undo; domino draw, undo, swap and scoring; 16 completed new-mode bot matches");
        return checks;
    }
}
