using System;
using UnityEngine;

namespace ChessCards
{
    public sealed partial class ChessCardsApp
    {
        Texture2D matchCloth, matchWood;
        Color MatchVelvet => ActiveTheme?.MatchVelvet ?? Hex("5C132B");
        Color MatchBurgundy => ActiveTheme?.MatchBurgundy ?? Hex("64192F");
        int MatchLabelSize => ActiveTheme?.MatchLabelSize ?? 22;
        bool MatchOrnaments => ActiveTheme?.MatchOrnaments ?? true;

        Rect MatchRegion(ScreenBlock block)
        { return Region(block, ChessCardsScreen.DefaultMatchRect(block, portrait)); }

        void LoadMatchStyle(ChessCardsTheme config)
        {
            if (matchCloth != null) Destroy(matchCloth);
            if (matchWood != null) Destroy(matchWood);
            matchCloth = CourtyardSurface(config?.MatchVelvet ?? Hex("5C132B"), false);
            matchWood = CourtyardSurface(config?.MatchWood ?? Hex("88431E"), true);
        }

        void DrawMatchTable()
        {
            Rect r = MatchRegion(ScreenBlock.Table);
            float rim = Math.Max(4, Math.Min(24, ActiveTheme?.TableRim ?? 12));
            float radius = Math.Max(rim + 7, ActiveTheme?.TableCornerRadius ?? 32);
            Framed(new Rect(r.x - 8, r.y + 7, r.width + 16, r.height), ActiveTheme?.MatchWood ?? Hex("88431E"), radius + 7, 4);
            Framed(r, MatchBurgundy, radius, 3);
            Rounded(Inset(r, 4), Color.white, radius - 4, ActiveTheme?.MatchWoodTexture ?? matchWood);
            Rect felt = Inset(r, rim);
            Framed(felt, MatchVelvet, radius - rim, 2);
            Rounded(Inset(felt, 4), Color.white, Math.Max(0, radius - rim - 4), ActiveTheme?.MatchClothTexture ?? matchCloth);
            if (!MatchOrnaments) return;
            DrawYardCorner(new Rect(r.x, r.y, 34, 34), false, false);
            DrawYardCorner(new Rect(r.x + r.width - 34, r.y, 34, 34), true, false);
            DrawYardCorner(new Rect(r.x, r.y + r.height - 34, 34, 34), false, true);
            DrawYardCorner(new Rect(r.x + r.width - 34, r.y + r.height - 34, 34, 34), true, true);
        }

        void DrawMatch()
        {
            if (match == null) { ReturnToCourtyard(); return; }
            DrawMatchTable();
            if (Button(MatchRegion(ScreenBlock.Back), T("‹ Во двор", "‹ Courtyard"), MatchBurgundy, true, 20)) confirmLeave = true;
            Rect title = MatchRegion(ScreenBlock.Title);
            Framed(title, MatchBurgundy, 22, 4);
            YardText(Inset(title, 5), ModeTitle(match.Mode) + (portrait ? "" : T(" × КАРТЫ", " × CARDS")),
                portrait ? 22 : (ActiveTheme?.MatchTitleSize ?? 32), TrimLight, true);
            if (MatchOrnaments && yardCrest != null && !portrait)
                GUI.DrawTexture(new Rect(title.x + title.width / 2 - 49, title.y - 17, 98, 31), yardCrest, ScaleMode.ScaleToFit);
            if (Button(MatchRegion(ScreenBlock.History), T("История", "History"), MatchBurgundy, true, 18)) historyOpen = true;
            if (Button(MatchRegion(ScreenBlock.Rules), T("Правила", "Rules"), MatchBurgundy, true, 18)) { rulesScroll = new Vector2(); rules = true; }
            if (Button(MatchRegion(ScreenBlock.Settings), "⚙", MatchBurgundy, true, 28)) settingsOpen = true;
            DrawMatchStatus();

            Rect board = MatchRegion(ScreenBlock.Board);
            board.width = board.height = Math.Min(board.width, board.height);
            if (match.Go != null) DrawGoBoard(board);
            else if (match.Domino != null) DrawDominoBoard(board);
            else DrawBoard(board);
            DrawPlayerInfo();
            if (match.Phase == MatchPhase.GoScoring)
            {
                Rect scoring = MatchRegion(ScreenBlock.Scoring);
                DrawGoScoring(scoring);
                return;
            }
            DrawCards();
            DrawMatchNotice();
            DrawMatchActions();
        }

