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
        public int DeckCount { get { return deck.Count; } }
        public int DiscardCount { get { return discard.Count; } }
        public int HistoryCount { get { return history.Count; } }
        public Card Top { get { return discard[discard.Count - 1]; } }
        public Side ActiveSide { get { return SideFor(Actor); } }
        public Side SideFor(int actor) { return actor == 0 ? HumanSide : ChessPosition.Other(HumanSide); }
        public int OwnerOf(Side side) { return side == HumanSide ? 0 : 1; }

        public CardMatch(int seed, ChessPosition position = null, GameMode mode = GameMode.Chess)
        {
            random = new Random(seed);
            Mode = position == null ? mode : position.Mode;
            if (Mode == GameMode.Go) Go = new GoPosition();
            else if (Mode == GameMode.Domino) Domino = new DominoPosition(random);
            else Board = position == null ? ChessPosition.Initial(mode) : position.Clone();
            HumanSide = Mode == GameMode.Go ? Side.Black : Side.White;
            Actor = 0; Winner = -1; Turn = 1;
            for (int color = 0; color < 4; color++)
            {
                for (int n = 1; n <= 3; n++) for (int copy = 0; copy < 4; copy++) deck.Add(new Card(color, n));
                for (int k = 1; k <= 3; k++) for (int copy = 0; copy < 2; copy++) deck.Add(new Card(color, (CardKind)k));
            }
            Shuffle(deck);
            for (int actor = 0; actor < 2; actor++) for (int n = 0; n < 5; n++) Hands[actor].Add(Pop());
            int start = deck.FindIndex(c => c.Kind == CardKind.Number);
            discard.Add(deck[start]); deck.RemoveAt(start);
            SetMessage("Выбери карту по цвету или значению."); CheckStart();
        }
        void Shuffle(List<Card> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); Card t = cards[i]; cards[i] = cards[j]; cards[j] = t; }
        }
        Card Pop() { int last = deck.Count - 1; Card card = deck[last]; deck.RemoveAt(last); return card; }
        void SetMessage(string text)
        {
            Message = text; Log.Add(text); if (Log.Count > 60) Log.RemoveAt(0);
        }
        public string UnavailableReason(int index)
        {
            if (Phase != MatchPhase.ChooseCard) return "Сейчас нужно перемещать фигуры.";
            if (index < 0 || index >= Hands[Actor].Count) return "Нет такой карты.";
            Card card = Hands[Actor][index];
            if (!card.Matches(Top)) return "Нужен тот же цвет или то же значение.";
            if (card.Kind == CardKind.Swap && (InCheck(Side.White) || InCheck(Side.Black)))
                return "Обмен недоступен, пока король под шахом.";
            if (card.Kind == CardKind.Undo)
            {
                if (history.Count == 0) return "Пока нечего отменять.";
                ChessPosition previous = history.Peek().Board;
                if (previous != null)
                {
                    if (previous.InCheck(ChessPosition.Other(ActiveSide))) return "Нельзя отменить защиту от шаха и ходить снова.";
                    if (previous.LegalMoves(ActiveSide).Count == 0) return "После отмены нет легального перемещения.";
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
            string effect = (Actor == 0 ? "Ты: " : "Бот: ") + card.ColorName + " «" + card.Title + "». ";
            Remaining = card.Kind == CardKind.Number ? card.Number : 1;
            SkipPending = card.Kind == CardKind.Skip;
            if (card.Kind == CardKind.Swap)
            { HumanSide = ChessPosition.Other(HumanSide); effect += "Стороны поменялись. "; }
            if (card.Kind == CardKind.Undo)
            {
                var previous = history.Pop();
                Board = previous.Board; Go = previous.Go; Domino = previous.Domino;
                Go?.ClearPass();
                LastMove = null;
                effect += "Последний ход отменён. ";
            }
            Phase = MatchPhase.MovePieces;
            SetMessage(effect + "Перемещений: " + Remaining + ".");
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
                if (CanPlay(index)) { SetMessage("Добрана подходящая карта — разыграй её."); return true; }
            }
            if (InCheck(ActiveSide))
            {
                Phase = MatchPhase.MovePieces; Remaining = 1;
                SetMessage("Подходящей карты нет, но король под шахом: одно защитное перемещение.");
            }
            else { SetMessage("Подходящая карта не пришла. Очередь переходит противнику."); EndTurn(); }
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
                SetMessage((Actor == 0 ? "Ты: " : "Бот: ") + wanted + " — продолжи взятие этой же шашкой.");
                return true; // Do not spend the next card move or hand off mid-capture.
            }
            MovesPlayed++; Remaining--;
            if (Mode == GameMode.Checkers)
            {
                SetMessage((Actor == 0 ? "Ты: " : "Бот: ") + wanted + ". Осталось перемещений: " + Remaining + ".");
                if (Board.LegalMoves(ChessPosition.Other(mover)).Count == 0)
                { Finish(Actor, "У противника нет шашек или допустимых ходов"); return true; }
                if (Board.WhiteKingQuietMoves >= 15 && Board.BlackKingQuietMoves >= 15)
                { Finish(-1, "По 15 ходов дамками без взятия и движения простых шашек"); return true; }
                if (Remaining == 0 || Board.LegalMoves(mover).Count == 0) EndTurn();
                return true;
            }
            Side enemy = ChessPosition.Other(mover);
            bool check = Board.InCheck(enemy);
            var replies = Board.LegalMoves(enemy);
            string moveText = (Actor == 0 ? "Ты: " : "Бот: ") + wanted + (check ? " — шах!" : "");
            SetMessage(moveText);
            if (check && replies.Count == 0) { Finish(Actor, "Мат"); return true; }
            if (Board.BareKings()) { Finish(-1, "На доске остались только короли"); return true; }
            if (Board.QuietMoves >= 100) { Finish(-1, "50 ходов без взятия и движения пешек"); return true; }
            if (check || Remaining == 0 || Board.LegalMoves(mover).Count == 0) EndTurn();
            else Message = moveText + " Осталось перемещений: " + Remaining + ".";
            return true;
        }
        bool InCheck(Side side) { return Board != null && Board.InCheck(side); }
        string ActorName { get { return Actor == 0 ? "Ты: " : "Бот: "; } }

        public bool PlaceStone(int point)
        {
            if (Go == null || Phase != MatchPhase.MovePieces || Remaining <= 0) return false;
            var previous = new MoveSnapshot(this);
            if (!Go.Place(ActiveSide, point, out int captured)) return false;
            history.Push(previous);
            int file = point % GoPosition.Size;
            string coordinate = "" + (char)('A' + file + (file >= 8 ? 1 : 0)) + (point / GoPosition.Size + 1);
            CompleteBoardAction(ActorName + coordinate + (captured > 0 ? ", взято камней: " + captured : ""));
            if (Remaining == 0) EndTurn();
            return true;
        }

        public bool PassGo()
        {
            if (Go == null || (Phase != MatchPhase.ChooseCard && Phase != MatchPhase.MovePieces)) return false;
            SetMessage(ActorName + "пас.");
            SkipPending = false;
            if (Go.Pass(ActiveSide))
            {
                Remaining = 0;
                Phase = MatchPhase.GoScoring;
                deadGoStones = new bool[GoPosition.PointCount];
                UpdateGoScore();
                SetMessage("Два паса подряд. Начался подсчёт очков.");
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

        public string GoScoreText
        {
            get
            {
                string white = (goWhiteArea + GoPosition.Komi).ToString("0.0",
                    System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
                return "Чёрные " + goBlackArea + "  ·  Белые " + white;
            }
        }

        public bool ConfirmGoScore()
        {
            if (Phase != MatchPhase.GoScoring) return false;
            Side winner = goBlackArea > goWhiteArea + GoPosition.Komi ? Side.Black : Side.White;
            Finish(OwnerOf(winner), GoScoreText);
            return true;
        }

        public bool ResumeGo()
        {
            if (Phase != MatchPhase.GoScoring) return false;
            deadGoStones = null;
            Go.ClearPass();
            SetMessage("Партия продолжена.");
            EndTurn();
            return true;
        }

        public bool PlaceDomino(int index, bool left)
        {
            if (Domino == null || Phase != MatchPhase.MovePieces || Remaining <= 0 || !Domino.CanPlace(ActiveSide, index, left)) return false;
            var previous = new MoveSnapshot(this);
            if (!Domino.Place(ActiveSide, index, left, out DominoTile placed)) return false;
            history.Push(previous);
            CompleteBoardAction(ActorName + placed + (left ? " слева" : " справа"));
            if (Domino.Rack(ActiveSide).Count == 0) Finish(Actor, "Все костяшки выложены");
            else if (!FinishBlockedDomino() && Remaining == 0) EndTurn();
            return true;
        }

        public bool DrawDomino()
        {
            if (Domino == null || Phase != MatchPhase.MovePieces || !Domino.Draw(ActiveSide)) return false;
            SetMessage(ActorName + "добор костяшки из базара.");
            FinishBlockedDomino();
            return true;
        }

        public bool PassDomino()
        {
            if (Domino == null || Phase != MatchPhase.MovePieces || Domino.StockCount > 0 || Domino.HasPlacement(ActiveSide)) return false;
            if (FinishBlockedDomino()) return true;
            SetMessage(ActorName + "нет подходящей костяшки. Пас.");
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
            Finish(winner, "Рыба. Очки на руках: " + player + " у тебя, " + opponent + " у бота");
            return true;
        }

        void CompleteBoardAction(string message)
        {
            MovesPlayed++;
            Remaining--;
            SetMessage(message + ". Осталось ходов: " + Remaining + ".");
        }

        void EndTurn()
        {
            bool repeat = SkipPending && !InCheck(ChessPosition.Other(ActiveSide));
            SkipPending = false; Remaining = 0; Turn++;
            if (repeat) SetMessage("Противник пропускает очередь. Разыграй следующую карту.");
            else Actor = 1 - Actor;
            Phase = MatchPhase.ChooseCard; CheckStart();
        }
        void CheckStart()
        {
            if (Phase == MatchPhase.Finished || Board == null) return;
            if (Board.LegalMoves(ActiveSide).Count == 0)
            {
                if (Mode == GameMode.Checkers) Finish(1 - Actor, "Нет шашек или допустимых ходов");
                else Finish(Board.InCheck(ActiveSide) ? 1 - Actor : -1, Board.InCheck(ActiveSide) ? "Мат" : "Пат");
            }
        }
        void Finish(int winner, string reason)
        {
            Phase = MatchPhase.Finished; Winner = winner; Remaining = 0; SkipPending = false;
            Result = winner == 0 ? "Победа! " + reason + "." : winner == 1 ? "Победа бота. " + reason + "." : "Ничья. " + reason + ".";
            SetMessage(Result);
        }
        public void Resign()
        { if (Phase != MatchPhase.Finished) Finish(1 - Actor, "Сдача"); }
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
            if (index < 0 || index > 2 || Repaired(index) || Coins < Prices[index]) return false;
            Coins -= Prices[index];
            if (index == 0) Bench = true; else if (index == 1) Lamp = true; else House = true;
            return true;
        }
        public void AwardWin() { Coins += 100; Wins++; }
    }
}
