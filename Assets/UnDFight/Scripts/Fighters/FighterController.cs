using System;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Fighter simulation state machine running on the 60 Hz tick with Z = 0 locked
    /// and visual-only interpolation between simulation ticks.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class FighterController : MonoBehaviour
    {
        public static event Action<FighterController, AttackType> OnAnyAttackStarted;
        public static event Action<FighterController, bool> OnAnyDashStarted;
        public const int KnockDownDurationTicks = 45;
        public const int GetUpDurationTicks = 40;
        public const int WakeupInvincibilityGraceTicks = 6;
        public const float BaseKnockbackVelocityScale = 2.0f;

        public const int MaxSuperGauge = 100;
        public const int SuperGainLightHitAttacker = 14;
        public const int SuperGainHeavyHitAttacker = 20;
        public const int SuperGainHitDefender = 7;
        public const int SuperGainBlockAttacker = 6;
        public const int SuperGainBlockDefender = 9;
        public const int SuperGainWhiff = 2;

        [Header("Data & References")]
        [SerializeField] private FighterData fighterData;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private CapsuleCollider hurtboxCollider;
        [SerializeField] private Animator fighterAnimator;
        [SerializeField] private bool squashVisualOnCrouch = false;

        private const float TurnDurationSeconds = 8f / 60f;
        public const int StageThrowReleaseTick = 4;
        public const int StageThrowTotalTicks = 16;
        public const int StageInteractCooldownTicks = 30;

        private static readonly int StateParamHash = Animator.StringToHash("State");
        private static readonly int AttackTypeParamHash = Animator.StringToHash("AttackType");
        private static readonly int AttackTriggerParamHash = Animator.StringToHash("AttackTrigger");
        private static readonly int IsGroundedParamHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsTurningParamHash = Animator.StringToHash("IsTurning");
        private static readonly int HitReactStateHash = Animator.StringToHash("HitReact");
        private static readonly int BlockStateHash = Animator.StringToHash("Block");
        private static readonly int IdleStateHash = Animator.StringToHash("Idle");
        private static readonly int Turn180StateHash = Animator.StringToHash("Turn180");

        private FightSimulation _simulation;
        private PlayerInputHandler _inputHandler;

        private Vector2 _prevSimPos;
        private Vector2 _currSimPos;
        private Vector2 _simVelocity;

        private int _facingSign = 1;
        private bool _isGrounded = true;
        private FighterState _state = FighterState.Idle;
        private AttackType _currentAttack = AttackType.None;
        private MoveData _currentMoveData;
        private int _stateTicks;
        private int _stateTotalTicks;
        private int _currentHealth;
        private bool _hasRegisteredHit;
        private bool _blockFromCrouch;
        private bool _inputsEnabled = true;
        private int _wakeupInvincibilityTicks;
        private int _ticksSinceLastHit;
        private float _knockbackDecelPerSec;
        private int _superGauge;
        private int _receivedComboHits;
        private int _receivedComboDamage;
        private bool _grantedStartSuperOnCurrentMove;
        private bool _hasUsedAirAttackThisJump;
        private float _baseHurtboxHeight = 1.81f;
        private float _baseHurtboxCenterY = 0.905f;
        private float _visualYaw = 90f;
        private float _prevVisualYaw = 90f;
        private float _turnStartYaw = 90f;
        private float _turnTargetYaw = 90f;
        private float _turnElapsed = TurnDurationSeconds;
        private bool _isTurnAnimActive;
        private FighterState _turnStartState;
        private StageThrowableProp _pendingThrowProp;
        private int _stageInteractCooldownTicks;
        private Rigidbody _hurtboxRigidbody;

        public FightSimulation Simulation => _simulation;
        public CapsuleCollider HurtboxCollider => hurtboxCollider != null ? hurtboxCollider : (hurtboxCollider = GetComponentInChildren<CapsuleCollider>());
        public FighterData Data => fighterData;
        public FighterState State => _state;
        public AttackType CurrentAttack => _currentAttack;
        public MoveData CurrentMoveData => _currentMoveData;
        public int StateTicks => _stateTicks;
        public int StateTotalTicks => _stateTotalTicks;
        public Vector2 SimPosition => _currSimPos;
        public Vector2 SimVelocity => _simVelocity;
        public int FacingSign => _facingSign;
        public float VisualYaw => _visualYaw;
        public bool IsTurnAnimActive => _isTurnAnimActive;
        public bool IsGrounded => _isGrounded;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => fighterData != null ? fighterData.maxHealth : 1000;
        public int SuperGauge => _superGauge;
        public float SuperNormalized => Mathf.Clamp01((float)_superGauge / MaxSuperGauge);
        public bool CanUseSpecial => _superGauge >= MaxSuperGauge;
        public int ReceivedComboHits => _receivedComboHits;
        public int ReceivedComboDamage => _receivedComboDamage;
        public bool HasRegisteredHit => _hasRegisteredHit;
        public bool InputsEnabled => _inputsEnabled;
        public int WakeupInvincibilityTicks => _wakeupInvincibilityTicks;
        public int TicksSinceLastHit => _ticksSinceLastHit;
        public bool IsCrouching =>
            _state == FighterState.Crouch ||
            (_state == FighterState.Block && _blockFromCrouch) ||
            (_state == FighterState.Attack && _currentAttack == AttackType.CrouchKick);
        public bool IsDashInvulnerable
        {
            get
            {
                if (_state != FighterState.DashBackward)
                {
                    return false;
                }
                int invulTicks = fighterData != null ? fighterData.backdashInvulnerableTicks : 5;
                return invulTicks > 0 && _stateTicks <= invulTicks;
            }
        }
        public bool IsInvulnerable =>
            _wakeupInvincibilityTicks > 0 ||
            IsDashInvulnerable ||
            _state == FighterState.KnockDown ||
            _state == FighterState.GetUp ||
            _state == FighterState.Victory ||
            _state == FighterState.Defeat;
        public PlayerInputHandler InputHandler => _inputHandler;

        public float PushboxRadius => fighterData != null ? fighterData.pushboxRadius : 0.36f;
        public float CurrentHurtboxHeight
        {
            get
            {
                float fullH = fighterData != null ? fighterData.heightMeters : FighterScaling.ReferenceHeightMeters;
                return IsCrouching ? fullH * (fighterData != null ? fighterData.crouchHeightRatio : 0.62f) : fullH;
            }
        }

        private void Awake()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0);
            }
            if (hurtboxCollider == null)
            {
                hurtboxCollider = GetComponentInChildren<CapsuleCollider>();
            }
            if (fighterAnimator == null)
            {
                fighterAnimator = GetComponentInChildren<Animator>();
            }
            EnsureKinematicHurtboxRigidbody();
            SetupAnimatorForFixedTick();
            ApplyFighterDataScaling();
        }

        private void EnsureKinematicHurtboxRigidbody()
        {
            if (_hurtboxRigidbody == null)
            {
                _hurtboxRigidbody = GetComponent<Rigidbody>();
            }
            if (_hurtboxRigidbody != null)
            {
                _hurtboxRigidbody.isKinematic = true;
                _hurtboxRigidbody.useGravity = false;
                _hurtboxRigidbody.interpolation = RigidbodyInterpolation.None;
            }
        }

        private void SetupAnimatorForFixedTick()
        {
            if (fighterAnimator == null)
            {
                return;
            }
            fighterAnimator.applyRootMotion = false;
            fighterAnimator.enabled = false;
        }

        public void SetFighterData(FighterData data)
        {
            fighterData = data;
            if (_currentHealth <= 0)
            {
                _currentHealth = MaxHealth;
            }
            ApplyFighterDataScaling();
        }

        public void InitializeSimulation(FightSimulation simulation, int playerIndex)
        {
            _simulation = simulation;
            if (StagePhysicsLayers.HasDedicatedLayers && gameObject.layer == 0)
            {
                gameObject.layer = StagePhysicsLayers.FighterHurtboxLayer;
            }
            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0);
            }
            if (hurtboxCollider == null)
            {
                hurtboxCollider = GetComponentInChildren<CapsuleCollider>();
            }
            if (_inputHandler == null)
            {
                _inputHandler = GetComponent<PlayerInputHandler>();
            }
            _inputHandler.Configure(playerIndex);

            Vector3 p = transform.position;
            _currSimPos = new Vector2(p.x, Mathf.Max(simulation != null ? simulation.GroundY : 0f, p.y));
            _prevSimPos = _currSimPos;
            _simVelocity = Vector2.zero;
            _isGrounded = _currSimPos.y <= (simulation != null ? simulation.GroundY : 0f) + 0.001f;
            _currentHealth = MaxHealth;
            _state = FighterState.Idle;
            _currentAttack = AttackType.None;
            _currentMoveData = null;
            _stateTicks = 0;
            _stateTotalTicks = 0;
            _hasRegisteredHit = false;
            _blockFromCrouch = false;
            _wakeupInvincibilityTicks = 0;
            _ticksSinceLastHit = 0;
            _knockbackDecelPerSec = 0f;
            _superGauge = 0;
            _receivedComboHits = 0;
            _receivedComboDamage = 0;
            _grantedStartSuperOnCurrentMove = false;
            _hasUsedAirAttackThisJump = false;
            _pendingThrowProp = null;
            _stageInteractCooldownTicks = 0;
            EnsureKinematicHurtboxRigidbody();
            SetupAnimatorForFixedTick();
            ApplyFighterDataScaling();
            SnapVisualYaw();
            StepAnimatorTick(0f);
        }

        /// <summary>
        /// Resets fighter state, health, and position cleanly at the start of a round.
        /// </summary>
        public void ResetForRound(float startX, int facingSign, float groundY = 0f)
        {
            _currSimPos = new Vector2(startX, groundY);
            _prevSimPos = _currSimPos;
            _simVelocity = Vector2.zero;
            _facingSign = facingSign >= 0 ? 1 : -1;
            _isGrounded = true;
            _currentHealth = MaxHealth;
            _state = FighterState.Idle;
            _currentAttack = AttackType.None;
            _currentMoveData = null;
            _stateTicks = 0;
            _stateTotalTicks = 0;
            _hasRegisteredHit = false;
            _blockFromCrouch = false;
            _wakeupInvincibilityTicks = 0;
            _ticksSinceLastHit = 0;
            _knockbackDecelPerSec = 0f;
            _receivedComboHits = 0;
            _receivedComboDamage = 0;
            _grantedStartSuperOnCurrentMove = false;
            _hasUsedAirAttackThisJump = false;
            _pendingThrowProp = null;
            _stageInteractCooldownTicks = 0;
            _inputHandler?.ClearBufferedInputs();

            transform.position = new Vector3(_currSimPos.x, _currSimPos.y, 0f);
            UpdateHurtboxDimensions();
            SnapVisualYaw();

            if (fighterAnimator != null && fighterAnimator.runtimeAnimatorController != null)
            {
                fighterAnimator.Play(IdleStateHash, 0, 0f);
            }
            StepAnimatorTick(0f);
        }

        public void SetInputsEnabled(bool enabled)
        {
            _inputsEnabled = enabled;
        }

        public void SetMatchResultPose(FighterState resultState)
        {
            if (resultState != FighterState.Victory &&
                resultState != FighterState.Defeat &&
                resultState != FighterState.KnockDown)
            {
                return;
            }

            _simVelocity.x = 0f;
            _knockbackDecelPerSec = 0f;
            _stateTotalTicks = int.MaxValue;
            _blockFromCrouch = false;
            EnterState(resultState);
            StepAnimatorTick(0f);
        }

        public void HoldVisualForHitstop()
        {
            _prevSimPos = _currSimPos;
            _prevVisualYaw = _visualYaw;
        }

        /// <summary>
        /// Restores health during Training mode auto-regeneration and allows getting up if knocked out at 0 HP.
        /// </summary>
        public void RegenerateHealthTick(int amount)
        {
            if (amount <= 0 || _currentHealth >= MaxHealth)
            {
                return;
            }

            bool wasZeroHp = _currentHealth <= 0;
            _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);

            if (wasZeroHp && _currentHealth > 0 && _state == FighterState.KnockDown && _stateTicks >= _stateTotalTicks && _isGrounded)
            {
                _simVelocity.x = 0f;
                _stateTotalTicks = GetUpDurationTicks;
                _wakeupInvincibilityTicks = GetUpDurationTicks + WakeupInvincibilityGraceTicks;
                EnterState(FighterState.GetUp);
                StepAnimatorTick(0f);
            }
        }

        private void ApplyFighterDataScaling()
        {
            if (fighterData == null)
            {
                return;
            }

            float modelScale = fighterData.VisualScale;
            if (visualRoot != null)
            {
                visualRoot.localScale = new Vector3(modelScale, modelScale, modelScale);
            }

            if (hurtboxCollider != null)
            {
                float hurtboxScale = fighterData.HurtboxScale;
                _baseHurtboxHeight = FighterScaling.ReferenceHeightMeters * hurtboxScale;
                _baseHurtboxCenterY = _baseHurtboxHeight * 0.5f;
                hurtboxCollider.radius = fighterData.pushboxRadius;
                hurtboxCollider.height = _baseHurtboxHeight;
                hurtboxCollider.center = new Vector3(0f, _baseHurtboxCenterY, 0f);
            }
        }

        public void SetFacing(int sign) => SetFacing(sign, false);

        /// <summary>
        /// Flips logical facing immediately and either snaps visual yaw (round init)
        /// or triggers a smooth 8-tick 180-degree front-facing visual pivot.
        /// </summary>
        public void SetFacing(int sign, bool immediate)
        {
            int newSign = sign >= 0 ? 1 : -1;
            bool changed = newSign != _facingSign;
            _facingSign = newSign;

            if (immediate)
            {
                SnapVisualYaw();
                return;
            }

            if (!changed)
            {
                return;
            }

            _turnStartYaw = _visualYaw;
            _turnTargetYaw = GetTargetYawForFacing(_facingSign);
            _turnElapsed = 0f;

            bool canPlayTurnAnim = _isGrounded &&
                                   (_state == FighterState.Idle ||
                                    _state == FighterState.WalkForward ||
                                    _state == FighterState.WalkBackward ||
                                    _state == FighterState.Crouch);
            if (canPlayTurnAnim)
            {
                _turnStartState = _state;
                _isTurnAnimActive = true;
                if (fighterAnimator != null && fighterAnimator.runtimeAnimatorController != null)
                {
                    fighterAnimator.SetBool(IsTurningParamHash, true);
                    fighterAnimator.CrossFadeInFixedTime(Turn180StateHash, 0.02f, 0, 0f);
                }
            }
            else if (ShouldSnapVisualTurnForState(_state))
            {
                SnapVisualYaw();
            }
        }

        public void SetSimPositionX(float x)
        {
            _currSimPos.x = x;
        }

        public void MarkHitRegistered()
        {
            _hasRegisteredHit = true;
        }

        /// <summary>
        /// Deterministic world-space XY hurtbox rectangle for 60 Hz combat resolution.
        /// </summary>
        public Rect GetHurtboxRect()
        {
            float hurtboxScale = fighterData != null ? fighterData.HurtboxScale : 1f;
            float width = PushboxRadius * 2.1f * hurtboxScale;
            float height = CurrentHurtboxHeight;
            return new Rect(_currSimPos.x - width * 0.5f, _currSimPos.y, width, height);
        }

        /// <summary>
        /// Returns active world-space XY hitbox and move data only during [Startup+1 .. Startup+Active]
        /// and only if this execution has not yet registered a hit.
        /// </summary>
        public bool TryGetActiveHitbox(out Rect worldHitbox, out MoveData moveData)
        {
            if (_state == FighterState.Attack &&
                !_hasRegisteredHit &&
                _currentMoveData != null &&
                _currentMoveData.IsActiveTick(_stateTicks))
            {
                float scale = fighterData != null ? fighterData.HurtboxScale : 1f;
                worldHitbox = _currentMoveData.GetWorldHitboxRect(_currSimPos, _facingSign, scale);
                moveData = _currentMoveData;
                return true;
            }

            worldHitbox = default;
            moveData = null;
            return false;
        }

        /// <summary>
        /// Evaluates whether this fighter blocks an incoming attack based on state, guard input, and move height.
        /// </summary>
        public bool CanBlockMove(MoveData move, int attackerFacingSign)
        {
            if (move == null || !_isGrounded || IsInvulnerable)
            {
                return false;
            }

            bool inBlockableState =
                _state == FighterState.Idle ||
                _state == FighterState.WalkBackward ||
                _state == FighterState.Crouch ||
                _state == FighterState.Block;

            if (!inBlockableState)
            {
                return false;
            }

            FighterInputState input = _inputHandler != null ? _inputHandler.CurrentInput : default;
            bool holdingBack = input.MoveX * _facingSign < -0.2f;
            bool holdingGuard = input.HoldBlock || holdingBack;

            if (!holdingGuard)
            {
                return false;
            }

            // Attacker striking from directly behind cannot be blocked unless using dedicated guard
            if (attackerFacingSign == _facingSign && !input.HoldBlock && _state != FighterState.Block)
            {
                return false;
            }

            if (move.hitHeight == HitHeight.Unblockable)
            {
                return false;
            }

            bool crouchGuard = IsCrouching || input.MoveY < -0.5f;
            if (move.hitHeight == HitHeight.Overhead)
            {
                return !crouchGuard;
            }

            return crouchGuard
                ? (move.hitHeight == HitHeight.Low || move.hitHeight == HitHeight.Mid)
                : (move.hitHeight == HitHeight.High || move.hitHeight == HitHeight.Mid);
        }

        public bool CanTechIncomingThrow(int currentTick)
        {
            if (!_isGrounded || IsInvulnerable || _inputHandler == null)
            {
                return false;
            }

            bool inTechableState =
                _state == FighterState.Idle ||
                _state == FighterState.WalkForward ||
                _state == FighterState.WalkBackward ||
                _state == FighterState.Crouch ||
                (_state == FighterState.Block && _stateTotalTicks == 0) ||
                (_state == FighterState.Attack && _currentAttack == AttackType.Throw);

            return inTechableState && _inputHandler.HasRecentThrowTechInput(currentTick, 7);
        }

        public void ApplyThrowTechPushback(int opponentFacingSign)
        {
            float pushDir = opponentFacingSign >= 0 ? 1f : -1f;
            const float pushSpeed = 4.2f;
            const int techStunTicks = 10;

            _blockFromCrouch = false;
            _receivedComboHits = 0;
            _receivedComboDamage = 0;
            _simVelocity.x = pushDir * pushSpeed;
            _knockbackDecelPerSec = pushSpeed / (techStunTicks * FightSimulation.TickDeltaTime);
            _stateTotalTicks = techStunTicks;

            bool wasInHit = _state == FighterState.Hit;
            EnterState(FighterState.Hit, forceResetTicks: true);
            if (wasInHit && fighterAnimator != null && fighterAnimator.runtimeAnimatorController != null)
            {
                fighterAnimator.Play(HitReactStateHash, 0, 0f);
            }
        }

        public void AddSuperGauge(int amount)
        {
            if (amount == 0)
            {
                return;
            }
            _superGauge = Mathf.Clamp(_superGauge + amount, 0, MaxSuperGauge);
        }

        public void SetSuperGauge(int value)
        {
            _superGauge = Mathf.Clamp(value, 0, MaxSuperGauge);
        }

        public void ResetSuperGauge()
        {
            _superGauge = 0;
        }

        public int ConsumeStartSuperBonus()
        {
            if (!_grantedStartSuperOnCurrentMove)
            {
                return 0;
            }
            _grantedStartSuperOnCurrentMove = false;
            return SuperGainWhiff;
        }

        public static float ComputeComboScalingMultiplier(int hitIndex)
        {
            if (hitIndex <= 1) return 1.00f;
            if (hitIndex == 2) return 0.85f;
            if (hitIndex == 3) return 0.72f;
            return Mathf.Max(0.50f, 0.72f - (hitIndex - 3) * 0.12f);
        }

        /// <summary>
        /// Backwards-compatible overload for callers not inspecting combo tracking outputs.
        /// </summary>
        public void ApplyIncomingAttack(
            MoveData move,
            int attackerFacingSign,
            out HitOutcomeType outcome,
            out int damageDealt,
            out float knockbackVelocity)
        {
            ApplyIncomingAttack(
                move,
                attackerFacingSign,
                out outcome,
                out damageDealt,
                out knockbackVelocity,
                out _,
                out _);
        }

        /// <summary>
        /// Resolves damage (with combo scaling), stun, knockback (scaled by weight), block, or knockdown from an incoming strike.
        /// </summary>
        public void ApplyIncomingAttack(
            MoveData move,
            int attackerFacingSign,
            out HitOutcomeType outcome,
            out int damageDealt,
            out float knockbackVelocity,
            out int comboCount,
            out int comboDamage)
        {
            float pushDir = attackerFacingSign >= 0 ? 1f : -1f;
            float kbMult = fighterData != null ? fighterData.KnockbackMultiplier : 1f;

            if (CanBlockMove(move, attackerFacingSign))
            {
                _receivedComboHits = 0;
                _receivedComboDamage = 0;

                int chip = Mathf.Min(move.chipDamage, Mathf.Max(0, _currentHealth - 1));
                _currentHealth -= chip;
                damageDealt = chip;

                FighterInputState input = _inputHandler != null ? _inputHandler.CurrentInput : default;
                _blockFromCrouch = IsCrouching || input.MoveY < -0.5f;

                float kbSpeed = move.knockback * BaseKnockbackVelocityScale * move.blockKnockbackFactor * kbMult;
                knockbackVelocity = pushDir * kbSpeed;
                _simVelocity.x = knockbackVelocity;

                int stunTicks = Mathf.Max(1, move.blockstunTicks);
                _knockbackDecelPerSec = Mathf.Abs(kbSpeed) / (stunTicks * FightSimulation.TickDeltaTime);
                _stateTotalTicks = stunTicks;

                bool wasBlocking = _state == FighterState.Block;
                EnterState(FighterState.Block, forceResetTicks: true);
                if (wasBlocking && fighterAnimator != null && fighterAnimator.runtimeAnimatorController != null)
                {
                    fighterAnimator.Play(BlockStateHash, 0, 0f);
                }

                outcome = HitOutcomeType.Blocked;
                comboCount = 0;
                comboDamage = 0;
                return;
            }

            if (_state == FighterState.Hit || _state == FighterState.KnockDown)
            {
                _receivedComboHits++;
            }
            else
            {
                _receivedComboHits = 1;
                _receivedComboDamage = 0;
            }

            float scaling = ComputeComboScalingMultiplier(_receivedComboHits);
            int scaledDamage = move.damage <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(move.damage * scaling));

            int prevHealth = _currentHealth;
            _currentHealth = Mathf.Max(0, _currentHealth - scaledDamage);
            damageDealt = prevHealth - _currentHealth;
            _receivedComboDamage += damageDealt;
            comboCount = _receivedComboHits;
            comboDamage = _receivedComboDamage;
            _blockFromCrouch = false;
            _ticksSinceLastHit = 0;

            float hitKbSpeed = move.knockback * BaseKnockbackVelocityScale * kbMult;
            knockbackVelocity = pushDir * hitKbSpeed;
            _simVelocity.x = knockbackVelocity;

            int hitStun = Mathf.Max(1, move.hitstunTicks);
            _knockbackDecelPerSec = Mathf.Abs(hitKbSpeed) / (hitStun * FightSimulation.TickDeltaTime);

            bool shouldKnockDown = _currentHealth <= 0 || move.causesKnockdown;
            if (shouldKnockDown)
            {
                _stateTotalTicks = KnockDownDurationTicks;
                EnterState(FighterState.KnockDown, forceResetTicks: true);
                outcome = HitOutcomeType.KnockDown;
            }
            else
            {
                _stateTotalTicks = hitStun;
                bool wasInHit = _state == FighterState.Hit;
                EnterState(FighterState.Hit, forceResetTicks: true);
                if (wasInHit && fighterAnimator != null && fighterAnimator.runtimeAnimatorController != null)
                {
                    fighterAnimator.Play(HitReactStateHash, 0, 0f);
                }
                outcome = HitOutcomeType.Hit;
            }
        }

        public void StepTick(int tick, float dt)
        {
            _prevSimPos = _currSimPos;
            AdvanceVisualTurn(dt);
            _inputHandler?.SyncSimulationTick(tick);
            FighterInputState input = _inputHandler != null ? _inputHandler.CurrentInput : default;

            if (_wakeupInvincibilityTicks > 0)
            {
                _wakeupInvincibilityTicks--;
            }

            if (_stageInteractCooldownTicks > 0)
            {
                _stageInteractCooldownTicks--;
            }

            _ticksSinceLastHit++;
            _stateTicks++;

            // Terminal round-end states stay posed until MatchManager resets the round
            if (_state == FighterState.Victory || _state == FighterState.Defeat)
            {
                ApplyHorizontalDecel(dt);
                ApplyGravityAndIntegrate(dt);
                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
                return;
            }

            // Locked timed states: Attack, Hit, KnockDown, GetUp, and active Blockstun
            if (_state == FighterState.Attack ||
                _state == FighterState.Hit ||
                _state == FighterState.KnockDown ||
                _state == FighterState.GetUp ||
                (_state == FighterState.Block && _stateTotalTicks > 0))
            {
                if (_state == FighterState.Attack)
                {
                    if (_pendingThrowProp != null)
                    {
                        if (_stateTicks < StageThrowReleaseTick)
                        {
                            _pendingThrowProp.UpdateGrabTransform(GetThrowHandWorldPosition());
                        }
                        else
                        {
                            StageThrowableProp toLaunch = _pendingThrowProp;
                            _pendingThrowProp = null;
                            toLaunch.LaunchProjectile(this, _facingSign);
                        }
                    }

                    if (_currentAttack == AttackType.JumpAttack)
                    {
                        // Preserve parabolic horizontal & vertical jump trajectory under gravity
                    }
                    else if (_currentMoveData != null && _currentMoveData.lungeSpeed > 0f && _stateTicks <= _currentMoveData.LastActiveTick)
                    {
                        _simVelocity.x = _facingSign * _currentMoveData.lungeSpeed;
                    }
                    else
                    {
                        _simVelocity.x = 0f;
                    }
                }
                else
                {
                    ApplyHorizontalDecel(dt);
                }

                ApplyGravityAndIntegrate(dt);

                if (_state == FighterState.Attack && _currentAttack == AttackType.JumpAttack)
                {
                    if (_isGrounded)
                    {
                        _hasUsedAirAttackThisJump = false;
                        _simVelocity.x = 0f;
                        _knockbackDecelPerSec = 0f;
                        EnterState(FighterState.Idle);
                        UpdateHurtboxDimensions();
                        StepAnimatorTick(dt);
                        return;
                    }

                    if (_stateTicks >= _stateTotalTicks)
                    {
                        EnterState(FighterState.Jump);
                        UpdateHurtboxDimensions();
                        StepAnimatorTick(dt);
                        return;
                    }
                }

                bool hitstopCleared = _simulation == null || _simulation.HitstopTicksRemaining <= 0;
                if (_state == FighterState.Attack &&
                    _inputsEnabled &&
                    _isGrounded &&
                    _hasRegisteredHit &&
                    hitstopCleared &&
                    _inputHandler != null &&
                    _inputHandler.ConsumeChainAttackInput(tick, _currentAttack, CanUseSpecial, out AttackType cancelAttack) &&
                    StartAttack(cancelAttack))
                {
                    UpdateHurtboxDimensions();
                    StepAnimatorTick(dt);
                    return;
                }

                if (_stateTicks >= _stateTotalTicks && _isGrounded)
                {
                    if (_state == FighterState.KnockDown)
                    {
                        if (_currentHealth > 0)
                        {
                            _simVelocity.x = 0f;
                            _stateTotalTicks = GetUpDurationTicks;
                            _wakeupInvincibilityTicks = GetUpDurationTicks + WakeupInvincibilityGraceTicks;
                            EnterState(FighterState.GetUp);
                        }
                        // At 0 HP stay lying in KnockDown until round transition
                    }
                    else if (_state == FighterState.Block)
                    {
                        _stateTotalTicks = 0;
                        _simVelocity.x = 0f;
                        if (!_inputsEnabled || !input.HoldBlock)
                        {
                            _blockFromCrouch = false;
                            EnterState(FighterState.Idle);
                        }
                    }
                    else
                    {
                        _simVelocity.x = 0f;
                        _knockbackDecelPerSec = 0f;
                        EnterState(FighterState.Idle);
                    }
                }

                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
                return;
            }

            // Timed Dash states (DashForward cancelable into Attack or Jump from tick 8+)
            if (_state == FighterState.DashForward || _state == FighterState.DashBackward)
            {
                bool isForward = _state == FighterState.DashForward;
                if (isForward && _inputsEnabled && _stateTicks >= 8)
                {
                    if (_inputHandler != null &&
                        _inputHandler.ConsumeAttackInput(tick, CanUseSpecial, isGrounded: true, isCrouchingInput: input.MoveY < -0.5f, out AttackType dashCancelAttack) &&
                        StartAttack(dashCancelAttack))
                    {
                        UpdateHurtboxDimensions();
                        StepAnimatorTick(dt);
                        return;
                    }

                    if (input.MoveY > 0.5f)
                    {
                        StartJump(input.MoveX, dt);
                        return;
                    }
                }

                int decelTicks = isForward ? 4 : 5;
                int remaining = _stateTotalTicks - _stateTicks;
                float decelFactor = remaining <= decelTicks
                    ? Mathf.Clamp(remaining / (float)(decelTicks + 1), 0.25f, 1f)
                    : 1f;
                float baseDashSpeed = isForward
                    ? (fighterData != null ? fighterData.EffectiveDashForwardSpeed : 8.2f)
                    : (fighterData != null ? fighterData.EffectiveDashBackwardSpeed : 7.0f);
                int moveDir = isForward ? _facingSign : -_facingSign;
                _simVelocity.x = moveDir * baseDashSpeed * decelFactor;

                ApplyGravityAndIntegrate(dt);

                if (_stateTicks >= _stateTotalTicks && _isGrounded)
                {
                    _simVelocity.x = 0f;
                    EnterState(FighterState.Idle);
                }

                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
                return;
            }

            // Airborne state
            if (!_isGrounded)
            {
                _state = FighterState.Jump;
                if (_inputsEnabled &&
                    !_hasUsedAirAttackThisJump &&
                    _inputHandler != null &&
                    _inputHandler.ConsumeAttackInput(tick, allowSpecial: false, isGrounded: false, isCrouchingInput: false, out AttackType airAttack) &&
                    StartAirAttack(airAttack))
                {
                    ApplyGravityAndIntegrate(dt);
                    if (_isGrounded)
                    {
                        _hasUsedAirAttackThisJump = false;
                        _simVelocity.x = 0f;
                        EnterState(FighterState.Idle);
                    }
                    UpdateHurtboxDimensions();
                    StepAnimatorTick(dt);
                    return;
                }

                ApplyGravityAndIntegrate(dt);
                if (_isGrounded)
                {
                    _hasUsedAirAttackThisJump = false;
                    _simVelocity.x = 0f;
                    EnterState(FighterState.Idle);
                }
                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
                return;
            }

            // Freeze player actions during RoundIntro / RoundOver / MatchOver
            if (!_inputsEnabled)
            {
                _simVelocity.x = 0f;
                _blockFromCrouch = false;
                EnterState(FighterState.Idle);
                ApplyGravityAndIntegrate(dt);
                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
                return;
            }

            // Grounded input consumption: Stage Interact > Attack > Jump > Dash > Block/Crouch > Walk > Idle
            if (_inputHandler != null &&
                CanAttemptStageInteract() &&
                _inputHandler.HasStageInteractCombo(tick) &&
                FindNearestReadyStageProp() != null &&
                _inputHandler.ConsumeStageInteractCombo(tick) &&
                TryUseNearestStageProp())
            {
                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
                return;
            }

            if (_inputHandler != null &&
                _inputHandler.ConsumeAttackInput(tick, CanUseSpecial, isGrounded: true, isCrouchingInput: input.MoveY < -0.5f, out AttackType attackType) &&
                StartAttack(attackType))
            {
                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
                return;
            }

            if (input.MoveY > 0.5f)
            {
                StartJump(input.MoveX, dt);
                return;
            }

            if (!input.HoldBlock &&
                _inputHandler != null &&
                _inputHandler.ConsumeBufferedDash(tick, _facingSign, out bool isForwardDash))
            {
                StartDash(isForwardDash, dt);
                return;
            }

            // Crouch (supports holding Down + Block/Back as crouch guard with low hurtbox)
            if (input.MoveY < -0.5f)
            {
                _simVelocity.x = 0f;
                _blockFromCrouch = false;
                EnterState(FighterState.Crouch);
                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
                return;
            }

            // Standing dedicated Block button (holding Back alone walks backward and guards on impact)
            if (input.HoldBlock)
            {
                _simVelocity.x = 0f;
                _stateTotalTicks = 0;
                _blockFromCrouch = false;
                EnterState(FighterState.Block);
                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
                return;
            }

            _blockFromCrouch = false;
            float relativeMove = input.MoveX * _facingSign;
            if (relativeMove > 0.2f)
            {
                float fwdSpeed = fighterData != null ? fighterData.EffectiveWalkForwardSpeed : 4.2f;
                _simVelocity.x = _facingSign * fwdSpeed;
                EnterState(FighterState.WalkForward);
            }
            else if (relativeMove < -0.2f)
            {
                float backSpeed = fighterData != null ? fighterData.EffectiveWalkBackwardSpeed : 3.2f;
                _simVelocity.x = -_facingSign * backSpeed;
                EnterState(FighterState.WalkBackward);
            }
            else
            {
                _simVelocity.x = 0f;
                EnterState(FighterState.Idle);
            }

            ApplyGravityAndIntegrate(dt);
            UpdateHurtboxDimensions();
            StepAnimatorTick(dt);
        }

        private void ApplyHorizontalDecel(float dt)
        {
            if (_knockbackDecelPerSec <= 0f || Mathf.Abs(_simVelocity.x) < 1e-4f)
            {
                _simVelocity.x = 0f;
                return;
            }

            _simVelocity.x = Mathf.MoveTowards(_simVelocity.x, 0f, _knockbackDecelPerSec * dt);
        }

        private void ApplyGravityAndIntegrate(float dt)
        {
            float groundY = _simulation != null ? _simulation.GroundY : 0f;
            float minX = _simulation != null ? _simulation.StageMinX : -8f;
            float maxX = _simulation != null ? _simulation.StageMaxX : 8f;

            if (!_isGrounded)
            {
                float gravity = fighterData != null ? fighterData.EffectiveGravity : 34f;
                _simVelocity.y -= gravity * dt;
            }

            _currSimPos += _simVelocity * dt;
            _currSimPos.x = Mathf.Clamp(_currSimPos.x, minX, maxX);

            if (_currSimPos.y <= groundY)
            {
                _currSimPos.y = groundY;
                _simVelocity.y = 0f;
                _isGrounded = true;
                _hasUsedAirAttackThisJump = false;
            }
        }

        public bool IsInStageInteractCooldown => _stageInteractCooldownTicks > 0;

        public bool CanAttemptStageInteract()
        {
            if (!_inputsEnabled || !_isGrounded || _currentHealth <= 0 || _stageInteractCooldownTicks > 0)
            {
                return false;
            }

            return _state == FighterState.Idle ||
                   _state == FighterState.WalkForward ||
                   _state == FighterState.WalkBackward ||
                   (_state == FighterState.Block && _stateTotalTicks == 0);
        }

        public StageThrowableProp FindNearestReadyStageProp()
        {
            return StageInteractableManager.FindNearestReadyThrowableInScene(this);
        }

        public StageThrowableProp GetInteractableStagePropInRange()
        {
            return CanAttemptStageInteract() ? FindNearestReadyStageProp() : null;
        }

        public Vector3 GetThrowHandWorldPosition(float fightPlaneZ = 0f)
        {
            float fullH = fighterData != null ? fighterData.heightMeters : FighterScaling.ReferenceHeightMeters;
            return new Vector3(_currSimPos.x + _facingSign * 0.45f, _currSimPos.y + fullH * 0.56f, fightPlaneZ);
        }

        /// <summary>
        /// Public entry point for grabbing and throwing the nearest ready StageThrowableProp
        /// (triggered by HoldBlock + LightPunch or optionally by CpuAiController).
        /// </summary>
        public bool TryUseNearestStageProp()
        {
            if (!CanAttemptStageInteract())
            {
                return false;
            }

            StageThrowableProp prop = FindNearestReadyStageProp();
            if (prop == null)
            {
                return false;
            }

            _simVelocity.x = 0f;
            _blockFromCrouch = false;
            _hasRegisteredHit = true;
            _currentAttack = AttackType.HeavyPunch;
            _currentMoveData = null;
            _stateTotalTicks = StageThrowTotalTicks;
            _stageInteractCooldownTicks = StageInteractCooldownTicks;
            _pendingThrowProp = prop;

            EnterState(FighterState.Attack, triggerAttack: true, forceResetTicks: true);
            prop.BeginGrab(this);
            return true;
        }

        private void StartJump(float moveX, float dt)
        {
            float jumpForce = fighterData != null ? fighterData.jumpForce : 12.5f;
            float walkSpeed = fighterData != null ? fighterData.EffectiveWalkForwardSpeed : 4.2f;
            _blockFromCrouch = false;
            _hasUsedAirAttackThisJump = false;
            _simVelocity.y = jumpForce;
            _simVelocity.x = moveX * walkSpeed;
            _isGrounded = false;
            EnterState(FighterState.Jump);
            ApplyGravityAndIntegrate(dt);
            UpdateHurtboxDimensions();
            StepAnimatorTick(dt);
        }

        private void StartDash(bool isForward, float dt)
        {
            _blockFromCrouch = false;
            _knockbackDecelPerSec = 0f;
            int ticks = isForward
                ? (fighterData != null ? Mathf.Max(1, fighterData.dashForwardTicks) : 14)
                : (fighterData != null ? Mathf.Max(1, fighterData.dashBackwardTicks) : 16);
            float speed = isForward
                ? (fighterData != null ? fighterData.EffectiveDashForwardSpeed : 8.2f)
                : (fighterData != null ? fighterData.EffectiveDashBackwardSpeed : 7.0f);

            _stateTotalTicks = ticks;
            _simVelocity.x = (isForward ? _facingSign : -_facingSign) * speed;
            EnterState(isForward ? FighterState.DashForward : FighterState.DashBackward, forceResetTicks: true);
            OnAnyDashStarted?.Invoke(this, isForward);
            ApplyGravityAndIntegrate(dt);
            UpdateHurtboxDimensions();
            StepAnimatorTick(dt);
        }

        private bool StartAirAttack(AttackType attackType)
        {
            if (attackType != AttackType.JumpAttack || _hasUsedAirAttackThisJump)
            {
                return false;
            }

            _hasUsedAirAttackThisJump = true;
            AddSuperGauge(SuperGainWhiff);
            _grantedStartSuperOnCurrentMove = true;
            _blockFromCrouch = false;
            _hasRegisteredHit = false;
            _currentAttack = AttackType.JumpAttack;
            _currentMoveData = fighterData != null ? fighterData.GetMoveData(AttackType.JumpAttack) : null;
            _stateTotalTicks = _currentMoveData != null ? _currentMoveData.TotalTicks : GetFallbackAttackTotalTicks(AttackType.JumpAttack);
            EnterState(FighterState.Attack, triggerAttack: true, forceResetTicks: true);
            return true;
        }

        private bool StartAttack(AttackType attackType)
        {
            if (attackType == AttackType.None || (attackType == AttackType.JumpAttack && _isGrounded))
            {
                return false;
            }

            if (attackType == AttackType.Special)
            {
                if (!CanUseSpecial)
                {
                    return false;
                }
                _superGauge = Mathf.Max(0, _superGauge - MaxSuperGauge);
                _grantedStartSuperOnCurrentMove = false;
            }
            else
            {
                AddSuperGauge(SuperGainWhiff);
                _grantedStartSuperOnCurrentMove = true;
            }

            _simVelocity.x = 0f;
            _blockFromCrouch = false;
            _hasRegisteredHit = false;
            _currentAttack = attackType;
            _currentMoveData = fighterData != null ? fighterData.GetMoveData(attackType) : null;
            _stateTotalTicks = _currentMoveData != null ? _currentMoveData.TotalTicks : GetFallbackAttackTotalTicks(attackType);
            EnterState(FighterState.Attack, triggerAttack: true, forceResetTicks: true);
            return true;
        }

        private void EnterState(FighterState newState, bool triggerAttack = false, bool forceResetTicks = false)
        {
            if (_state == newState && newState != FighterState.Attack && !forceResetTicks)
            {
                return;
            }
            _state = newState;
            _stateTicks = 0;

            if (ShouldSnapVisualTurnForState(newState))
            {
                SnapVisualYaw();
            }
            else if (_isTurnAnimActive && newState != _turnStartState)
            {
                _isTurnAnimActive = false;
            }

            if (newState == FighterState.Idle || newState == FighterState.Block || newState == FighterState.GetUp)
            {
                _receivedComboHits = 0;
                _receivedComboDamage = 0;
            }

            if (newState != FighterState.Attack)
            {
                if (_pendingThrowProp != null)
                {
                    _pendingThrowProp.CancelGrabAndDrop();
                    _pendingThrowProp = null;
                }
                _currentAttack = AttackType.None;
                _currentMoveData = null;
                _hasRegisteredHit = false;
                _grantedStartSuperOnCurrentMove = false;
            }
            else if (triggerAttack)
            {
                if (fighterAnimator != null)
                {
                    fighterAnimator.SetInteger(AttackTypeParamHash, (int)_currentAttack);
                    fighterAnimator.SetTrigger(AttackTriggerParamHash);
                }
                OnAnyAttackStarted?.Invoke(this, _currentAttack);
            }
        }

        private static bool ShouldSnapVisualTurnForState(FighterState state)
        {
            return state == FighterState.Attack ||
                   state == FighterState.Hit ||
                   state == FighterState.Block ||
                   state == FighterState.KnockDown ||
                   state == FighterState.GetUp ||
                   state == FighterState.Victory ||
                   state == FighterState.Defeat ||
                   state == FighterState.DashForward ||
                   state == FighterState.DashBackward;
        }

        private static float GetTargetYawForFacing(int facingSign)
        {
            return facingSign >= 0 ? 90f : 270f;
        }

        private void SnapVisualYaw()
        {
            float targetYaw = GetTargetYawForFacing(_facingSign);
            _visualYaw = targetYaw;
            _prevVisualYaw = targetYaw;
            _turnStartYaw = targetYaw;
            _turnTargetYaw = targetYaw;
            _turnElapsed = TurnDurationSeconds;
            _isTurnAnimActive = false;
            ApplyVisualYawToRoot(_visualYaw);
        }

        private void AdvanceVisualTurn(float dt)
        {
            _prevVisualYaw = _visualYaw;
            if (_turnElapsed < TurnDurationSeconds)
            {
                _turnElapsed = Mathf.Min(TurnDurationSeconds, _turnElapsed + dt);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_turnElapsed / TurnDurationSeconds));
                // Interpolating linearly in [90, 270] always passes through 180 deg (chest/face toward camera), never 0 deg (back).
                _visualYaw = Mathf.Lerp(_turnStartYaw, _turnTargetYaw, t);
                if (_turnElapsed >= TurnDurationSeconds)
                {
                    _isTurnAnimActive = false;
                }
            }
            else
            {
                _visualYaw = _turnTargetYaw;
                _isTurnAnimActive = false;
            }
            ApplyVisualYawToRoot(_visualYaw);
        }

        private void ApplyVisualYawToRoot(float yaw)
        {
            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private void StepAnimatorTick(float dt)
        {
            if (fighterAnimator == null || fighterAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            fighterAnimator.SetInteger(StateParamHash, (int)_state);
            fighterAnimator.SetInteger(AttackTypeParamHash, (int)_currentAttack);
            fighterAnimator.SetBool(IsGroundedParamHash, _isGrounded);
            fighterAnimator.SetBool(IsTurningParamHash, _isTurnAnimActive);
            fighterAnimator.Update(dt);
        }

        private static int GetFallbackAttackTotalTicks(AttackType attack)
        {
            return attack switch
            {
                AttackType.LightPunch => 16,
                AttackType.HeavyPunch => 27,
                AttackType.LightKick => 19,
                AttackType.HeavyKick => 33,
                AttackType.Special => 28,
                AttackType.CrouchKick => 28,
                AttackType.JumpAttack => 18,
                AttackType.Throw => 22,
                _ => 16
            };
        }

        private void UpdateHurtboxDimensions()
        {
            if (hurtboxCollider == null || fighterData == null)
            {
                return;
            }

            float targetHeight = IsCrouching
                ? _baseHurtboxHeight * fighterData.crouchHeightRatio
                : _baseHurtboxHeight;
            hurtboxCollider.height = targetHeight;
            hurtboxCollider.center = new Vector3(0f, targetHeight * 0.5f, 0f);

            if (visualRoot != null)
            {
                float modelScale = fighterData.VisualScale;
                float yScale = (squashVisualOnCrouch && IsCrouching)
                    ? modelScale * fighterData.crouchHeightRatio
                    : modelScale;
                visualRoot.localScale = new Vector3(modelScale, yScale, modelScale);
            }
        }

        private void LateUpdate()
        {
            float alpha = _simulation != null ? _simulation.InterpolationAlpha : 1f;
            Vector2 interp = Vector2.Lerp(_prevSimPos, _currSimPos, alpha);
            transform.position = new Vector3(interp.x, interp.y, 0f);

            if (visualRoot != null)
            {
                float yaw = Mathf.Lerp(_prevVisualYaw, _visualYaw, alpha);
                ApplyVisualYawToRoot(yaw);
            }
        }
    }
}
