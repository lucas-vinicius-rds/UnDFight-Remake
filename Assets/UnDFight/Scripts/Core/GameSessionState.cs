namespace UnDFight
{
    public enum GameMode
    {
        VersusCpu = 0,
        LocalVersus = 1,
        Training = 2,
        ArcadeTower = 3
    }

    public enum CpuDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    /// <summary>
    /// Shared cross-scene match session state holding active GameMode, CPU difficulty, selected fighters, and selected stage.
    /// </summary>
    public static class GameSessionState
    {
        public const string DefaultStageSceneName = "Arena_Favela";

        public static GameMode CurrentMode { get; set; } = GameMode.LocalVersus;
        public static CpuDifficulty SelectedCpuDifficulty { get; set; } = CpuDifficulty.Medium;
        public static FighterData SelectedFighterP1 { get; set; }
        public static FighterData SelectedFighterP2 { get; set; }
        public static string SelectedStageSceneName { get; set; } = DefaultStageSceneName;
        public static StageWeatherVariant SelectedWeather { get; set; } = StageWeatherVariant.Day;
        public static bool OpenStageSelectOnCharacterSelect { get; set; }
        public static ArcadeTowerRun ActiveTowerRun { get; set; }

        public static bool IsTrainingMode => CurrentMode == GameMode.Training;
        public static bool IsArcadeTowerMode => CurrentMode == GameMode.ArcadeTower;
        public static bool P1SelectsBothFighters => CurrentMode == GameMode.VersusCpu || CurrentMode == GameMode.Training;

        public static void ClearTowerRun()
        {
            ActiveTowerRun = null;
        }

        public static void SelectMode(GameMode mode)
        {
            CurrentMode = mode;
        }

        public static void SelectWeather(StageWeatherVariant variant)
        {
            SelectedWeather = variant;
        }

        public static void CycleWeather(int direction = 1)
        {
            const int count = 3;
            int step = direction >= 0 ? 1 : -1;
            int next = (((int)SelectedWeather + step) % count + count) % count;
            SelectedWeather = (StageWeatherVariant)next;
        }

        public static string GetWeatherLabel(StageWeatherVariant variant, StageData stage = null)
        {
            return variant switch
            {
                StageWeatherVariant.Night => "NOITE",
                StageWeatherVariant.RainyNight => "NOITE CHUVOSA",
                _ => (stage != null && !string.IsNullOrWhiteSpace(stage.dayVariantLabel))
                    ? stage.dayVariantLabel.ToUpperInvariant()
                    : "DIA"
            };
        }

        public static void CycleCpuDifficulty(int direction = 1)
        {
            const int count = 3;
            int step = direction >= 0 ? 1 : -1;
            int next = (((int)SelectedCpuDifficulty + step) % count + count) % count;
            SelectedCpuDifficulty = (CpuDifficulty)next;
        }

        public static string GetCpuDifficultyLabel(CpuDifficulty diff)
        {
            return diff switch
            {
                CpuDifficulty.Easy => "FÁCIL",
                CpuDifficulty.Hard => "DIFÍCIL",
                _ => "MÉDIO"
            };
        }

        public static void SelectFighters(FighterData p1, FighterData p2)
        {
            SelectedFighterP1 = p1;
            SelectedFighterP2 = p2;
        }

        public static void SelectStage(StageData stage)
        {
            if (stage != null && !string.IsNullOrWhiteSpace(stage.sceneName))
            {
                SelectedStageSceneName = stage.sceneName;
            }
        }

        public static void SelectStageScene(string sceneName)
        {
            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                SelectedStageSceneName = sceneName;
            }
        }

        public static string ResolveStageSceneName(StageRoster roster, string fallbackSceneName = "Training")
        {
            if (!string.IsNullOrWhiteSpace(SelectedStageSceneName))
            {
                if (roster == null || roster.FindBySceneName(SelectedStageSceneName) != null)
                {
                    return SelectedStageSceneName;
                }
            }

            if (roster != null && roster.Count > 0)
            {
                StageData first = roster.GetStage(0);
                if (first != null && !string.IsNullOrWhiteSpace(first.sceneName))
                {
                    return first.sceneName;
                }
            }

            return !string.IsNullOrWhiteSpace(fallbackSceneName) ? fallbackSceneName : DefaultStageSceneName;
        }

        public static void Reset()
        {
            CurrentMode = GameMode.LocalVersus;
            SelectedCpuDifficulty = CpuDifficulty.Medium;
            SelectedFighterP1 = null;
            SelectedFighterP2 = null;
            SelectedStageSceneName = DefaultStageSceneName;
            SelectedWeather = StageWeatherVariant.Day;
            OpenStageSelectOnCharacterSelect = false;
            ActiveTowerRun = null;
        }

        /// <summary>
        /// Resolves P1 and P2 fighters from session selection with deterministic fallback to FighterRoster or scene defaults.
        /// Supports mirror matches (p1 == p2).
        /// </summary>
        public static void ResolveFighters(
            FighterRoster roster,
            FighterData sceneDefaultP1,
            FighterData sceneDefaultP2,
            out FighterData p1,
            out FighterData p2)
        {
            FighterData rosterFirst = roster != null && roster.Count > 0 ? roster.GetFighter(0) : null;
            FighterData rosterSecond = roster != null && roster.Count > 1 ? roster.GetFighter(1) : rosterFirst;

            p1 = SelectedFighterP1 != null ? SelectedFighterP1 : (sceneDefaultP1 != null ? sceneDefaultP1 : rosterFirst);
            p2 = SelectedFighterP2 != null ? SelectedFighterP2 : (sceneDefaultP2 != null ? sceneDefaultP2 : rosterSecond);
        }
    }
}
