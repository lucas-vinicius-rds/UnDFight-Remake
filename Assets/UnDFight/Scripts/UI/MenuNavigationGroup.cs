using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UnDFight
{
    public sealed class MenuItemPointerHook : MonoBehaviour, IPointerEnterHandler
    {
        private MenuNavigationGroup _group;
        private int _index;

        public void Bind(MenuNavigationGroup group, int index)
        {
            _group = group;
            _index = index;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _group?.SetSelectedIndex(_index);
        }
    }

    public sealed class MenuItemEntry
    {
        public UnityEngine.UI.Button Button;
        public UnityEngine.UI.Image Background;
        public UnityEngine.UI.Image AccentBar;
        public UnityEngine.UI.Image AccentGlow;
        public TextMeshProUGUI Label;
        public Action OnSubmit;
        public Action<int> OnHorizontalStep;
        public Color NormalBg = new Color(0.086f, 0.098f, 0.133f, 0.88f);      // #161922
        public Color SelectedBg = new Color(0.180f, 0.145f, 0.095f, 0.96f);    // Warm dark bronze-amber tint
        public Color NormalAccent = new Color(0.290f, 0.247f, 0.208f, 0.75f);  // #4A3F35
        public Color SelectedAccent = new Color(1.000f, 0.722f, 0.000f, 1.0f); // #FFB800
    }

    /// <summary>
    /// Unified Keyboard (WASD/Arrows + Enter/J/Esc/K), Gamepad (D-Pad/Stick + South/East),
    /// and Mouse hover/click navigation handler for vertical menu lists.
    /// </summary>
    public class MenuNavigationGroup : MonoBehaviour
    {
        private const float InitialRepeatDelay = 0.28f;
        private const float RepeatInterval = 0.11f;

        private readonly List<MenuItemEntry> _items = new List<MenuItemEntry>();
        private int _selectedIndex;
        private float _verticalRepeatTimer;
        private float _horizontalRepeatTimer;
        private int _lastVerticalDir;
        private int _lastHorizontalDir;
        private bool _inputLocked;

        public int SelectedIndex => _selectedIndex;
        public int ItemCount => _items.Count;
        public Action OnCancel;

        public void ClearItems()
        {
            _items.Clear();
            _selectedIndex = 0;
        }

        public void AddItem(MenuItemEntry entry)
        {
            if (entry == null || entry.Button == null)
            {
                return;
            }

            int idx = _items.Count;
            _items.Add(entry);

            var hook = entry.Button.GetComponent<MenuItemPointerHook>();
            if (hook == null)
            {
                hook = entry.Button.gameObject.AddComponent<MenuItemPointerHook>();
            }
            hook.Bind(this, idx);

            entry.Button.onClick.RemoveAllListeners();
            entry.Button.onClick.AddListener(() =>
            {
                if (_inputLocked) return;
                SetSelectedIndex(idx);
                entry.OnSubmit?.Invoke();
            });

            RefreshVisuals();
        }

        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
        }

        public void SetSelectedIndex(int index)
        {
            if (_items.Count == 0)
            {
                _selectedIndex = 0;
                return;
            }

            _selectedIndex = ((index % _items.Count) + _items.Count) % _items.Count;
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                MenuItemEntry item = _items[i];
                bool selected = i == _selectedIndex;
                if (item.Background != null)
                {
                    item.Background.color = selected ? item.SelectedBg : item.NormalBg;
                }
                if (item.AccentBar != null)
                {
                    item.AccentBar.color = selected ? item.SelectedAccent : item.NormalAccent;
                }
                if (item.AccentGlow != null)
                {
                    item.AccentGlow.enabled = selected;
                    item.AccentGlow.color = new Color(item.SelectedAccent.r, item.SelectedAccent.g, item.SelectedAccent.b, 0.28f);
                }
                if (item.Label != null)
                {
                    item.Label.color = selected ? new Color(1.0f, 0.86f, 0.38f, 1f) : new Color(0.84f, 0.84f, 0.82f, 1f);
                }
            }
        }

        private void OnEnable()
        {
            _verticalRepeatTimer = 0f;
            _horizontalRepeatTimer = 0f;
            _lastVerticalDir = 0;
            _lastHorizontalDir = 0;
            RefreshVisuals();
        }

        private void Update()
        {
            if (_inputLocked || _items.Count == 0)
            {
                return;
            }

            ReadNavigationAxes(out int vertDir, out int horizDir, out bool submitPressed, out bool cancelPressed);

            if (vertDir != 0)
            {
                if (vertDir != _lastVerticalDir || _verticalRepeatTimer <= 0f)
                {
                    SetSelectedIndex(_selectedIndex - vertDir); // up (+1) moves to previous index
                    _verticalRepeatTimer = vertDir != _lastVerticalDir ? InitialRepeatDelay : RepeatInterval;
                }
                else
                {
                    _verticalRepeatTimer -= Time.unscaledDeltaTime;
                }
            }
            else
            {
                _verticalRepeatTimer = 0f;
            }
            _lastVerticalDir = vertDir;

            if (horizDir != 0)
            {
                if (horizDir != _lastHorizontalDir || _horizontalRepeatTimer <= 0f)
                {
                    _items[_selectedIndex].OnHorizontalStep?.Invoke(horizDir);
                    _horizontalRepeatTimer = horizDir != _lastHorizontalDir ? InitialRepeatDelay : RepeatInterval;
                }
                else
                {
                    _horizontalRepeatTimer -= Time.unscaledDeltaTime;
                }
            }
            else
            {
                _horizontalRepeatTimer = 0f;
            }
            _lastHorizontalDir = horizDir;

            if (submitPressed)
            {
                _items[_selectedIndex].OnSubmit?.Invoke();
            }
            else if (cancelPressed)
            {
                OnCancel?.Invoke();
            }
        }

        private static void ReadNavigationAxes(out int vertDir, out int horizDir, out bool submit, out bool cancel)
        {
            vertDir = 0;
            horizDir = 0;
            submit = false;
            cancel = false;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) vertDir += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) vertDir -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizDir += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizDir -= 1;

                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                    kb.jKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    submit = true;
                }

                if (kb.escapeKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
                {
                    cancel = true;
                }
            }

            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
            {
                Gamepad pad = pads[i];
                if (pad == null || !pad.added) continue;

                Vector2 dpad = pad.dpad.ReadValue();
                Vector2 stick = pad.leftStick.ReadValue();
                float vy = Mathf.Abs(dpad.y) > 0.3f ? dpad.y : (Mathf.Abs(stick.y) > 0.5f ? stick.y : 0f);
                float hx = Mathf.Abs(dpad.x) > 0.3f ? dpad.x : (Mathf.Abs(stick.x) > 0.5f ? stick.x : 0f);

                if (Mathf.Abs(vy) > 0.3f && vertDir == 0) vertDir = vy > 0f ? 1 : -1;
                if (Mathf.Abs(hx) > 0.3f && horizDir == 0) horizDir = hx > 0f ? 1 : -1;

                if (pad.buttonSouth.wasPressedThisFrame) submit = true;
                if (pad.buttonEast.wasPressedThisFrame) cancel = true;
            }
        }
    }
}
