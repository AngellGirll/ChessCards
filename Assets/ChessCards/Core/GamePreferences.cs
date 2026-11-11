using System;

namespace ChessCards
{
    public enum GameLanguage { Russian, English }
    public enum BotDifficulty { Easy, Normal, Hard }
    public enum ModeSelection { Manual, Random }

    [Serializable]
    public sealed class GamePreferences
    {
        static readonly GameMode[] Modes = { GameMode.Chess, GameMode.Checkers, GameMode.Go, GameMode.Domino };

        public GameLanguage Language = GameLanguage.Russian;
        public BotDifficulty Difficulty = BotDifficulty.Normal;
        public ModeSelection Selection = ModeSelection.Manual;
        public float Music = .35f;
        public float Effects = .75f;

        public void Normalize()
        {
            if (!Enum.IsDefined(typeof(GameLanguage), Language)) Language = GameLanguage.Russian;
            if (!Enum.IsDefined(typeof(BotDifficulty), Difficulty)) Difficulty = BotDifficulty.Normal;
            if (!Enum.IsDefined(typeof(ModeSelection), Selection)) Selection = ModeSelection.Manual;
            Music = ClampVolume(Music, .35f);
            Effects = ClampVolume(Effects, .75f);
        }

        static float ClampVolume(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(0f, Math.Min(1f, value));
        }

        public GameMode DrawMode(Random random)
        {
            return Modes[random.Next(Modes.Length)];
        }
    }
}
