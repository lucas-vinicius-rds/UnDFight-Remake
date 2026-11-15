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

        private Key _lightPunchKey;
        private Key _heavyPunchKey;
        private Key _lightKickKey;
        private Key _heavyKickKey;
        private Key _blockKey;

        public int PlayerIndex => playerIndex;
        public InputAssignmentMode AssignmentMode => assignmentMode;
        public Gamepad ActiveGamepad => _activeGamepad;
        public FighterInputState CurrentInput => _currentInput;

        public string ActiveDeviceLabel
        {
            get
            {
                string kb = playerIndex == 0
                    ? $"KB(WASD+{_lightPunchKey}/{_heavyPunchKey}/{_lightKickKey}/{_heavyKickKey}/{_blockKey})"
                    : $"KB(Arrows+{_lightPunchKey}/{_heavyPunchKey}/{_lightKickKey}/{_heavyKickKey}/{_blockKey})";
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
            _bufferedButtonExpireTick.Clear();
        }

        public void ClearBufferedInputs()
        {
            _bufferedButtonExpireTick.Clear();
        }

        private void SampleDevices()
        {
            float moveX = 0f;
            float moveY = 0f;
            bool holdBlock = false;

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
                if (WasKeyPressedThisFrame(kb, _lightPunchKey)) BufferButton(FighterInputButton.LightPunch);
                if (WasKeyPressedThisFrame(kb, _heavyPunchKey)) BufferButton(FighterInputButton.HeavyPunch);
                if (WasKeyPressedThisFrame(kb, _lightKickKey)) BufferButton(FighterInputButton.LightKick);
                if (WasKeyPressedThisFrame(kb, _heavyKickKey)) BufferButton(FighterInputButton.HeavyKick);
                if (WasKeyPressedThisFrame(kb, _blockKey)) BufferButton(FighterInputButton.Block);
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

                if (_activeGamepad.buttonWest.wasPressedThisFrame) BufferButton(FighterInputButton.LightPunch);
                if (_activeGamepad.buttonNorth.wasPressedThisFrame) BufferButton(FighterInputButton.HeavyPunch);
                if (_activeGamepad.buttonSouth.wasPressedThisFrame) BufferButton(FighterInputButton.LightKick);
                if (_activeGamepad.buttonEast.wasPressedThisFrame) BufferButton(FighterInputButton.HeavyKick);
                if (_activeGamepad.rightShoulder.wasPressedThisFrame) BufferButton(FighterInputButton.Block);
            }

            _currentInput.MoveX = Mathf.Clamp(moveX, -1f, 1f);
            _currentInput.MoveY = Mathf.Clamp(moveY, -1f, 1f);
            _currentInput.HoldBlock = holdBlock;
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

        public void BufferButton(FighterInputButton button)
        {
            _bufferedButtonExpireTick[button] = _currentSimTick + bufferWindowTicks;
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

        public bool ConsumeAttackInput(int currentTick, out AttackType attackType)
        {
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
    }
}
