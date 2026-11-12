using System;
using System.Collections.Generic;

namespace ChessCards
{
    public enum CardKind { Number, Swap, Undo, Skip }
    public enum MatchPhase { ChooseCard, MovePieces, Finished, GoScoring }
    public struct Card
    {
        public int Color, Number;
        public CardKind Kind;
        public Card(int color, int number) { Color = color; Number = number; Kind = CardKind.Number; }
        public Card(int color, CardKind kind) { Color = color; Number = 0; Kind = kind; }
        public bool Matches(Card top)
        { return Color == top.Color || (Kind == top.Kind && (Kind != CardKind.Number || Number == top.Number)); }
        public string Title
        { get { return Kind == CardKind.Number ? Number.ToString() : Kind == CardKind.Swap ? "ОБМЕН" : Kind == CardKind.Undo ? "ОТМЕНА" : "ПРОПУСК"; } }
        public string GetTitle(bool english)
        { return !english || Kind == CardKind.Number ? Title : Kind == CardKind.Swap ? "SWAP" : Kind == CardKind.Undo ? "UNDO" : "SKIP"; }
        public string GetColorName(bool english)
        { return english ? EnglishColors[Color] : ColorName; }
        static readonly string[] EnglishColors = { "Red", "Blue", "Green", "Yellow" };
        public string ColorName
        { get { return new[] { "Красная", "Синяя", "Зелёная", "Жёлтая" }[Color]; } }
    }

    public sealed class CardMatch
    {
        public ChessPosition Board { get; private set; }
        public GoPosition Go { get; private set; }
        public DominoPosition Domino { get; private set; }
        public GameMode Mode { get; }
        sealed class MoveSnapshot
        {
            public readonly ChessPosition Board;
            public readonly GoPosition Go;
            public readonly DominoPosition Domino;

            public MoveSnapshot(CardMatch match)
            {
                Board = match.Board?.Clone();
                Go = match.Go?.Clone();
                Domino = match.Domino?.Clone();
            }
        }
        public readonly List<Card>[] Hands = { new List<Card>(), new List<Card>() };
        readonly List<Card> deck = new List<Card>();
        readonly List<Card> discard = new List<Card>();
        readonly Stack<MoveSnapshot> history = new Stack<MoveSnapshot>();
        readonly Random random;
        bool[] deadGoStones;
        int goBlackArea, goWhiteArea;
        public Side HumanSide { get; private set; }
        public int Actor { get; private set; }
        public MatchPhase Phase { get; private set; }
        public int Remaining { get; private set; }
        public int Winner { get; private set; } // -1 draw, 0 human, 1 bot; only meaningful in Finished.
        public int Turn { get; private set; }
        public int MovesPlayed { get; private set; }
        public bool SkipPending { get; private set; }
        public string Message { get; private set; }
        public string Result { get; private set; }
        public ChessMove? LastMove { get; private set; }
        public readonly List<string> Log = new List<string>();
        readonly List<string> englishLog = new List<string>();
        string englishMessage, englishResult;
        public IReadOnlyList<string> GetLog(bool english) { return english ? englishLog : Log; }
        public string GetMessage(bool english) { return english ? englishMessage : Message; }
        public string GetResult(bool english) { return english ? englishResult : Result; }
        public int DeckCount { get { return deck.Count; } }
        public int DiscardCount { get { return discard.Count; } }
        public int HistoryCount { get { return history.Count; } }
        public Card Top { get { return discard[discard.Count - 1]; } }
        public Side ActiveSide { get { return SideFor(Actor); } }
        public Side SideFor(int actor) { return actor == 0 ? HumanSide : ChessPosition.Other(HumanSide); }
        public int OwnerOf(Side side) { return side == HumanSide ? 0 : 1; }