        void DrawMatchStatus()
        {
            Rect r = MatchRegion(ScreenBlock.MatchStatus);
            // Two short lines reserve room for check/capture/moves and the turn number.
            Framed(r, MatchBurgundy, 24, 3);
            string headline = match.Phase == MatchPhase.Finished ? match.GetResult(English) :
                match.Phase == MatchPhase.GoScoring ? T("Подсчёт очков", "Scoring") :
                match.Actor == 0 ? T("Твой ход", "Your turn") : T("Ход бота", "Bot's turn");
            YardText(new Rect(r.x + 12, r.y + 2, r.width - 24, r.height * .56f), headline, 27, TrimLight, true);
            string detail = T("Ход ", "Turn ") + match.Turn;
            if (match.Phase == MatchPhase.ChooseCard) detail += match.Actor == 0
                ? T("  •  Выбери карту", "  •  Choose a card") : T("  •  Выбирает карту", "  •  Choosing a card");
            else if (match.Phase == MatchPhase.MovePieces)
                detail += match.Board != null && match.Board.ForcedCaptureFrom >= 0
                    ? T("  •  Продолжи взятие", "  •  Continue capturing")
                    : T("  •  Осталось ходов: ", "  •  Moves left: ") + match.Remaining;
            Label(new Rect(r.x + 12, r.y + r.height * .56f, r.width - 24, r.height * .4f), detail, 17, ink, true);
        }

        void DrawPlayerInfo()
        {
            Rect r = MatchRegion(ScreenBlock.PlayerInfo);
            string side = match.HumanSide == Side.White ? T("Ты играешь белыми", "You play White") : T("Ты играешь чёрными", "You play Black");
            if (match.Domino != null)
            {
                side = T("Костяшек у бота: ", "Bot tiles: ") + match.Domino.Rack(match.SideFor(1)).Count;
                YardText(r, side, MatchLabelSize, ink, true);
                return;
            }
            float icon = Math.Min(34, r.height);
            float textWidth = Math.Min(r.width - icon - 12, side.Length * MatchLabelSize * .65f);
            float left = r.x + (r.width - textWidth - icon - 12) / 2;
            DrawPiece(new Rect(left, r.y, icon, icon), new Piece(PieceType.Checker, match.HumanSide));
            YardText(new Rect(left + icon + 12, r.y, textWidth, r.height), side, MatchLabelSize, ink, false);
        }

        void DrawCards()
        {
            DrawCardPile(MatchRegion(ScreenBlock.Deck), match.DeckCount, T("Колода", "Deck"), null, false);
            bool preview = match.Actor == 1 && pendingBotCard >= 0 && pendingBotCard < match.Hands[1].Count;
            // The actual discard stays visible while the opponent reveals the next card.
            DrawCardPile(MatchRegion(ScreenBlock.Discard), match.DiscardCount, T("Сброс", "Discard"), match.Top, false);
            DrawCardPile(MatchRegion(ScreenBlock.OpponentHand), match.Hands[1].Count,
                preview ? T("Бот играет", "Bot plays") : T("У бота", "Bot hand"),
                preview ? (Card?)match.Hands[1][pendingBotCard] : null, preview);
            DrawHand(MatchRegion(ScreenBlock.Cards));
        }

