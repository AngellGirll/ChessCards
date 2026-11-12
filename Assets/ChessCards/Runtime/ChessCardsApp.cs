using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessCards
{
    // Self-contained prototype UI. No Canvas, EventSystem or inspector wiring required.
    public sealed partial class ChessCardsApp : MonoBehaviour
    {
        const string SaveKey = "ChessCards.Restoration.v1";
        CardMatch match;
        GameMode selectedMode = GameMode.Chess;
        bool modeSelection;
        ChessBot bot;
        RestorationProgress progress;
        Font font;
        Texture2D[,] pieces = new Texture2D[2, 9];
        readonly Texture2D[] cardFaces = new Texture2D[6];
        Texture2D cardBody, cardFrame, cardBack;
        GUIStyle text, center, bold;
        bool initialized, location = true, rules, confirmLeave, credited;
        int selected = -1;
        bool illegalGoMove;
        float BotTurnDelay { get { return balance != null ? Math.Max(0, balance.TurnDelay) : .85f; } }
        float BotMoveDelay { get { return balance != null ? Math.Max(0, balance.MoveDelay) : .8f; } }
        float BotCardPreviewDelay { get { return balance != null ? Math.Max(0, balance.CardRevealDelay) : 1.4f; } }
        float nextBot;
        int botTurn = -1, pendingBotCard = -1;
        bool historyOpen;
        Vector2 historyScroll;
        Card? announcedCard;
        int announcingActor;
        float cardAnnouncementUntil;
        List<ChessMove> promotions;
        List<ChessMove> selectedMoves = new List<ChessMove>();
        Vector2 rulesScroll;
        int lastRepair = -1;
        bool showLastResult;
        bool portrait;
        float w, h;
        Color background = Hex("200D31"), panel = Hex("492469"), ink = Hex("FFF9F2"), muted = Hex("F0D8FB"), gold = Hex("FFE098");
        readonly Color[] cardColors = { Hex("E22E49"), Hex("315DE5"), Hex("24A322"), Hex("E79D23") };
        static Color Hex(string s) { return new Color(Convert.ToInt32(s.Substring(0, 2), 16) / 255f, Convert.ToInt32(s.Substring(2, 2), 16) / 255f, Convert.ToInt32(s.Substring(4, 2), 16) / 255f); }
        void Awake()
        {
            if (!InitializeSceneFlow()) return;
            Application.targetFrameRate = 60;
            try { progress = JsonUtility.FromJson<RestorationProgress>(PlayerPrefs.GetString(SaveKey, "{}")); }
            catch (Exception) { progress = new RestorationProgress(); }
            if (progress == null) progress = new RestorationProgress();
            progress.Coins = Math.Max(0, progress.Coins); progress.Wins = Math.Max(0, progress.Wins);
            LoadPresentation();
            StartScenePreview();
        }
        void Save() { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(progress)); PlayerPrefs.Save(); }
        void OnApplicationPause(bool paused) { if (paused && progress != null) { Save(); SavePreferences(); } }
        void OnApplicationQuit() { if (progress != null) { Save(); SavePreferences(); } }
        void StartMatch()
        {
            int seed = Environment.TickCount;
            match = new CardMatch(seed, null, selectedMode, balance != null ? balance.StartingHand : 5,
                balance != null ? balance.CopiesPerNumberAndColor : 4, balance != null ? balance.CopiesPerSpecialAndColor : 2);
            bot = new ChessBot(seed ^ 76543, preferences.Difficulty);
            modeSelection = false;
            selected = -1; selectedMoves.Clear(); ResetHandSelection(); promotions = null; credited = false; location = false;
            botTurn = -1; pendingBotCard = -1; historyOpen = false;
            announcedCard = null; cardAnnouncementUntil = 0;
            illegalGoMove = false;
            lastRepair = -1; showLastResult = false;
            nextBot = Time.unscaledTime + BotTurnDelay;
            OpenScene(matchScene);
        }
        void Update()
        {
            if (sceneTransition || location || match == null) return;
            CreditResult();
            if (match.Phase == MatchPhase.Finished || match.Phase == MatchPhase.GoScoring || match.Actor != 1)
            { botTurn = -1; pendingBotCard = -1; return; }
            float now = Time.unscaledTime;
            if (settingsOpen || rules || historyOpen || confirmLeave || promotions != null)
            {
                // A paused overlay must not make the next action overdue on resume.
                nextBot = now + BotTurnDelay;
                if (pendingBotCard >= 0) cardAnnouncementUntil = now + BotTurnDelay + BotMoveDelay;
                return;
            }
            if (botTurn != match.Turn)
            {
                // Re-arm at every handoff, even after a long human turn or a skip.
                botTurn = match.Turn; pendingBotCard = -1;
                nextBot = now + BotTurnDelay;
                return;
            }
            if (now < nextBot) return;
            if (match.Phase == MatchPhase.ChooseCard)
            {
                if (pendingBotCard < 0)
                {
                    if (match.Go != null && match.Go.AcceptPass(match.ActiveSide))
                    {
                        match.PassGo();
                        Feedback(moveSound);
                        cardAnnouncementUntil = 0;
                        return;
                    }
                    int card = bot.ChooseCard(match);
                    if (card >= 0)
                    {
                        pendingBotCard = card;
                        AnnounceCard(match.Hands[1][card], 1);
                        nextBot = now + BotCardPreviewDelay;
                        return; // Show the card before undoing or rotating the board.
                    }
                    match.Draw();
                }
                else
                {
                    int card = pendingBotCard; pendingBotCard = -1;
                    if (!match.PlayCard(card))
                    { nextBot = now + BotTurnDelay; return; }
                }
            }
            else if (bot.PlayBoardAction(match)) Feedback(moveSound);
            illegalGoMove = false;
            nextBot = now + BotMoveDelay;
            selected = -1; selectedMoves.Clear(); CreditResult();
        }
        void AnnounceCard(Card card, int actor)
        {
            announcedCard = card;
            announcingActor = actor;
            cardAnnouncementUntil = Time.unscaledTime + BotCardPreviewDelay + 1.2f;
        }

        string AnnouncementText(Card card)
        {
            string who = announcingActor == 0 ? T("Ты", "You") : T("Бот", "Bot");
            string effect = card.Kind == CardKind.Number ? card.Number + (card.Number == 1 ? T(" ход.", " move.") : T(" хода подряд.", " moves in a row.")) :
                card.Kind == CardKind.Swap ? T("меняемся сторонами, затем 1 ход.", "swap sides, then make 1 move.") :
                card.Kind == CardKind.Undo ? T("отмена последнего хода, затем 1 ход.", "undo the last move, then make 1 move.") :
                T("1 ход, затем пропуск противника", "1 move, then the opponent skips a turn") +
                    (match.Mode == GameMode.Chess ? T(" (кроме шаха).", " (unless in check).") : ".");
            return who + ": «" + card.GetTitle(English) + "» — " + effect;
        }

        void CreditResult()
        {
            if (match == null || credited || match.Phase != MatchPhase.Finished) return;
            credited = true;
            lastRepair = -1;
            showLastResult = true;
            if (match.Winner == 0)
            {
                progress.AwardWin(WinReward);
                Save();
                Feedback(winSound);
            }
        }
        void Styles()
        {
            if (initialized) return;
            text = new GUIStyle { font = font, fontSize = 22, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            text.normal.textColor = ink;
            center = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            bold = new GUIStyle(text) { fontStyle = FontStyle.Bold };
            initialized = true;
        }
        void OnGUI()
        {
            Styles();
            GUI.color = Color.white; GUI.backgroundColor = Color.white; GUI.contentColor = Color.white;
            Fill(new Rect(0, 0, Screen.width, Screen.height), background);
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
#if UNITY_WEBGL && !UNITY_EDITOR
            portrait = false;
#else
            portrait = safe.height > safe.width;
#endif
            w = portrait ? 720 : 1280; h = portrait ? 1280 : 720;
            float scale = Mathf.Min(safe.width / w, safe.height / h);
            float ox = safe.x + (safe.width - w * scale) / 2;
            float oy = Screen.height - safe.y - safe.height + (safe.height - h * scale) / 2;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(ox, oy, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            bool blocked = sceneTransition || settingsOpen || modeSelection || rules || historyOpen || confirmLeave || promotions != null;
            if (!location) Backdrop(gameRoom, .63f);
            GUI.enabled = !blocked;
            if (location) DrawLocation(); else DrawMatch();
            GUI.enabled = true;
            if (sceneTransition) { GUI.matrix = oldMatrix; return; }
            if (settingsOpen) DrawSettings();
            else if (modeSelection) DrawModeSelection();
            else if (rules) DrawRules();
            else if (historyOpen) DrawHistory();
            else if (confirmLeave) DrawLeave();
            else if (promotions != null) DrawPromotion();
            GUI.matrix = oldMatrix;
        }
        void Fill(Rect r, Color color)
        {
            Color old = GUI.color; GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
        }
        void Label(Rect r, string value, int size = 22, Color? color = null, bool centered = false, bool strong = false)
        {
            GUIStyle style = strong ? bold : centered ? center : text;
            style.fontSize = size; style.normal.textColor = color ?? ink;
            style.alignment = centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            DrawStaticText(r, value, style);
        }

        static void DrawStaticText(Rect r, string value, GUIStyle style)
        {
            if (Event.current.type == EventType.Repaint)
                style.Draw(r, new GUIContent(value), false, false, false, false);
        }

        bool Button(Rect r, string value, Color? color = null, bool enabled = true, int size = 22)
        {
            bool old = GUI.enabled;
            GUI.enabled = old && enabled;
            Color face = enabled ? color ?? Royal : Hex("564461");
            if (GUI.enabled && r.Contains(Event.current.mousePosition))
                face = Color.Lerp(face, Color.white, .10f);
            Framed(r, face, Math.Min(15, r.height / 3), 3);
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (value.Length > 0)
            {
                Label(new Rect(r.x + 1, r.y + 2, r.width, r.height), value, size, Shadow, true, true);
                Label(r, value, size, enabled ? ink : muted, true, true);
            }
            GUI.enabled = old;
            if (clicked) Feedback(clickSound);
            return clicked;
        }

        string MatchStatus()
        {
            if (match.Phase == MatchPhase.GoScoring) return T("Подсчёт очков", "Scoring");
            if (match.Phase == MatchPhase.Finished) return match.GetResult(English);

            string turn = match.Actor == 0 ? T("Твоя очередь", "Your turn") : T("Ход бота", "Bot's turn");
            string action = T("перемещений: ", "moves left: ") + match.Remaining;
            if (match.Phase == MatchPhase.ChooseCard) action = T("выбор карты", "choose a card");
            else if (match.Board != null && match.Board.ForcedCaptureFrom >= 0)
                action = T("продолжи взятие", "continue capturing");
            return turn + "  •  " + action;
        }

        void DrawMatch()
        {
            if (match == null) { ReturnToCourtyard(); return; }
            if (Button(Region(ScreenBlock.Back, new Rect(28, 22, 124, 52)), T("Во двор", "Courtyard"), Royal, true, 18)) confirmLeave = true;
            Banner(Region(ScreenBlock.Title, new Rect(167, 20, portrait ? 195 : 580, 55)), ModeTitle(match.Mode) + (portrait ? "" : T(" × КАРТЫ", " × CARDS")), portrait ? 22 : 31);
            if (Button(Region(ScreenBlock.History, new Rect(w - 347, 22, 100, 52)), T("История", "History"), Blue, true, 18)) historyOpen = true;
            if (Button(Region(ScreenBlock.Rules, new Rect(w - 236, 22, 100, 52)), T("Правила", "Rules"), Blue, true, 18)) { rulesScroll = new Vector2(); rules = true; }
            if (Button(Region(ScreenBlock.Settings, new Rect(w - 125, 22, 97, 52)), "⚙", Royal, true, 28)) settingsOpen = true;
            Rect status = Region(ScreenBlock.MatchStatus, new Rect(28, 90, w - 56, portrait ? 100 : 62));
            Framed(status, RoyalDeep, 14, 3);
            string side = match.HumanSide == Side.White ? T("белые", "White") : T("чёрные", "Black");
            string phase = MatchStatus();
            Label(new Rect(status.x + 14, status.y + 4, status.width - 28, status.height * .48f), phase, portrait ? 23 : 24, match.Actor == 0 ? gold : ink, false, true);
            string playerInfo = match.Domino == null ? T("Ты: ", "You: ") + side : T("Костяшек у бота: ", "Bot tiles: ") + match.Domino.Rack(match.SideFor(1)).Count;
            Label(new Rect(status.x + 14, status.y + status.height * .52f, status.width - 28, status.height * .43f), playerInfo + T("  •  Очередь №", "  •  Turn #") + match.Turn + T("  •  Карт у бота: ", "  •  Bot cards: ") + match.Hands[1].Count, portrait ? 20 : 18, muted);
            float size = portrait ? 560 : 496;
            Rect board = Region(ScreenBlock.Board, new Rect(portrait ? 80 : 48, portrait ? 210 : 190, size, size));
            board.width = board.height = Math.Min(board.width, board.height);
            if (match.Go != null) DrawGoBoard(board);
            else if (match.Domino != null) DrawDominoBoard(board);
            else DrawBoard(board);
            Rect cards = Region(ScreenBlock.Cards, new Rect(portrait ? 32 : 590, portrait ? 802 : 180, portrait ? 656 : 650, 321));
            float right = cards.x, top = cards.y, width = cards.width;
            if (match.Phase == MatchPhase.GoScoring)
            {
                DrawGoScoring(right, top, width);
                return;
            }
            DrawCards(right, top, width);
            Rect messageArea = Region(ScreenBlock.Notice, new Rect(right, portrait ? 1126 : 508, width, portrait ? 64 : 70));
            if (match.Phase == MatchPhase.Finished && portrait) messageArea.height = Math.Min(30, messageArea.height);
            bool announcing = !illegalGoMove && match.Phase != MatchPhase.Finished && Time.unscaledTime < cardAnnouncementUntil && announcedCard.HasValue;
            if (announcing)
            {
                Card card = announcedCard.Value;
                Framed(messageArea, cardColors[card.Color], 12, 2);
                Label(new Rect(messageArea.x + 12, messageArea.y + 4, messageArea.width - 24, messageArea.height - 8), AnnouncementText(card), 20, ink);
            }
            else
            {
                Framed(messageArea, RoyalDeep, 10, 2);
                string message = illegalGoMove ? T("Ход оставляет группу без свободных точек или повторяет позицию.", "This move leaves a group without liberties or repeats the position.") : match.GetMessage(English);
                Label(new Rect(messageArea.x + 12, messageArea.y, messageArea.width - 24, messageArea.height), message, 19, ink);
            }
            Rect actions = Region(ScreenBlock.Actions, new Rect(right, portrait ? 1200 : 650, width, 54));
            if (match.Phase == MatchPhase.Finished)
            {
                Label(new Rect(right, portrait ? 1160 : 594, width, 32), match.Winner == 0 ? "+" + WinReward + T(" монет зачислено", " coins awarded") : T("Можно попробовать ещё раз", "Play another round"), 22, gold, true);
                if (Button(new Rect(actions.x, actions.y, actions.width * .56f, actions.height), T("Вернуться во двор", "Return to courtyard"), Hex("87643B"))) { ReturnToCourtyard(); GUIUtility.ExitGUI(); }
                if (Button(new Rect(actions.x + actions.width * .59f, actions.y, actions.width * .41f, actions.height), T("Ещё партия", "Play again"))) { Replay(); GUIUtility.ExitGUI(); }
            }
            else
            {
                bool canDraw = match.Actor == 0 && match.Phase == MatchPhase.ChooseCard && match.PlayableCards().Count == 0;
                float drawWidth = match.Go == null ? actions.width * .56f : actions.width * .38f;
                if (Button(new Rect(actions.x, actions.y, drawWidth, actions.height), T("Взять карту", "Draw a card"), Green, canDraw))
                { match.Draw(); ResetHandSelection(); selected = -1; illegalGoMove = false; CreditResult(); GUIUtility.ExitGUI(); }
                if (match.Go != null && Button(new Rect(actions.x + actions.width * .41f, actions.y, actions.width * .22f, actions.height), T("Пас", "Pass"), panel, match.Actor == 0))
                { match.PassGo(); ResetHandSelection(); illegalGoMove = false; cardAnnouncementUntil = 0; CreditResult(); GUIUtility.ExitGUI(); }
                float deckStart = match.Go == null ? .59f : .65f;
                Framed(new Rect(actions.x + actions.width * deckStart, actions.y, actions.width * (1 - deckStart), actions.height), Blue, 14, 3);
                Label(new Rect(actions.x + actions.width * deckStart, actions.y, actions.width * (1 - deckStart), actions.height), T("Колода: ", "Deck: ") + match.DeckCount + T("\nСброс: ", "\nDiscard: ") + match.DiscardCount, 18, muted, true);
            }
            if (!portrait && match.Phase != MatchPhase.Finished)
            {
                var log = match.GetLog(English);
                string lines = "";
                for (int i = Math.Max(0, log.Count - 2); i < log.Count; i++) lines += log[i] + "\n";
                Framed(new Rect(right, 584, width, 56), RoyalDeep, 12, 2);
                Label(new Rect(right + 12, 584, width - 24, 56), lines, 16, muted);
            }
        }
        void DrawGoScoring(float x, float y, float width)
        {
            Framed(new Rect(x, y, width, 232), RoyalDeep, 20, 4);
            Label(new Rect(x + 18, y + 16, width - 36, 54), match.GetGoScoreText(English), 30, gold, true, true);
            Label(new Rect(x + 24, y + 75, width - 48, 40), T("У белых уже учтены 7,5 очка коми.", "White's score includes 7.5 komi."), 20, muted, true);
            Label(new Rect(x + 24, y + 122, width - 48, 84),
                T("Нажми на камень в мёртвой группе, чтобы исключить всю группу из подсчёта. Повторное нажатие возвращает группу.", "Tap a dead group to exclude it from the score. Tap again to restore it."), 23, ink);

            Rect actions = Region(ScreenBlock.Actions, new Rect(x, portrait ? 1200 : 650, width, 54));
            float buttonWidth = (actions.width - 16) / 2;
            if (Button(new Rect(actions.x, actions.y, buttonWidth, actions.height), T("Продолжить игру", "Resume play"), panel))
            {
                match.ResumeGo();
                cardAnnouncementUntil = 0;
                GUIUtility.ExitGUI();
            }
            if (Button(new Rect(actions.x + buttonWidth + 16, actions.y, buttonWidth, actions.height), T("Подтвердить счёт", "Confirm score"), Green))
            {
                match.ConfirmGoScore();
                CreditResult();
                GUIUtility.ExitGUI();
            }
        }

        void DrawGoBoard(Rect r)
        {
            int size = GoPosition.Size;
            float cell = r.width / size;
            BoardFrame(r);
            Fill(r, Hex("DDB274"));
            Color grid = Hex("705233");
            float left = r.x + cell / 2, top = r.y + cell / 2;
            for (int i = 0; i < size; i++)
            {
                Fill(new Rect(left + i * cell - 1, top, 2, cell * (size - 1)), grid);
                Fill(new Rect(left, top + i * cell - 1, cell * (size - 1), 2), grid);
                Label(new Rect(r.x + i * cell, r.y + r.height + 4, cell, 22), ((char)('A' + i + (i >= 8 ? 1 : 0))).ToString(), 16, muted, true);
                Label(new Rect(r.x - 29, r.y + i * cell, 22, cell), (size - i).ToString(), 16, muted, true);
            }
            for (int row = 0; row < size; row++) for (int col = 0; col < size; col++)
            {
                int point = (size - 1 - row) * size + col;
                var target = new Rect(r.x + col * cell, r.y + row * cell, cell, cell);
                Side? stone = match.Go.StoneAt(point);
                if (stone.HasValue)
                {
                    var icon = pieces[(int)stone.Value, (int)PieceType.Checker];
                    bool marked = match.IsGoStoneMarked(point);
                    Color oldColor = GUI.color;
                    if (marked) GUI.color = new Color(1, 1, 1, .38f);
                    if (icon != null) GUI.DrawTexture(target, icon, ScaleMode.ScaleToFit);
                    else Label(target, stone == Side.White ? "○" : "●", 28, grid, true);
                    GUI.color = oldColor;
                    if (marked) Label(target, "×", 30, Hex("72352B"), true, true);
                    else if (match.Go.LastPoint == point)
                        Fill(new Rect(target.x + cell / 2 - 3, target.y + cell / 2 - 3, 6, 6), gold);
                    if (match.Phase == MatchPhase.GoScoring && GUI.Button(target, GUIContent.none, GUIStyle.none))
                    {
                        match.ToggleGoDeadGroup(point);
                        GUIUtility.ExitGUI();
                    }
                }
                else if ((row == 2 || row == 6) && (col == 2 || col == 6) || row == 4 && col == 4)
                    Fill(new Rect(target.x + cell / 2 - 3, target.y + cell / 2 - 3, 6, 6), grid);

                if (!stone.HasValue && match.Actor == 0 && match.Phase == MatchPhase.MovePieces &&
                    GUI.Button(target, GUIContent.none, GUIStyle.none))
                {
                    bool placed = match.PlaceStone(point);
                    illegalGoMove = !placed;
                    if (placed) Feedback(moveSound);
                    cardAnnouncementUntil = 0;
                    CreditResult();
                    GUIUtility.ExitGUI();
                }
            }
        }

        void DrawDominoBoard(Rect r)
        {
            var domino = match.Domino;
            var rack = domino.Rack(match.HumanSide);
            bool active = match.Actor == 0 && match.Phase == MatchPhase.MovePieces;
            bool canMove = active && domino.HasPlacement(match.HumanSide);
            float tileWidth = (r.width - 62) / 7;
            float tileHeight = 30;
            Framed(r, RoyalDeep, 20, 5);
            Label(new Rect(r.x + 16, r.y + 6, r.width - 32, 33), T("Цепочка", "Chain"), 24, gold, false, true);
            if (domino.Chain.Count == 0)
                Label(new Rect(r.x + 16, r.y + 44, r.width - 32, 70), T("Первой можно выложить любую костяшку.", "Any tile can start the chain."), 20, muted, true);
            else
            {
                Label(new Rect(r.x + 16, r.y + 40, r.width - 32, 28),
                    T("Слева ", "Left ") + domino.LeftEnd + T("    •    Справа ", "    •    Right ") + domino.RightEnd, 18, muted);
                for (int i = 0; i < domino.Chain.Count; i++)
                    DrawDominoTile(new Rect(r.x + 16 + i % 7 * (tileWidth + 5), r.y + 72 + i / 7 * 38, tileWidth, tileHeight), domino.Chain[i], false);
            }

            float half = (r.width - 48) / 2;
            if (Button(new Rect(r.x + 16, r.y + 231, half, 48), T("Выложить слева", "Play on the left"), Hex("386579"),
                canMove && domino.CanPlace(match.HumanSide, selected, true), 18))
                PlaySelectedDomino(true);
            if (Button(new Rect(r.x + 32 + half, r.y + 231, half, 48), T("Выложить справа", "Play on the right"), Hex("386579"),
                canMove && domino.CanPlace(match.HumanSide, selected, false), 18))
                PlaySelectedDomino(false);

            if (active && !canMove)
            {
                string action = domino.StockCount > 0 ? T("Взять из базара · ", "Draw a tile · ") + domino.StockCount : T("Пас · базар пуст", "Pass · no tiles left");
                if (Button(new Rect(r.x + 16, r.y + 286, r.width - 32, 43), action, Hex("87643B"), true, 19))
                {
                    if (domino.StockCount > 0) match.DrawDomino(); else match.PassDomino();
                    selected = -1; cardAnnouncementUntil = 0;
                    CreditResult();
                    GUIUtility.ExitGUI();
                }
            }
            else Label(new Rect(r.x + 16, r.y + 286, r.width - 32, 43),
                (canMove ? T("Выбери костяшку и край цепочки", "Choose a tile and an end") : T("Базар: ", "Boneyard: ") + domino.StockCount), 18, muted, true);

            Label(new Rect(r.x + 16, r.y + 330, r.width - 32, 29), T("Твои костяшки · ", "Your tiles · ") + rack.Count, 19, gold);
            for (int i = 0; i < rack.Count; i++)
            {
                var target = new Rect(r.x + 16 + i % 7 * (tileWidth + 5), r.y + 361 + i / 7 * 40, tileWidth, tileHeight);
                bool fits = canMove && (domino.CanPlace(match.HumanSide, i, true) || domino.CanPlace(match.HumanSide, i, false));
                DrawDominoTile(target, rack[i], fits && selected == i);
                if (active && !fits) Fill(target, new Color(0, 0, 0, .24f));
                if (fits && GUI.Button(target, GUIContent.none, GUIStyle.none))
                { selected = selected == i ? -1 : i; GUIUtility.ExitGUI(); }
            }
        }

        void PlaySelectedDomino(bool left)
        {
            if (match.PlaceDomino(selected, left)) Feedback(moveSound);
            selected = -1; cardAnnouncementUntil = 0;
            CreditResult();
            GUIUtility.ExitGUI();
        }

        void DrawDominoTile(Rect r, DominoTile tile, bool highlighted)
        {
            Color edge = highlighted ? gold : Hex("9F8B68");
            Fill(r, edge);
            Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Hex("F6E8CB"));
            Fill(new Rect(r.x + r.width / 2 - 1, r.y + 3, 2, r.height - 6), edge);
            DrawPips(r.x + r.width / 4, r.y + r.height / 2, r.height, tile.Left);
            DrawPips(r.x + r.width * .75f, r.y + r.height / 2, r.height, tile.Right);
        }

        void DrawPips(float x, float y, float size, int number)
        {
            float delta = size * .21f;
            float pip = size * .11f;
            Color color = Hex("273548");
            for (int row = -1; row <= 1; row++) for (int col = -1; col <= 1; col++)
            {
                bool centerPip = row == 0 && col == 0 && number % 2 == 1;
                bool diagonal = row != 0 && row == col && number >= 2;
                bool otherDiagonal = row != 0 && row == -col && number >= 4;
                bool middleSides = row == 0 && col != 0 && number == 6;
                if (centerPip || diagonal || otherDiagonal || middleSides)
                    Fill(new Rect(x + col * delta - pip / 2, y + row * delta - pip / 2, pip, pip), color);
            }
        }

        void DrawBoard(Rect r)
        {
            float cell = r.width / 8;
            BoardFrame(r);
            bool whiteBottom = match.HumanSide == Side.White;
            var board = match.Board;
            bool whiteCheck = board.InCheck(Side.White), blackCheck = board.InCheck(Side.Black);
            for (int row = 0; row < 8; row++) for (int col = 0; col < 8; col++)
            {
                int file = whiteBottom ? col : 7 - col, rank = whiteBottom ? 7 - row : row;
                int square = rank * 8 + file;
                Rect tile = new Rect(r.x + col * cell, r.y + row * cell, cell, cell);
                Color c = (file + rank) % 2 == 0 ? (ActiveTheme?.BoardDark ?? Hex("31807C")) : (ActiveTheme?.BoardLight ?? Hex("F9E6BB"));
                if (match.LastMove.HasValue && (match.LastMove.Value.From == square || match.LastMove.Value.To == square)) c = (file + rank) % 2 == 0 ? Hex("A5AA61") : Hex("D7CB7C");
                if (square == selected) c = Hex("E7B55A");
                Piece p = board.Squares[square];
                if (p.Type == PieceType.King && (p.Side == Side.White ? whiteCheck : blackCheck)) c = Hex("BC6464");
                Fill(tile, c);
                bool legal = false;
                foreach (var m in selectedMoves) if (m.To == square) { legal = true; break; }
                if (legal) Fill(new Rect(tile.x + cell * .37f, tile.y + cell * .37f, cell * .26f, cell * .26f), Hex("3A725D"));
                if (p.Type != PieceType.None)
                {
                    bool captured = match.Mode == GameMode.Checkers && CheckersRules.Pending(board, square);
                    Color oldColor = GUI.color;
                    if (captured) GUI.color = new Color(1, 1, 1, .35f);
                    DrawPiece(Inset(tile, 3), p);
                    GUI.color = oldColor;
                    if (captured) Label(tile, "×", 36, Hex("AF4652"), true);
                }
                bool active = match.Actor == 0 && match.Phase == MatchPhase.MovePieces;
                if (active && GUI.Button(tile, GUIContent.none, GUIStyle.none)) { SelectSquare(square); GUIUtility.ExitGUI(); }
            }
            for (int i = 0; i < 8; i++)
            {
                int file = whiteBottom ? i : 7 - i, rank = whiteBottom ? 7 - i : i;
                Label(new Rect(r.x + i * cell, r.y + r.height + 5, cell, 24), "" + (char)('a' + file), 17, muted, true);
                Label(new Rect(r.x - 28, r.y + i * cell, 24, cell), (rank + 1).ToString(), 17, muted, true);
            }
        }
        void SelectSquare(int square)
        {
            var choices = selectedMoves.FindAll(m => m.To == square);
            if (choices.Count > 0)
            {
                if (choices[0].Promotion != PieceType.None) promotions = choices;
                else { if (match.Move(choices[0])) Feedback(moveSound); CreditResult(); }
                selected = -1; selectedMoves.Clear(); SelectForcedCapture(); return;
            }
            if (match.Board.ForcedCaptureFrom >= 0) { SelectForcedCapture(); return; }
            Piece p = match.Board.Squares[square]; selectedMoves.Clear();
            if (p.Type == PieceType.None || p.Side != match.HumanSide || selected == square) { selected = -1; return; }
            selected = square;
            foreach (var m in match.Board.LegalMoves(match.HumanSide)) if (m.From == square) selectedMoves.Add(m);
        }
        void SelectForcedCapture()
        {
            if (match.Actor != 0 || match.Phase != MatchPhase.MovePieces || match.Board.ForcedCaptureFrom < 0) return;
            selected = match.Board.ForcedCaptureFrom;
            selectedMoves.Clear(); selectedMoves.AddRange(match.Board.LegalMoves(match.HumanSide));
        }
        void DrawCardFace(Rect r, Card card, bool highlighted, float opacity = 1)
        {
            int face = card.Kind == CardKind.Number ? card.Number - 1 : (int)card.Kind + 2;
            Texture2D art = face >= 0 && face < cardFaces.Length ? cardFaces[face] : null;
            if (cardBody == null || cardFrame == null || art == null)
            {
                Fill(r, cardColors[card.Color]);
                Label(r, card.GetTitle(English), 26, ink, true);
                return;
            }
            Color old = GUI.color;
            GUI.color = new Color(.13f, .035f, .10f, .68f * opacity);
            GUI.DrawTexture(new Rect(r.x + 2, r.y + 4, r.width, r.height), cardBody);
            if (highlighted)
            {
                GUI.color = new Color(TrimLight.r, TrimLight.g, TrimLight.b, opacity);
                GUI.DrawTexture(Inset(r, -2), cardBody);
            }
            Color color = cardColors[card.Color];
            GUI.color = new Color(color.r, color.g, color.b, opacity);
            GUI.DrawTexture(r, cardBody);
            GUI.color = new Color(1, 1, 1, opacity);
            GUI.DrawTexture(r, cardFrame);
            GUI.DrawTexture(r, art);
            GUI.color = old;
        }
        string HandCardCaption(Card card)
        {
            if (card.Kind != CardKind.Number) return card.GetTitle(English);
            return card.Number == 1 ? T("ХОД", "MOVE") : T("ХОДА", "MOVES");
        }
        void DrawCards(float x, float y, float width)
        {
            Card shown = match.Top;
            bool preview = match.Actor == 1 && pendingBotCard >= 0 && pendingBotCard < match.Hands[1].Count;
            if (preview) shown = match.Hands[1][pendingBotCard];
            Framed(new Rect(x, y, width, 94), Royal, 17, 4);
            Framed(new Rect(x, y + 99, width, 222), RoyalDeep, 16, 3);
            DrawCardFace(new Rect(x + 14, y + 5, 58, 81), shown, preview);
            Label(new Rect(x + 90, y + 8, width - 202, 25), preview ? T("БОТ ПОКАЗЫВАЕТ КАРТУ", "BOT REVEALS A CARD") : T("ВЕРХНЯЯ КАРТА СБРОСА", "TOP DISCARD"), 16, preview ? gold : muted);
            Label(new Rect(x + 90, y + 34, width - 202, 40), shown.GetColorName(English) + " «" + shown.GetTitle(English) + "»", 24, ink, false, true);
            if (cardBack != null) GUI.DrawTexture(new Rect(x + width - 67, y + 7, 46, 64), cardBack);
            Label(new Rect(x + width - 90, y + 72, 90, 20), T("БОТ ×", "BOT ×") + match.Hands[1].Count, 15, muted, true);
            DrawHand(x, y, width);
        }
        void Overlay(Rect r, string title)
        {
            Fill(new Rect(0, 0, w, h), new Color(.06f, .01f, .11f, .76f));
            Framed(r, RoyalDeep, 26, 5);
            Banner(new Rect(r.x + 13, r.y + 9, r.width - 26, 65), title, 30);
        }
        void DrawLeave()
        {
            Rect r = new Rect(40, h / 2 - 170, w - 80, 340); Overlay(r, T("Вернуться во двор?", "Return to the courtyard?"));
            Label(new Rect(r.x + 24, r.y + 86, r.width - 48, 110), T("Текущая партия завершится без награды. Монеты и ремонт сохранятся.", "Leaving ends this game without a reward. Your coins and repairs are saved."), 24);
            if (Button(new Rect(r.x + 24, r.y + 232, (r.width - 64) / 2, 64), T("Остаться", "Stay"))) confirmLeave = false;
            if (Button(new Rect(r.x + 40 + (r.width - 64) / 2, r.y + 232, (r.width - 64) / 2, 64), T("Во двор", "Courtyard"), Hex("805246")))
            { ReturnToCourtyard(); }
        }
        void DrawPromotion()
        {
            Rect r = new Rect(40, h / 2 - 150, w - 80, 300); Overlay(r, T("Превращение пешки", "Pawn promotion"));
            string[] titles = { T("Ферзь", "Queen"), T("Ладья", "Rook"), T("Слон", "Bishop"), T("Конь", "Knight") };
            for (int i = 0; i < promotions.Count; i++)
            {
                float cw = (r.width - 60) / 4;
                Rect option = new Rect(r.x + 18 + i * (cw + 6), r.y + 112, cw, 128);
                if (Button(option, "", Blue))
                {
                    if (match.Move(promotions[i])) Feedback(moveSound);
                    promotions = null;
                    CreditResult();
                    GUIUtility.ExitGUI();
                }
                DrawPiece(new Rect(option.x + (option.width - 84) / 2, option.y + 8, 84, 84),
                    new Piece(promotions[i].Promotion, match.HumanSide));
                Label(new Rect(option.x + 4, option.y + 91, option.width - 8, 29), titles[i], 20, ink, true, true);
            }
        }
        string ModeTitle(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Checkers: return T("ШАШКИ", "CHECKERS");
                case GameMode.Go: return T("ГО", "GO");
                case GameMode.Domino: return T("ДОМИНО", "DOMINOES");
                default: return T("ШАХМАТЫ", "CHESS");
            }
        }

        void DrawModeChoice(Rect r, GameMode mode)
        {
            if (Button(r, "", modeColors[(int)mode]))
            { selectedMode = mode; StartMatch(); GUIUtility.ExitGUI(); }
            var icon = modeArt[(int)mode];
            if (icon != null)
                GUI.DrawTexture(new Rect(r.x + 10, r.y + 5, r.width - 20, r.height - 66), icon, ScaleMode.ScaleToFit);
            Label(new Rect(r.x + 8, r.y + r.height - 65, r.width - 16, 34), ModeTitle(mode), 25, ink, true, true);
            string detail = mode == GameMode.Chess ? T("Поставь мат королю", "Checkmate the king") :
                mode == GameMode.Checkers ? T("Русские шашки 8×8", "Russian checkers 8×8") :
                mode == GameMode.Go ? T("Территория на доске 9×9", "Territory on a 9×9 board") :
                T("Выложи все костяшки", "Play all your tiles");
            Label(new Rect(r.x + 10, r.y + r.height - 30, r.width - 20, 24), detail, 17, ink, true);
        }

        void DrawModeSelection()
        {
            Rect r = Region(ScreenBlock.ModePicker, portrait ? new Rect(40, h / 2 - 300, w - 80, 600) : new Rect(200, 100, 880, 520));
            Overlay(r, T("Выбери игру", "Choose a game"));
            if (Button(new Rect(r.x + r.width - 68, r.y + 13, 50, 49), "×", Red, true, 30))
            { modeSelection = false; GUIUtility.ExitGUI(); }
            float tileWidth = (r.width - 64) / 2;
            float tileHeight = portrait ? 166 : 148;
            for (int i = 0; i < enabledModes.Count; i++)
            {
                var tile = new Rect(r.x + 24 + i % 2 * (tileWidth + 16), r.y + 100 + i / 2 * (tileHeight + 16), tileWidth, tileHeight);
                DrawModeChoice(tile, enabledModes[i]);
            }
            if (Button(new Rect(r.x + 24, r.y + r.height - 80, r.width - 48, 56), T("Назад", "Back"), Hex("87643B"))) modeSelection = false;
        }

        void DrawHistory()
        {
            Rect r = new Rect(24, 24, w - 48, h - 48);
            Overlay(r, T("История действий", "Move history"));
            Rect view = new Rect(r.x + 20, r.y + 88, r.width - 40, r.height - 186);
            string content = "";
            var log = match.GetLog(English);
            for (int i = 0; i < log.Count; i++) content += (i + 1) + ". " + log[i] + "\n\n";
            GUIStyle style = new GUIStyle(text) { fontSize = portrait ? 23 : 21, alignment = TextAnchor.UpperLeft };
            float contentHeight = style.CalcHeight(new GUIContent(content), view.width - 36) + 20;
            historyScroll = GUI.BeginScrollView(view, historyScroll, new Rect(0, 0, view.width - 26, contentHeight));
            DrawStaticText(new Rect(4, 0, view.width - 36, contentHeight), content, style);
            GUI.EndScrollView();
            if (Button(new Rect(r.x + 24, r.y + r.height - 80, r.width - 48, 56), T("Вернуться к партии", "Back to game"), Hex("87643B"))) historyOpen = false;
        }
        void DrawRules()
        {
            if (location || match == null) { rules = false; return; }
            GameMode mode = match.Mode;
            Rect r = new Rect(24, 24, w - 48, h - 48); Overlay(r, T("Как играть", "How to play"));
            Rect view = new Rect(r.x + 20, r.y + 88, r.width - 40, r.height - 186);
            string content =
                "ЦЕЛЬ\nПоставь мат боту. За победу — 100 монет. Во дворе выбери объект и оплати ремонт. Пат и поражение не дают награду.\n\n" +
                "ОДНА КАРТА ЗА ОЧЕРЕДЬ\nНажми карту, чтобы приподнять её, и ещё раз, чтобы сыграть. Выбирай карту того же цвета или значения, что верхняя карта сброса. Числа 1–3 дают столько же шахматных перемещений подряд. Можно двигать одну фигуру несколько раз. Специальные карты совпадают по цвету или своему эффекту.\n\n" +
                "ДОБОР\nЕсли доступных карт нет, возьми одну. Подходящую можно сразу сыграть. Иначе очередь переходит боту. Рука автоматически не пополняется. Если колода закончилась, сброс перемешивается, верхняя карта остаётся.\n\n" +
                "ШАХ И МАТ\nШах немедленно завершает серию перемещений: противник должен защититься. Если подходящая карта не пришла при шахе, всё равно даётся одно защитное перемещение. Мат проверяется сразу после каждого перемещения.\n\n" +
                "ОБМЕН СТОРОН\nИгрок и бот меняются армиями, а доска разворачивается. Руки карт остаются у своих владельцев. Затем сыгравший делает одно перемещение новой стороной. Карта недоступна, пока любой король под шахом.\n\n" +
                "ОТМЕНА\nОткатывает последнее шахматное перемещение: возвращает взятую фигуру, права рокировки и другие параметры доски. Карты не возвращаются. Затем даётся одно своё перемещение. Нельзя отменить защиту противника от шаха и продолжить атаку без его ответа.\n\n" +
                "ПРОПУСК\nДаёт одно перемещение, после которого противник пропускает очередь. Если ты поставишь шах, пропуск отменяется: противник получает возможность защититься.\n\n" +
                "ШАХМАТНЫЕ ОСОБЕННОСТИ\nЕсть рокировка, выбор превращения пешки и взятие на проходе. На проходе можно взять только следующим шахматным перемещением. Карта без перемещения не расходует эту возможность. Короля нельзя брать или оставлять под ударом.\n\n" +
                "НИЧЬЯ\nПат, два оставшихся короля или 100 перемещений без движения пешек и взятий. Троекратное повторение в этом прототипе не проверяется.\n\n" +
                "УПРАВЛЕНИЕ\nРазыграй карту, затем выбери фигуру и подсвеченную клетку. Нажми фигуру повторно, чтобы снять выбор. Правила можно прокручивать. Сохраняются монеты и ремонт; незавершённая партия при закрытии не сохраняется.";
            if (mode == GameMode.Checkers)
                content = "ШАШКИ × КАРТЫ\nРусские шашки на доске 8×8. У каждой стороны по 12 шашек; ходят по тёмным клеткам. Для победы забери все шашки соперника или лиши их допустимых ходов.\n\n" +
                    "ХОДЫ И ВЗЯТИЯ\nПростая шашка ходит на соседнюю диагональную клетку вперёд, а бьёт в обе стороны. Если есть взятие, нужно бить. При нескольких вариантах можно выбрать любой.\n\n" +
                    "СЕРИЯ ВЗЯТИЙ\nПродолжай брать одной и той же шашкой, пока возможны взятия. Вся цепочка считается одним перемещением карты. Подсветка автоматически остаётся на этой шашке. Взятые шашки отмечаются крестиком и снимаются после всей цепочки; повторно брать или перепрыгивать их нельзя.\n\n" +
                    "ДАМКА\nНа последней горизонтали шашка превращается в дамку. Дамка ходит на любое свободное расстояние по диагонали. Если превращение произошло при взятии, цепочка продолжается уже дамкой.\n\n" +
                    "КАРТЫ\nНажми карту, чтобы приподнять её, и ещё раз, чтобы сыграть. Одна карта за очередь, совпадение по цвету или значению. Числа 1–3 дают столько перемещений подряд. Если доступных карт нет, возьми одну: подходящую можно сыграть сразу; иначе очередь переходит противнику, даже если на доске было взятие. Рука не пополняется автоматически.\n\n" +
                    "СПЕЦИАЛЬНЫЕ КАРТЫ\nОбмен меняет владельцев армий и разворачивает доску. Отмена возвращает последнее завершённое перемещение целиком, включая всю цепочку взятий. Пропуск даёт одно перемещение, затем противник пропускает очередь. У каждой специальной карты есть одно своё перемещение после эффекта.\n\n" +
                    "НИЧЬЯ\nПосле 15 завершённых перемещений дамками каждой стороны без взятия и движения простой шашки. Остальные турнирные ограничения ничьей в прототипе не проверяются.\n\n" +
                    "НАГРАДА И УПРАВЛЕНИЕ\nЗа победу — 100 монет в общий баланс двора. Нажми карту, шашку и подсвеченную клетку. Во время серии выбирай следующую подсвеченную клетку. История показывает карты и перемещения. Партия при закрытии не сохраняется, монеты и ремонт сохраняются.";
            if (mode == GameMode.Go)
                content = "ГО 9×9\nТы начинаешь чёрными. Нажимай на свободные пересечения линий. Камни соединяются по горизонтали и вертикали. Когда у группы противника не остаётся соседних свободных точек, её камни снимаются. Нельзя оставлять свою группу без свободных точек или повторять уже сыгранную позицию.\n\n" +
                    "КАРТЫ\nНажми карту дважды: сначала для выбора, затем для розыгрыша. Карта даёт 1–3 постановки камней. Обмен меняет цвета, отмена возвращает позицию до последней постановки, а пропуск даёт один ход и оставляет очередь у сыгравшего карту. Правила подбора карт по цвету и значению общие.\n\n" +
                    "ПАС И ПОДСЧЁТ\nПас завершает очередь, в том числе до выбора карты. После двух пасов открывается подсчёт. Нажми на камни в мёртвых группах и подтверди результат. Если статус группы неясен, продолжи партию. Очки дают камни на доске и окружённые свободные точки. Белым добавляется 7,5 очка. Победа даёт 100 монет.";
            else if (mode == GameMode.Domino)
                content = "ДОМИНО С ДОБОРОМ\nНабор 0–6. На старте по 7 костяшек. Первый ход твой, выложи любую. Дальше выбери костяшку и нажми кнопку края цепочки с таким же числом точек.\n\n" +
                    "КАРТЫ И БАЗАР\nНажми карту дважды: сначала для выбора, затем для розыгрыша. Карта даёт 1–3 выкладывания. Обмен меняет руки костяшек, отмена возвращает позицию до последнего выкладывания, пропуск даёт один ход и оставляет очередь у сыгравшего карту. Если подходящей костяшки нет, после розыгрыша карты бери из базара до подходящей. Добор костяшки не тратит ход карты. При пустом базаре пас передаёт очередь.\n\n" +
                    "ПОБЕДА\nПобеждает тот, кто первым выложил все костяшки. Если базар пуст и оба заблокированы, выигрывает тот, у кого на руках меньше точек. При равенстве ничья. Победа даёт 100 монет.";
            if (English) content = EnglishRules.For(mode);
            content = content.Replace("100 монет", WinReward + " монет").Replace("100 coins", WinReward + " coins");
            GUIStyle ruleStyle = new GUIStyle(text) { fontSize = portrait ? 23 : 21, alignment = TextAnchor.UpperLeft };
            float contentHeight = ruleStyle.CalcHeight(new GUIContent(content), view.width - 36) + 20;
            rulesScroll = GUI.BeginScrollView(view, rulesScroll, new Rect(0, 0, view.width - 26, contentHeight));
            DrawStaticText(new Rect(4, 0, view.width - 36, contentHeight), content, ruleStyle);
            GUI.EndScrollView();
            if (Button(new Rect(r.x + 24, r.y + r.height - 80, r.width - 48, 56), T("Понятно", "Done"), Hex("87643B"))) rules = false;
        }
    }
}