        public CardMatch(int seed, ChessPosition position = null, GameMode mode = GameMode.Chess,
            int handSize = 5, int numberCopies = 4, int specialCopies = 2)
        {
            random = new Random(seed);
            Mode = position == null ? mode : position.Mode;
            if (Mode == GameMode.Go) Go = new GoPosition();
            else if (Mode == GameMode.Domino) Domino = new DominoPosition(random);
            else Board = position == null ? ChessPosition.Initial(mode) : position.Clone();
            HumanSide = Mode == GameMode.Go ? Side.Black : Side.White;
            Actor = 0; Winner = -1; Turn = 1;
            numberCopies = Math.Max(1, Math.Min(4, numberCopies));
            specialCopies = Math.Max(0, Math.Min(2, specialCopies));
            for (int color = 0; color < 4; color++)
            {
                for (int n = 1; n <= 3; n++) for (int copy = 0; copy < numberCopies; copy++) deck.Add(new Card(color, n));
                for (int k = 1; k <= 3; k++) for (int copy = 0; copy < specialCopies; copy++) deck.Add(new Card(color, (CardKind)k));
            }
            Shuffle(deck);
            int start = deck.FindIndex(c => c.Kind == CardKind.Number);
            // Keep the original deal order when a starting number remains after dealing.
            int dealCount = Math.Max(1, Math.Min(handSize, (deck.Count - 1) / 2));
            if (start >= deck.Count - dealCount * 2)
            {
                Card first = deck[start];
                deck.RemoveAt(start);
                deck.Insert(0, first);
            }
            for (int actor = 0; actor < 2; actor++)
                for (int n = 0; n < dealCount; n++) Hands[actor].Add(Pop());
            start = deck.FindIndex(c => c.Kind == CardKind.Number);
            discard.Add(deck[start]);
            deck.RemoveAt(start);
            SetMessage("Выбери карту по цвету или значению.", "Choose a card of the same color or value."); CheckStart();
        }
        void Shuffle(List<Card> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); Card t = cards[i]; cards[i] = cards[j]; cards[j] = t; }
        }
        Card Pop() { int last = deck.Count - 1; Card card = deck[last]; deck.RemoveAt(last); return card; }
        void SetMessage(string russian, string english)
        {
            Message = russian;
            englishMessage = english;
            Log.Add(russian);
            englishLog.Add(english);
            if (Log.Count > 60)
            {
                Log.RemoveAt(0);
                englishLog.RemoveAt(0);
            }
        }
        public string UnavailableReason(int index, bool english = false)
        {
            if (Phase != MatchPhase.ChooseCard)
                return english ? "Play an action on the board first." : "Сейчас нужно сделать ход на поле.";
            if (index < 0 || index >= Hands[Actor].Count)
                return english ? "There is no such card." : "Нет такой карты.";
            Card card = Hands[Actor][index];
            if (!card.Matches(Top))
                return english ? "Match the color or value." : "Нужен тот же цвет или то же значение.";
            if (card.Kind == CardKind.Swap && (InCheck(Side.White) || InCheck(Side.Black)))
                return english ? "You cannot swap while a king is in check." : "Обмен недоступен, пока король под шахом.";
            if (card.Kind == CardKind.Undo)
            {
                if (history.Count == 0) return english ? "No move to undo yet." : "Пока нечего отменять.";
                ChessPosition previous = history.Peek().Board;
                if (previous != null)
                {
                    if (previous.InCheck(ChessPosition.Other(ActiveSide)))
                        return english ? "You cannot undo a defense against check and move again." : "Нельзя отменить защиту от шаха и ходить снова.";
                    if (previous.LegalMoves(ActiveSide).Count == 0)
                        return english ? "There would be no legal move after undoing." : "После отмены нет легального перемещения.";
                }
            }
            return "";
        }
        public bool CanPlay(int index) { return UnavailableReason(index).Length == 0; }
        public List<int> PlayableCards()
        {
            var result = new List<int>();
            if (Phase == MatchPhase.ChooseCard)
                for (int i = 0; i < Hands[Actor].Count; i++) if (CanPlay(i)) result.Add(i);
            return result;
        }
        public bool PlayCard(int index)
        {
            if (!CanPlay(index)) return false;
            Card card = Hands[Actor][index]; Hands[Actor].RemoveAt(index); discard.Add(card);
            string effect = ActorName + card.ColorName + " «" + card.Title + "». ";
            string effectEnglish = ActorNameEnglish + card.GetColorName(true) + " " + card.GetTitle(true) + ". ";
            Remaining = card.Kind == CardKind.Number ? card.Number : 1;
            SkipPending = card.Kind == CardKind.Skip;
            if (card.Kind == CardKind.Swap)
            { HumanSide = ChessPosition.Other(HumanSide); effect += "Стороны поменялись. "; effectEnglish += "Sides swapped. "; }
            if (card.Kind == CardKind.Undo)
            {
                var previous = history.Pop();
                Board = previous.Board; Go = previous.Go; Domino = previous.Domino;
                Go?.ClearPass();
                LastMove = null;
                effect += "Последний ход отменён. ";
                effectEnglish += "Last move undone. ";
            }
            Phase = MatchPhase.MovePieces;
            SetMessage(effect + "Перемещений: " + Remaining + ".", effectEnglish + "Moves: " + Remaining + ".");
            CheckStart();
            return true;
        }
        public bool Draw()
        {
            if (Phase != MatchPhase.ChooseCard || PlayableCards().Count != 0) return false;
            if (deck.Count == 0 && discard.Count > 1)
            {
                Card top = Top; discard.RemoveAt(discard.Count - 1);
                deck.AddRange(discard); discard.Clear(); discard.Add(top); Shuffle(deck);
            }
            if (deck.Count > 0)
            {
                Hands[Actor].Add(Pop()); int index = Hands[Actor].Count - 1;
                if (CanPlay(index)) { SetMessage("Добрана подходящая карта — разыграй её.", "The card you drew matches. Play it now."); return true; }
            }
            if (InCheck(ActiveSide))
            {
                Phase = MatchPhase.MovePieces; Remaining = 1;
                SetMessage("Подходящей карты нет, но король под шахом: одно защитное перемещение.", "No matching card. You still get one move to escape check.");
            }
            else { SetMessage("Подходящая карта не пришла. Очередь переходит противнику.", "The card did not match. The other player takes their turn."); EndTurn(); }
            return true;
        }
        public bool Move(ChessMove wanted)
        {
            if (Board == null || Phase != MatchPhase.MovePieces || Remaining <= 0) return false;
            bool found = false;
            foreach (var m in Board.LegalMoves(ActiveSide))
                if (m.From == wanted.From && m.To == wanted.To && m.Promotion == wanted.Promotion) { found = true; break; }
            if (!found) return false;
            Side mover = ActiveSide;
            // All jumps of one checkers capture belong to a single reversible move.
            if (Board.ForcedCaptureFrom < 0) history.Push(new MoveSnapshot(this));
            Board.ApplyUnchecked(wanted); LastMove = wanted;
            if (Mode == GameMode.Checkers && Board.ForcedCaptureFrom >= 0)
            {
                SetMessage(ActorName + wanted + " — продолжи взятие этой же шашкой.", ActorNameEnglish + wanted + " — continue capturing with the same piece.");
                return true; // Do not spend the next card move or hand off mid-capture.
            }
            MovesPlayed++; Remaining--;
            if (Mode == GameMode.Checkers)
            {
                SetMessage(ActorName + wanted + ". Осталось перемещений: " + Remaining + ".", ActorNameEnglish + wanted + ". Moves left: " + Remaining + ".");
                if (Board.LegalMoves(ChessPosition.Other(mover)).Count == 0)
                { Finish(Actor, "У противника нет шашек или допустимых ходов", "The opponent has no pieces or legal moves"); return true; }
                if (Board.WhiteKingQuietMoves >= 15 && Board.BlackKingQuietMoves >= 15)
                { Finish(-1, "По 15 ходов дамками без взятия и движения простых шашек", "Both sides played 15 king moves without a capture or man move"); return true; }
                if (Remaining == 0 || Board.LegalMoves(mover).Count == 0) EndTurn();
                return true;
            }
            Side enemy = ChessPosition.Other(mover);
            bool check = Board.InCheck(enemy);
            var replies = Board.LegalMoves(enemy);
            string moveText = ActorName + wanted + (check ? " — шах!" : "");
            string moveTextEnglish = ActorNameEnglish + wanted + (check ? " — check!" : "");
            SetMessage(moveText, moveTextEnglish);
            if (check && replies.Count == 0) { Finish(Actor, "Мат", "Checkmate"); return true; }
            if (Board.BareKings()) { Finish(-1, "На доске остались только короли", "Only kings remain"); return true; }
            if (Board.QuietMoves >= 100) { Finish(-1, "50 ходов без взятия и движения пешек", "50 moves without a capture or pawn move"); return true; }
            if (check || Remaining == 0 || Board.LegalMoves(mover).Count == 0) EndTurn();
            else
            {
                Message = moveText + " Осталось перемещений: " + Remaining + ".";
                englishMessage = moveTextEnglish + " Moves left: " + Remaining + ".";
            }
            return true;
        }
        bool InCheck(Side side) { return Board != null && Board.InCheck(side); }
        string ActorName { get { return Actor == 0 ? "Ты: " : "Бот: "; } }

        string ActorNameEnglish { get { return Actor == 0 ? "You: " : "Bot: "; } }

        public bool PlaceStone(int point)
        {
            if (Go == null || Phase != MatchPhase.MovePieces || Remaining <= 0) return false;
            var previous = new MoveSnapshot(this);
            if (!Go.Place(ActiveSide, point, out int captured)) return false;
            history.Push(previous);
            int file = point % GoPosition.Size;
            string coordinate = "" + (char)('A' + file + (file >= 8 ? 1 : 0)) + (point / GoPosition.Size + 1);
            CompleteBoardAction(ActorName + coordinate + (captured > 0 ? ", взято камней: " + captured : ""),
                ActorNameEnglish + coordinate + (captured > 0 ? ", stones captured: " + captured : ""));
            if (Remaining == 0) EndTurn();
            return true;
        }

        public bool PassGo()
        {
            if (Go == null || (Phase != MatchPhase.ChooseCard && Phase != MatchPhase.MovePieces)) return false;
            SetMessage(ActorName + "пас.", ActorNameEnglish + "pass.");
            SkipPending = false;
            if (Go.Pass(ActiveSide))
            {
                Remaining = 0;
                Phase = MatchPhase.GoScoring;
                deadGoStones = new bool[GoPosition.PointCount];
                UpdateGoScore();
                SetMessage("Два паса подряд. Начался подсчёт очков.", "Two consecutive passes. Scoring has started.");
            }
            else EndTurn();
            return true;
        }

        public bool IsGoStoneMarked(int point)
        {
            return deadGoStones != null && deadGoStones[point];
        }

        public bool ToggleGoDeadGroup(int point)
        {
            if (Phase != MatchPhase.GoScoring || !Go.ToggleDeadGroup(point, deadGoStones)) return false;
            UpdateGoScore();
            return true;
        }

        void UpdateGoScore()
        {
            Go.CountAreas(out goBlackArea, out goWhiteArea, deadGoStones);
        }

        public string GoScoreText { get { return GetGoScoreText(false); } }

        public string GetGoScoreText(bool english)
        {
            string white = (goWhiteArea + GoPosition.Komi).ToString("0.0",
                System.Globalization.CultureInfo.InvariantCulture);
            return english ? "Black " + goBlackArea + "  ·  White " + white : "Чёрные " + goBlackArea + "  ·  Белые " + white.Replace('.', ',');
        }

        public bool ConfirmGoScore()
        {
            if (Phase != MatchPhase.GoScoring) return false;
            Side winner = goBlackArea > goWhiteArea + GoPosition.Komi ? Side.Black : Side.White;
            Finish(OwnerOf(winner), GoScoreText, GetGoScoreText(true));
            return true;
        }

        public bool ResumeGo()
        {
            if (Phase != MatchPhase.GoScoring) return false;
            deadGoStones = null;
            Go.ClearPass();
            SetMessage("Партия продолжена.", "Play resumed.");
            EndTurn();
            return true;
        }

        public bool PlaceDomino(int index, bool left)
        {
            if (Domino == null || Phase != MatchPhase.MovePieces || Remaining <= 0 || !Domino.CanPlace(ActiveSide, index, left)) return false;
            var previous = new MoveSnapshot(this);
            if (!Domino.Place(ActiveSide, index, left, out DominoTile placed)) return false;
            history.Push(previous);
            CompleteBoardAction(ActorName + placed + (left ? " слева" : " справа"), ActorNameEnglish + placed + (left ? " on the left" : " on the right"));
            if (Domino.Rack(ActiveSide).Count == 0) Finish(Actor, "Все костяшки выложены", "All tiles played");
            else if (!FinishBlockedDomino() && Remaining == 0) EndTurn();
            return true;
        }

        public bool DrawDomino()
        {
            if (Domino == null || Phase != MatchPhase.MovePieces || !Domino.Draw(ActiveSide)) return false;
            SetMessage(ActorName + "добор костяшки из базара.", ActorNameEnglish + "drew from the boneyard.");
            FinishBlockedDomino();
            return true;
        }

        public bool PassDomino()
        {
            if (Domino == null || Phase != MatchPhase.MovePieces || Domino.StockCount > 0 || Domino.HasPlacement(ActiveSide)) return false;
            if (FinishBlockedDomino()) return true;
            SetMessage(ActorName + "нет подходящей костяшки. Пас.", ActorNameEnglish + "no matching tile. Pass.");
            SkipPending = false;
            EndTurn();
            return true;
        }

        bool FinishBlockedDomino()
        {
            if (Domino.StockCount > 0 || Domino.HasPlacement(Side.White) || Domino.HasPlacement(Side.Black)) return false;
            int player = Domino.PipTotal(HumanSide);
            int opponent = Domino.PipTotal(ChessPosition.Other(HumanSide));
            int winner = player == opponent ? -1 : player < opponent ? 0 : 1;
            Finish(winner, "Рыба. Очки на руках: " + player + " у тебя, " + opponent + " у бота",
                "Blocked game. Pips left: " + player + " for you, " + opponent + " for the bot");
            return true;
        }

        void CompleteBoardAction(string russian, string english)
        {
            MovesPlayed++;
            Remaining--;
            SetMessage(russian + ". Осталось ходов: " + Remaining + ".", english + ". Moves left: " + Remaining + ".");
        }

        void EndTurn()
        {
            bool repeat = SkipPending && !InCheck(ChessPosition.Other(ActiveSide));
            SkipPending = false; Remaining = 0; Turn++;
            if (repeat) SetMessage("Противник пропускает очередь. Разыграй следующую карту.", "The opponent skips their turn. Play another card.");
            else Actor = 1 - Actor;
            Phase = MatchPhase.ChooseCard; CheckStart();
        }
        void CheckStart()
        {
            if (Phase == MatchPhase.Finished || Board == null) return;
            if (Board.LegalMoves(ActiveSide).Count == 0)
            {
                if (Mode == GameMode.Checkers) Finish(1 - Actor, "Нет шашек или допустимых ходов", "No pieces or legal moves");
                else
                {
                    bool check = Board.InCheck(ActiveSide);
                    Finish(check ? 1 - Actor : -1, check ? "Мат" : "Пат", check ? "Checkmate" : "Stalemate");
                }
            }
        }
        void Finish(int winner, string reason, string reasonEnglish)
        {
            Phase = MatchPhase.Finished; Winner = winner; Remaining = 0; SkipPending = false;
            Result = winner == 0 ? "Победа! " + reason + "." : winner == 1 ? "Победа бота. " + reason + "." : "Ничья. " + reason + ".";
            englishResult = (winner == 0 ? "You win! " : winner == 1 ? "The bot wins. " : "Draw. ") + reasonEnglish + ".";
            SetMessage(Result, englishResult);
        }
        public void Resign()
        { if (Phase != MatchPhase.Finished) Finish(1 - Actor, "Сдача", "Resignation"); }
        public ChessPosition UndoPreview() { return history.Count == 0 ? null : history.Peek().Board?.Clone(); }
    }

    [Serializable]
    public sealed class RestorationProgress
    {
        public int Coins, Wins;
        public bool Bench, Lamp, House;
        public static readonly int[] Prices = { 60, 100, 180 };
        public bool Repaired(int index) { return index == 0 ? Bench : index == 1 ? Lamp : House; }
        public bool Repair(int index)
        {
            return index >= 0 && index < Prices.Length && Repair(index, Prices[index]);
        }

        public bool Repair(int index, int price)
        {
            if (index < 0 || index >= Prices.Length || price < 0 || Repaired(index) || Coins < price) return false;
            Coins -= price;
            if (index == 0) Bench = true; else if (index == 1) Lamp = true; else House = true;
            return true;
        }
        public void AwardWin(int reward = 100)
        {
            Coins = (int)Math.Min(int.MaxValue, (long)Coins + Math.Max(0, reward));
            if (Wins < int.MaxValue) Wins++;
        }
    }
}
