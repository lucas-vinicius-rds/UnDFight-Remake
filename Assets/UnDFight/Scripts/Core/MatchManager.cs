using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnDFight
{
    /// <summary>
    /// Best-of-3 match rules, 99-second countdown timer, round intro/KO/Time Over flow,
    /// and post-round victory/defeat states driven strictly by the 60 Hz simulation clock.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FightSimulation simulation;

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
        public int RemainingSeconds => Mathf.Clamp(Mathf.CeilToInt(RoundTimerTicks / (float)FightSimulation.TickRate), 0, roundDurationSeconds);
        public int PhaseTicks { get; private set; }
        public int WinnerPlayerIndex { get; private set; } = -1;
        public string BannerText { get; private set; } = string.Empty;
        public string SubtitleText { get; private set; } = string.Empty;
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
            if (Phase != MatchPhase.MatchOver)
            {
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame))
            {
                RestartMatch();
            }
        }

        public void InitializeMatch(FightSimulation targetSimulation)
        {
            simulation = targetSimulation;
            if (simulation != null)
            {
                simulation.SetMatchManager(this);
            }

            _initialized = true;
            RestartMatch();
        }

        public void RestartMatch()
        {
            CurrentRound = 1;
            P1RoundsWon = 0;
            P2RoundsWon = 0;
            WinnerPlayerIndex = -1;
            StartRound();
        }

        public void StartRound()
        {
            Phase = MatchPhase.RoundIntro;
            PhaseTicks = 0;
            _wasTimeOver = false;
            WinnerPlayerIndex = -1;
            RoundTimerTicks = roundDurationSeconds * FightSimulation.TickRate;
            BannerText = $"ROUND {CurrentRound}";
            SubtitleText = string.Empty;

            ResetFightersForRound();
            SetFightersInputsEnabled(false);
            OnMatchStateChanged?.Invoke(this);
        }

        /// <summary>
        /// Immediately transitions from RoundIntro to Fighting with full timer (primarily for deterministic combat tests).
        /// </summary>
        public void SkipIntroToFighting()
        {
            Phase = MatchPhase.Fighting;
            PhaseTicks = 0;
            BannerText = string.Empty;
            SubtitleText = string.Empty;
            SetFightersInputsEnabled(true);
            OnMatchStateChanged?.Invoke(this);
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

            if (RoundTimerTicks > 0)
            {
                RoundTimerTicks--;
            }

            FighterController p1 = simulation != null ? simulation.Fighter1 : null;
            FighterController p2 = simulation != null ? simulation.Fighter2 : null;
            if (p1 == null || p2 == null)
            {
                return;
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
                    SubtitleText = "PRESS ENTER OR R TO REMATCH";
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
