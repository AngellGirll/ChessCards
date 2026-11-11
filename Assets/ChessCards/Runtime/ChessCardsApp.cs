using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessCards
{
    // Self-contained prototype UI. No Canvas, EventSystem or inspector wiring required.
    public sealed class ChessCardsApp : MonoBehaviour
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
        Texture2D cardBody, cardBack;
        GUIStyle text, center, bold;
        bool initialized, location = true, rules, confirmLeave, credited;
        int selected = -1, page;
        string boardNotice = "";
        const float BotTurnDelay = .85f;
        const float BotMoveDelay = .8f;
        const float BotCardPreviewDelay = 1.4f;
        float nextBot;
        int botTurn = -1, pendingBotCard = -1;
        bool historyOpen;
        Vector2 historyScroll;
        string cardAnnouncement = "";
        float cardAnnouncementUntil;
        int announcementColor;
        List<ChessMove> promotions;
        List<ChessMove> selectedMoves = new List<ChessMove>();
        Vector2 rulesScroll;
        string notice = "Выигрывай партии и возвращай жизнь старому двору.";
        bool portrait;
        float w, h;
        readonly Color background = Hex("171E2B"), panel = Hex("252E40"), ink = Hex("F7F1E4"), muted = Hex("AAB4C5"), gold = Hex("EBC270");
        readonly Color[] cardColors = { Hex("CA5360"), Hex("4D80B6"), Hex("519B81"), Hex("D9AD4C") };
        static Color Hex(string s) { return new Color(Convert.ToInt32(s.Substring(0, 2), 16) / 255f, Convert.ToInt32(s.Substring(2, 2), 16) / 255f, Convert.ToInt32(s.Substring(4, 2), 16) / 255f); }
        void Awake()
        {
            Application.targetFrameRate = 60;
            font = Resources.Load<Font>("ChessCards/UI");
            cardBody = Resources.Load<Texture2D>("ChessCards/Cards/Body");
            cardBack = Resources.Load<Texture2D>("ChessCards/Cards/Back");
            string[] faces = { "1", "2", "3", "Swap", "Undo", "Skip" };
            for (int i = 0; i < faces.Length; i++)
                cardFaces[i] = Resources.Load<Texture2D>("ChessCards/Cards/Face" + faces[i]);
            for (int side = 0; side < 2; side++) for (int kind = 1; kind <= 6; kind++)
                pieces[side, kind] = Resources.Load<Texture2D>("ChessCards/" + (side == 0 ? "White" : "Black") + (PieceType)kind);
            for (int side = 0; side < 2; side++)
            {
                pieces[side, (int)PieceType.Checker] = Resources.Load<Texture2D>("ChessCards/" + (side == 0 ? "White" : "Black") + "Checker");
                pieces[side, (int)PieceType.CrownedChecker] = Resources.Load<Texture2D>("ChessCards/" + (side == 0 ? "White" : "Black") + "CrownedChecker");
            }
            try { progress = JsonUtility.FromJson<RestorationProgress>(PlayerPrefs.GetString(SaveKey, "{}")); }
            catch (Exception) { progress = new RestorationProgress(); }
            if (progress == null) progress = new RestorationProgress();
            progress.Coins = Math.Max(0, progress.Coins); progress.Wins = Math.Max(0, progress.Wins);
        }
        void Save() { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(progress)); PlayerPrefs.Save(); }
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() { Save(); }
        void StartMatch()
        {
            int seed = Environment.TickCount;
            match = new CardMatch(seed, null, selectedMode); bot = new ChessBot(seed ^ 76543);
            modeSelection = false;
            selected = -1; selectedMoves.Clear(); page = 0; promotions = null; credited = false; location = false;
            botTurn = -1; pendingBotCard = -1; historyOpen = false;
            cardAnnouncement = ""; cardAnnouncementUntil = 0;
            boardNotice = "";
            nextBot = Time.unscaledTime + BotTurnDelay;
        }
        void Update()
        {
            if (location || match == null) return;
            CreditResult();
            if (match.Phase == MatchPhase.Finished || match.Phase == MatchPhase.GoScoring || match.Actor != 1)
            { botTurn = -1; pendingBotCard = -1; return; }
            float now = Time.unscaledTime;
            if (rules || historyOpen || confirmLeave || promotions != null)
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
            else bot.PlayBoardAction(match);
            nextBot = now + BotMoveDelay;
            selected = -1; selectedMoves.Clear(); CreditResult();
        }
        void AnnounceCard(Card card, int actor)
        {
            string who = actor == 0 ? "Ты" : "Бот";
            string effect = card.Kind == CardKind.Number ? card.Number + (card.Number == 1 ? " перемещение." : " перемещения подряд.") :
                card.Kind == CardKind.Swap ? "меняемся сторонами, затем 1 ход." :
                card.Kind == CardKind.Undo ? "отмена последнего хода, затем 1 ход." :
                "1 перемещение, затем пропуск противника" + (match.Mode == GameMode.Chess ? " (кроме шаха)." : ".");
            cardAnnouncement = who + ": «" + card.Title + "» — " + effect;
            announcementColor = card.Color;
            cardAnnouncementUntil = Time.unscaledTime + BotCardPreviewDelay + 1.2f;
        }
        void CreditResult()
        {
            if (match == null || credited || match.Phase != MatchPhase.Finished) return;
            credited = true;
            if (match.Winner == 0) { progress.AwardWin(); Save(); notice = "Победа! +100 монет. Выбери, что восстановить."; }
            else notice = match.Result;
        }
        void Styles()
        {
            if (initialized) return;
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 22, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            text.normal.textColor = ink;
            center = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            bold = new GUIStyle(text) { fontStyle = FontStyle.Bold };
            GUI.skin.button.font = font;
            initialized = true;
        }
        void OnGUI()
        {
            Styles();
            GUI.color = Color.white; GUI.backgroundColor = Color.white; GUI.contentColor = Color.white;
            Fill(new Rect(0, 0, Screen.width, Screen.height), background);
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            portrait = safe.height > safe.width; w = portrait ? 720 : 1280; h = portrait ? 1280 : 720;
            float scale = Mathf.Min(safe.width / w, safe.height / h);
            float ox = safe.x + (safe.width - w * scale) / 2;
            float oy = Screen.height - safe.y - safe.height + (safe.height - h * scale) / 2;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(ox, oy, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            bool blocked = modeSelection || rules || historyOpen || confirmLeave || promotions != null;
            GUI.enabled = !blocked;
            if (location) DrawLocation(); else DrawMatch();
            GUI.enabled = true;
            if (modeSelection) DrawModeSelection();
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
            GUI.Label(r, value, style);
        }
        bool Button(Rect r, string value, Color? color = null, bool enabled = true, int size = 22)
        {
            bool old = GUI.enabled; GUI.enabled = old && enabled;
            Fill(r, GUI.enabled ? color ?? panel : Hex("303746"));
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            Label(r, value, size, GUI.enabled ? ink : muted, true);
            GUI.enabled = old; return clicked;
        }
        void DrawLocation()
        {
            Label(new Rect(32, 20, w - 260, 50), "КОРОЛЕВСКИЙ ДВОР", portrait ? 30 : 36, gold, false, true);
            Label(new Rect(w - 230, 22, 200, 50), progress.Coins + " монет", 26, gold, true);
            Label(new Rect(32, 75, w - 64, 48), "Восстановлено: " + ((progress.Bench ? 1 : 0) + (progress.Lamp ? 1 : 0) + (progress.House ? 1 : 0)) + "/3    •    Побед: " + progress.Wins, 22, muted);
            Rect scene = portrait ? new Rect(32, 140, 656, 400) : new Rect(32, 145, 720, 440);
            DrawCourtyard(scene);
            float x = portrait ? 32 : 790, y = portrait ? 575 : 145, width = portrait ? 656 : 458;
            string[] names = { "Скамейка", "Фонарь", "Дом" };
            string[] details = { "Починить сиденье и спинку", "Вернуть свет во двор", "Восстановить крышу и фасад" };
            for (int i = 0; i < 3; i++)
            {
                int index = i; float yy = y + i * (portrait ? 150 : 128);
                Fill(new Rect(x, yy, width, portrait ? 135 : 115), panel);
                Label(new Rect(x + 18, yy + 8, width - 36, 38), names[i], 27, ink, false, true);
                Label(new Rect(x + 18, yy + 44, width - 36, 30), details[i], 18, muted);
                bool repaired = progress.Repaired(i);
                if (Button(new Rect(x + width - 245, yy + (portrait ? 83 : 75), 225, portrait ? 42 : 32), repaired ? "Восстановлено" : "Починить • " + RestorationProgress.Prices[i], repaired ? Hex("3F705E") : Hex("7B6239"), !repaired && progress.Coins >= RestorationProgress.Prices[i], 19))
                { progress.Repair(index); Save(); notice = names[index] + ": восстановлено!"; }
            }
            Label(new Rect(32, portrait ? 1045 : 592, w - 64, 58), notice, 21, muted);
            float by = portrait ? 1130 : 654;
            if (Button(new Rect(32, by, portrait ? 420 : 690, 64 - (portrait ? 0 : 10)), "ИГРАТЬ  •  награда 100 монет", Hex("87643B"), true, 24)) { modeSelection = true; GUIUtility.ExitGUI(); }
            if (Button(new Rect(portrait ? 470 : 1000, by, portrait ? 218 : 248, 64 - (portrait ? 0 : 10)), "Правила", panel)) rules = true;
        }
        void DrawCourtyard(Rect r)
        {
            // Simple vector placeholder art, with visible changes for each paid repair.
            Fill(r, Hex("323F50"));
            float sx = r.width / 720, sy = r.height / 440;
            GUI.BeginGroup(r);
            Shape(0, 270, 720, 170, Hex("465C50"), sx, sy);
            Shape(0, 365, 720, 75, Hex("9B927B"), sx, sy);
            Shape(380, 140, 270, 225, progress.House ? Hex("D5B387") : Hex("766F6B"), sx, sy);
            Shape(355, 115, 320, 35, progress.House ? Hex("A56351") : Hex("514C51"), sx, sy);
            if (!progress.House) { Shape(440, 115, 65, 35, Hex("323F50"), sx, sy); Shape(570, 140, 12, 140, Hex("514C51"), sx, sy); }
            Shape(418, 190, 65, 75, progress.House ? Hex("F1D898") : Hex("363A40"), sx, sy);
            Shape(545, 190, 65, 75, progress.House ? Hex("F1D898") : Hex("363A40"), sx, sy);
            Shape(492, 275, 52, 90, Hex("625246"), sx, sy);
            Shape(260, 135, 14, 230, Hex("242D38"), sx, sy);
            Shape(243, 115, 49, 44, progress.Lamp ? Hex("F1D08B") : Hex("64707B"), sx, sy);
            Shape(235, 108, 65, 10, Hex("242D38"), sx, sy);
            Shape(80, 292, progress.Bench ? 150 : 58, 18, progress.Bench ? Hex("B78358") : Hex("72675D"), sx, sy);
            Shape(80, 270, progress.Bench ? 150 : 90, 15, progress.Bench ? Hex("B78358") : Hex("72675D"), sx, sy);
            Shape(73, 325, 164, 18, progress.Bench ? Hex("B78358") : Hex("72675D"), sx, sy);
            Shape(85, 343, 12, 28, Hex("242D38"), sx, sy); Shape(211, 343, 12, 28, Hex("242D38"), sx, sy);
            if (!progress.Bench) Shape(153, 325, 32, 18, Hex("465C50"), sx, sy);
            Shape(35, 186, 25, 180, Hex("665244"), sx, sy);
            Shape(8, 135, 115, 94, Hex("597761"), sx, sy);
            Label(new Rect(18, 12, r.width - 36, 44), progress.House && progress.Bench && progress.Lamp ? "ДВОР СНОВА ЖИВЁТ" : "СТАРЫЙ ДВОР", 24, ink, false, true);
            GUI.EndGroup();
        }
        void Shape(float x, float y, float width, float height, Color c, float sx, float sy)
        { Fill(new Rect(x * sx, y * sy, width * sx, height * sy), c); }
        void DrawMatch()
        {
            if (match == null) { location = true; return; }
            if (Button(new Rect(28, 22, 136, 52), "Во двор", panel, true, 21)) confirmLeave = true;
            Label(new Rect(182, 20, w - 510, 54), ModeTitle(match.Mode) + (portrait ? "" : " × КАРТЫ"), portrait ? 25 : 32, gold, false, true);
            if (Button(new Rect(w - 306, 22, 132, 52), "История", panel, true, 20)) historyOpen = true;
            if (Button(new Rect(w - 160, 22, 132, 52), "Правила", panel, true, 20)) rules = true;
            Fill(new Rect(28, 90, w - 56, portrait ? 100 : 62), panel);
            string side = match.HumanSide == Side.White ? "белые" : "чёрные";
            string phase = match.Phase == MatchPhase.GoScoring ? "Подсчёт очков" : match.Phase == MatchPhase.Finished ? match.Result : (match.Actor == 0 ? "Твоя очередь" : "Ход бота") + "  •  " + (match.Phase == MatchPhase.ChooseCard ? "выбор карты" : match.Board != null && match.Board.ForcedCaptureFrom >= 0 ? "продолжи взятие" : "перемещений: " + match.Remaining);
            Label(new Rect(42, 94, w - 84, portrait ? 48 : 30), phase, portrait ? 23 : 24, match.Actor == 0 ? gold : ink, false, true);
            string playerInfo = match.Domino == null ? "Ты: " + side : "Костяшек у бота: " + match.Domino.Rack(match.SideFor(1)).Count;
            Label(new Rect(42, portrait ? 140 : 124, w - 84, 34), playerInfo + "  •  Очередь №" + match.Turn + "  •  Карт у бота: " + match.Hands[1].Count, portrait ? 20 : 18, muted);
            float size = portrait ? 560 : 496;
            Rect board = new Rect(portrait ? 80 : 48, portrait ? 210 : 190, size, size);
            if (match.Go != null) DrawGoBoard(board);
            else if (match.Domino != null) DrawDominoBoard(board);
            else DrawBoard(board);
            float right = portrait ? 32 : 590, top = portrait ? 802 : 180, width = portrait ? 656 : 650;
            if (match.Phase == MatchPhase.GoScoring)
            {
                DrawGoScoring(right, top, width);
                return;
            }
            DrawCards(right, top, width);
            float messageY = portrait ? 1126 : 508;
            float messageHeight = match.Phase == MatchPhase.Finished && portrait ? 30 : portrait ? 64 : 70;
            bool announcing = boardNotice.Length == 0 && match.Phase != MatchPhase.Finished && Time.unscaledTime < cardAnnouncementUntil && cardAnnouncement.Length > 0;
            if (announcing)
            {
                Fill(new Rect(right, messageY, width, messageHeight), cardColors[announcementColor]);
                Label(new Rect(right + 12, messageY + 4, width - 24, messageHeight - 8), cardAnnouncement, 21, ink);
            }
            else Label(new Rect(right, messageY, width, messageHeight), boardNotice.Length > 0 ? boardNotice : match.Message, 21, muted);
            float actionY = portrait ? 1200 : 650;
            if (match.Phase == MatchPhase.Finished)
            {
                Label(new Rect(right, portrait ? 1160 : 594, width, 32), match.Winner == 0 ? "+100 монет уже зачислено" : "Можно попробовать ещё раз", 22, gold, true);
                if (Button(new Rect(right, actionY, width * .56f, 54), "Вернуться во двор", Hex("87643B"))) { location = true; GUIUtility.ExitGUI(); }
                if (Button(new Rect(right + width * .59f, actionY, width * .41f, 54), "Ещё партия")) { StartMatch(); GUIUtility.ExitGUI(); }
            }
            else
            {
                bool canDraw = match.Actor == 0 && match.Phase == MatchPhase.ChooseCard && match.PlayableCards().Count == 0;
                float drawWidth = match.Go == null ? width * .56f : width * .38f;
                if (Button(new Rect(right, actionY, drawWidth, 54), "Взять карту", Hex("386579"), canDraw))
                { match.Draw(); page = Math.Max(0, (match.Hands[0].Count - 1) / 4); selected = -1; boardNotice = ""; CreditResult(); GUIUtility.ExitGUI(); }
                if (match.Go != null && Button(new Rect(right + width * .41f, actionY, width * .22f, 54), "Пас", panel, match.Actor == 0))
                { match.PassGo(); boardNotice = ""; cardAnnouncementUntil = 0; CreditResult(); GUIUtility.ExitGUI(); }
                float deckStart = match.Go == null ? .59f : .65f;
                Label(new Rect(right + width * deckStart, actionY, width * (1 - deckStart), 54), "Колода: " + match.DeckCount + "\nСброс: " + match.DiscardCount, 18, muted, true);
            }
            if (!portrait && match.Phase != MatchPhase.Finished)
            {
                string lines = "";
                int start = Math.Max(0, match.Log.Count - 2);
                for (int i = start; i < match.Log.Count; i++) lines += match.Log[i] + "\n";
                Label(new Rect(right, 584, width, 56), lines, 17, muted);
            }
        }
        void DrawGoScoring(float x, float y, float width)
        {
            Fill(new Rect(x, y, width, 232), panel);
            Label(new Rect(x + 18, y + 16, width - 36, 54), match.GoScoreText, 30, gold, true, true);
            Label(new Rect(x + 24, y + 75, width - 48, 40), "У белых уже учтены 7,5 очка коми.", 20, muted, true);
            Label(new Rect(x + 24, y + 122, width - 48, 84),
                "Нажми на камень в мёртвой группе, чтобы исключить всю группу из подсчёта. Повторное нажатие возвращает группу.", 23, ink);

            float buttonY = portrait ? 1200 : 650;
            float buttonWidth = (width - 16) / 2;
            if (Button(new Rect(x, buttonY, buttonWidth, 54), "Продолжить игру", panel))
            {
                match.ResumeGo();
                cardAnnouncementUntil = 0;
                GUIUtility.ExitGUI();
            }
            if (Button(new Rect(x + buttonWidth + 16, buttonY, buttonWidth, 54), "Подтвердить счёт", Hex("87643B")))
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
            Fill(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), Hex("8F663B"));
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
                    boardNotice = placed ? "" : "Этот ход оставляет группу без свободных точек или повторяет позицию.";
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
            Fill(r, panel);
            Label(new Rect(r.x + 16, r.y + 6, r.width - 32, 33), "Цепочка", 24, gold, false, true);
            if (domino.Chain.Count == 0)
                Label(new Rect(r.x + 16, r.y + 44, r.width - 32, 70), "Первой можно выложить любую костяшку.", 20, muted, true);
            else
            {
                Label(new Rect(r.x + 16, r.y + 40, r.width - 32, 28),
                    "Слева " + domino.LeftEnd + "    •    Справа " + domino.RightEnd, 18, muted);
                for (int i = 0; i < domino.Chain.Count; i++)
                    DrawDominoTile(new Rect(r.x + 16 + i % 7 * (tileWidth + 5), r.y + 72 + i / 7 * 38, tileWidth, tileHeight), domino.Chain[i], false);
            }

            float half = (r.width - 48) / 2;
            if (Button(new Rect(r.x + 16, r.y + 231, half, 48), "Выложить слева", Hex("386579"),
                canMove && domino.CanPlace(match.HumanSide, selected, true), 18))
                PlaySelectedDomino(true);
            if (Button(new Rect(r.x + 32 + half, r.y + 231, half, 48), "Выложить справа", Hex("386579"),
                canMove && domino.CanPlace(match.HumanSide, selected, false), 18))
                PlaySelectedDomino(false);

            if (active && !canMove)
            {
                string action = domino.StockCount > 0 ? "Взять из базара · " + domino.StockCount : "Пас · базар пуст";
                if (Button(new Rect(r.x + 16, r.y + 286, r.width - 32, 43), action, Hex("87643B"), true, 19))
                {
                    if (domino.StockCount > 0) match.DrawDomino(); else match.PassDomino();
                    selected = -1; cardAnnouncementUntil = 0;
                    CreditResult();
                    GUIUtility.ExitGUI();
                }
            }
            else Label(new Rect(r.x + 16, r.y + 286, r.width - 32, 43),
                (canMove ? "Выбери костяшку и край цепочки" : "Базар: " + domino.StockCount), 18, muted, true);

            Label(new Rect(r.x + 16, r.y + 330, r.width - 32, 29), "Твои костяшки · " + rack.Count, 19, gold);
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
            match.PlaceDomino(selected, left);
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
            Fill(new Rect(r.x - 5, r.y - 5, r.width + 10, r.height + 10), Hex("A58B63"));
            bool whiteBottom = match.HumanSide == Side.White;
            var board = match.Board;
            bool whiteCheck = board.InCheck(Side.White), blackCheck = board.InCheck(Side.Black);
            for (int row = 0; row < 8; row++) for (int col = 0; col < 8; col++)
            {
                int file = whiteBottom ? col : 7 - col, rank = whiteBottom ? 7 - row : row;
                int square = rank * 8 + file;
                Rect tile = new Rect(r.x + col * cell, r.y + row * cell, cell, cell);
                Color c = (file + rank) % 2 == 0 ? Hex("739084") : Hex("E1D6BE");
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
                    Texture2D icon = pieces[(int)p.Side, (int)p.Type];
                    bool captured = match.Mode == GameMode.Checkers && CheckersRules.Pending(board, square);
                    Color oldColor = GUI.color;
                    if (captured) GUI.color = new Color(1, 1, 1, .35f);
                    if (icon != null) GUI.DrawTexture(new Rect(tile.x + 4, tile.y + 4, cell - 8, cell - 8), icon, ScaleMode.ScaleToFit);
                    else Label(tile, p.Type.ToString().Substring(0, 1), 34, p.Side == Side.White ? Color.white : Color.black, true);
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
                else { match.Move(choices[0]); CreditResult(); }
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
            if (cardBody == null || art == null)
            {
                Fill(r, cardColors[card.Color]);
                Label(r, card.Title, 26, ink, true);
                return;
            }
            Color old = GUI.color;
            GUI.color = new Color(0, 0, 0, .26f * opacity);
            GUI.DrawTexture(new Rect(r.x + 4, r.y + 6, r.width, r.height), cardBody);
            if (highlighted)
            {
                GUI.color = new Color(gold.r, gold.g, gold.b, .92f);
                GUI.DrawTexture(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), cardBody);
            }
            Color color = cardColors[card.Color];
            GUI.color = new Color(color.r, color.g, color.b, opacity);
            GUI.DrawTexture(r, cardBody);
            GUI.color = new Color(1, 1, 1, opacity);
            GUI.DrawTexture(r, art);
            GUI.color = old;
        }
        void DrawCards(float x, float y, float width)
        {
            Card shown = match.Top;
            bool preview = match.Actor == 1 && pendingBotCard >= 0 && pendingBotCard < match.Hands[1].Count;
            if (preview) shown = match.Hands[1][pendingBotCard];
            Fill(new Rect(x, y, width, 94), panel);
            DrawCardFace(new Rect(x + 14, y + 5, 58, 81), shown, preview);
            Label(new Rect(x + 90, y + 8, width - 202, 25), preview ? "БОТ ПОКАЗЫВАЕТ КАРТУ" : "ВЕРХНЯЯ КАРТА СБРОСА", 16, preview ? gold : muted);
            Label(new Rect(x + 90, y + 34, width - 202, 40), shown.ColorName + " «" + shown.Title + "»", 24, ink, false, true);
            if (cardBack != null) GUI.DrawTexture(new Rect(x + width - 67, y + 7, 46, 64), cardBack);
            Label(new Rect(x + width - 90, y + 72, 90, 20), "БОТ ×" + match.Hands[1].Count, 15, muted, true);
            int count = match.Hands[0].Count, pages = Math.Max(1, (count + 3) / 4);
            page = Math.Min(page, pages - 1);
            Label(new Rect(x, y + 101, width - 150, 34), "Твоя рука: " + count + "  •  " + (page + 1) + "/" + pages, 19, muted);
            if (Button(new Rect(x + width - 140, y + 100, 62, 36), "←", panel, page > 0)) page--;
            if (Button(new Rect(x + width - 64, y + 100, 62, 36), "→", panel, page < pages - 1)) page++;
            float gap = 12, slotWidth = (width - 3 * gap) / 4;
            for (int slot = 0; slot < 4; slot++)
            {
                int i = page * 4 + slot; if (i >= count) break;
                Card card = match.Hands[0][i];
                bool playable = match.Actor == 0 && match.CanPlay(i);
                Rect r = new Rect(x + slot * (slotWidth + gap) + (slotWidth - 120) / 2, y + 146, 120, 168);
                DrawCardFace(r, card, playable, playable ? 1 : .66f);
                bool old = GUI.enabled; GUI.enabled = old && playable;
                if (GUI.Button(r, new GUIContent("", match.Actor == 0 ? match.UnavailableReason(i) : "Ход бота"), GUIStyle.none))
                { if (match.PlayCard(i)) AnnounceCard(card, 0); selected = -1; selectedMoves.Clear(); boardNotice = ""; CreditResult(); GUIUtility.ExitGUI(); }
                GUI.enabled = old;
            }
            if (count == 0) Label(new Rect(x, y + 152, width, 150), "Рука пуста. В свою очередь возьми карту.", 22, muted, true);
        }
        void Overlay(Rect r, string title)
        {
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, .85f)); Fill(r, panel);
            Label(new Rect(r.x + 24, r.y + 18, r.width - 48, 58), title, 28, gold, false, true);
        }
        void DrawLeave()
        {
            Rect r = new Rect(40, h / 2 - 170, w - 80, 340); Overlay(r, "Вернуться во двор?");
            Label(new Rect(r.x + 24, r.y + 86, r.width - 48, 110), "Текущая партия завершится без награды. Монеты и ремонт сохранятся.", 24);
            if (Button(new Rect(r.x + 24, r.y + 232, (r.width - 64) / 2, 64), "Остаться")) confirmLeave = false;
            if (Button(new Rect(r.x + 40 + (r.width - 64) / 2, r.y + 232, (r.width - 64) / 2, 64), "Во двор", Hex("805246")))
            { confirmLeave = false; location = true; promotions = null; }
        }
        void DrawPromotion()
        {
            Rect r = new Rect(40, h / 2 - 150, w - 80, 300); Overlay(r, "Превращение пешки");
            string[] titles = { "Ферзь", "Ладья", "Слон", "Конь" };
            for (int i = 0; i < promotions.Count; i++)
            {
                float cw = (r.width - 60) / 4;
                if (Button(new Rect(r.x + 18 + i * (cw + 6), r.y + 112, cw, 120), titles[i], Hex("386579")))
                { match.Move(promotions[i]); promotions = null; CreditResult(); GUIUtility.ExitGUI(); }
            }
        }
        static string ModeTitle(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Checkers: return "ШАШКИ";
                case GameMode.Go: return "ГО";
                case GameMode.Domino: return "ДОМИНО";
                default: return "ШАХМАТЫ";
            }
        }

        void DrawModeChoice(Rect r, GameMode mode, string description)
        {
            if (Button(r, "", mode == selectedMode ? Hex("3C4A5C") : Hex("303C50")))
            { selectedMode = mode; StartMatch(); GUIUtility.ExitGUI(); }
            if (mode == GameMode.Domino) DrawDominoTile(new Rect(r.x + 12, r.y + 38, 58, 32), new DominoTile(2, 5), false);
            else
            {
                Texture2D icon = mode == GameMode.Chess ? pieces[0, (int)PieceType.King] :
                    pieces[mode == GameMode.Go ? 1 : 0, (int)PieceType.Checker];
                if (icon != null) GUI.DrawTexture(new Rect(r.x + 10, r.y + 24, 62, 62), icon, ScaleMode.ScaleToFit);
            }
            Label(new Rect(r.x + 82, r.y + 12, r.width - 94, 42), ModeTitle(mode), 25, gold, false, true);
            Label(new Rect(r.x + 82, r.y + 53, r.width - 94, r.height - 61), description, 19, ink);
        }

        void DrawModeSelection()
        {
            Rect r = portrait ? new Rect(40, h / 2 - 300, w - 80, 600) : new Rect(200, 100, 880, 520);
            Overlay(r, "Выбери игру");
            float tileWidth = (r.width - 64) / 2;
            float tileHeight = portrait ? 166 : 148;
            var modes = new[] { GameMode.Chess, GameMode.Checkers, GameMode.Go, GameMode.Domino };
            var descriptions = new[] { "Поставь мат королю.", "Русские шашки 8×8.", "Займи территорию на доске 9×9.", "Выложи все костяшки." };
            for (int i = 0; i < modes.Length; i++)
            {
                var tile = new Rect(r.x + 24 + i % 2 * (tileWidth + 16), r.y + 100 + i / 2 * (tileHeight + 16), tileWidth, tileHeight);
                DrawModeChoice(tile, modes[i], descriptions[i]);
            }
            if (Button(new Rect(r.x + 24, r.y + r.height - 80, r.width - 48, 56), "Назад", Hex("87643B"))) modeSelection = false;
        }

        void DrawHistory()
        {
            Rect r = new Rect(24, 24, w - 48, h - 48);
            Overlay(r, "История действий");
            Rect view = new Rect(r.x + 20, r.y + 88, r.width - 40, r.height - 186);
            string content = "";
            for (int i = 0; i < match.Log.Count; i++) content += (i + 1) + ". " + match.Log[i] + "\n\n";
            GUIStyle style = new GUIStyle(text) { fontSize = portrait ? 23 : 21, alignment = TextAnchor.UpperLeft };
            float contentHeight = style.CalcHeight(new GUIContent(content), view.width - 36) + 20;
            historyScroll = GUI.BeginScrollView(view, historyScroll, new Rect(0, 0, view.width - 26, contentHeight));
            GUI.Label(new Rect(4, 0, view.width - 36, contentHeight), content, style);
            GUI.EndScrollView();
            if (Button(new Rect(r.x + 24, r.y + r.height - 80, r.width - 48, 56), "Вернуться к партии", Hex("87643B"))) historyOpen = false;
        }
        void DrawRules()
        {
            Rect r = new Rect(24, 24, w - 48, h - 48); Overlay(r, "Как играть");
            Rect view = new Rect(r.x + 20, r.y + 88, r.width - 40, r.height - 186);
            string content =
                "ЦЕЛЬ\nПоставь мат боту. За победу — 100 монет. Во дворе выбери объект и оплати ремонт. Пат и поражение не дают награду.\n\n" +
                "ОДНА КАРТА ЗА ОЧЕРЕДЬ\nРазыграй карту того же цвета или значения, что верхняя карта сброса. Числа 1–3 дают столько же шахматных перемещений подряд. Можно двигать одну фигуру несколько раз. Специальные карты совпадают по цвету или своему эффекту.\n\n" +
                "ДОБОР\nЕсли доступных карт нет, возьми одну. Подходящую можно сразу сыграть. Иначе очередь переходит боту. Рука автоматически не пополняется. Если колода закончилась, сброс перемешивается, верхняя карта остаётся.\n\n" +
                "ШАХ И МАТ\nШах немедленно завершает серию перемещений: противник должен защититься. Если подходящая карта не пришла при шахе, всё равно даётся одно защитное перемещение. Мат проверяется сразу после каждого перемещения.\n\n" +
                "ОБМЕН СТОРОН\nИгрок и бот меняются армиями, а доска разворачивается. Руки карт остаются у своих владельцев. Затем сыгравший делает одно перемещение новой стороной. Карта недоступна, пока любой король под шахом.\n\n" +
                "ОТМЕНА\nОткатывает последнее шахматное перемещение: возвращает взятую фигуру, права рокировки и другие параметры доски. Карты не возвращаются. Затем даётся одно своё перемещение. Нельзя отменить защиту противника от шаха и продолжить атаку без его ответа.\n\n" +
                "ПРОПУСК\nДаёт одно перемещение, после которого противник пропускает очередь. Если ты поставишь шах, пропуск отменяется: противник получает возможность защититься.\n\n" +
                "ШАХМАТНЫЕ ОСОБЕННОСТИ\nЕсть рокировка, выбор превращения пешки и взятие на проходе. На проходе можно взять только следующим шахматным перемещением. Карта без перемещения не расходует эту возможность. Короля нельзя брать или оставлять под ударом.\n\n" +
                "НИЧЬЯ\nПат, два оставшихся короля или 100 перемещений без движения пешек и взятий. Троекратное повторение в этом прототипе не проверяется.\n\n" +
                "УПРАВЛЕНИЕ\nНажми карту, затем свою фигуру и подсвеченную клетку. Нажми фигуру повторно, чтобы снять выбор. Стрелки перелистывают руку. Правила можно прокручивать. Сохраняются монеты и ремонт; незавершённая партия при закрытии не сохраняется.";
            if ((!location && match != null ? match.Mode : selectedMode) == GameMode.Checkers)
                content = "ШАШКИ × КАРТЫ\nРусские шашки на доске 8×8. У каждой стороны по 12 шашек; ходят по тёмным клеткам. Для победы забери все шашки соперника или лиши их допустимых ходов.\n\n" +
                    "ХОДЫ И ВЗЯТИЯ\nПростая шашка ходит на соседнюю диагональную клетку вперёд, а бьёт в обе стороны. Если есть взятие, нужно бить. При нескольких вариантах можно выбрать любой.\n\n" +
                    "СЕРИЯ ВЗЯТИЙ\nПродолжай брать одной и той же шашкой, пока возможны взятия. Вся цепочка считается одним перемещением карты. Подсветка автоматически остаётся на этой шашке. Взятые шашки отмечаются крестиком и снимаются после всей цепочки; повторно брать или перепрыгивать их нельзя.\n\n" +
                    "ДАМКА\nНа последней горизонтали шашка превращается в дамку. Дамка ходит на любое свободное расстояние по диагонали. Если превращение произошло при взятии, цепочка продолжается уже дамкой.\n\n" +
                    "КАРТЫ\nОдна карта за очередь, совпадение по цвету или значению. Числа 1–3 дают столько перемещений подряд. Если доступных карт нет, возьми одну: подходящую можно сыграть сразу; иначе очередь переходит противнику, даже если на доске было взятие. Рука не пополняется автоматически.\n\n" +
                    "СПЕЦИАЛЬНЫЕ КАРТЫ\nОбмен меняет владельцев армий и разворачивает доску. Отмена возвращает последнее завершённое перемещение целиком, включая всю цепочку взятий. Пропуск даёт одно перемещение, затем противник пропускает очередь. У каждой специальной карты есть одно своё перемещение после эффекта.\n\n" +
                    "НИЧЬЯ\nПосле 15 завершённых перемещений дамками каждой стороны без взятия и движения простой шашки. Остальные турнирные ограничения ничьей в прототипе не проверяются.\n\n" +
                    "НАГРАДА И УПРАВЛЕНИЕ\nЗа победу — 100 монет в общий баланс двора. Нажми карту, шашку и подсвеченную клетку. Во время серии выбирай следующую подсвеченную клетку. История показывает карты и перемещения. Партия при закрытии не сохраняется, монеты и ремонт сохраняются.";
            GameMode mode = location || match == null ? selectedMode : match.Mode;
            if (mode == GameMode.Go)
                content = "ГО 9×9\nТы начинаешь чёрными. Нажимай на свободные пересечения линий. Камни соединяются по горизонтали и вертикали. Когда у группы противника не остаётся соседних свободных точек, её камни снимаются. Нельзя оставлять свою группу без свободных точек или повторять уже сыгранную позицию.\n\n" +
                    "КАРТЫ\nКарта даёт 1–3 постановки камней. Обмен меняет цвета, отмена возвращает позицию до последней постановки, а пропуск даёт один ход и оставляет очередь у сыгравшего карту. Правила подбора карт по цвету и значению общие.\n\n" +
                    "ПАС И ПОДСЧЁТ\nПас завершает очередь, в том числе до выбора карты. После двух пасов открывается подсчёт. Нажми на камни в мёртвых группах и подтверди результат. Если статус группы неясен, продолжи партию. Очки дают камни на доске и окружённые свободные точки. Белым добавляется 7,5 очка. Победа даёт 100 монет.";
            else if (mode == GameMode.Domino)
                content = "ДОМИНО С ДОБОРОМ\nНабор 0–6. На старте по 7 костяшек. Первый ход твой, выложи любую. Дальше выбери костяшку и нажми кнопку края цепочки с таким же числом точек.\n\n" +
                    "КАРТЫ И БАЗАР\nКарта даёт 1–3 выкладывания. Обмен меняет руки костяшек, отмена возвращает позицию до последнего выкладывания, пропуск даёт один ход и оставляет очередь у сыгравшего карту. Если подходящей костяшки нет, после розыгрыша карты бери из базара до подходящей. Добор костяшки не тратит ход карты. При пустом базаре пас передаёт очередь.\n\n" +
                    "ПОБЕДА\nПобеждает тот, кто первым выложил все костяшки. Если базар пуст и оба заблокированы, выигрывает тот, у кого на руках меньше точек. При равенстве ничья. Победа даёт 100 монет.";
            GUIStyle ruleStyle = new GUIStyle(text) { fontSize = portrait ? 23 : 21, alignment = TextAnchor.UpperLeft };
            float contentHeight = ruleStyle.CalcHeight(new GUIContent(content), view.width - 36) + 20;
            rulesScroll = GUI.BeginScrollView(view, rulesScroll, new Rect(0, 0, view.width - 26, contentHeight));
            GUI.Label(new Rect(4, 0, view.width - 36, contentHeight), content, ruleStyle);
            GUI.EndScrollView();
            if (Button(new Rect(r.x + 24, r.y + r.height - 80, r.width - 48, 56), "Понятно", Hex("87643B"))) rules = false;
        }
    }
}
