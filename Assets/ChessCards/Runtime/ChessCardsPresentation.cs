using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessCards
{
    public sealed partial class ChessCardsApp
    {
        const string PreferencesKey = "ChessCards.Preferences.v1";
        readonly System.Random modeRandom = new System.Random();
        GamePreferences preferences;
        bool settingsOpen;
        bool audioStarted;
        Texture2D courtyard, restoredCourtyard, gameRoom;
        readonly Texture2D[] modeArt = new Texture2D[4];
        readonly Texture2D[] repairArt = new Texture2D[3];
        Texture2D coinArt, chessPieces;
        AudioSource musicSource, effectsSource;
        AudioClip clickSound, moveSound, winSound;

        static readonly Color Royal = Hex("6C29A0"), RoyalDeep = Hex("2B104F"), Blue = Hex("1A57A4"),
            Green = Hex("397B13"), Amber = Hex("935015"), Red = Hex("9A2547"), Teal = Hex("185D58"),
            Trim = Hex("FFBD4C"), TrimDark = Hex("9F4B14"), TrimLight = Hex("FFE499"), Shadow = Hex("210E31");
        static readonly Color[] ModeColors = { Blue, Red, Teal, Amber };
        readonly Dictionary<Color, Texture2D> gradients = new Dictionary<Color, Texture2D>();

        bool English { get { return preferences.Language == GameLanguage.English; } }
        string T(string russian, string english) { return English ? english : russian; }

        void LoadPresentation()
        {
            preferences = new GamePreferences();
            try { JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(PreferencesKey, "{}"), preferences); }
            catch (ArgumentException) { preferences = new GamePreferences(); }
            preferences.Normalize();
            const string art = "ChessCards/Royal/";
            courtyard = Resources.Load<Texture2D>(art + "Courtyard");
            restoredCourtyard = Resources.Load<Texture2D>(art + "RestoredCourtyard");
            LoadCourtyardStyle(art);
            gameRoom = Resources.Load<Texture2D>(art + "GameRoom");
            string[] modeNames = { "Chess", "Checkers", "Go", "Domino" };
            string[] repairNames = { "Bench", "Lantern", "Cottage" };
            for (int i = 0; i < modeNames.Length; i++) modeArt[i] = Resources.Load<Texture2D>(art + modeNames[i]);
            for (int i = 0; i < repairNames.Length; i++) repairArt[i] = Resources.Load<Texture2D>(art + repairNames[i]);
            coinArt = Resources.Load<Texture2D>(art + "Coin");
            chessPieces = Resources.Load<Texture2D>(art + "ChessPieces");

            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.clip = Resources.Load<AudioClip>(art + "CourtyardMusic");
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            clickSound = Resources.Load<AudioClip>(art + "Click");
            moveSound = Resources.Load<AudioClip>(art + "Move");
            winSound = Resources.Load<AudioClip>(art + "Win");
            ApplyVolumes();
        }

        void ApplyVolumes()
        {
            musicSource.volume = preferences.Music;
            effectsSource.volume = preferences.Effects;
            if (audioStarted && musicSource.clip != null && musicSource.volume > 0 && !musicSource.isPlaying)
                musicSource.Play();
            else if (musicSource.volume == 0 && musicSource.isPlaying)
                musicSource.Stop();
        }

        void Feedback(AudioClip sound)
        {
            if (!audioStarted) { audioStarted = true; ApplyVolumes(); }
            if (effectsSource.volume > 0 && sound != null) effectsSource.PlayOneShot(sound);
        }

        void SavePreferences()
        {
            PlayerPrefs.SetString(PreferencesKey, JsonUtility.ToJson(preferences));
            PlayerPrefs.Save();
        }

        void PlayFromCourtyard()
        {
            if (preferences.Selection == ModeSelection.Random)
            {
                selectedMode = preferences.DrawMode(modeRandom);
                StartMatch();
            }
            else modeSelection = true;
        }

        void Replay()
        {
            if (preferences.Selection == ModeSelection.Random) selectedMode = preferences.DrawMode(modeRandom);
            StartMatch();
        }

        void DrawSettings()
        {
            Rect r = portrait ? new Rect(32, 285, w - 64, 710) : new Rect(300, 36, 680, 648);
            Overlay(r, T("Настройки", "Settings"));
            float x = r.x + 28, width = r.width - 56, y = r.y + 92;
            GameLanguage languageBefore = preferences.Language;
            BotDifficulty difficultyBefore = preferences.Difficulty;
            ModeSelection selectionBefore = preferences.Selection;
            float musicBefore = preferences.Music, effectsBefore = preferences.Effects;
            var languages = new[] { "Русский", "English" };
            preferences.Language = (GameLanguage)OptionRow(x, y, width, T("Язык", "Language"), languages, (int)preferences.Language);
            var difficulties = new[] { T("Легко", "Easy"), T("Обычно", "Normal"), T("Сложно", "Hard") };
            preferences.Difficulty = (BotDifficulty)OptionRow(x, y + 88, width, T("Сложность бота", "Bot difficulty"), difficulties, (int)preferences.Difficulty);
            var selection = new[] { T("Выбор", "Choose"), T("Рандом", "Random") };
            preferences.Selection = (ModeSelection)OptionRow(x, y + 176, width, T("Режим на старте партии", "Game mode at the start"), selection, (int)preferences.Selection);
            preferences.Music = VolumeRow(x, y + 280, width, T("Музыка", "Music"), preferences.Music);
            preferences.Effects = VolumeRow(x, y + 368, width, T("Звуки", "Sound effects"), preferences.Effects);
            if (preferences.Language != languageBefore || preferences.Difficulty != difficultyBefore ||
                preferences.Selection != selectionBefore || preferences.Music != musicBefore || preferences.Effects != effectsBefore)
            {
                ApplyVolumes();
                SavePreferences();
            }
            Label(new Rect(x, r.y + r.height - (portrait ? 126 : 110), width, portrait ? 38 : 22),
                T("Сложность меняется со следующей партии.", "Difficulty applies to the next game."), 17, muted, true);
            if (Button(new Rect(x, r.y + r.height - 77, width, 52), T("Готово", "Done"), Green))
            {
                SavePreferences();
                settingsOpen = false;
                GUIUtility.ExitGUI();
            }
        }

        int OptionRow(float x, float y, float width, string title, string[] choices, int selectedOption)
        {
            Label(new Rect(x, y, width, 27), title, 19, gold, false, true);
            float optionWidth = (width - (choices.Length - 1) * 10) / choices.Length;
            for (int i = 0; i < choices.Length; i++)
            {
                if (Button(new Rect(x + i * (optionWidth + 10), y + 32, optionWidth, 45), (i == selectedOption ? "✓ " : "") + choices[i], i == selectedOption ? Green : Blue, true, 20))
                    selectedOption = i;
            }
            return selectedOption;
        }

        float VolumeRow(float x, float y, float width, string title, float volume)
        {
            Label(new Rect(x, y, width, 27), title + "  " + (int)Math.Round(volume * 100) + "%", 19, gold, false, true);
            if (Button(new Rect(x, y + 32, 52, 44), "−", Blue)) volume = Math.Max(0f, volume - .1f);
            float trackWidth = width - 126, tickWidth = trackWidth / 10;
            for (int i = 0; i < 10; i++)
            {
                float step = (i + 1) / 10f;
                if (Button(new Rect(x + 63 + i * tickWidth, y + 35, tickWidth - 3, 38), "", step <= volume + .001f ? Green : RoyalDeep))
                    volume = step;
            }
            if (Button(new Rect(x + width - 52, y + 32, 52, 44), "+", Blue)) volume = Math.Min(1f, volume + .1f);
            return volume;
        }

        void Backdrop(Texture2D texture, float shade)
        {
            var r = new Rect(0, 0, w, h);
            if (texture != null) GUI.DrawTexture(r, texture, ScaleMode.ScaleAndCrop);
            Fill(r, new Color(.10f, .035f, .16f, shade));
        }

        void Rounded(Rect r, Color color, float radius, Texture2D texture = null)
        {
            GUI.DrawTexture(r, texture ?? Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, radius);
        }

        Texture2D Gloss(Color color)
        {
            if (gradients.TryGetValue(color, out Texture2D texture)) return texture;
            texture = new Texture2D(1, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[32];
            Color bottom = Color.Lerp(color, Color.black, .30f);
            Color top = Color.Lerp(color, Color.white, .24f);
            for (int i = 0; i < pixels.Length; i++)
            {
                float position = i / (pixels.Length - 1f);
                pixels[i] = position > .65f ? Color.Lerp(color, top, (position - .65f) / .35f) : Color.Lerp(bottom, color, position / .65f);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            gradients.Add(color, texture);
            return texture;
        }

        static Rect Inset(Rect r, float amount)
        { return new Rect(r.x + amount, r.y + amount, r.width - amount * 2, r.height - amount * 2); }

        void Framed(Rect r, Color color, float radius = 16, float trim = 4)
        {
            Rounded(new Rect(r.x, r.y + 5, r.width, r.height), new Color(.06f, .02f, .10f, .75f), radius);
            Rounded(r, TrimDark, radius);
            Rounded(Inset(r, 1), Color.white, radius - 1, Gloss(Trim));
            Rounded(Inset(r, trim), Shadow, radius - trim);
            Rounded(Inset(r, trim + 1), Color.white, radius - trim - 1, Gloss(color));
        }

        void Banner(Rect r, string title, int size)
        {
            Framed(r, Royal, 23, 5);
            var inner = Inset(r, 7);
            Label(new Rect(inner.x + 1, inner.y + 2, inner.width, inner.height), title, size, Shadow, true, true);
            Label(inner, title, size, TrimLight, true, true);
        }

        void BoardFrame(Rect board)
        {
            Framed(Inset(board, -13), Amber, 20, 6);
            Fill(Inset(board, -2), Hex("713419"));
        }

        void DrawPiece(Rect r, Piece piece)
        {
            int kind = (int)piece.Type;
            if (chessPieces != null && kind > 0 && kind <= 6)
            {
                const float width = 1f / 6f;
                GUI.DrawTextureWithTexCoords(r, chessPieces,
                    new Rect((kind - 1) * width, piece.Side == Side.White ? .5f : 0f, width, .5f));
                return;
            }
            Texture2D icon = pieces[(int)piece.Side, kind];
            if (icon != null) GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit);
            else Label(r, piece.Type.ToString().Substring(0, 1), 34, piece.Side == Side.White ? Color.white : Color.black, true);
        }

        void DrawCoin(Rect r)
        {
            if (coinArt != null) GUI.DrawTexture(r, coinArt, ScaleMode.ScaleToFit);
            else
            {
                Rounded(r, TrimDark, r.width / 2);
                Rounded(Inset(r, 2), Trim, r.width / 2 - 2);
                Label(r, "•", 18, TrimLight, true, true);
            }
        }
    }
}
