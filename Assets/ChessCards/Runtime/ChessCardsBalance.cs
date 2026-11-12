using UnityEngine;

namespace ChessCards
{
    [CreateAssetMenu(menuName = "Chess Cards/Balance", fileName = "Game Balance")]
    public sealed class ChessCardsBalance : ScriptableObject
    {
        [Header("Card deck")]
        [Range(1, 12)] public int StartingHand = 5;
        [Range(1, 4)] public int CopiesPerNumberAndColor = 4;
        [Range(0, 2)] public int CopiesPerSpecialAndColor = 2;

        [Header("Restoration")]
        [Min(0)] public int WinReward = 100;
        [Min(0)] public int BenchPrice = 60;
        [Min(0)] public int LanternPrice = 100;
        [Min(0)] public int HousePrice = 180;

        [Header("Bot")]
        [Min(0)] public float TurnDelay = .85f;
        [Min(0)] public float MoveDelay = .8f;
        [Min(0)] public float CardRevealDelay = 1.4f;
        public GameMode[] EnabledModes = { GameMode.Chess, GameMode.Checkers, GameMode.Go, GameMode.Domino };

        [Header("Defaults for a new save")]
        public GameLanguage DefaultLanguage;
        public BotDifficulty DefaultDifficulty = BotDifficulty.Normal;
        public ModeSelection DefaultSelection;
        [Range(0, 1)] public float DefaultMusic = .35f;
        [Range(0, 1)] public float DefaultEffects = .75f;
    }
}