        void DrawCardPile(Rect area, int count, string caption, Card? face, bool highlighted)
        {
            float height = Math.Max(20, Math.Min(area.height - 33, area.width * 1.1f));
            float width = height / 1.4f;
            Rect card = new Rect(area.x + (area.width - width) / 2 - 3, area.y, width, height);
            if (count > 0)
            {
                int layers = Math.Min(4, count - 1);
                for (int i = layers; i > 0; i--)
                {
                    Rect under = new Rect(card.x + i * 1.6f, card.y + i * 2, card.width, card.height);
                    Rounded(under, TrimDark, 9);
                    Rounded(Inset(under, 2), TrimLight, 7);
                }
                if (face.HasValue) DrawCardFace(card, face.Value, highlighted);
                else if (cardBack != null) GUI.DrawTexture(card, cardBack);
                else Framed(card, Blue, 10, 3);
            }
            else
            {
                Rounded(card, new Color(0, 0, 0, .18f), 10);
                Label(card, "—", 27, muted, true);
            }
            YardText(new Rect(area.x, area.y + area.height - 27, area.width, 27), caption + " · " + count,
                portrait ? 19 : MatchLabelSize, ink, true);
        }

        string HandSelectionHint()
        {
            if (selectedHandCard < 0 || selectedHandCard >= match.Hands[0].Count || match.Phase == MatchPhase.Finished) return "";
            if (match.Actor != 0) return T("Сейчас ход бота", "It is the bot's turn");
            string reason = match.UnavailableReason(selectedHandCard, English);
            return reason.Length == 0 ? T("Нажми ещё раз, чтобы сыграть", "Press again to play") : reason;
        }

        void DrawMatchNotice()
        {
            string message;
            if (match.Phase == MatchPhase.Finished)
                message = match.Winner == 0 ? "+" + WinReward + T(" монет зачислено", " coins awarded") : T("Можно попробовать ещё раз", "Play another round");
            else if (illegalGoMove) message = T("Ход оставляет группу без свободных точек или повторяет позицию.", "This move leaves a group without liberties or repeats the position.");
            else if (Time.unscaledTime < cardAnnouncementUntil && announcedCard.HasValue) message = AnnouncementText(announcedCard.Value);
            else
            {
                message = HandSelectionHint();
                if (message.Length == 0) message = match.GetMessage(English);
            }
            YardText(MatchRegion(ScreenBlock.Notice), message, portrait ? 19 : 20, ink, true);
        }

        bool MatchAction(Rect r, string label, bool enabled, Color color)
        {
            if (enabled) return Button(r, label, color, true, 24);
            Framed(r, ActiveTheme?.MatchDisabled ?? Hex("704D3D"), 19, 3);
            YardText(Inset(r, 5), label, 24, Hex("CBB8A4"), true);
            return false;
        }

        void DrawMatchActions()
        {
            Rect r = MatchRegion(ScreenBlock.Actions);
            if (match.Phase == MatchPhase.Finished)
            {
                float half = (r.width - 12) / 2;
                if (Button(new Rect(r.x, r.y, half, r.height), T("Во двор", "Courtyard"), MatchBurgundy)) { ReturnToCourtyard(); GUIUtility.ExitGUI(); }
                if (Button(new Rect(r.x + half + 12, r.y, half, r.height), T("Ещё партия", "Play again"), Green)) { Replay(); GUIUtility.ExitGUI(); }
                return;
            }
            bool canDraw = match.Actor == 0 && match.Phase == MatchPhase.ChooseCard && match.PlayableCards().Count == 0;
            float drawWidth = match.Go == null ? r.width : r.width * .62f;
            if (MatchAction(new Rect(r.x, r.y, drawWidth, r.height), T("Взять карту", "Draw a card"), canDraw, Green))
            { match.Draw(); ResetHandSelection(); selected = -1; illegalGoMove = false; CreditResult(); GUIUtility.ExitGUI(); }
            if (match.Go != null && MatchAction(new Rect(r.x + drawWidth + 12, r.y, r.width - drawWidth - 12, r.height), T("Пас", "Pass"), match.Actor == 0, MatchBurgundy))
            { match.PassGo(); ResetHandSelection(); illegalGoMove = false; cardAnnouncementUntil = 0; CreditResult(); GUIUtility.ExitGUI(); }
        }
    }
}
