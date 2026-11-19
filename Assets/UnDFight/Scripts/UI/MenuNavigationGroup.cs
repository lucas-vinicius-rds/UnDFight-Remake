using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnDFight
{
    public sealed class MenuItemEntry
    {
        public UnityEngine.UI.Button Button;
        public UnityEngine.UI.Image Background;
        public UnityEngine.UI.Image AccentBar;
        public UnityEngine.UI.Image AccentGlow;
        public UnityEngine.UI.Image SweepImage;
        public UnityEngine.UI.Image LeftDiamond;
        public UnityEngine.UI.Image RightDiamond;
        public TextMeshProUGUI Label;
        public Action OnSubmit;
        public Action<int> OnHorizontalStep;

        public bool UseMkxCleanStyle;
        public Color NormalBg = MenuUiFactory.PanelDarkBg;
        public Color SelectedBg = new Color(0.118f, 0.215f, 0.345f, 0.96f);
        public Color NormalAccent = MenuUiFactory.SlateBorderMid;
        public Color SelectedAccent = MenuUiFactory.UnDFBlue;
        public Color SweepSelectedColor = new Color(0.118f, 0.337f, 0.600f, 0.52f); // #1E5699 soft sweep
        public Color DiamondSelectedColor = MenuUiFactory.UnDFSky;                  // #8BC9EE diamonds
        public Color NormalLabelColor = MenuUiFactory.TextInactive;                 // #8594A8
        public Color SelectedLabelColor = MenuUiFactory.TextActive;                 // #F1F5F9

        internal float VisualBlend;
        internal bool VisualInitialized;
    }

    /// <summary>
    /// Unified Keyboard (WASD/Arrows + Enter/J/Esc/K), Gamepad (D-Pad/Stick + South/East),
    /// and Mouse hover/click navigation handler for vertical or horizontal menu lists,
    /// supporting smooth unscaledDeltaTime MKX clean sweep + diamond transitions.
    /// </summary>
    public class MenuNavigationGroup : MonoBehaviour
    {
        private const float InitialRepeatDelay = 0.28f;
        private const float RepeatInterval = 0.11f;
        private const float TransitionSpeed = 14f;

        private readonly List<MenuItemEntry> _items = new List<MenuItemEntry>();
        private int _selectedIndex;
        private float _verticalRepeatTimer;
        private float _horizontalRepeatTimer;
        private int _lastVerticalDir;
        private int _lastHorizontalDir;
        private bool _inputLocked;
        private bool _horizontalPrimary;

        public int SelectedIndex => _selectedIndex;
        public int ItemCount => _items.Count;
        public bool HorizontalPrimary
        {
            get => _horizontalPrimary;
            set => _horizontalPrimary = value;
        }
        public Action OnCancel;
        public Action<int> OnSelectionChanged;

        public static event Action OnAnySelectionMoved;
        public static event Action OnAnySubmitted;
        public static event Action OnAnyCancelled;

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
                SetSelectedIndex(idx, notifyAudio: false);
                OnAnySubmitted?.Invoke();
                entry.OnSubmit?.Invoke();
            });

            RefreshVisuals(immediate: true);
        }

        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
        }

        public void SetSelectedIndex(int index, bool notifyAudio = true)
        {
            if (_items.Count == 0)
            {
                _selectedIndex = 0;
                return;
            }

            int next = ((index % _items.Count) + _items.Count) % _items.Count;
            bool changed = next != _selectedIndex;
            _selectedIndex = next;
            RefreshVisuals(immediate: !Application.isPlaying);
            OnSelectionChanged?.Invoke(_selectedIndex);
            if (changed && notifyAudio)
            {
                OnAnySelectionMoved?.Invoke();
            }
        }

        public void RefreshVisuals(bool immediate = true)
        {
            float dt = Application.isPlaying ? Mathf.Min(Time.unscaledDeltaTime, 0.1f) : 1f;
            float step = immediate ? 1f : Mathf.Clamp01(dt * TransitionSpeed);

            for (int i = 0; i < _items.Count; i++)
            {
                MenuItemEntry item = _items[i];
                bool selected = i == _selectedIndex;
                float target = selected ? 1f : 0f;

                if (immediate || !item.VisualInitialized)
                {
                    item.VisualBlend = target;
                    item.VisualInitialized = true;
                }
                else
                {
                    item.VisualBlend = Mathf.MoveTowards(item.VisualBlend, target, step);
                }

                ApplyItemVisualBlend(item, item.VisualBlend, selected);
            }
        }

        private static void ApplyItemVisualBlend(MenuItemEntry item, float t, bool selected)
        {
            if (item.UseMkxCleanStyle)
            {
                if (item.Background != null)
                {
                    item.Background.color = Color.Lerp(item.NormalBg, item.SelectedBg, t);
                }
                if (item.SweepImage != null)
                {
                    item.SweepImage.enabled = t > 0.005f;
                    Color sw = item.SweepSelectedColor;
                    sw.a *= t;
                    item.SweepImage.color = sw;
                    // Propagate alpha to thin sweep hairlines if present
                    for (int c = 0; c < item.SweepImage.transform.childCount; c++)
                    {
                        var childImg = item.SweepImage.transform.GetChild(c).GetComponent<UnityEngine.UI.Image>();
                        if (childImg != null)
                        {
                            Color hc = MenuUiFactory.UnDFBlue;
                            hc.a = 0.55f * t;
                            childImg.color = hc;
                        }
                    }
                }
                if (item.LeftDiamond != null)
                {
                    item.LeftDiamond.enabled = t > 0.01f;
                    Color dc = item.DiamondSelectedColor;
                    dc.a = t;
                    item.LeftDiamond.color = dc;
                    float sc = Mathf.Lerp(0.65f, 1.0f, t);
                    item.LeftDiamond.rectTransform.localScale = new Vector3(sc, sc, 1f);
                }
                if (item.RightDiamond != null)
                {
                    item.RightDiamond.enabled = t > 0.01f;
                    Color dc = item.DiamondSelectedColor;
                    dc.a = t;
                    item.RightDiamond.color = dc;
                    float sc = Mathf.Lerp(0.65f, 1.0f, t);
                    item.RightDiamond.rectTransform.localScale = new Vector3(sc, sc, 1f);
                }
                if (item.Label != null)
                {
                    item.Label.color = Color.Lerp(item.NormalLabelColor, item.SelectedLabelColor, t);
                }
                return;
            }

            if (item.Background != null)
            {
                item.Background.color = Color.Lerp(item.NormalBg, item.SelectedBg, t);
            }
            if (item.AccentBar != null)
            {
                item.AccentBar.color = Color.Lerp(item.NormalAccent, item.SelectedAccent, t);
            }
            if (item.AccentGlow != null)
            {
                item.AccentGlow.enabled = t > 0.01f;
                item.AccentGlow.color = new Color(item.SelectedAccent.r, item.SelectedAccent.g, item.SelectedAccent.b, 0.28f * t);
            }
            if (item.Label != null)
            {
                item.Label.color = Color.Lerp(item.NormalLabelColor, item.SelectedLabelColor, t);
            }
        }

        private void OnEnable()
        {
            _verticalRepeatTimer = 0f;
            _horizontalRepeatTimer = 0f;
            _lastVerticalDir = 0;
            _lastHorizontalDir = 0;
            RefreshVisuals(immediate: true);
        }

        private void Update()
        {
            if (_items.Count > 0)
            {
                RefreshVisuals(immediate: false);
            }

            if (_inputLocked || _items.Count == 0)
            {
                return;
            }

            ReadNavigationAxes(out int vertDir, out int horizDir, out bool submitPressed, out bool cancelPressed);

            if (_horizontalPrimary)
            {
                int primaryDir = horizDir != 0 ? horizDir : -vertDir;
                if (primaryDir != 0)
                {
                    if (primaryDir != _lastHorizontalDir || _horizontalRepeatTimer <= 0f)
                    {
                        SetSelectedIndex(_selectedIndex + primaryDir);
                        _horizontalRepeatTimer = primaryDir != _lastHorizontalDir ? InitialRepeatDelay : RepeatInterval;
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
                _lastHorizontalDir = primaryDir;
                _lastVerticalDir = 0;
            }
            else
            {
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
            }

            if (submitPressed)
            {
                OnAnySubmitted?.Invoke();
                _items[_selectedIndex].OnSubmit?.Invoke();
            }
            else if (cancelPressed)
            {
                OnAnyCancelled?.Invoke();
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
