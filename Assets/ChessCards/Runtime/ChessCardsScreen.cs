using System;
using UnityEngine;

namespace ChessCards
{
    public enum ScreenKind { Courtyard, Match }
    public enum ScreenBlock
    {
        Title, Settings, Wallet, Wins, Restoration, Play, Reward, Notice,
        Board, Cards, MatchStatus, Back, History, Rules, Actions, ModePicker, SettingsWindow,
        // Append only: existing prefab values are serialized as integers.
        Table, Deck, Discard, OpponentHand, PlayerInfo, Scoring
    }

    [Serializable]
    public sealed class ScreenPlacement
    {
        public ScreenBlock Block;
        public Rect Landscape;
        public Rect Portrait;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Chess Cards/Screen Layout")]
    public sealed class ChessCardsScreen : MonoBehaviour
    {
        public ScreenKind Kind;
        [Tooltip("Mode used when playing this scene directly in the Editor.")]
        public GameMode PreviewMode;
        [Tooltip("Optional visual theme override for this scene.")]
        public ChessCardsTheme ThemeOverride;
        [Tooltip("Coordinates in 1280 × 720 or 720 × 1280. Origin is the upper-left corner.")]
        public ScreenPlacement[] Placements = Array.Empty<ScreenPlacement>();

        public static Rect DefaultMatchRect(ScreenBlock block, bool portrait)
        {
            switch (block)
            {
                case ScreenBlock.Title: return portrait ? new Rect(167, 20, 195, 55) : new Rect(334, 23, 514, 55);
                case ScreenBlock.Settings: return portrait ? new Rect(595, 22, 97, 52) : new Rect(1174, 22, 72, 52);
                case ScreenBlock.Back: return portrait ? new Rect(28, 22, 124, 52) : new Rect(36, 22, 166, 52);
                case ScreenBlock.History: return portrait ? new Rect(373, 22, 100, 52) : new Rect(925, 22, 112, 52);
                case ScreenBlock.Rules: return portrait ? new Rect(484, 22, 100, 52) : new Rect(1049, 22, 112, 52);
                case ScreenBlock.Table: return portrait ? new Rect(28, 88, 664, 1170) : new Rect(34, 105, 1212, 590);
                case ScreenBlock.Board: return portrait ? new Rect(80, 210, 560, 560) : new Rect(84, 136, 488, 488);
                case ScreenBlock.MatchStatus: return portrait ? new Rect(66, 108, 588, 72) : new Rect(662, 128, 538, 70);
                case ScreenBlock.Deck: return portrait ? new Rect(91, 838, 150, 116) : new Rect(660, 216, 170, 154);
                case ScreenBlock.Discard: return portrait ? new Rect(285, 838, 150, 116) : new Rect(850, 216, 170, 154);
                case ScreenBlock.OpponentHand: return portrait ? new Rect(479, 838, 150, 116) : new Rect(1040, 216, 170, 154);
                case ScreenBlock.Cards: return portrait ? new Rect(52, 961, 616, 169) : new Rect(640, 386, 580, 202);
                case ScreenBlock.PlayerInfo: return portrait ? new Rect(80, 800, 560, 32) : new Rect(102, 654, 452, 32);
                case ScreenBlock.Notice: return portrait ? new Rect(52, 1141, 616, 48) : new Rect(650, 594, 560, 43);
                case ScreenBlock.Actions: return portrait ? new Rect(52, 1200, 616, 48) : new Rect(700, 648, 460, 50);
                case ScreenBlock.Scoring: return portrait ? new Rect(62, 858, 596, 280) : new Rect(644, 236, 570, 320);
                case ScreenBlock.SettingsWindow: return portrait ? new Rect(32, 285, 656, 710) : new Rect(300, 36, 680, 648);
                default: return new Rect(0, 0, 100, 40);
            }
        }

        public Rect GetRect(ScreenBlock block, bool portrait, Rect fallback)
        {
            if (Placements == null) return fallback;
            foreach (var placement in Placements)
            {
                if (placement == null || placement.Block != block) continue;
                Rect rect = portrait ? placement.Portrait : placement.Landscape;
                if (rect.width > 0 && rect.height > 0) return rect;
                break;
            }
            return fallback;
        }
    }
}
