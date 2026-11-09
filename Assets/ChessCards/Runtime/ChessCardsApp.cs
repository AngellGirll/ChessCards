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
        ChessBot bot;
        RestorationProgress progress;
        Font font;
        Texture2D[,] pieces = new Texture2D[2, 7];
        GUIStyle text, center, bold;
        bool initialized, location = true, rules, confirmLeave, credited;
        int selected = -1, page;
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
        readonly Color[] cardColors = { Hex("AF4652"), Hex("386FA4"), Hex("448B72"), Hex("AA842D") };
        static Color Hex(string s) { return new Color(Convert.ToInt32(s.Substring(0, 2), 16) / 255f, Convert.ToInt32(s.Substring(2, 2), 16) / 255f, Convert.ToInt32(s.Substring(4, 2), 16) / 255f); }
        void Awake()
        {
            Application.targetFrameRate = 60;
            font = Resources.Load<Font>("ChessCards/UI");
            for (int side = 0; side < 2; side++) for (int kind = 1; kind <= 6; kind++)
                pieces[side, kind] = Resources.Load<Texture2D>("ChessCards/" + (side == 0 ? "White" : "Black") + (PieceType)kind);
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
            match = new CardMatch(seed); bot = new ChessBot(seed ^ 76543);
            selected = -1; page = 0; promotions = null; credited = false; location = false;
            botTurn = -1; pendingBotCard = -1; historyOpen = false;
            cardAnnouncement = ""; cardAnnouncementUntil = 0;
            nextBot = Time.unscaledTime + BotTurnDelay;
        }
        void Update()
        {
            if (location || match == null) return;
            CreditResult();
            if (match.Phase == MatchPhase.Finished || match.Actor != 1)
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
            else match.Move(bot.ChooseMove(match));
            nextBot = now + BotMoveDelay;
            selected = -1; selectedMoves.Clear(); CreditResult();
        }
        void AnnounceCard(Card card, int actor)
        {
            string who = actor == 0 ? "Ты" : "Бот";
            string effect = card.Kind == CardKind.Number ? card.Number + (card.Number == 1 ? " перемещение." : " перемещения подряд.") :
                card.Kind == CardKind.Swap ? "меняемся армиями; затем 1 перемещение." :
                card.Kind == CardKind.Undo ? "последнее перемещение возвращается назад." :
                "1 перемещение, затем пропуск противника (кроме шаха).";
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
            bool blocked = rules || historyOpen || confirmLeave || promotions != null;
            GUI.enabled = !blocked;
            if (location) DrawLocation(); else DrawMatch();
            GUI.enabled = true;
            if (rules) DrawRules();
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
            if (Button(new Rect(32, by, portrait ? 420 : 690, 64 - (portrait ? 0 : 10)), "ИГРАТЬ  •  награда 100 монет", Hex("87643B"), true, 24)) { StartMatch(); GUIUtility.ExitGUI(); }
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
            Label(new Rect(182, 20, w - 510, 54), portrait ? "ПАРТИЯ" : "ШАХМАТЫ × КАРТЫ", portrait ? 25 : 32, gold, false, true);
            if (Button(new Rect(w - 306, 22, 132, 52), "История", panel, true, 20)) historyOpen = true;
            if (Button(new Rect(w - 160, 22, 132, 52), "Правила", panel, true, 20)) rules = true;
            Fill(new Rect(28, 90, w - 56, portrait ? 100 : 62), panel);
            string side = match.HumanSide == Side.White ? "белые" : "чёрные";
            string phase = match.Phase == MatchPhase.Finished ? match.Result : (match.Actor == 0 ? "Твоя очередь" : "Ход бота") + "  •  " + (match.Phase == MatchPhase.ChooseCard ? "выбор карты" : "перемещений: " + match.Remaining);
            Label(new Rect(42, 94, w - 84, portrait ? 48 : 30), phase, portrait ? 23 : 24, match.Actor == 0 ? gold : ink, false, true);
            Label(new Rect(42, portrait ? 140 : 124, w - 84, 34), "Ты: " + side + "  •  Очередь №" + match.Turn + "  •  Карт у бота: " + match.Hands[1].Count, portrait ? 20 : 18, muted);
            float size = portrait ? 624 : 496;
            Rect board = new Rect(portrait ? 48 : 48, portrait ? 218 : 190, size, size);
            DrawBoard(board);
            float right = portrait ? 32 : 590, top = portrait ? 875 : 180, width = portrait ? 656 : 650;
            DrawCards(right, top, width);
            float messageY = portrait ? 1110 : 470;
            bool announcing = match.Phase != MatchPhase.Finished && Time.unscaledTime < cardAnnouncementUntil && cardAnnouncement.Length > 0;
            if (announcing)
            {
                Fill(new Rect(right, messageY, width, portrait ? 70 : 84), cardColors[announcementColor]);
                Label(new Rect(right + 12, messageY + 4, width - 24, portrait ? 62 : 76), cardAnnouncement, 21, ink);
            }
            else Label(new Rect(right, messageY, width, portrait ? 70 : 84), match.Message, 21, muted);
            float actionY = portrait ? 1200 : 650;
            if (match.Phase == MatchPhase.Finished)
            {
                Label(new Rect(right, portrait ? 1150 : 568, width, 42), match.Winner == 0 ? "+100 монет уже зачислено" : "Можно попробовать ещё раз", 22, gold, true);
                if (Button(new Rect(right, actionY, width * .56f, 54), "Вернуться во двор", Hex("87643B"))) { location = true; GUIUtility.ExitGUI(); }
                if (Button(new Rect(right + width * .59f, actionY, width * .41f, 54), "Ещё партия")) { StartMatch(); GUIUtility.ExitGUI(); }
            }
            else
            {
                bool canDraw = match.Actor == 0 && match.Phase == MatchPhase.ChooseCard && match.PlayableCards().Count == 0;
                if (Button(new Rect(right, actionY, width * .56f, 54), "Взять карту", Hex("386579"), canDraw))
                { match.Draw(); page = Math.Max(0, (match.Hands[0].Count - 1) / 4); selected = -1; CreditResult(); GUIUtility.ExitGUI(); }
                Label(new Rect(right + width * .59f, actionY, width * .41f, 54), "Колода: " + match.DeckCount + "\nСброс: " + match.DiscardCount, 18, muted, true);
            }
            if (!portrait)
            {
                string lines = "";
                int start = Math.Max(0, match.Log.Count - 2);
                for (int i = start; i < match.Log.Count; i++) lines += match.Log[i] + "\n";
                Label(new Rect(right, 566, width, 74), lines, 17, muted);
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
                    if (icon != null) GUI.DrawTexture(new Rect(tile.x + 4, tile.y + 4, cell - 8, cell - 8), icon, ScaleMode.ScaleToFit);
                    else Label(tile, p.Type.ToString().Substring(0, 1), 34, p.Side == Side.White ? Color.white : Color.black, true);
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
                selected = -1; selectedMoves.Clear(); return;
            }
            Piece p = match.Board.Squares[square]; selectedMoves.Clear();
            if (p.Type == PieceType.None || p.Side != match.HumanSide || selected == square) { selected = -1; return; }
            selected = square;
            foreach (var m in match.Board.LegalMoves(match.HumanSide)) if (m.From == square) selectedMoves.Add(m);
        }
        void DrawCards(float x, float y, float width)
        {
            Card top = match.Top;
            Fill(new Rect(x, y, width, 54), panel);
            Fill(new Rect(x, y, 14, 54), cardColors[top.Color]);
            Label(new Rect(x + 26, y, width - 32, 54), "Сброс: " + top.ColorName + " «" + top.Title + "»", 23, ink, false, true);
            int count = match.Hands[0].Count, pages = Math.Max(1, (count + 3) / 4);
            page = Math.Min(page, pages - 1);
            Label(new Rect(x, y + 61, width - 150, 32), "Твоя рука: " + count + "  •  " + (page + 1) + "/" + pages, 19, muted);
            if (Button(new Rect(x + width - 140, y + 60, 62, 36), "←", panel, page > 0)) page--;
            if (Button(new Rect(x + width - 64, y + 60, 62, 36), "→", panel, page < pages - 1)) page++;
            float gap = 12, cw = (width - 3 * gap) / 4;
            for (int slot = 0; slot < 4; slot++)
            {
                int i = page * 4 + slot; if (i >= count) break;
                Card card = match.Hands[0][i];
                bool playable = match.Actor == 0 && match.CanPlay(i);
                Rect r = new Rect(x + slot * (cw + gap), y + 106, cw, portrait ? 114 : 154);
                Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), playable ? gold : panel);
                Fill(r, playable ? cardColors[card.Color] : Color.Lerp(cardColors[card.Color], background, .57f));
                Label(new Rect(r.x + 4, r.y + 8, r.width - 8, portrait ? 42 : 62), card.Title, card.Kind == CardKind.Number ? 40 : 18, ink, true, card.Kind == CardKind.Number);
                string sub = card.Kind == CardKind.Number ? card.Number + " перемещ." : card.Kind == CardKind.Swap ? "Сменить сторону\n+1 перемещение" : card.Kind == CardKind.Undo ? "Откатить ход\n+1 перемещение" : "+1 перемещение\nПропуск врага";
                Label(new Rect(r.x + 4, r.y + (portrait ? 53 : 80), r.width - 8, 55), sub, 16, ink, true);
                bool old = GUI.enabled; GUI.enabled = old && playable;
                if (GUI.Button(r, new GUIContent("", match.Actor == 0 ? match.UnavailableReason(i) : "Ход бота"), GUIStyle.none))
                { if (match.PlayCard(i)) AnnounceCard(card, 0); selected = -1; selectedMoves.Clear(); CreditResult(); GUIUtility.ExitGUI(); }
                GUI.enabled = old;
            }
            if (count == 0) Label(new Rect(x, y + 112, width, 110), "Рука пуста. В свою очередь возьми карту.", 22, muted, true);
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
            GUIStyle ruleStyle = new GUIStyle(text) { fontSize = portrait ? 23 : 21, alignment = TextAnchor.UpperLeft };
            float contentHeight = ruleStyle.CalcHeight(new GUIContent(content), view.width - 36) + 20;
            rulesScroll = GUI.BeginScrollView(view, rulesScroll, new Rect(0, 0, view.width - 26, contentHeight));
            GUI.Label(new Rect(4, 0, view.width - 36, contentHeight), content, ruleStyle);
            GUI.EndScrollView();
            if (Button(new Rect(r.x + 24, r.y + r.height - 80, r.width - 48, 56), "Понятно", Hex("87643B"))) rules = false;
        }
    }
}
