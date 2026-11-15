using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnDFight
{
    /// <summary>
    /// Best-of-3 match rules, 99-second countdown timer (or infinite Training mode with auto health regen),
    /// dynamic session fighter spawning, round intro/KO/Time Over flow, and post-round states driven by 60 Hz ticks.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public const int TrainingRegenDelayTicks = 90;
        public const int TrainingRegenPerTick = 25;

        [Header("References")]
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private FighterRoster fighterRoster;
        [SerializeField] private FighterData defaultFighterP1;
        [SerializeField] private FighterData defaultFighterP2;
        [SerializeField] private CpuAiController cpuAiController;

        [Header("Match Rules (60 Hz Ticks)")]
        [SerializeField, Min(1)] private int roundsToWin = 2;
        [SerializeField, Min(10)] private int roundDurationSeconds = 99;
        [SerializeField, Min(1)] private int roundIntroTicks = 90;
        [SerializeField, Min(1)] private int roundOverTicks = 150;
        [SerializeField] private int fightBannerLingeringTicks = 30;
        [SerializeField] private float p1StartX = -2.2f;
        [SerializeField] private float p2StartX = 2.2f;
        [SerializeField] private bool autoStartOnAwake = true;

        private bool _initialized;
        private bool _wasTimeOver;

        public MatchPhase Phase { get; private set; } = MatchPhase.RoundIntro;
        public int CurrentRound { get; private set; } = 1;
        public int P1RoundsWon { get; private set; }
        public int P2RoundsWon { get; private set; }
        public int RoundsToWin => roundsToWin;
        public int RoundDurationSeconds => roundDurationSeconds;
        public int RoundTimerTicks { get; private set; }
        public int RemainingSeconds => IsTrainingMode
            ? roundDurationSeconds
            : Mathf.Clamp(Mathf.CeilToInt(RoundTimerTicks / (float)FightSimulation.TickRate), 0, roundDurationSeconds);
        public int PhaseTicks { get; private set; }
        public int WinnerPlayerIndex { get; private set; } = -1;
        public string BannerText { get; private set; } = string.Empty;
        public string SubtitleText { get; private set; } = string.Empty;
        public bool IsTrainingMode { get; private set; }
        public bool InputsAllowed => Phase == MatchPhase.Fighting;
        public bool CombatActive => Phase == MatchPhase.Fighting;

        public event Action<MatchManager> OnMatchStateChanged;

        private void Awake()
        {
            if (simulation == null)
            {
                simulation = FindAnyObjectByType<FightSimulation>();
            }
            if (autoStartOnAwake && !_initialized)
            {
                InitializeMatch(simulation);
            }
        }

        private void Update()
        {
            if (Phase != MatchPhase.MatchOver || (simulation != null && simulation.IsPaused))
            {
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                RestartMatch();
            }
        }

        public void SetTrainingMode(bool enabled)
        {
            IsTrainingMode = enabled;
        }

        public void InitializeMatch(FightSimulation targetSimulation, bool? trainingModeOverride = null)
        {
            simulation = targetSimulation;
            if (simulation != null)
            {
                simulation.SetMatchManager(this);
            }

            IsTrainingMode = trainingModeOverride ?? GameSessionState.IsTrainingMode;

            if (Application.isPlaying)
            {
                SpawnSessionFightersIfNeeded();
                ConfigureCpuAiForCurrentMode();
                SettingsManager.ApplySettings(SettingsManager.Current, applyResolution: false);
            }

            _initialized = true;
            RestartMatch();
        }

        private void ConfigureCpuAiForCurrentMode()
        {
            if (simulation == null)
            {
                return;
            }

            if (cpuAiController == null)
            {
                cpuAiController = simulation.CpuAi != null
                    ? simulation.CpuAi
                    : (simulation.GetComponent<CpuAiController>() ?? simulation.gameObject.AddComponent<CpuAiController>());
            }

            simulation.SetCpuAiController(cpuAiController);
            cpuAiController.Configure(simulation, simulation.Fighter2, simulation.Fighter1, this);
            cpuAiController.SetDifficulty(GameSessionState.SelectedCpuDifficulty);

            if (GameSessionState.CurrentMode == GameMode.VersusCpu)
            {
                cpuAiController.SetEnabled(true);
            }
            else
            {
                // LocalVersus & Training start with CPU disabled (F2 can toggle CPU in Training)
                cpuAiController.SetEnabled(false);
            }
        }

        public void RestartMatch()
        {
            if (simulation != null)
            {
                simulation.SetPaused(false);
            }

            CurrentRound = 1;
            P1RoundsWon = 0;
            P2RoundsWon = 0;
            WinnerPlayerIndex = -1;
            StartRound();
        }

        public void StartRound()
        {
            PhaseTicks = 0;
            _wasTimeOver = false;
            WinnerPlayerIndex = -1;
            RoundTimerTicks = roundDurationSeconds * FightSimulation.TickRate;

            ResetFightersForRound();

            if (IsTrainingMode)
            {
                Phase = MatchPhase.Fighting;
                BannerText = string.Empty;
                SubtitleText = string.Empty;
                SetFightersInputsEnabled(true);
                OnMatchStateChanged?.Invoke(this);
                return;
            }

            Phase = MatchPhase.RoundIntro;
            BannerText = $"ROUND {CurrentRound}";
            SubtitleText = string.Empty;
            SetFightersInputsEnabled(false);
            OnMatchStateChanged?.Invoke(this);
        }

        public void SkipIntroToFighting()
        {
            Phase = MatchPhase.Fighting;
            PhaseTicks = 0;
            BannerText = string.Empty;
            SubtitleText = string.Empty;
            SetFightersInputsEnabled(true);
            OnMatchStateChanged?.Invoke(this);
        }

        public void GoToCharacterSelect()
        {
            GameSessionState.OpenStageSelectOnCharacterSelect = false;
            simulation?.SetPaused(false);
            SceneManager.LoadScene("CharacterSelect");
        }

        public void GoToStageSelect()
        {
            FighterData p1 = simulation != null && simulation.Fighter1 != null ? simulation.Fighter1.Data : defaultFighterP1;
            FighterData p2 = simulation != null && simulation.Fighter2 != null ? simulation.Fighter2.Data : defaultFighterP2;
            if (GameSessionState.SelectedFighterP1 == null || GameSessionState.SelectedFighterP2 == null)
            {
                GameSessionState.SelectFighters(
                    GameSessionState.SelectedFighterP1 != null ? GameSessionState.SelectedFighterP1 : p1,
                    GameSessionState.SelectedFighterP2 != null ? GameSessionState.SelectedFighterP2 : p2);
            }
            GameSessionState.OpenStageSelectOnCharacterSelect = true;
            simulation?.SetPaused(false);
            SceneManager.LoadScene("CharacterSelect");
        }

        public void GoToMainMenu()
        {
            GameSessionState.OpenStageSelectOnCharacterSelect = false;
            simulation?.SetPaused(false);
            SceneManager.LoadScene("MainMenu");
        }

        public void StepTick(int simulationTick)
        {
            if (!_initialized)
            {
                return;
            }

            PhaseTicks++;

            switch (Phase)
            {
                case MatchPhase.RoundIntro:
                    StepRoundIntro();
                    break;
                case MatchPhase.Fighting:
                    StepFighting();
                    break;
                case MatchPhase.RoundOver:
                    StepRoundOver();
                    break;
                case MatchPhase.MatchOver:
                    break;
            }
        }

        private void StepRoundIntro()
        {
            int fightCueTick = Mathf.RoundToInt(roundIntroTicks * 0.6f);
            if (PhaseTicks == fightCueTick)
            {
                BannerText = "FIGHT!";
                OnMatchStateChanged?.Invoke(this);
            }

            if (PhaseTicks >= roundIntroTicks)
            {
                Phase = MatchPhase.Fighting;
                PhaseTicks = 0;
                BannerText = "FIGHT!";
                SubtitleText = string.Empty;
                SetFightersInputsEnabled(true);
                OnMatchStateChanged?.Invoke(this);
            }
        }

        private void StepFighting()
        {
            if (PhaseTicks == fightBannerLingeringTicks && !string.IsNullOrEmpty(BannerText))
            {
                BannerText = string.Empty;
                OnMatchStateChanged?.Invoke(this);
            }

            FighterController p1 = simulation != null ? simulation.Fighter1 : null;
            FighterController p2 = simulation != null ? simulation.Fighter2 : null;
            if (p1 == null || p2 == null)
            {
                return;
            }

            if (IsTrainingMode)
            {
                TryRegenerateTrainingFighter(p1);
                TryRegenerateTrainingFighter(p2);
                return;
            }

            if (RoundTimerTicks > 0)
            {
                RoundTimerTicks--;
            }

            bool p1Ko = p1.CurrentHealth <= 0;
            bool p2Ko = p2.CurrentHealth <= 0;
            if (p1Ko || p2Ko)
            {
                int roundWinner = (p1Ko && p2Ko) ? -1 : (p2Ko ? 0 : 1);
                EnterRoundOver(roundWinner, isTimeOver: false);
                return;
            }

            if (RoundTimerTicks <= 0)
            {
                int roundWinner = p1.CurrentHealth > p2.CurrentHealth ? 0 :
                    (p2.CurrentHealth > p1.CurrentHealth ? 1 : -1);
                EnterRoundOver(roundWinner, isTimeOver: true);
            }
        }

        private static void TryRegenerateTrainingFighter(FighterController fighter)
        {
            if (fighter != null &&
                fighter.CurrentHealth < fighter.MaxHealth &&
                fighter.TicksSinceLastHit >= TrainingRegenDelayTicks)
            {
                fighter.RegenerateHealthTick(TrainingRegenPerTick);
            }
        }

        private void StepRoundOver()
        {
            FighterController p1 = simulation != null ? simulation.Fighter1 : null;
            FighterController p2 = simulation != null ? simulation.Fighter2 : null;

            if (WinnerPlayerIndex == 0 && p1 != null)
            {
                TryTriggerVictoryPose(p1);
            }
            else if (WinnerPlayerIndex == 1 && p2 != null)
            {
                TryTriggerVictoryPose(p2);
            }

            if (PhaseTicks >= roundOverTicks)
            {
                if (P1RoundsWon >= roundsToWin || P2RoundsWon >= roundsToWin)
                {
                    Phase = MatchPhase.MatchOver;
                    PhaseTicks = 0;
                    int championIndex = P1RoundsWon >= roundsToWin ? 0 : 1;
                    WinnerPlayerIndex = championIndex;
                    string winnerName = GetFighterDisplayName(championIndex).ToUpperInvariant();
                    BannerText = $"{winnerName} WINS!";
                    SubtitleText = string.Empty;
                    OnMatchStateChanged?.Invoke(this);
                }
                else
                {
                    CurrentRound++;
                    StartRound();
                }
            }
        }

        private void EnterRoundOver(int roundWinnerIndex, bool isTimeOver)
        {
            Phase = MatchPhase.RoundOver;
            PhaseTicks = 0;
            _wasTimeOver = isTimeOver;
            WinnerPlayerIndex = roundWinnerIndex;

            if (roundWinnerIndex == 0)
            {
                P1RoundsWon++;
            }
            else if (roundWinnerIndex == 1)
            {
                P2RoundsWon++;
            }

            if (isTimeOver)
            {
                BannerText = roundWinnerIndex < 0 ? "DRAW GAME!" : "TIME OVER!";
            }
            else
            {
                BannerText = roundWinnerIndex < 0 ? "DOUBLE K.O.!" : "K.O.!";
            }
            SubtitleText = string.Empty;

            SetFightersInputsEnabled(false);

            FighterController p1 = simulation != null ? simulation.Fighter1 : null;
            FighterController p2 = simulation != null ? simulation.Fighter2 : null;

            if (isTimeOver)
            {
                if (roundWinnerIndex == 0 && p2 != null) p2.SetMatchResultPose(FighterState.Defeat);
                if (roundWinnerIndex == 1 && p1 != null) p1.SetMatchResultPose(FighterState.Defeat);
            }

            OnMatchStateChanged?.Invoke(this);
        }

        private void SpawnSessionFightersIfNeeded()
        {
            if (simulation == null)
            {
                return;
            }

            GameSessionState.ResolveFighters(
                fighterRoster,
                defaultFighterP1 != null ? defaultFighterP1 : simulation.Fighter1?.Data,
                defaultFighterP2 != null ? defaultFighterP2 : simulation.Fighter2?.Data,
                out FighterData desiredP1,
                out FighterData desiredP2);

            FighterController p1 = ReplaceFighterIfNeeded(simulation.Fighter1, desiredP1, p1StartX, 0, "Fighter1");
            FighterController p2 = ReplaceFighterIfNeeded(simulation.Fighter2, desiredP2, p2StartX, 1, "Fighter2");

            simulation.SetParticipants(p1, p2, simulation.FightCamera);
        }

        private FighterController ReplaceFighterIfNeeded(
            FighterController existing,
            FighterData desiredData,
            float startX,
            int playerIndex,
            string slotPrefix)
        {
            if (desiredData == null || desiredData.prefab == null)
            {
                return existing;
            }

            if (existing != null && existing.Data == desiredData)
            {
                return existing;
            }

            float groundY = simulation != null ? simulation.GroundY : 0f;
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            GameObject instance = Instantiate(desiredData.prefab, new Vector3(startX, groundY, 0f), Quaternion.identity);
            instance.name = $"{slotPrefix}_{desiredData.fighterName}";
            var controller = instance.GetComponent<FighterController>();
            if (controller != null)
            {
                controller.SetFighterData(desiredData);
                controller.InputHandler?.Configure(playerIndex);
            }
            return controller;
        }

        private static void TryTriggerVictoryPose(FighterController winner)
        {
            if (winner.IsGrounded &&
                winner.State != FighterState.Attack &&
                winner.State != FighterState.Victory &&
                winner.State != FighterState.Hit &&
                winner.State != FighterState.KnockDown &&
                winner.State != FighterState.GetUp)
            {
                winner.SetMatchResultPose(FighterState.Victory);
            }
        }

        private void ResetFightersForRound()
        {
            if (simulation == null)
            {
                return;
            }

            float groundY = simulation.GroundY;
            simulation.ClearHitstop();
            cpuAiController?.ResetObservationState();
            simulation.Fighter1?.ResetForRound(p1StartX, 1, groundY);
            simulation.Fighter2?.ResetForRound(p2StartX, -1, groundY);
            simulation.ForceUpdateFacing();
        }

        private void SetFightersInputsEnabled(bool enabled)
        {
            if (simulation == null)
            {
                return;
            }

            simulation.Fighter1?.SetInputsEnabled(enabled);
            simulation.Fighter2?.SetInputsEnabled(enabled);
        }

        private string GetFighterDisplayName(int playerIndex)
        {
            FighterController fighter = playerIndex == 0 ? simulation?.Fighter1 : simulation?.Fighter2;
            if (fighter != null && fighter.Data != null && !string.IsNullOrWhiteSpace(fighter.Data.fighterName))
            {
                return fighter.Data.fighterName;
            }
            return playerIndex == 0 ? "Player 1" : "Player 2";
        }
    }
}
