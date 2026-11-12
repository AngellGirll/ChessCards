using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessCards
{
    public sealed partial class ChessCardsApp
    {
        const float HandLift = 17;
        readonly List<Rect> handSlots = new List<Rect>();
        float[] handLifts = new float[8];
        int selectedHandCard = -1, frontHandCard = -1;

        void ResetHandSelection()
        {
            selectedHandCard = -1;
            frontHandCard = -1;
            Array.Clear(handLifts, 0, handLifts.Length);
        }

        int LayoutHand(Rect area, int count)
        {
            handSlots.Clear();
            int rows = count > 36 ? 3 : count > 16 ? 2 : 1;
            int perRow = (count + rows - 1) / rows;
            float cardHeight = rows == 1 ? 148 : area.height / rows + 24;
            float cardWidth = cardHeight / 1.4f;
            float rowStep = rows == 1 ? 0 : (area.height - cardHeight) / (rows - 1);
            for (int first = 0; first < count; first += perRow)
            {
                int columns = Math.Min(perRow, count - first);
                float step = columns == 1 ? 0 : Math.Min(cardWidth * .82f, (area.width - cardWidth) / (columns - 1));
                float left = area.x + (area.width - cardWidth - (columns - 1) * step) / 2;
                for (int column = 0; column < columns; column++)
                    handSlots.Add(new Rect(left + column * step, area.y + first / perRow * rowStep, cardWidth, cardHeight));
            }
            return rows;
        }

        int HandCardAt(Vector2 point)
        {
            // Fixed slots keep the target stable as the raised card moves.
            for (int i = handSlots.Count - 1; i >= 0; i--)
                if (handSlots[i].Contains(point)) return i;
            return -1;
        }

        void DrawHand(float x, float y, float width)
        {
            int count = match.Hands[0].Count;
            Label(new Rect(x + 14, y + 101, 191, 31), T("Твоя рука: ", "Your hand: ") + count, 19, muted);
            if (count == 0)
            {
                ResetHandSelection();
                Label(new Rect(x, y + 152, width, 150), T("Рука пуста. В свою очередь возьми карту.", "Your hand is empty. Draw a card on your turn."), 22, muted, true);
                return;
            }

            if (selectedHandCard >= count || frontHandCard >= count) ResetHandSelection();
            if (handLifts.Length < count) Array.Resize(ref handLifts, count);
            int rows = LayoutHand(new Rect(x + 15, y + 151, width - 30, 149), count);
            Vector2 pointer = Event.current.mousePosition;
            int pointed = GUI.enabled ? HandCardAt(pointer) : -1;
            if (pointed < 0 && GUI.enabled && frontHandCard >= 0 && LiftedHandCard(frontHandCard).Contains(pointer))
                pointed = frontHandCard;
            int focused = !Application.isMobilePlatform && pointed >= 0 ? pointed : selectedHandCard;
            float step = 155 * Time.unscaledDeltaTime;
            if (Event.current.type == EventType.Repaint)
            {
                frontHandCard = focused;
                for (int i = 0; i < count; i++)
                    handLifts[i] = i == focused ? Math.Min(HandLift, handLifts[i] + step) : Math.Max(0, handLifts[i] - step);
            }

            bool canChoose = match.Actor == 0 && match.Phase == MatchPhase.ChooseCard;
            bool dense = rows > 1 || count > 9;
            for (int i = 0; i < count; i++)
                if (i != focused) DrawHandCard(i, canChoose && match.CanPlay(i), dense, false);
            if (focused >= 0) DrawHandCard(focused, canChoose && match.CanPlay(focused), dense, true);

            if (selectedHandCard >= 0)
            {
                string hint = match.Actor != 0 ? T("Ход бота", "Bot's turn") :
                    match.UnavailableReason(selectedHandCard, English);
                if (hint.Length == 0) hint = T("Нажми ещё раз, чтобы сыграть", "Press again to play");
                Label(new Rect(x + 195, y + 101, width - 209, 30), hint, 15, gold, true);
            }
            else if (focused >= 0 && dense)
            {
                Card card = match.Hands[0][focused];
                Label(new Rect(x + 195, y + 101, width - 209, 30),
                    card.GetColorName(English) + " «" + card.GetTitle(English) + "»", 16, gold, true);
            }

            if (GUI.Button(new Rect(x + 15, y + 132, width - 30, 174), GUIContent.none, GUIStyle.none))
            {
                int hit = frontHandCard >= 0 && LiftedHandCard(frontHandCard).Contains(pointer) ? frontHandCard : pointed;
                if (hit >= 0) ChooseHandCard(hit, canChoose);
                else ResetHandSelection();
            }
        }

        Rect LiftedHandCard(int index)
        {
            Rect card = handSlots[index];
            card.y -= handLifts[index];
            return card;
        }

        void DrawHandCard(int index, bool playable, bool dense, bool focused)
        {
            Rect card = LiftedHandCard(index);
            Card value = match.Hands[0][index];
            DrawCardFace(card, value, playable || focused, playable || focused ? 1 : .78f);
            if (dense)
            {
                string mark = value.Kind == CardKind.Number ? value.Number.ToString() :
                    value.Kind == CardKind.Swap ? "⇄" : value.Kind == CardKind.Undo ? "↶" : "⊘";
                Label(new Rect(card.x + 4, card.y + 4, 23, 21), mark, 17, ink, true, true);
            }
            if (!dense)
            {
                Rect slot = handSlots[index];
                Label(new Rect(slot.x, slot.y + slot.height + 2, slot.width, 17),
                    HandCardCaption(value), 12, playable || focused ? ink : muted, true, true);
            }
        }

        void ChooseHandCard(int index, bool canChoose)
        {
            if (selectedHandCard != index)
            {
                selectedHandCard = index;
                Feedback(clickSound);
                GUIUtility.ExitGUI();
                return;
            }
            if (!canChoose) return;

            Card card = match.Hands[0][index];
            if (!match.PlayCard(index)) return;
            AnnounceCard(card, 0);
            Feedback(clickSound);
            ResetHandSelection();
            selected = -1;
            selectedMoves.Clear();
            illegalGoMove = false;
            CreditResult();
            GUIUtility.ExitGUI();
        }
    }
}
