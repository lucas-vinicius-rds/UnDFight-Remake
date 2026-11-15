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
        [Header("Data & References")]
        [SerializeField] private FighterData fighterData;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private CapsuleCollider hurtboxCollider;

        private FightSimulation _simulation;
        private PlayerInputHandler _inputHandler;

        private Vector2 _prevSimPos;
        private Vector2 _currSimPos;
        private Vector2 _simVelocity;

        private int _facingSign = 1;
        private bool _isGrounded = true;
        private FighterState _state = FighterState.Idle;
        private AttackType _currentAttack = AttackType.None;
        private int _stateTicks;
        private int _stateTotalTicks;
        private int _currentHealth;
        private float _baseHurtboxHeight = 1.81f;
        private float _baseHurtboxCenterY = 0.905f;

        public FighterData Data => fighterData;
        public FighterState State => _state;
        public AttackType CurrentAttack => _currentAttack;
        public int StateTicks => _stateTicks;
        public Vector2 SimPosition => _currSimPos;
        public Vector2 SimVelocity => _simVelocity;
        public int FacingSign => _facingSign;
        public bool IsGrounded => _isGrounded;
        public int CurrentHealth => _currentHealth;
        public PlayerInputHandler InputHandler => _inputHandler;

        public float PushboxRadius => fighterData != null ? fighterData.pushboxRadius : 0.36f;
        public float CurrentHurtboxHeight
        {
            get
            {
                float fullH = fighterData != null ? fighterData.heightMeters : FighterScaling.ReferenceHeightMeters;
                return _state == FighterState.Crouch ? fullH * (fighterData != null ? fighterData.crouchHeightRatio : 0.62f) : fullH;
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
            ApplyFighterDataScaling();
        }

        public void SetFighterData(FighterData data)
        {
            fighterData = data;
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
            _currentHealth = fighterData != null ? fighterData.maxHealth : 1000;
            ApplyFighterDataScaling();
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

        public void StepTick(int tick, float dt)
        {
            _prevSimPos = _currSimPos;
            _inputHandler.SyncSimulationTick(tick);
            FighterInputState input = _inputHandler.CurrentInput;

            _stateTicks++;

            // Handle timed skeleton states (Attack, Hit, KnockDown)
            if (_state == FighterState.Attack || _state == FighterState.Hit || _state == FighterState.KnockDown)
            {
                ApplyGravityAndIntegrate(dt);
                if (_stateTicks >= _stateTotalTicks && _isGrounded)
                {
                    EnterState(FighterState.Idle);
                }
                UpdateHurtboxDimensions();
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
                return;
            }

            // Grounded input consumption: Attack > Jump > Block > Crouch > Walk > Idle
            if (_inputHandler.ConsumeAttackInput(tick, out AttackType attackType))
            {
                _simVelocity.x = 0f;
                _currentAttack = attackType;
                _stateTotalTicks = GetAttackTotalTicks(attackType);
                EnterState(FighterState.Attack);
                UpdateHurtboxDimensions();
                return;
            }

            if (input.MoveY > 0.5f)
            {
                float jumpForce = fighterData != null ? fighterData.jumpForce : 12.5f;
                float walkSpeed = fighterData != null ? fighterData.EffectiveWalkForwardSpeed : 4.2f;
                _simVelocity.y = jumpForce;
                _simVelocity.x = input.MoveX * walkSpeed;
                _isGrounded = false;
                EnterState(FighterState.Jump);
                ApplyGravityAndIntegrate(dt);
                UpdateHurtboxDimensions();
                return;
            }

            bool holdingBack = input.MoveX * _facingSign < -0.2f;
            if (input.HoldBlock || holdingBack)
            {
                // Holding back acts as WalkBackward when opponent isn't attacking (Block button always forces Block;
                // holding Back also enters Block when pressed with Block or stays WalkBackward -> Block if stationary?
                // Prompt: "Bloquear = segurar para trás ou botão de guarda." Let's support Block on HoldBlock OR Back+Hold,
                // Wait: if holding Back always blocks, how do you WalkBackward? If Block button -> Block; if holding Back ->
                // WalkBackward while simultaneously guarding, OR let's make Block when HoldBlock OR Back while stationary?
                // Wait: if holding Back enters WalkBackward (which is the standard SF back-walk + high guard!), let's check:
                // if input.HoldBlock -> FighterState.Block; if holdingBack -> FighterState.WalkBackward (with IsGuarding = true)!
                // Wait: the prompt lists states: Idle, WalkForward, WalkBackward, Jump, Crouch, Block, Attack, Hit, KnockDown,
                // and says: "Bloquear = segurar para trás ou botão de guarda."
                // So if HoldBlock is pressed -> FighterState.Block; if holdingBack without forward -> WalkBackward (which also counts as guarding),
                // OR if holdingBack + HoldBlock -> Block. Let's expose IsBlocking => _state == FighterState.Block || _state == FighterState.WalkBackward!
                if (input.HoldBlock)
                {
                    _simVelocity.x = 0f;
                    EnterState(FighterState.Block);
                    UpdateHurtboxDimensions();
                    return;
                }
            }

            if (input.MoveY < -0.5f)
            {
                _simVelocity.x = 0f;
                EnterState(FighterState.Crouch);
                UpdateHurtboxDimensions();
                return;
            }

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

        private void EnterState(FighterState newState)
        {
            if (_state == newState && newState != FighterState.Attack)
            {
                return;
            }
            _state = newState;
            _stateTicks = 0;
            if (newState != FighterState.Attack)
            {
                _currentAttack = AttackType.None;
            }
        }

        private static int GetAttackTotalTicks(AttackType attack)
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

            float targetHeight = _state == FighterState.Crouch
                ? _baseHurtboxHeight * fighterData.crouchHeightRatio
                : _baseHurtboxHeight;
            hurtboxCollider.height = targetHeight;
            hurtboxCollider.center = new Vector3(0f, targetHeight * 0.5f, 0f);

            if (visualRoot != null)
            {
                float modelScale = fighterData.ModelScale;
                float yScale = _state == FighterState.Crouch ? modelScale * fighterData.crouchHeightRatio : modelScale;
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
                // Rotate capsule/model to face opponent (+X when FacingSign=1, -X when FacingSign=-1)
                float targetYaw = _facingSign >= 0 ? 90f : -90f;
                visualRoot.localRotation = Quaternion.Euler(0f, targetYaw, 0f);
            }
        }
    }
}
