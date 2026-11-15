namespace UnDFight
{
    public enum GameMode
    {
        VersusCpu = 0,
        LocalVersus = 1,
        Training = 2
    }

    /// <summary>
    /// Shared cross-scene match session state holding active GameMode and selected fighters.
    /// </summary>
    public static class GameSessionState
    {
        public static GameMode CurrentMode { get; set; } = GameMode.LocalVersus;
        public static FighterData SelectedFighterP1 { get; set; }
        public static FighterData SelectedFighterP2 { get; set; }

        public static bool IsTrainingMode => CurrentMode == GameMode.Training;
        public static bool P1SelectsBothFighters => CurrentMode == GameMode.VersusCpu || CurrentMode == GameMode.Training;

        public static void SelectMode(GameMode mode)
        {
            CurrentMode = mode;
        }

        public static void SelectFighters(FighterData p1, FighterData p2)
        {
            SelectedFighterP1 = p1;
            SelectedFighterP2 = p2;
        }

        public static void Reset()
        {
            CurrentMode = GameMode.LocalVersus;
            SelectedFighterP1 = null;
            SelectedFighterP2 = null;
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
