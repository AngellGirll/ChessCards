using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessCards
{
    public sealed partial class ChessCardsApp
    {
        [SerializeField] ChessCardsBalance balance = null;
        [SerializeField] ChessCardsTheme theme = null;
        [SerializeField] bool useSeparateScenes = false;
        [SerializeField] string courtyardScene = "Main";
        [SerializeField] string matchScene = "Match";

        static ChessCardsApp instance;
        ChessCardsScreen screen;
        bool sceneTransition;
        readonly List<GameMode> enabledModes = new List<GameMode>(4);
        ChessCardsTheme ActiveTheme { get { return screen != null && screen.ThemeOverride != null ? screen.ThemeOverride : theme; } }

        bool InitializeSceneFlow()
        {
            if (useSeparateScenes)
            {
                if (instance != null && instance != this)
                {
                    enabled = false;
                    Destroy(gameObject);
                    return false;
                }
                instance = this;
                DontDestroyOnLoad(gameObject);
                SceneManager.sceneLoaded += OnSceneLoaded;
                screen = FindAnyObjectByType<ChessCardsScreen>();
            }
            if (balance != null && balance.EnabledModes != null)
                foreach (GameMode mode in balance.EnabledModes)
                    if (Enum.IsDefined(typeof(GameMode), mode) && !enabledModes.Contains(mode)) enabledModes.Add(mode);
            if (enabledModes.Count == 0)
                enabledModes.AddRange(new[] { GameMode.Chess, GameMode.Checkers, GameMode.Go, GameMode.Domino });
            selectedMode = enabledModes[0];
            return true;
        }

        void StartScenePreview()
        {
            if (screen == null || screen.Kind != ScreenKind.Match || match != null) return;
            selectedMode = enabledModes.Contains(screen.PreviewMode) ? screen.PreviewMode : enabledModes[0];
            StartMatch();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            sceneTransition = false;
            screen = FindAnyObjectByType<ChessCardsScreen>();
            ApplyTheme();
            StartScenePreview();
            nextBot = Time.unscaledTime + BotTurnDelay;
        }

        void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        void OpenScene(string scene)
        {
            if (!useSeparateScenes || sceneTransition || SceneManager.GetActiveScene().name == scene) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning("Chess Cards: add scene '" + scene + "' to the build scene list.");
                return;
            }
            sceneTransition = true;
            if (SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single) == null) sceneTransition = false;
        }

        void ReturnToCourtyard()
        {
            location = true;
            confirmLeave = false;
            promotions = null;
            ResetHandSelection();
            OpenScene(courtyardScene);
        }

        Rect Region(ScreenBlock block, Rect fallback)
        {
            ScreenKind kind = location ? ScreenKind.Courtyard : ScreenKind.Match;
            return screen != null && screen.Kind == kind ? screen.GetRect(block, portrait, fallback) : fallback;
        }

        int WinReward { get { return Math.Max(0, balance != null ? balance.WinReward : 100); } }
        int RepairPrice(int index)
        {
            if (balance == null) return RestorationProgress.Prices[index];
            return Math.Max(0, index == 0 ? balance.BenchPrice : index == 1 ? balance.LanternPrice : balance.HousePrice);
        }
    }
}
