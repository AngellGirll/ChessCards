using System;
using System.Collections.Generic;
using ChessCards;

static class CoreTests
{
    static int checks;
    static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    static int S(string name) { return name[0] - 'a' + (name[1] - '1') * 8; }
    static ChessMove M(string from, string to, PieceType promotion = PieceType.None) { return new ChessMove(S(from), S(to), promotion); }
    static bool Legal(ChessPosition p, Side side, string from, string to, PieceType promotion = PieceType.None)
    { return p.LegalMoves(side).Exists(m => m.From == S(from) && m.To == S(to) && m.Promotion == promotion); }
    static void Make(ChessPosition p, Side side, string from, string to)
    { Check(Legal(p, side, from, to), "Expected legal move " + from + to); p.ApplyUnchecked(M(from,to)); }
    static ChessPosition Position(params string[] pieces)
    {
        var p = new ChessPosition { CastleRights = 0 };
        foreach (string item in pieces)
        {
            PieceType type = item[1] == 'K' ? PieceType.King : item[1] == 'Q' ? PieceType.Queen : item[1] == 'R' ? PieceType.Rook : item[1] == 'B' ? PieceType.Bishop : item[1] == 'N' ? PieceType.Knight : PieceType.Pawn;
            p.Squares[S(item.Substring(2))] = new Piece(type, item[0] == 'w' ? Side.White : Side.Black);
        }
        return p;
    }
    static long Perft(ChessPosition p, Side side, int depth)
    {
        if (depth == 0) return 1;
        long result = 0;
        foreach (var m in p.LegalMoves(side)) { var n = p.Clone(); n.ApplyUnchecked(m); result += Perft(n, ChessPosition.Other(side), depth-1); }
        return result;
    }
    static void SetCard(CardMatch m, CardKind kind, int number = 1)
    {
        m.Hands[m.Actor].Clear();
        m.Hands[m.Actor].Add(kind == CardKind.Number ? new Card(m.Top.Color, number) : new Card(m.Top.Color, kind));
        Check(m.PlayCard(0), "Could not play " + kind + ": " + m.UnavailableReason(0));
    }
    static void Move(CardMatch m, string from, string to)
    { Check(m.Move(M(from, to)), "Match rejected " + from + to); }
    static void Main()
    {
        Check(Perft(ChessPosition.Initial(), Side.White, 1) == 20, "Perft 1");
        Check(Perft(ChessPosition.Initial(), Side.White, 2) == 400, "Perft 2");
        Check(Perft(ChessPosition.Initial(), Side.White, 3) == 8902, "Perft 3");
        Check(Perft(ChessPosition.Initial(), Side.White, 4) == 197281, "Perft 4");
        Console.WriteLine("PASS standard chess initial perft depths 1–4");
        var pin = Position("wKe1", "wRe2", "bRe8", "bKa8");
        Check(!Legal(pin, Side.White, "e2", "d2"), "Pinned rook moved");
        var p = Position("wKe1", "wRa1", "wRh1", "bKe8"); p.CastleRights = 3;
        Check(Legal(p,Side.White,"e1","g1") && Legal(p,Side.White,"e1","c1"), "Castling missing");
        p.Squares[S("f8")] = new Piece(PieceType.Rook,Side.Black);
        Check(!Legal(p,Side.White,"e1","g1"), "Castling through check");
        p.Squares[S("f8")] = new Piece(); p.ApplyUnchecked(M("e1","c1"));
        Check(p.Squares[S("d1")].Type == PieceType.Rook && p.CastleRights == 0, "Castling rook/rights");
        p = Position("wKe1", "wPe5", "bPd7", "bKe8");
        Make(p,Side.Black,"d7","d5"); Check(Legal(p,Side.White,"e5","d6"),"En passant missing");
        var ep = p.Clone(); ep.ApplyUnchecked(M("e5","d6"));
        Check(ep.Squares[S("d5")].Type == PieceType.None,"En passant did not remove pawn");
        Make(p,Side.White,"e1","f1"); Check(!Legal(p,Side.White,"e5","d6"),"En passant did not expire");
        p = Position("wKh1", "wPa7", "bKh8");
        Check(p.LegalMoves(Side.White).FindAll(m=>m.From == S("a7") && m.To == S("a8")).Count == 4,"Four promotions missing");
        Check(!Legal(p,Side.White,"a7","a8"),"Promotion must specify piece");
        p.ApplyUnchecked(M("a7","a8",PieceType.Knight)); Check(p.Squares[S("a8")].Type == PieceType.Knight,"Promotion incorrect");
        Console.WriteLine("PASS pins, castling, en passant expiry, four promotion choices");

        var match = new CardMatch(25); Check(match.Hands[0].Count == 5 && match.Hands[1].Count == 5,"Initial hands");
        Check(match.DeckCount + match.DiscardCount + match.Hands[0].Count + match.Hands[1].Count == 72,"Deck total");
        SetCard(match,CardKind.Number,3);
        Move(match,"e2","e4"); Move(match,"e4","e5");
        Check(match.Actor==0 && match.Remaining==1,"Consecutive moves"); Move(match,"g1","f3");
        Check(match.Actor==1 && match.Phase==MatchPhase.ChooseCard,"Turn handoff");
        var beforeSwap = match.Board.Clone(); SetCard(match,CardKind.Swap);
        Check(match.HumanSide == Side.Black && match.ActiveSide==Side.White && match.Actor==1,"Ownership swap incorrect");
        for(int i=0;i<64;i++) Check(match.Board.Squares[i].Type==beforeSwap.Squares[i].Type && match.Board.Squares[i].Side==beforeSwap.Squares[i].Side,"Swap moved a piece");
        Move(match,"d2","d4"); Check(match.Actor==0 && match.ActiveSide==Side.Black,"Swap handoff incorrect");
        Console.WriteLine("PASS card sequences, repeat same piece, army ownership swap");

        match = new CardMatch(3, Position("wKh1","wRa2","bKh8","bPb2"));
        SetCard(match,CardKind.Number); Move(match,"a2","b2");
        SetCard(match,CardKind.Undo);
        Check(match.Board.Squares[S("a2")].Type==PieceType.Rook && match.Board.Squares[S("b2")].Type==PieceType.Pawn,"Undo capture restoration");
        Check(match.Actor==1 && match.Remaining==1 && match.HistoryCount==0,"Undo phase");
        Move(match,"h8","h7");
        match = new CardMatch(4); SetCard(match,CardKind.Skip); Move(match,"e2","e4");
        Check(match.Actor==0 && match.Phase==MatchPhase.ChooseCard,"Skip did not repeat turn");
        match = new CardMatch(4, Position("wKa1","wRa2","bKe8","bPa7"));
        SetCard(match,CardKind.Skip); Move(match,"a2","e2");
        Check(match.Actor==1 && match.Board.InCheck(Side.Black),"Skip suppressed defense");
        match.Hands[1].Clear(); match.Hands[1].Add(new Card(match.Top.Color,CardKind.Swap));
        Check(!match.CanPlay(0),"Swap allowed during check");
        match = new CardMatch(4, Position("wKa1","wRa2","bKe8","bPa7"));
        SetCard(match,CardKind.Number,3); Move(match,"a2","e2");
        Check(match.Actor==1 && match.Remaining==0,"Check did not burn moves");
        Console.WriteLine("PASS undo capture, skip, check interrupts sequence and skip");

        match = new CardMatch(4, Position("wKf6","wQg6","bKh8"));
        SetCard(match,CardKind.Number,3); Move(match,"g6","g7");
        Check(match.Phase==MatchPhase.Finished && match.Winner==0,"Mate not detected immediately");
        Check(!match.Move(M("f6","f7")) && !match.Draw() && !match.PlayCard(0),"Finished match accepts actions");
        var stalemate = new CardMatch(4, Position("wKh1","bKf2","bQg3"));
        Check(stalemate.Phase==MatchPhase.Finished && stalemate.Winner==-1 && stalemate.Result.Contains("Пат"),"Stalemate");
        var wallet = new RestorationProgress(); wallet.AwardWin();
        Check(wallet.Coins==100 && wallet.Wins==1,"Reward");
        Check(wallet.Repair(0) && wallet.Coins==40 && !wallet.Repair(0) && !wallet.Repair(2),"Purchase protection");
        Console.WriteLine("PASS immediate mate, stalemate, finished-state guards, repair economy");

        // Deterministically find both successful and unsuccessful draw paths, including check exception.
        bool foundPlayable=false, foundFailed=false, foundDefense=false;
        for(int seed=0;seed<200 && !(foundPlayable&&foundFailed&&foundDefense);seed++)
        {
            match = new CardMatch(seed); match.Hands[0].Clear(); Check(match.Draw(),"Draw blocked with empty hand");
            if(match.Actor==0 && match.Phase==MatchPhase.ChooseCard) { foundPlayable=true; Check(match.CanPlay(0),"Draw stayed without playable card"); }
            if(match.Actor==1) foundFailed=true;
            var checkedMatch = new CardMatch(seed,Position("wKe1","bRe8","bKa8","wPa2"));
            checkedMatch.Hands[0].Clear(); checkedMatch.Draw();
            if(checkedMatch.Phase==MatchPhase.MovePieces)
            { foundDefense=true; Check(checkedMatch.Actor==0 && checkedMatch.Remaining==1,"Check draw skipped defense"); }
        }
        Check(foundPlayable && foundFailed && foundDefense,"Draw branches not exercised");
        Console.WriteLine("PASS successful draw, failed draw, mandatory defense without card");

        // Bots play actual games to exercise card recycling, undo, swaps and legal state transitions.
        int simulated=0, recycled=0, finished=0;
        for(int seed=1;seed<=8;seed++)
        {
            match = new CardMatch(seed); var bot = new ChessBot(seed+900); int previousDeck=match.DeckCount;
            for(int action=0;action<450 && match.Phase!=MatchPhase.Finished;action++)
            {
                if(match.Phase==MatchPhase.ChooseCard)
                { int card=bot.ChooseCard(match); Check(card<0 ? match.Draw() : match.PlayCard(card),"Bot card action failed"); }
                else
                { Side mover=match.ActiveSide; Check(match.Move(bot.ChooseMove(match)),"Bot illegal move"); Check(!match.Board.InCheck(mover),"Bot left own king checked"); }
                int whiteKings=0,blackKings=0;
                foreach(var piece in match.Board.Squares) if(piece.Type==PieceType.King) { if(piece.Side==Side.White) whiteKings++; else blackKings++; }
                Check(whiteKings==1 && blackKings==1,"King disappeared");
                if(match.DeckCount > previousDeck) recycled++;
                previousDeck=match.DeckCount; simulated++;
            }
            if(match.Phase==MatchPhase.Finished) finished++;
        }
        Console.WriteLine("PASS bot simulation: " + simulated + " actions, " + finished + " finished games, " + recycled + " deck recycles");
        Console.WriteLine("ALL PASSED — " + checks + " assertions");
    }
}
