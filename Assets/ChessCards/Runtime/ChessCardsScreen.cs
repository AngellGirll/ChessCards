using System;
using UnityEngine;

namespace ChessCards
{
    public enum ScreenKind { Courtyard, Match }
    public enum ScreenBlock
    {
        Title, Settings, Wallet, Wins, Restoration, Play, Reward, Notice,
        Board, Cards, MatchStatus, Back, History, Rules, Actions, ModePicker, SettingsWindow
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
