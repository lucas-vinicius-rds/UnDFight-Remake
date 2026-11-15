using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnDFight
{
    /// <summary>
    /// Fair 60 Hz CPU AI controller driving a fighter through PlayerInputHandler virtual inputs.
    /// Never reads raw opponent controller inputs; only observes opponent simulation state
    /// through a configurable reaction-delay ring buffer.
    /// </summary>
    public class CpuAiController : MonoBehaviour
    {
        [Serializable]
        public struct DifficultyProfile
        {
            public int ReactionDelayTicks;
            public int DecisionIntervalTicks;
            [Range(0f, 1f)] public float GuardProbability;
            [Range(0f, 1f)] public float PunishProbability;
            [Range(0f, 1f)] public float Aggression;
        }

        private struct ObservedSnapshot
        {
            public int Tick;
            public FighterState State;
            public AttackType Attack;
            public HitHeight MoveHeight;
            public bool IsInRecovery;
            public int AttackEpisodeId;
            public float OpponentSimX;
        }

        private const int MaxObservationHistory = 64;
        private const float DefenseRange = 2.15f;
        private const float ApproachThreshold = 1.85f;
        private const float CloseRange = 1.35f;
        private const float PointBlankRange = 0.90f;

        [Header("References")]
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private FighterController controlledFighter;
        [SerializeField] private FighterController opponentFighter;
        [SerializeField] private MatchManager matchManager;

        [Header("AI Settings")]
        [SerializeField] private CpuDifficulty difficulty = CpuDifficulty.Medium;
        [SerializeField] private bool aiEnabled;
        [SerializeField] private int randomSeed = 1337;
        [SerializeField] private bool allowTrainingF2Toggle = true;

        private readonly List<ObservedSnapshot> _history = new List<ObservedSnapshot>(MaxObservationHistory);
        private System.Random _rng;
        private DifficultyProfile _profile;
        private float? _guardProbabilityOverride;

        private FighterInputState _plannedInput;
        private int _lastDecisionTick = -1;
        private int _lastObservedTick = -1;
        private int _attackEpisodeCounter;
        private FighterState _prevRecordedOppState = FighterState.Idle;
        private AttackType _prevRecordedOppAttack = AttackType.None;
        private int _evaluatedDefenseEpisode = -1;
        private bool _willGuardEpisode;
        private int _punishedEpisode = -1;
        private int _attackCooldownTicks;

        public bool IsEnabled => aiEnabled;
        public CpuDifficulty Difficulty => difficulty;
        public DifficultyProfile ActiveProfile => _profile;
        public FighterController ControlledFighter => controlledFighter;
        public FighterController OpponentFighter => opponentFighter;

        public event Action<bool> OnAiToggled;

        public static DifficultyProfile GetProfile(CpuDifficulty diff)
        {
            return diff switch
            {
                CpuDifficulty.Easy => new DifficultyProfile
                {
                    ReactionDelayTicks = 18,
                    DecisionIntervalTicks = 14,
                    GuardProbability = 0.28f,
                    PunishProbability = 0.25f,
                    Aggression = 0.38f
                },
                CpuDifficulty.Hard => new DifficultyProfile
                {
                    ReactionDelayTicks = 4,
                    DecisionIntervalTicks = 4,
                    GuardProbability = 0.88f,
                    PunishProbability = 0.90f,
                    Aggression = 0.85f
                },
                _ => new DifficultyProfile
                {
                    ReactionDelayTicks = 10,
                    DecisionIntervalTicks = 8,
                    GuardProbability = 0.62f,
                    PunishProbability = 0.60f,
                    Aggression = 0.65f
                }
            };
        }

        private void Awake()
        {
            EnsureInitialized();
            if (simulation == null)
            {
                simulation = GetComponent<FightSimulation>();
            }
            if (matchManager == null)
            {
                matchManager = FindAnyObjectByType<MatchManager>();
            }
        }

        private void Update()
        {
            if (!allowTrainingF2Toggle || !GameSessionState.IsTrainingMode)
            {
                return;
            }
            if (simulation != null && simulation.IsPaused)
            {
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame)
            {
                SetEnabled(!aiEnabled);
            }
        }

        public void Configure(
            FightSimulation sim,
            FighterController self,
            FighterController opponent,
            MatchManager match = null)
        {
            simulation = sim;
            controlledFighter = self;
            opponentFighter = opponent;
            if (match != null)
            {
                matchManager = match;
            }
            EnsureInitialized();
            ResetObservationState();
        }

        public void SetDifficulty(CpuDifficulty newDifficulty)
        {
            difficulty = newDifficulty;
            _profile = GetProfile(difficulty);
            if (_guardProbabilityOverride.HasValue)
            {
                _profile.GuardProbability = _guardProbabilityOverride.Value;
            }
        }

        public void SetSeed(int seed)
        {
            randomSeed = seed;
            _rng = new System.Random(randomSeed);
        }

        public void SetGuardProbabilityOverride(float? probability)
        {
            _guardProbabilityOverride = probability.HasValue ? Mathf.Clamp01(probability.Value) : (float?)null;
            SetDifficulty(difficulty);
        }

        public void SetEnabled(bool enabled)
        {
            EnsureInitialized();
            aiEnabled = enabled;
            ResetObservationState();

            if (controlledFighter != null && controlledFighter.InputHandler != null)
            {
                if (enabled)
                {
                    controlledFighter.InputHandler.SetSimulatedInput(default, true);
                }
                else
                {
                    controlledFighter.InputHandler.ClearSimulatedInput();
                }
            }

            OnAiToggled?.Invoke(aiEnabled);
        }

        public void ResetObservationState()
        {
            _history.Clear();
            _plannedInput = default;
            _lastDecisionTick = -1;
            _lastObservedTick = -1;
            _attackEpisodeCounter = 0;
            _prevRecordedOppState = FighterState.Idle;
            _prevRecordedOppAttack = AttackType.None;
            _evaluatedDefenseEpisode = -1;
            _willGuardEpisode = false;
            _punishedEpisode = -1;
            _attackCooldownTicks = 0;
        }

        /// <summary>
        /// Captures the opponent's visible simulation state for the current tick into the delay ring buffer.
        /// Never accesses opponent's PlayerInputHandler.
        /// </summary>
        public void ObserveTick(int currentTick)
        {
            if (opponentFighter == null || currentTick == _lastObservedTick)
            {
                return;
            }

            _lastObservedTick = currentTick;
            FighterState oppState = opponentFighter.State;
            AttackType oppAttack = opponentFighter.CurrentAttack;

            if (oppState == FighterState.Attack &&
                (_prevRecordedOppState != FighterState.Attack || _prevRecordedOppAttack != oppAttack))
            {
                _attackEpisodeCounter++;
            }

            _prevRecordedOppState = oppState;
            _prevRecordedOppAttack = oppAttack;

            HitHeight height = HitHeight.Mid;
            bool inRecovery = false;
            if (oppState == FighterState.Attack)
            {
                MoveData move = opponentFighter.CurrentMoveData;
                if (move != null)
                {
                    height = move.hitHeight;
                    inRecovery = opponentFighter.StateTicks > move.LastActiveTick;
                }
                else
                {
                    height = InferAttackHeight(oppAttack);
                    inRecovery = opponentFighter.StateTicks > InferActiveEndTick(oppAttack);
                }
            }

            _history.Add(new ObservedSnapshot
            {
                Tick = currentTick,
                State = oppState,
                Attack = oppAttack,
                MoveHeight = height,
                IsInRecovery = inRecovery,
                AttackEpisodeId = _attackEpisodeCounter,
                OpponentSimX = opponentFighter.SimPosition.x
            });

            if (_history.Count > MaxObservationHistory)
            {
                _history.RemoveAt(0);
            }
        }

        /// <summary>
        /// Executes one 60 Hz AI decision & virtual input injection step before FighterController.StepTick.
        /// </summary>
        public void StepAiTick(int currentTick)
        {
            EnsureInitialized();

            if (!aiEnabled || controlledFighter == null || opponentFighter == null)
            {
                return;
            }

            PlayerInputHandler inputHandler = controlledFighter.InputHandler;
            if (inputHandler == null)
            {
                return;
            }

            inputHandler.SyncSimulationTick(currentTick);

            if ((matchManager != null && !matchManager.InputsAllowed) ||
                controlledFighter.State == FighterState.Victory ||
                controlledFighter.State == FighterState.Defeat)
            {
                _plannedInput = default;
                inputHandler.SetSimulatedInput(default, true);
                return;
            }

            if (_attackCooldownTicks > 0)
            {
                _attackCooldownTicks--;
            }

            if (!TryGetDelayedObservation(currentTick, out ObservedSnapshot observed))
            {
                // Reaction delay window has not elapsed yet: observe nothing actionable from recent frames
                EvaluateSpacingPlan(currentTick, Mathf.Abs(opponentFighter.SimPosition.x - controlledFighter.SimPosition.x), inputHandler);
                inputHandler.SetSimulatedInput(_plannedInput, true);
                return;
            }

            float absDx = Mathf.Abs(observed.OpponentSimX - controlledFighter.SimPosition.x);
            bool canAct = IsControlledActionable();

            // 1. Reactive defense based purely on delayed simulation snapshot
            if (observed.State == FighterState.Attack && !observed.IsInRecovery && absDx <= DefenseRange)
            {
                if (_evaluatedDefenseEpisode != observed.AttackEpisodeId)
                {
                    _evaluatedDefenseEpisode = observed.AttackEpisodeId;
                    _willGuardEpisode = _rng.NextDouble() <= _profile.GuardProbability;
                }

                if (_willGuardEpisode)
                {
                    bool lowAttack = observed.MoveHeight == HitHeight.Low || observed.Attack == AttackType.LightKick;
                    _plannedInput = new FighterInputState
                    {
                        MoveX = -controlledFighter.FacingSign,
                        MoveY = lowAttack ? -1f : 0f,
                        HoldBlock = true
                    };
                    inputHandler.SetSimulatedInput(_plannedInput, true);
                    return;
                }
            }

            // 2. Punish opponent whiff/recovery inside close range
            if (canAct && _attackCooldownTicks <= 0 && absDx <= CloseRange &&
                observed.State == FighterState.Attack && observed.IsInRecovery &&
                _punishedEpisode != observed.AttackEpisodeId)
            {
                _punishedEpisode = observed.AttackEpisodeId;
                if (_rng.NextDouble() <= _profile.PunishProbability)
                {
                    AttackType punishMove = ChooseCloseRangeAttack(absDx);
                    TriggerAttack(punishMove, inputHandler, currentTick);
                    inputHandler.SetSimulatedInput(_plannedInput, true);
                    return;
                }
            }

            // 3. Tactical spacing & aggression evaluated at DifficultyProfile cadence
            EvaluateSpacingPlan(currentTick, absDx, inputHandler);
            inputHandler.SetSimulatedInput(_plannedInput, true);
        }

        private void EvaluateSpacingPlan(int currentTick, float absDx, PlayerInputHandler inputHandler)
        {
            if (_lastDecisionTick >= 0 && currentTick - _lastDecisionTick < _profile.DecisionIntervalTicks)
            {
                return;
            }

            _lastDecisionTick = currentTick;
            float facing = controlledFighter.FacingSign;
            bool canAct = IsControlledActionable();

            if (absDx > ApproachThreshold)
            {
                // Advance toward opponent cleanly; Easy difficulty occasionally pauses briefly
                bool advance = difficulty != CpuDifficulty.Easy || _rng.NextDouble() < 0.85;
                _plannedInput = new FighterInputState
                {
                    MoveX = advance ? facing : 0f,
                    MoveY = 0f,
                    HoldBlock = false
                };
                return;
            }

            if (absDx > CloseRange)
            {
                // Footsies / mid-range (1.35..1.85): advance, micro-step back, or poke with mid/low kick
                double roll = _rng.NextDouble();
                if (canAct && _attackCooldownTicks <= 0 && roll < _profile.Aggression * 0.42f)
                {
                    AttackType poke = _rng.NextDouble() < 0.55 ? AttackType.LightKick : AttackType.HeavyPunch;
                    TriggerAttack(poke, inputHandler, currentTick);
                }
                else if (roll < _profile.Aggression + 0.15f)
                {
                    _plannedInput = new FighterInputState { MoveX = facing };
                }
                else
                {
                    _plannedInput = new FighterInputState { MoveX = -facing * 0.5f };
                }
                return;
            }

            // Close combat range (<= 1.35)
            if (canAct && _attackCooldownTicks <= 0 && _rng.NextDouble() <= _profile.Aggression)
            {
                AttackType move = ChooseCloseRangeAttack(absDx);
                TriggerAttack(move, inputHandler, currentTick);
                return;
            }

            double defensiveRoll = _rng.NextDouble();
            if (defensiveRoll < 0.35)
            {
                _plannedInput = new FighterInputState { MoveX = -facing, HoldBlock = true };
            }
            else if (defensiveRoll < 0.65)
            {
                _plannedInput = new FighterInputState { MoveX = facing };
            }
            else
            {
                _plannedInput = default;
            }
        }

        private AttackType ChooseCloseRangeAttack(float absDx)
        {
            double r = _rng.NextDouble();
            if (absDx <= PointBlankRange)
            {
                if (r < 0.38) return AttackType.LightPunch;
                if (r < 0.66) return AttackType.LightKick;
                if (r < 0.86) return AttackType.HeavyPunch;
                return AttackType.HeavyKick;
            }

            if (r < 0.30) return AttackType.LightKick;
            if (r < 0.62) return AttackType.HeavyPunch;
            if (r < 0.84) return AttackType.HeavyKick;
            return AttackType.LightPunch;
        }

        private void TriggerAttack(AttackType attack, PlayerInputHandler inputHandler, int currentTick)
        {
            _plannedInput = default;
            inputHandler.SyncSimulationTick(currentTick);
            inputHandler.BufferButton(AttackToButton(attack));
            _attackCooldownTicks = attack switch
            {
                AttackType.LightPunch => 18,
                AttackType.LightKick => 22,
                AttackType.HeavyPunch => 28,
                AttackType.HeavyKick => 34,
                _ => 20
            };
        }

        private bool IsControlledActionable()
        {
            FighterState s = controlledFighter.State;
            return controlledFighter.IsGrounded &&
                   (s == FighterState.Idle ||
                    s == FighterState.WalkForward ||
                    s == FighterState.WalkBackward ||
                    s == FighterState.Crouch ||
                    s == FighterState.Block);
        }

        private bool TryGetDelayedObservation(int currentTick, out ObservedSnapshot snapshot)
        {
            int targetTick = currentTick - _profile.ReactionDelayTicks;
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (_history[i].Tick <= targetTick)
                {
                    snapshot = _history[i];
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        private void EnsureInitialized()
        {
            if (_rng == null)
            {
                _rng = new System.Random(randomSeed);
            }
            _profile = GetProfile(difficulty);
            if (_guardProbabilityOverride.HasValue)
            {
                _profile.GuardProbability = _guardProbabilityOverride.Value;
            }
        }

        private static FighterInputButton AttackToButton(AttackType attack)
        {
            return attack switch
            {
                AttackType.HeavyPunch => FighterInputButton.HeavyPunch,
                AttackType.LightKick => FighterInputButton.LightKick,
                AttackType.HeavyKick => FighterInputButton.HeavyKick,
                _ => FighterInputButton.LightPunch
            };
        }

        private static HitHeight InferAttackHeight(AttackType attack)
        {
            return attack switch
            {
                AttackType.LightKick => HitHeight.Low,
                AttackType.HeavyKick => HitHeight.High,
                _ => HitHeight.Mid
            };
        }

        private static int InferActiveEndTick(AttackType attack)
        {
            return attack switch
            {
                AttackType.LightPunch => 7,
                AttackType.HeavyPunch => 11,
                AttackType.LightKick => 8,
                AttackType.HeavyKick => 13,
                _ => 8
            };
        }
    }
}
