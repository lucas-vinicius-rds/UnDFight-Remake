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
        public const int KnockDownDurationTicks = 45;
        public const int GetUpDurationTicks = 40;
        public const int WakeupInvincibilityGraceTicks = 6;
        public const float BaseKnockbackVelocityScale = 2.0f;

        [Header("Data & References")]
        [SerializeField] private FighterData fighterData;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private CapsuleCollider hurtboxCollider;
        [SerializeField] private Animator fighterAnimator;
        [SerializeField] private bool squashVisualOnCrouch = false;

        private static readonly int StateParamHash = Animator.StringToHash("State");
        private static readonly int AttackTypeParamHash = Animator.StringToHash("AttackType");
        private static readonly int AttackTriggerParamHash = Animator.StringToHash("AttackTrigger");
        private static readonly int IsGroundedParamHash = Animator.StringToHash("IsGrounded");
        private static readonly int HitReactStateHash = Animator.StringToHash("HitReact");
        private static readonly int BlockStateHash = Animator.StringToHash("Block");
        private static readonly int IdleStateHash = Animator.StringToHash("Idle");

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
        private float _knockbackDecelPerSec;
        private float _baseHurtboxHeight = 1.81f;
        private float _baseHurtboxCenterY = 0.905f;

        public FighterData Data => fighterData;
        public FighterState State => _state;
        public AttackType CurrentAttack => _currentAttack;
        public MoveData CurrentMoveData => _currentMoveData;
        public int StateTicks => _stateTicks;
        public int StateTotalTicks => _stateTotalTicks;
        public Vector2 SimPosition => _currSimPos;
        public Vector2 SimVelocity => _simVelocity;
        public int FacingSign => _facingSign;
        public bool IsGrounded => _isGrounded;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => fighterData != null ? fighterData.maxHealth : 1000;
        public bool HasRegisteredHit => _hasRegisteredHit;
        public bool InputsEnabled => _inputsEnabled;
        public int WakeupInvincibilityTicks => _wakeupInvincibilityTicks;
        public bool IsCrouching => _state == FighterState.Crouch || (_state == FighterState.Block && _blockFromCrouch);
        public bool IsInvulnerable =>
            _wakeupInvincibilityTicks > 0 ||
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
            SetupAnimatorForFixedTick();
            ApplyFighterDataScaling();
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
            _knockbackDecelPerSec = 0f;
            SetupAnimatorForFixedTick();
            ApplyFighterDataScaling();
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
            _knockbackDecelPerSec = 0f;
            _inputHandler?.ClearBufferedInputs();

            transform.position = new Vector3(_currSimPos.x, _currSimPos.y, 0f);
            UpdateHurtboxDimensions();

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
        }

        private void ApplyFighterDataScaling()
        {
            if (fighterData == null)
            {
                return;
            }

            float modelScale = fighterData.ModelScale;
            if (visualRoot != null)
            {
                float signX = _facingSign >= 0 ? 1f : -1f;
                visualRoot.localScale = new Vector3(modelScale * signX, modelScale, modelScale);
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

        public void SetFacing(int sign)
        {
            _facingSign = sign >= 0 ? 1 : -1;
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

            bool crouchGuard = IsCrouching || input.MoveY < -0.5f;
            return crouchGuard
                ? (move.hitHeight == HitHeight.Low || move.hitHeight == HitHeight.Mid)
                : (move.hitHeight == HitHeight.High || move.hitHeight == HitHeight.Mid);
        }

        /// <summary>
        /// Resolves damage, stun, knockback (scaled by weight), block, or knockdown from an incoming strike.
        /// </summary>
        public void ApplyIncomingAttack(
            MoveData move,
            int attackerFacingSign,
            out HitOutcomeType outcome,
            out int damageDealt,
            out float knockbackVelocity)
        {
            float pushDir = attackerFacingSign >= 0 ? 1f : -1f;
            float kbMult = fighterData != null ? fighterData.KnockbackMultiplier : 1f;

            if (CanBlockMove(move, attackerFacingSign))
            {
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
                return;
            }

            int prevHealth = _currentHealth;
            _currentHealth = Mathf.Max(0, _currentHealth - move.damage);
            damageDealt = prevHealth - _currentHealth;
            _blockFromCrouch = false;

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
            _inputHandler?.SyncSimulationTick(tick);
            FighterInputState input = _inputHandler != null ? _inputHandler.CurrentInput : default;

            if (_wakeupInvincibilityTicks > 0)
            {
                _wakeupInvincibilityTicks--;
            }

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
                if (_state != FighterState.Attack)
                {
                    ApplyHorizontalDecel(dt);
                }

                ApplyGravityAndIntegrate(dt);

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

            // Airborne state
            if (!_isGrounded)
            {
                _state = FighterState.Jump;
                ApplyGravityAndIntegrate(dt);
                if (_isGrounded)
                {
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

            // Grounded input consumption: Attack > Jump > Block/Crouch > Walk > Idle
            if (_inputHandler != null && _inputHandler.ConsumeAttackInput(tick, out AttackType attackType))
            {
                _simVelocity.x = 0f;
                _blockFromCrouch = false;
                _hasRegisteredHit = false;
                _currentAttack = attackType;
                _currentMoveData = fighterData != null ? fighterData.GetMoveData(attackType) : null;
                _stateTotalTicks = _currentMoveData != null ? _currentMoveData.TotalTicks : GetFallbackAttackTotalTicks(attackType);
                EnterState(FighterState.Attack, triggerAttack: true);
                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
                return;
            }

            if (input.MoveY > 0.5f)
            {
                float jumpForce = fighterData != null ? fighterData.jumpForce : 12.5f;
                float walkSpeed = fighterData != null ? fighterData.EffectiveWalkForwardSpeed : 4.2f;
                _blockFromCrouch = false;
                _simVelocity.y = jumpForce;
                _simVelocity.x = input.MoveX * walkSpeed;
                _isGrounded = false;
                EnterState(FighterState.Jump);
                ApplyGravityAndIntegrate(dt);
                UpdateHurtboxDimensions();
                StepAnimatorTick(dt);
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
            }
        }

        private void EnterState(FighterState newState, bool triggerAttack = false, bool forceResetTicks = false)
        {
            if (_state == newState && newState != FighterState.Attack && !forceResetTicks)
            {
                return;
            }
            _state = newState;
            _stateTicks = 0;
            if (newState != FighterState.Attack)
            {
                _currentAttack = AttackType.None;
                _currentMoveData = null;
                _hasRegisteredHit = false;
            }
            else if (triggerAttack && fighterAnimator != null)
            {
                fighterAnimator.SetInteger(AttackTypeParamHash, (int)_currentAttack);
                fighterAnimator.SetTrigger(AttackTriggerParamHash);
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
                float modelScale = fighterData.ModelScale;
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
                float targetYaw = _facingSign >= 0 ? 90f : -90f;
                visualRoot.localRotation = Quaternion.Euler(0f, targetYaw, 0f);
            }
        }
    }
}
