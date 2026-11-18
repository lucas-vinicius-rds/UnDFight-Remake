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
        [SerializeField] private bool allowStagePropThrows = false;
        [SerializeField, Range(0f, 1f)] private float stagePropThrowChance = 0.08f;

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
        private int _evaluatedThrowTechEpisode = -1;
        private int _punishedEpisode = -1;
        private int _attackCooldownTicks;

        public bool IsEnabled => aiEnabled;
        public bool AllowStagePropThrows => allowStagePropThrows;
        public CpuDifficulty Difficulty => difficulty;

        public void SetAllowStagePropThrows(bool enabled)
        {
            allowStagePropThrows = enabled;
        }
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
            _evaluatedThrowTechEpisode = -1;
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

            if (allowStagePropThrows &&
                canAct &&
                _attackCooldownTicks <= 0 &&
                absDx > PointBlankRange &&
                controlledFighter.GetInteractableStagePropInRange() != null &&
                _rng.NextDouble() < stagePropThrowChance)
            {
                _plannedInput = new FighterInputState { HoldBlock = true, LastTickSampled = currentTick };
                inputHandler.SetSimulatedInput(_plannedInput, true);
                if (controlledFighter.TryUseNearestStageProp())
                {
                    _attackCooldownTicks = Mathf.Max(22, _profile.DecisionIntervalTicks * 3);
                    return;
                }
            }

            // 0. Combo String & Chain cancel follow-ups on hit during Medium & Hard difficulties
            if (controlledFighter.State == FighterState.Attack &&
                controlledFighter.HasRegisteredHit &&
                controlledFighter.IsGrounded &&
                difficulty != CpuDifficulty.Easy &&
                absDx <= CloseRange + 0.35f &&
                _attackCooldownTicks <= 24)
            {
                float chainChance = difficulty == CpuDifficulty.Hard ? 0.78f : 0.42f;
                if (controlledFighter.CanUseSpecial &&
                    PlayerInputHandler.IsChainCancelAllowed(controlledFighter.CurrentAttack, AttackType.Special, true, controlledFighter.CanUseEx) &&
                    _rng.NextDouble() < chainChance)
                {
                    TriggerAttack(AttackType.Special, inputHandler, currentTick);
                    return;
                }

                if (controlledFighter.CanUseEx &&
                    PlayerInputHandler.IsChainCancelAllowed(controlledFighter.CurrentAttack, AttackType.ExHeavyPunch, controlledFighter.CanUseSpecial, true) &&
                    _rng.NextDouble() < chainChance * 0.65)
                {
                    AttackType exFollow = _rng.NextDouble() < 0.5 ? AttackType.ExHeavyPunch : AttackType.ExHeavyKick;
                    TriggerAttack(exFollow, inputHandler, currentTick);
                    return;
                }

                if ((controlledFighter.CurrentAttack == AttackType.LightPunch || controlledFighter.CurrentAttack == AttackType.LightKick) &&
                    inputHandler.DialComboQueueCount == 0 &&
                    _rng.NextDouble() < chainChance)
                {
                    if (TryQueueCpuComboString(controlledFighter.CurrentAttack, inputHandler, currentTick))
                    {
                        return;
                    }

                    AttackType followUp = _rng.NextDouble() < 0.55 ? AttackType.HeavyPunch : AttackType.HeavyKick;
                    TriggerAttack(followUp, inputHandler, currentTick);
                    return;
                }
            }

            // 0b. Aerial JumpAttack during offensive jumps when approaching opponent
            if (!controlledFighter.IsGrounded &&
                controlledFighter.State == FighterState.Jump &&
                absDx <= 1.75f &&
                _attackCooldownTicks <= 0 &&
                _rng.NextDouble() < (difficulty == CpuDifficulty.Easy ? 0.35 : 0.72))
            {
                TriggerAttack(AttackType.JumpAttack, inputHandler, currentTick);
                inputHandler.SetSimulatedInput(_plannedInput, true);
                return;
            }

            // 1. Reactive defense & Throw Tech based purely on delayed simulation snapshot
            if (observed.State == FighterState.Attack && !observed.IsInRecovery && absDx <= DefenseRange)
            {
                if (observed.Attack == AttackType.Throw || observed.MoveHeight == HitHeight.Unblockable)
                {
                    if (_evaluatedThrowTechEpisode != observed.AttackEpisodeId && absDx <= CloseRange)
                    {
                        _evaluatedThrowTechEpisode = observed.AttackEpisodeId;
                        float techChance = difficulty switch
                        {
                            CpuDifficulty.Easy => 0.15f,
                            CpuDifficulty.Hard => 0.80f,
                            _ => 0.45f
                        };
                        if (_rng.NextDouble() < techChance)
                        {
                            inputHandler.BufferThrowInput(currentTick);
                            _attackCooldownTicks = 14;
                            return;
                        }
                    }
                }
                else
                {
                    if (_evaluatedDefenseEpisode != observed.AttackEpisodeId)
                    {
                        _evaluatedDefenseEpisode = observed.AttackEpisodeId;
                        _willGuardEpisode = _rng.NextDouble() <= _profile.GuardProbability;
                    }

                    if (_willGuardEpisode)
                    {
                        bool lowAttack = observed.MoveHeight == HitHeight.Low ||
                                         observed.Attack == AttackType.LightKick ||
                                         observed.Attack == AttackType.CrouchKick;
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
                // Jompes ProjectileShot Special at mid/long range on Medium/Hard
                if (canAct &&
                    _attackCooldownTicks <= 0 &&
                    difficulty != CpuDifficulty.Easy &&
                    controlledFighter.CanUseSpecial &&
                    controlledFighter.Data != null &&
                    controlledFighter.Data.specialMove != null &&
                    controlledFighter.Data.specialMove.spawnsProjectile &&
                    absDx <= 5.5f &&
                    _rng.NextDouble() < _profile.Aggression * 0.40)
                {
                    TriggerAttack(AttackType.Special, inputHandler, currentTick);
                    return;
                }

                // Advance toward opponent cleanly; dash forward at long range based on Aggression
                if (canAct && absDx > 2.80f && _attackCooldownTicks <= 0 && _rng.NextDouble() < _profile.Aggression * 0.45)
                {
                    inputHandler.BufferDashDirection(controlledFighter.FacingSign, currentTick);
                    _attackCooldownTicks = 18;
                    _plannedInput = default;
                    return;
                }

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
                // Footsies / mid-range (1.35..1.85): advance, dash, jump-in, micro-step back, or poke
                double roll = _rng.NextDouble();
                if (canAct && _attackCooldownTicks <= 0 && roll < _profile.Aggression * 0.42f)
                {
                    AttackType poke = _rng.NextDouble() < 0.55 ? AttackType.LightKick : AttackType.HeavyPunch;
                    TriggerAttack(poke, inputHandler, currentTick);
                }
                else if (canAct && difficulty != CpuDifficulty.Easy && roll < _profile.Aggression * 0.58f)
                {
                    _plannedInput = new FighterInputState { MoveX = facing, MoveY = 1f };
                    _attackCooldownTicks = 12;
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
            bool opponentGuarding = opponentFighter != null &&
                                    (opponentFighter.State == FighterState.Block ||
                                     (opponentFighter.State == FighterState.Crouch && absDx <= 1.05f));
            if (canAct && _attackCooldownTicks <= 0 && opponentGuarding && absDx <= 1.05f &&
                _rng.NextDouble() < _profile.Aggression * 0.65)
            {
                TriggerAttack(AttackType.Throw, inputHandler, currentTick);
                return;
            }

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
            if (controlledFighter != null && difficulty != CpuDifficulty.Easy)
            {
                if (controlledFighter.CanUseSpecial)
                {
                    float rawSpecialProb = difficulty == CpuDifficulty.Hard ? 0.35f : 0.20f;
                    if (_rng.NextDouble() < rawSpecialProb)
                    {
                        return AttackType.Special;
                    }
                }

                if (controlledFighter.CanUseEx)
                {
                    float exProb = difficulty == CpuDifficulty.Hard ? 0.32f : 0.18f;
                    if (_rng.NextDouble() < exProb)
                    {
                        if (opponentFighter != null && !opponentFighter.IsGrounded)
                        {
                            return AttackType.ExHeavyKick;
                        }
                        return _rng.NextDouble() < 0.55 ? AttackType.ExHeavyPunch : AttackType.ExHeavyKick;
                    }
                }
            }

            double r = _rng.NextDouble();
            if (absDx <= PointBlankRange)
            {
                if (r < 0.30) return AttackType.LightPunch;
                if (r < 0.52) return AttackType.LightKick;
                if (r < 0.70) return AttackType.CrouchKick;
                if (r < 0.85) return AttackType.HeavyPunch;
                if (r < 0.94) return AttackType.Throw;
                return AttackType.HeavyKick;
            }

            if (r < 0.25) return AttackType.LightKick;
            if (r < 0.45) return AttackType.CrouchKick;
            if (r < 0.72) return AttackType.HeavyPunch;
            if (r < 0.88) return AttackType.HeavyKick;
            return AttackType.LightPunch;
        }

        private void TriggerAttack(AttackType attack, PlayerInputHandler inputHandler, int currentTick)
        {
            _plannedInput = attack == AttackType.CrouchKick
                ? new FighterInputState { MoveY = -1f, LastTickSampled = currentTick }
                : default;
            inputHandler.SyncSimulationTick(currentTick);
            inputHandler.SetSimulatedInput(_plannedInput, true);

            if (attack == AttackType.Throw)
            {
                inputHandler.BufferThrowInput(currentTick);
            }
            else if (attack == AttackType.ExHeavyPunch || attack == AttackType.ExHeavyKick)
            {
                inputHandler.BufferExInput(attack, currentTick);
            }
            else
            {
                inputHandler.BufferButton(AttackToButton(attack));
            }

            _attackCooldownTicks = attack switch
            {
                AttackType.LightPunch => 18,
                AttackType.LightKick => 22,
                AttackType.HeavyPunch => 28,
                AttackType.HeavyKick => 34,
                AttackType.CrouchKick => 32,
                AttackType.JumpAttack => 22,
                AttackType.Throw => 26,
                AttackType.ExHeavyPunch => 28,
                AttackType.ExHeavyKick => 32,
                AttackType.Special => 38,
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

        private bool TryQueueCpuComboString(AttackType openerAttack, PlayerInputHandler inputHandler, int currentTick)
        {
            ComboStringDefinition[] strings = controlledFighter != null && controlledFighter.Data != null
                ? controlledFighter.Data.comboStrings
                : null;
            if (strings == null || strings.Length == 0)
            {
                return false;
            }

            int startIdx = _rng.Next(strings.Length);
            for (int offset = 0; offset < strings.Length; offset++)
            {
                ComboStringDefinition def = strings[(startIdx + offset) % strings.Length];
                if (def?.steps == null || def.steps.Length < 3 || def.steps[0].attackType != openerAttack)
                {
                    continue;
                }

                inputHandler.SyncSimulationTick(currentTick);
                for (int s = 1; s < def.steps.Length; s++)
                {
                    inputHandler.EnqueueDialComboInput(def.steps[s].inputButton, def.steps[s].requireCrouch);
                }

                bool endsCrouched = def.steps[def.steps.Length - 1].requireCrouch;
                _plannedInput = new FighterInputState
                {
                    MoveY = endsCrouched ? -1f : 0f,
                    LastTickSampled = currentTick
                };
                inputHandler.SetSimulatedInput(_plannedInput, true);
                _attackCooldownTicks = 28;
                return true;
            }

            return false;
        }

        private static FighterInputButton AttackToButton(AttackType attack)
        {
            return attack switch
            {
                AttackType.HeavyPunch => FighterInputButton.HeavyPunch,
                AttackType.ComboPunchFinisher => FighterInputButton.HeavyPunch,
                AttackType.LightKick => FighterInputButton.LightKick,
                AttackType.HeavyKick => FighterInputButton.HeavyKick,
                AttackType.ComboKickFinisher => FighterInputButton.HeavyKick,
                AttackType.CrouchKick => FighterInputButton.HeavyKick,
                AttackType.JumpAttack => FighterInputButton.HeavyKick,
                AttackType.Special => FighterInputButton.Special,
                _ => FighterInputButton.LightPunch
            };
        }

        private static HitHeight InferAttackHeight(AttackType attack)
        {
            return attack switch
            {
                AttackType.LightKick => HitHeight.Low,
                AttackType.CrouchKick => HitHeight.Low,
                AttackType.HeavyKick => HitHeight.High,
                AttackType.ComboKickFinisher => HitHeight.High,
                AttackType.JumpAttack => HitHeight.Overhead,
                AttackType.Throw => HitHeight.Unblockable,
                _ => HitHeight.Mid
            };
        }

        private static int InferActiveEndTick(AttackType attack)
        {
            return attack switch
            {
                AttackType.LightPunch => 7,
                AttackType.HeavyPunch => 11,
                AttackType.ComboPunchFinisher => 11,
                AttackType.LightKick => 8,
                AttackType.HeavyKick => 13,
                AttackType.ComboKickFinisher => 12,
                AttackType.CrouchKick => 12,
                AttackType.JumpAttack => 12,
                AttackType.Throw => 8,
                AttackType.Special => 15,
                _ => 8
            };
        }
    }
}
