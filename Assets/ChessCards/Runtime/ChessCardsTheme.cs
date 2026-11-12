using UnityEngine;

namespace ChessCards
{
    [CreateAssetMenu(menuName = "Chess Cards/Theme", fileName = "Royal Theme")]
    public sealed class ChessCardsTheme : ScriptableObject
    {
        [Header("Typography and scenery")]
        public Font InterfaceFont;
        public Texture2D Courtyard;
        public Texture2D RestoredCourtyard;
        public Texture2D GameRoom;

        [Header("Courtyard labels")]
        public string CourtyardTitleRu = "Королевский двор";
        public string CourtyardTitleEn = "Royal Courtyard";
        public string RestorationTitleRu = "Восстановление";
        public string RestorationTitleEn = "Restoration";

        [Header("Card hand layout")]
        [Range(0, 18)] public float HandLift = 17;
        [Range(.55f, .95f)] public float HandSpacing = .82f;

        [Header("Match table")]
        [Tooltip("Optional textures. Empty fields use procedural velvet and wood.")]
        public Texture2D MatchClothTexture;
        public Texture2D MatchWoodTexture;
        public Color MatchVelvet = new Color32(92, 19, 43, 255);
        public Color MatchWood = new Color32(136, 67, 30, 255);
        public Color MatchBurgundy = new Color32(100, 25, 47, 255);
        public Color MatchDisabled = new Color32(112, 77, 61, 255);
        [Range(0, 1)] public float MatchBackdropShade = .18f;
        [Range(4, 24)] public float TableRim = 12;
        [Range(8, 48)] public float TableCornerRadius = 32;
        [Range(80, 220)] public float HandCardHeight = 154;
        [Range(14, 32)] public int MatchLabelSize = 22;
        [Range(20, 40)] public int MatchTitleSize = 32;
        public bool MatchOrnaments = true;

        [Header("Cards and pieces")]
        public Texture2D CardBody;
        public Texture2D CardFrame;
        public Texture2D CardBack;
        [Tooltip("Order: 1, 2, 3, Swap, Undo, Skip.")]
        public Texture2D[] CardFaces = new Texture2D[6];
        public Texture2D ChessPiecesAtlas;
        [Tooltip("Order: Pawn, Knight, Bishop, Rook, Queen, King, Checker, Crowned Checker.")]
        public Texture2D[] WhitePieces = new Texture2D[8];
        public Texture2D[] BlackPieces = new Texture2D[8];

        [Header("Illustrations")]
        [Tooltip("Order: Chess, Checkers, Go, Domino.")]
        public Texture2D[] ModeIcons = new Texture2D[4];
        [Tooltip("Order: Bench, Lantern, Cottage.")]
        public Texture2D[] RepairIcons = new Texture2D[3];
        public Texture2D Coin;
        public Texture2D Crown;
        public Texture2D Crest;
        public Texture2D Corner;

        [Header("Audio")]
        public AudioClip Music;
        public AudioClip Click;
        public AudioClip Move;
        public AudioClip Win;

        [Header("Colors")]
        public Color ScreenBackground = new Color32(32, 13, 49, 255);
        public Color Panel = new Color32(73, 36, 105, 255);
        public Color Text = new Color32(255, 249, 242, 255);
        public Color MutedText = new Color32(240, 216, 251, 255);
        public Color GoldText = new Color32(255, 224, 152, 255);
        public Color Purple = new Color32(108, 41, 160, 255);
        public Color DarkPurple = new Color32(43, 16, 79, 255);
        public Color Blue = new Color32(26, 87, 164, 255);
        public Color Green = new Color32(57, 123, 19, 255);
        public Color Amber = new Color32(147, 80, 21, 255);
        public Color Red = new Color32(154, 37, 71, 255);
        public Color Teal = new Color32(24, 93, 88, 255);
        public Color Trim = new Color32(255, 189, 76, 255);
        public Color TrimDark = new Color32(159, 75, 20, 255);
        public Color TrimLight = new Color32(255, 228, 153, 255);
        public Color Shadow = new Color32(33, 14, 49, 255);
        public Color CourtyardBurgundy = new Color32(89, 29, 57, 255);
        public Color CourtyardVelvet = new Color32(78, 21, 49, 255);
        public Color CourtyardGround = new Color32(41, 17, 35, 255);
        public Color CourtyardWood = new Color32(128, 72, 45, 255);
        public Color CourtyardPrice = new Color32(101, 70, 58, 255);
        public Color CourtyardComplete = new Color32(190, 230, 160, 255);
        public Color BoardLight = new Color32(249, 230, 187, 255);
        public Color BoardDark = new Color32(49, 128, 124, 255);
        [Tooltip("Order: Red, Blue, Green, Yellow.")]
        public Color[] CardColors = { new Color32(226, 46, 73, 255), new Color32(49, 93, 229, 255),
            new Color32(36, 163, 34, 255), new Color32(231, 157, 35, 255) };
    }
}
