using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnDFight
{
    /// <summary>
    /// Reads New Input System (Keyboard with remappable strike/block keys + Gamepad with hot-plug)
    /// and maintains a 6-tick input buffer consumed at the fixed 60 Hz simulation tick.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        public const int DefaultBufferWindowTicks = 6;
        public const int DashDoubleTapWindowTicks = 12;

        [Header("Player Configuration")]
        [SerializeField, Range(0, 1)] private int playerIndex = 0;
        [SerializeField] private InputAssignmentMode assignmentMode = InputAssignmentMode.Auto;
        [SerializeField, Min(1)] private int bufferWindowTicks = DefaultBufferWindowTicks;
        [SerializeField, Range(0.1f, 0.9f)] private float stickDeadzone = 0.25f;

        private readonly Dictionary<FighterInputButton, int> _bufferedButtonExpireTick = new Dictionary<FighterInputButton, int>();
        private FighterInputState _currentInput;
        private Gamepad _activeGamepad;
        private int _currentSimTick;
        private bool _useSimulatedInput;
        private int _prevTapSign;
        private int _lastTapDir;
        private int _lastTapTick = -999;
        private int _lastDashEvalTick = -1;
        private int _bufferedDashDirection;
        private int _bufferedDashExpireTick = -1;
        private int _lastThrowAttemptTick = -999;

        private Key _lightPunchKey;
        private Key _heavyPunchKey;
        private Key _lightKickKey;
        private Key _heavyKickKey;
        private Key _blockKey;
        private Key _specialKey;

        public int PlayerIndex => playerIndex;
        public InputAssignmentMode AssignmentMode => assignmentMode;
        public Gamepad ActiveGamepad => _activeGamepad;
        public FighterInputState CurrentInput => _currentInput;

        public string ActiveDeviceLabel
        {
            get
            {
                string kb = playerIndex == 0
                    ? $"KB(WASD+{_lightPunchKey}/{_heavyPunchKey}/{_lightKickKey}/{_heavyKickKey}/{_blockKey}/{_specialKey})"
                    : $"KB(Arrows+{_lightPunchKey}/{_heavyPunchKey}/{_lightKickKey}/{_heavyKickKey}/{_blockKey}/{_specialKey})";
                return _activeGamepad != null ? $"{kb} + {_activeGamepad.displayName}" : kb;
            }
        }

        private void Awake()
        {
            ApplySettings(SettingsManager.Current, overrideDeviceMode: false);
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += HandleDeviceChange;
            ApplySettings(SettingsManager.Current, overrideDeviceMode: true);
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
        }

        public void Configure(int index)
        {
            playerIndex = Mathf.Clamp(index, 0, 1);
            ApplySettings(SettingsManager.Current, overrideDeviceMode: true);
        }

        public void Configure(int index, InputAssignmentMode mode)
        {
            playerIndex = Mathf.Clamp(index, 0, 1);
            assignmentMode = mode;
            ApplySettings(SettingsManager.Current, overrideDeviceMode: false);
        }

        public void SetAssignmentMode(InputAssignmentMode mode)
        {
            assignmentMode = mode;
            ResolveAssignedGamepad();
        }

        public void ApplySettings(GameSettingsData settings, bool overrideDeviceMode = true)
        {
            if (settings == null)
            {
                return;
            }

            if (overrideDeviceMode)
            {
                assignmentMode = settings.GetDeviceModeForPlayer(playerIndex);
            }

            PlayerKeyBindings bindings = settings.GetBindingsForPlayer(playerIndex);
            _lightPunchKey = bindings.GetKey(FighterInputButton.LightPunch, playerIndex);
            _heavyPunchKey = bindings.GetKey(FighterInputButton.HeavyPunch, playerIndex);
            _lightKickKey = bindings.GetKey(FighterInputButton.LightKick, playerIndex);
            _heavyKickKey = bindings.GetKey(FighterInputButton.HeavyKick, playerIndex);
            _blockKey = bindings.GetKey(FighterInputButton.Block, playerIndex);
            _specialKey = bindings.GetKey(FighterInputButton.Special, playerIndex);

            ResolveAssignedGamepad();
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad)
            {
                ResolveAssignedGamepad();
            }
        }

        public void ResolveAssignedGamepad()
        {
            var gamepads = Gamepad.all;
            switch (assignmentMode)
            {
                case InputAssignmentMode.KeyboardOnly:
                    _activeGamepad = null;
                    break;
                case InputAssignmentMode.Gamepad0:
                    _activeGamepad = gamepads.Count > 0 ? gamepads[0] : null;
                    break;
                case InputAssignmentMode.Gamepad1:
                    _activeGamepad = gamepads.Count > 1 ? gamepads[1] : null;
                    break;
                case InputAssignmentMode.Auto:
                default:
                    _activeGamepad = playerIndex < gamepads.Count ? gamepads[playerIndex] : null;
                    break;
            }
        }

        private void Update()
        {
            if (!_useSimulatedInput)
            {
                SampleDevices();
            }
        }

        public void SyncSimulationTick(int currentTick)
        {
            _currentSimTick = currentTick;
            if (currentTick > 0 && currentTick != _lastDashEvalTick)
            {
                _lastDashEvalTick = currentTick;
                EvaluateHorizontalTap(_currentInput.MoveX, currentTick);
            }
        }

        private void EvaluateHorizontalTap(float moveX, int currentTick)
        {
            int curSign = moveX > 0.2f ? 1 : (moveX < -0.2f ? -1 : 0);
            if (curSign != 0 && _prevTapSign == 0)
            {
                if (_lastTapDir == curSign && (currentTick - _lastTapTick) <= DashDoubleTapWindowTicks)
                {
                    BufferDashDirection(curSign, currentTick);
                    _lastTapDir = 0;
                }
                else
                {
                    _lastTapDir = curSign;
                    _lastTapTick = currentTick;
                }
            }
            _prevTapSign = curSign;
        }

        /// <summary>
        /// Overrides live device polling with deterministic input state for Edit Mode or scripted verification.
        /// </summary>
        public void SetSimulatedInput(FighterInputState state, bool enabled = true)
        {
            _useSimulatedInput = enabled;
            _currentInput = state;
            _currentInput.LastTickSampled = _currentSimTick;
        }

        public void ClearSimulatedInput()
        {
            _useSimulatedInput = false;
            _currentInput = default;
            ClearBufferedInputs();
        }

        public void ClearBufferedInputs()
        {
            _bufferedButtonExpireTick.Clear();
            _prevTapSign = 0;
            _lastTapDir = 0;
            _lastTapTick = -999;
            _lastDashEvalTick = -1;
            _bufferedDashDirection = 0;
            _bufferedDashExpireTick = -1;
            _lastThrowAttemptTick = -999;
        }

        private void SampleDevices()
        {
            float moveX = 0f;
            float moveY = 0f;
            bool holdBlock = false;
            bool holdSpecial = false;

            var kb = Keyboard.current;
            if (kb != null && assignmentMode != InputAssignmentMode.Gamepad0 && assignmentMode != InputAssignmentMode.Gamepad1)
            {
                if (playerIndex == 0)
                {
                    if (kb.aKey.isPressed) moveX -= 1f;
                    if (kb.dKey.isPressed) moveX += 1f;
                    if (kb.wKey.isPressed) moveY += 1f;
                    if (kb.sKey.isPressed) moveY -= 1f;
                }
                else
                {
                    if (kb.leftArrowKey.isPressed) moveX -= 1f;
                    if (kb.rightArrowKey.isPressed) moveX += 1f;
                    if (kb.upArrowKey.isPressed) moveY += 1f;
                    if (kb.downArrowKey.isPressed) moveY -= 1f;
                }

                if (IsKeyPressed(kb, _blockKey)) holdBlock = true;
                if (IsKeyPressed(kb, _specialKey)) holdSpecial = true;
                if (WasKeyPressedThisFrame(kb, _lightPunchKey)) BufferButton(FighterInputButton.LightPunch);
                if (WasKeyPressedThisFrame(kb, _heavyPunchKey)) BufferButton(FighterInputButton.HeavyPunch);
                if (WasKeyPressedThisFrame(kb, _lightKickKey)) BufferButton(FighterInputButton.LightKick);
                if (WasKeyPressedThisFrame(kb, _heavyKickKey)) BufferButton(FighterInputButton.HeavyKick);
                if (WasKeyPressedThisFrame(kb, _blockKey)) BufferButton(FighterInputButton.Block);
                if (WasKeyPressedThisFrame(kb, _specialKey)) BufferButton(FighterInputButton.Special);
            }

            if (_activeGamepad != null && _activeGamepad.added)
            {
                Vector2 stick = _activeGamepad.leftStick.ReadValue();
                Vector2 dpad = _activeGamepad.dpad.ReadValue();

                float gx = Mathf.Abs(dpad.x) > 0.1f ? dpad.x : (Mathf.Abs(stick.x) >= stickDeadzone ? stick.x : 0f);
                float gy = Mathf.Abs(dpad.y) > 0.1f ? dpad.y : (Mathf.Abs(stick.y) >= stickDeadzone ? stick.y : 0f);

                if (Mathf.Abs(gx) > Mathf.Abs(moveX)) moveX = Mathf.Sign(gx);
                if (Mathf.Abs(gy) > Mathf.Abs(moveY)) moveY = Mathf.Sign(gy);

                if (_activeGamepad.rightShoulder.isPressed || _activeGamepad.rightTrigger.ReadValue() > 0.5f)
                {
                    holdBlock = true;
                }
                if (_activeGamepad.leftShoulder.isPressed)
                {
                    holdSpecial = true;
                }

                if (_activeGamepad.buttonWest.wasPressedThisFrame) BufferButton(FighterInputButton.LightPunch);
                if (_activeGamepad.buttonNorth.wasPressedThisFrame) BufferButton(FighterInputButton.HeavyPunch);
                if (_activeGamepad.buttonSouth.wasPressedThisFrame) BufferButton(FighterInputButton.LightKick);
                if (_activeGamepad.buttonEast.wasPressedThisFrame) BufferButton(FighterInputButton.HeavyKick);
                if (_activeGamepad.rightShoulder.wasPressedThisFrame) BufferButton(FighterInputButton.Block);
                if (_activeGamepad.leftShoulder.wasPressedThisFrame) BufferButton(FighterInputButton.Special);
            }

            _currentInput.MoveX = Mathf.Clamp(moveX, -1f, 1f);
            _currentInput.MoveY = Mathf.Clamp(moveY, -1f, 1f);
            _currentInput.HoldBlock = holdBlock;
            _currentInput.HoldSpecial = holdSpecial;
            _currentInput.LastTickSampled = _currentSimTick;
        }

        private static bool IsKeyPressed(Keyboard kb, Key key)
        {
            return key != Key.None && kb[key] != null && kb[key].isPressed;
        }

        private static bool WasKeyPressedThisFrame(Keyboard kb, Key key)
        {
            return key != Key.None && kb[key] != null && kb[key].wasPressedThisFrame;
        }

        private bool TryGetValidExpireTick(FighterInputButton button, int currentTick, out int expireTick)
        {
            return _bufferedButtonExpireTick.TryGetValue(button, out expireTick) && currentTick <= expireTick;
        }

        public void BufferButton(FighterInputButton button)
        {
            _bufferedButtonExpireTick[button] = _currentSimTick + bufferWindowTicks;
            if ((button == FighterInputButton.LightPunch || button == FighterInputButton.LightKick) &&
                !_currentInput.HoldBlock &&
                TryGetValidExpireTick(FighterInputButton.LightPunch, _currentSimTick, out int lpExpire) &&
                TryGetValidExpireTick(FighterInputButton.LightKick, _currentSimTick, out int lkExpire) &&
                Mathf.Abs(lpExpire - lkExpire) <= 2)
            {
                _lastThrowAttemptTick = _currentSimTick;
            }
        }

        public void BufferDashDirection(int directionSign, int currentTick)
        {
            if (directionSign == 0)
            {
                return;
            }
            _bufferedDashDirection = directionSign > 0 ? 1 : -1;
            _bufferedDashExpireTick = currentTick + bufferWindowTicks;
        }

        public bool ConsumeBufferedDash(int currentTick, int facingSign, out bool isForwardDash)
        {
            if (_bufferedDashDirection != 0 && currentTick <= _bufferedDashExpireTick)
            {
                int dir = _bufferedDashDirection;
                _bufferedDashDirection = 0;
                _bufferedDashExpireTick = -1;
                isForwardDash = dir == (facingSign >= 0 ? 1 : -1);
                return true;
            }
            isForwardDash = false;
            return false;
        }

        public void BufferThrowInput(int currentTick)
        {
            _currentSimTick = Mathf.Max(_currentSimTick, currentTick);
            _bufferedButtonExpireTick[FighterInputButton.LightPunch] = currentTick + bufferWindowTicks;
            _bufferedButtonExpireTick[FighterInputButton.LightKick] = currentTick + bufferWindowTicks;
            _lastThrowAttemptTick = currentTick;
        }

        public bool TryConsumeThrowCombo(int currentTick)
        {
            if (_currentInput.HoldBlock)
            {
                return false;
            }

            if (TryGetValidExpireTick(FighterInputButton.LightPunch, currentTick, out int lpExpire) &&
                TryGetValidExpireTick(FighterInputButton.LightKick, currentTick, out int lkExpire) &&
                Mathf.Abs(lpExpire - lkExpire) <= 2)
            {
                _bufferedButtonExpireTick.Remove(FighterInputButton.LightPunch);
                _bufferedButtonExpireTick.Remove(FighterInputButton.LightKick);
                _lastThrowAttemptTick = currentTick;
                return true;
            }

            return false;
        }

        public bool HasRecentThrowTechInput(int currentTick, int windowTicks = 7)
        {
            if (_lastThrowAttemptTick >= 0 && currentTick - _lastThrowAttemptTick <= windowTicks)
            {
                return true;
            }

            return !_currentInput.HoldBlock &&
                   TryGetValidExpireTick(FighterInputButton.LightPunch, currentTick, out int lpExpire) &&
                   TryGetValidExpireTick(FighterInputButton.LightKick, currentTick, out int lkExpire) &&
                   Mathf.Abs(lpExpire - lkExpire) <= 2;
        }

        public void ConsumeThrowTechInput()
        {
            _lastThrowAttemptTick = -999;
            _bufferedButtonExpireTick.Remove(FighterInputButton.LightPunch);
            _bufferedButtonExpireTick.Remove(FighterInputButton.LightKick);
        }

        public bool ConsumeBufferedButton(FighterInputButton button, int currentTick)
        {
            if (_bufferedButtonExpireTick.TryGetValue(button, out int expireTick) && currentTick <= expireTick)
            {
                _bufferedButtonExpireTick.Remove(button);
                return true;
            }
            return false;
        }

        public void BufferExInput(AttackType exAttack, int currentTick)
        {
            _currentSimTick = Mathf.Max(_currentSimTick, currentTick);
            _bufferedButtonExpireTick[FighterInputButton.Special] = currentTick + bufferWindowTicks;
            if (exAttack == AttackType.ExHeavyKick)
            {
                _bufferedButtonExpireTick[FighterInputButton.HeavyKick] = currentTick + bufferWindowTicks;
            }
            else
            {
                _bufferedButtonExpireTick[FighterInputButton.HeavyPunch] = currentTick + bufferWindowTicks;
            }
        }

        public bool ConsumeAttackInput(int currentTick, out AttackType attackType)
        {
            return ConsumeAttackInput(currentTick, allowSpecial: true, out attackType);
        }

        public bool ConsumeAttackInput(int currentTick, bool allowSpecial, out AttackType attackType)
        {
            return ConsumeAttackInput(
                currentTick,
                allowSpecial,
                isGrounded: true,
                isCrouchingInput: _currentInput.MoveY < -0.5f,
                allowEx: false,
                out attackType);
        }

        public bool ConsumeAttackInput(
            int currentTick,
            bool allowSpecial,
            bool isGrounded,
            bool isCrouchingInput,
            out AttackType attackType)
        {
            return ConsumeAttackInput(currentTick, allowSpecial, isGrounded, isCrouchingInput, allowEx: false, out attackType);
        }

        public bool ConsumeAttackInput(
            int currentTick,
            bool allowSpecial,
            bool isGrounded,
            bool isCrouchingInput,
            bool allowEx,
            out AttackType attackType)
        {
            if (!isGrounded)
            {
                if (ConsumeBufferedButton(FighterInputButton.HeavyKick, currentTick) ||
                    ConsumeBufferedButton(FighterInputButton.HeavyPunch, currentTick) ||
                    ConsumeBufferedButton(FighterInputButton.LightKick, currentTick) ||
                    ConsumeBufferedButton(FighterInputButton.LightPunch, currentTick))
                {
                    attackType = AttackType.JumpAttack;
                    return true;
                }

                attackType = AttackType.None;
                return false;
            }

            if (allowEx)
            {
                if (TryConsumeExCombo(currentTick, FighterInputButton.HeavyPunch))
                {
                    attackType = AttackType.ExHeavyPunch;
                    return true;
                }
                if (TryConsumeExCombo(currentTick, FighterInputButton.HeavyKick))
                {
                    attackType = AttackType.ExHeavyKick;
                    return true;
                }
            }

            if (allowSpecial && TryConsumeSpecialShortcutOrButton(currentTick))
            {
                attackType = AttackType.Special;
                return true;
            }

            if (TryConsumeThrowCombo(currentTick))
            {
                attackType = AttackType.Throw;
                return true;
            }

            if (isCrouchingInput && ConsumeBufferedButton(FighterInputButton.HeavyKick, currentTick))
            {
                attackType = AttackType.CrouchKick;
                return true;
            }

            if (ConsumeBufferedButton(FighterInputButton.HeavyKick, currentTick))
            {
                attackType = AttackType.HeavyKick;
                return true;
            }
            if (ConsumeBufferedButton(FighterInputButton.HeavyPunch, currentTick))
            {
                attackType = AttackType.HeavyPunch;
                return true;
            }
            if (ConsumeBufferedButton(FighterInputButton.LightKick, currentTick))
            {
                attackType = AttackType.LightKick;
                return true;
            }
            if (ConsumeBufferedButton(FighterInputButton.LightPunch, currentTick))
            {
                attackType = AttackType.LightPunch;
                return true;
            }

            attackType = AttackType.None;
            return false;
        }

        public bool ConsumeChainAttackInput(int currentTick, AttackType currentAttack, bool allowSpecial, out AttackType nextAttack)
        {
            return ConsumeChainAttackInput(currentTick, currentAttack, allowSpecial, allowEx: false, out nextAttack);
        }

        public bool ConsumeChainAttackInput(int currentTick, AttackType currentAttack, bool allowSpecial, bool allowEx, out AttackType nextAttack)
        {
            if (allowEx)
            {
                if (IsChainCancelAllowed(currentAttack, AttackType.ExHeavyPunch, allowSpecial, allowEx: true) &&
                    TryConsumeExCombo(currentTick, FighterInputButton.HeavyPunch))
                {
                    nextAttack = AttackType.ExHeavyPunch;
                    return true;
                }
                if (IsChainCancelAllowed(currentAttack, AttackType.ExHeavyKick, allowSpecial, allowEx: true) &&
                    TryConsumeExCombo(currentTick, FighterInputButton.HeavyKick))
                {
                    nextAttack = AttackType.ExHeavyKick;
                    return true;
                }
            }

            if (allowSpecial && IsChainCancelAllowed(currentAttack, AttackType.Special, true, allowEx) && TryConsumeSpecialShortcutOrButton(currentTick))
            {
                nextAttack = AttackType.Special;
                return true;
            }

            if (_currentInput.MoveY < -0.5f &&
                IsChainCancelAllowed(currentAttack, AttackType.CrouchKick, allowSpecial, allowEx) &&
                ConsumeBufferedButton(FighterInputButton.HeavyKick, currentTick))
            {
                nextAttack = AttackType.CrouchKick;
                return true;
            }

            if (IsChainCancelAllowed(currentAttack, AttackType.HeavyKick, allowSpecial, allowEx) && ConsumeBufferedButton(FighterInputButton.HeavyKick, currentTick))
            {
                nextAttack = AttackType.HeavyKick;
                return true;
            }
            if (IsChainCancelAllowed(currentAttack, AttackType.HeavyPunch, allowSpecial, allowEx) && ConsumeBufferedButton(FighterInputButton.HeavyPunch, currentTick))
            {
                nextAttack = AttackType.HeavyPunch;
                return true;
            }
            if (IsChainCancelAllowed(currentAttack, AttackType.LightKick, allowSpecial, allowEx) && ConsumeBufferedButton(FighterInputButton.LightKick, currentTick))
            {
                nextAttack = AttackType.LightKick;
                return true;
            }
            if (IsChainCancelAllowed(currentAttack, AttackType.LightPunch, allowSpecial, allowEx) && ConsumeBufferedButton(FighterInputButton.LightPunch, currentTick))
            {
                nextAttack = AttackType.LightPunch;
                return true;
            }

            nextAttack = AttackType.None;
            return false;
        }

        public static bool IsChainCancelAllowed(AttackType fromAttack, AttackType toAttack, bool allowSpecial)
        {
            return IsChainCancelAllowed(fromAttack, toAttack, allowSpecial, allowEx: false);
        }

        public static bool IsChainCancelAllowed(AttackType fromAttack, AttackType toAttack, bool allowSpecial, bool allowEx)
        {
            if (toAttack == AttackType.None || toAttack == fromAttack)
            {
                return false;
            }

            if (toAttack == AttackType.Special)
            {
                return allowSpecial && fromAttack != AttackType.Special;
            }

            if (toAttack == AttackType.ExHeavyPunch || toAttack == AttackType.ExHeavyKick)
            {
                return allowEx &&
                       (fromAttack == AttackType.LightPunch ||
                        fromAttack == AttackType.LightKick ||
                        fromAttack == AttackType.HeavyPunch ||
                        fromAttack == AttackType.HeavyKick ||
                        fromAttack == AttackType.CrouchKick);
            }

            return fromAttack switch
            {
                AttackType.LightPunch => toAttack == AttackType.HeavyPunch || toAttack == AttackType.LightKick || toAttack == AttackType.HeavyKick || toAttack == AttackType.CrouchKick,
                AttackType.LightKick => toAttack == AttackType.HeavyPunch || toAttack == AttackType.HeavyKick || toAttack == AttackType.CrouchKick,
                _ => false
            };
        }

        private bool TryConsumeExCombo(int currentTick, FighterInputButton heavyButton)
        {
            if (!TryGetValidExpireTick(heavyButton, currentTick, out int heavyExpire))
            {
                return false;
            }

            if (_currentInput.HoldSpecial)
            {
                _bufferedButtonExpireTick.Remove(heavyButton);
                _bufferedButtonExpireTick.Remove(FighterInputButton.Special);
                return true;
            }

            if (TryGetValidExpireTick(FighterInputButton.Special, currentTick, out int spExpire) &&
                Mathf.Abs(spExpire - heavyExpire) <= 2)
            {
                _bufferedButtonExpireTick.Remove(heavyButton);
                _bufferedButtonExpireTick.Remove(FighterInputButton.Special);
                return true;
            }

            return false;
        }

        public bool HasStageInteractCombo(int currentTick)
        {
            if (!TryGetValidExpireTick(FighterInputButton.LightPunch, currentTick, out int lpExpire))
            {
                return false;
            }

            if (_currentInput.HoldBlock)
            {
                return true;
            }

            return TryGetValidExpireTick(FighterInputButton.Block, currentTick, out int blockExpire) &&
                   Mathf.Abs(blockExpire - lpExpire) <= 2;
        }

        public bool ConsumeStageInteractCombo(int currentTick)
        {
            if (!HasStageInteractCombo(currentTick))
            {
                return false;
            }

            if (!ConsumeBufferedButton(FighterInputButton.LightPunch, currentTick))
            {
                return false;
            }

            _bufferedButtonExpireTick.Remove(FighterInputButton.Block);
            return true;
        }

        private bool TryConsumeSpecialShortcutOrButton(int currentTick)
        {
            if (TryGetValidExpireTick(FighterInputButton.LightPunch, currentTick, out int lpExpire) &&
                TryGetValidExpireTick(FighterInputButton.HeavyPunch, currentTick, out int hpExpire) &&
                Mathf.Abs(lpExpire - hpExpire) <= 2)
            {
                _bufferedButtonExpireTick.Remove(FighterInputButton.LightPunch);
                _bufferedButtonExpireTick.Remove(FighterInputButton.HeavyPunch);
                _bufferedButtonExpireTick.Remove(FighterInputButton.Special);
                return true;
            }

            return ConsumeBufferedButton(FighterInputButton.Special, currentTick);
        }
    }
}
