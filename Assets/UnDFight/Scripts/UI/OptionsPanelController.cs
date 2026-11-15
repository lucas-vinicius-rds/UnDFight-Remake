using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UnDFight
{
    /// <summary>
    /// Shared Options modal panel (Audio volumes, Video resolution/fullscreen, P1/P2 device modes,
    /// interactive key remapping, and Reset to Defaults) persisted to JSON via SettingsManager.
    /// </summary>
    public class OptionsPanelController : MonoBehaviour
    {
        private sealed class KeyBindRow
        {
            public int PlayerIndex;
            public FighterInputButton ButtonType;
            public string ActionLabel;
            public TextMeshProUGUI ValueText;
            public MenuItemEntry Entry;
        }

        [SerializeField] private MenuNavigationGroup navigationGroup;
        [SerializeField] private Sprite whiteSprite;

        private GameSettingsData _working;
        private int _resolutionIndex;

        private UnityEngine.UI.Slider _masterSlider;
        private TextMeshProUGUI _masterVal;
        private UnityEngine.UI.Slider _musicSlider;
        private TextMeshProUGUI _musicVal;
        private UnityEngine.UI.Slider _sfxSlider;
        private TextMeshProUGUI _sfxVal;

        private TextMeshProUGUI _resolutionVal;
        private TextMeshProUGUI _fullscreenVal;
        private TextMeshProUGUI _p1DeviceVal;
        private TextMeshProUGUI _p2DeviceVal;
        private TextMeshProUGUI _hintFooterText;

        private readonly List<KeyBindRow> _bindRows = new List<KeyBindRow>();
        private KeyBindRow _listeningRow;
        private int _openCooldownFrames;

        public event Action OnClosed;
        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (_bindRows.Count == 0 && whiteSprite != null)
            {
                BuildUI(whiteSprite);
            }
        }

        public void SetWhiteSprite(Sprite sprite)
        {
            whiteSprite = sprite;
        }

        public void BuildUI(Sprite sprite)
        {
            whiteSprite = sprite;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            if (navigationGroup == null)
            {
                navigationGroup = GetComponent<MenuNavigationGroup>();
                if (navigationGroup == null)
                {
                    navigationGroup = gameObject.AddComponent<MenuNavigationGroup>();
                }
            }

            navigationGroup.ClearItems();
            _bindRows.Clear();

            var rootRt = GetComponent<RectTransform>();
            if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
            MenuUiFactory.StretchFull(rootRt, 0f, 0f, 0f, 0f);

            var backdrop = GetComponent<UnityEngine.UI.Image>();
            if (backdrop == null) backdrop = gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.sprite = whiteSprite;
            backdrop.color = new Color(0.02f, 0.022f, 0.03f, 0.86f);
            backdrop.raycastTarget = true;

            RectTransform modal = MenuUiFactory.CreatePanel("ModalBox", transform, MenuUiFactory.ObsidianBg, whiteSprite, raycastTarget: true);
            modal.anchorMin = new Vector2(0.5f, 0.5f);
            modal.anchorMax = new Vector2(0.5f, 0.5f);
            modal.pivot = new Vector2(0.5f, 0.5f);
            modal.sizeDelta = new Vector2(1480f, 900f);
            MenuUiFactory.AddBorder(modal, MenuUiFactory.BronzeBorder, whiteSprite, 2f);

            RectTransform topLine = MenuUiFactory.CreatePanel("TopGoldLine", modal, MenuUiFactory.AccentGold, whiteSprite);
            topLine.anchorMin = new Vector2(0f, 1f);
            topLine.anchorMax = new Vector2(1f, 1f);
            topLine.pivot = new Vector2(0.5f, 1f);
            topLine.sizeDelta = new Vector2(0f, 4f);

            TextMeshProUGUI title = MenuUiFactory.CreateText("Title", modal, "CONFIGURAÇÕES", 42f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, MenuUiFactory.AccentGold, characterSpacing: 7f);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            title.rectTransform.sizeDelta = new Vector2(-80f, 58f);

            // Top Left: Audio section
            TextMeshProUGUI audioHdr = MenuUiFactory.CreateText("AudioHeader", modal, "ÁUDIO", 22f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, MenuUiFactory.AccentAmber, characterSpacing: 5f);
            PlaceTopLeftRect(audioHdr.rectTransform, 44f, -86f, 670f, 34f);

            CreateSliderOptionRow(modal, whiteSprite, "Volume Geral (Master)", 44f, -126f, 670f, out _masterSlider, out _masterVal, v =>
            {
                _working.masterVolume = v;
                _masterVal.text = $"{Mathf.RoundToInt(v * 100f)}%";
                SettingsManager.ApplySettings(_working, applyResolution: false);
            });

            CreateSliderOptionRow(modal, whiteSprite, "Volume Música", 44f, -184f, 670f, out _musicSlider, out _musicVal, v =>
            {
                _working.musicVolume = v;
                _musicVal.text = $"{Mathf.RoundToInt(v * 100f)}%";
            });

            CreateSliderOptionRow(modal, whiteSprite, "Volume Efeitos (SFX)", 44f, -242f, 670f, out _sfxSlider, out _sfxVal, v =>
            {
                _working.sfxVolume = v;
                _sfxVal.text = $"{Mathf.RoundToInt(v * 100f)}%";
            });

            // Top Right: Video & Devices
            TextMeshProUGUI videoHdr = MenuUiFactory.CreateText("VideoHeader", modal, "VÍDEO E DISPOSITIVOS", 22f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, MenuUiFactory.AccentAmber, characterSpacing: 5f);
            PlaceTopLeftRect(videoHdr.rectTransform, 756f, -86f, 680f, 34f);

            CreateCyclerRow(modal, whiteSprite, "Resolução", 756f, -126f, 680f, out _resolutionVal, step =>
            {
                int count = SettingsManager.SupportedResolutions.Length;
                _resolutionIndex = ((_resolutionIndex + step) % count + count) % count;
                ResolutionOption res = SettingsManager.SupportedResolutions[_resolutionIndex];
                _working.resolutionWidth = res.width;
                _working.resolutionHeight = res.height;
                RefreshWidgets();
            });

            CreateCyclerRow(modal, whiteSprite, "Tela Cheia", 756f, -184f, 680f, out _fullscreenVal, _ =>
            {
                _working.fullscreen = !_working.fullscreen;
                RefreshWidgets();
            });

            CreateCyclerRow(modal, whiteSprite, "Dispositivo P1", 756f, -242f, 680f, out _p1DeviceVal, step => CycleDevice(0, step));
            CreateCyclerRow(modal, whiteSprite, "Dispositivo P2", 756f, -300f, 680f, out _p2DeviceVal, step => CycleDevice(1, step));

            // Middle: Key bindings (P1 Left Column, P2 Right Column)
            TextMeshProUGUI p1Hdr = MenuUiFactory.CreateText("P1ControlsHeader", modal, "CONTROLES P1 (WASD + GOLPES)", 23f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, MenuUiFactory.AccentGold);
            PlaceTopLeftRect(p1Hdr.rectTransform, 44f, -366f, 670f, 32f);

            TextMeshProUGUI p2Hdr = MenuUiFactory.CreateText("P2ControlsHeader", modal, "CONTROLES P2 (SETAS + GOLPES)", 23f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, MenuUiFactory.AccentGold);
            PlaceTopLeftRect(p2Hdr.rectTransform, 756f, -366f, 680f, 32f);

            CreateBindingColumn(modal, whiteSprite, 0, 44f, -404f, 670f);
            CreateBindingColumn(modal, whiteSprite, 1, 756f, -404f, 680f);

            // Footer buttons
            MenuItemEntry resetBtn = MenuUiFactory.CreateMenuButton("Btn_ResetDefaults", modal, "RESTAURAR PADRÃO", whiteSprite, 24f, TextAlignmentOptions.Center);
            PlaceTopLeftRect(resetBtn.Button.GetComponent<RectTransform>(), 44f, -764f, 420f, 62f);
            resetBtn.OnSubmit = ResetDefaults;
            navigationGroup.AddItem(resetBtn);

            MenuItemEntry saveCloseBtn = MenuUiFactory.CreateMenuButton("Btn_SaveClose", modal, "SALVAR E FECHAR", whiteSprite, 24f, TextAlignmentOptions.Center);
            PlaceTopLeftRect(saveCloseBtn.Button.GetComponent<RectTransform>(), 1016f, -764f, 420f, 62f);
            saveCloseBtn.OnSubmit = () => Close(saveChanges: true);
            navigationGroup.AddItem(saveCloseBtn);

            _hintFooterText = MenuUiFactory.CreateText(
                "FooterHint",
                modal,
                "WASD/Setas/D-Pad: Navegar  |  Esquerda/Direita: Ajustar  |  Enter/Clique: Remapear Tecla  |  Esc: Salvar e Voltar",
                19f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Color(0.74f, 0.80f, 0.88f, 1f));
            PlaceTopLeftRect(_hintFooterText.rectTransform, 44f, -842f, 1392f, 38f);

            navigationGroup.OnCancel = () =>
            {
                if (_listeningRow == null)
                {
                    Close(saveChanges: true);
                }
            };
        }

        private void CreateBindingColumn(Transform modal, Sprite whiteSprite, int playerIndex, float x, float startY, float width)
        {
            (FighterInputButton btn, string label)[] rows =
            {
                (FighterInputButton.LightPunch, "Soco Fraco"),
                (FighterInputButton.HeavyPunch, "Soco Forte"),
                (FighterInputButton.LightKick, "Chute Fraco"),
                (FighterInputButton.HeavyKick, "Chute Forte"),
                (FighterInputButton.Block, "Bloqueio"),
                (FighterInputButton.Special, "Especial")
            };

            for (int i = 0; i < rows.Length; i++)
            {
                float y = startY - i * 56f;
                MenuItemEntry entry = MenuUiFactory.CreateMenuButton($"Bind_P{playerIndex + 1}_{rows[i].btn}", modal, rows[i].label, whiteSprite, 21f, TextAlignmentOptions.MidlineLeft);
                PlaceTopLeftRect(entry.Button.GetComponent<RectTransform>(), x, y, width, 48f);

                TextMeshProUGUI valText = MenuUiFactory.CreateText("KeyValue", entry.Button.transform, "J", 22f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, MenuUiFactory.AccentGold);
                MenuUiFactory.StretchFull(valText.rectTransform, 24f, 4f, 22f, 4f);

                var bindRow = new KeyBindRow
                {
                    PlayerIndex = playerIndex,
                    ButtonType = rows[i].btn,
                    ActionLabel = rows[i].label,
                    ValueText = valText,
                    Entry = entry
                };
                _bindRows.Add(bindRow);

                entry.OnSubmit = () => BeginRebind(bindRow);
                navigationGroup.AddItem(entry);
            }
        }

        private void CreateSliderOptionRow(
            Transform parent,
            Sprite whiteSprite,
            string title,
            float x,
            float y,
            float width,
            out UnityEngine.UI.Slider slider,
            out TextMeshProUGUI valueText,
            Action<float> onValueChanged)
        {
            MenuItemEntry entry = MenuUiFactory.CreateMenuButton($"Opt_{title}", parent, title, whiteSprite, 21f, TextAlignmentOptions.MidlineLeft);
            PlaceTopLeftRect(entry.Button.GetComponent<RectTransform>(), x, y, width, 50f);

            slider = MenuUiFactory.CreateSlider("Slider", entry.Button.transform, whiteSprite);
            var sliderRt = slider.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.50f, 0.15f);
            sliderRt.anchorMax = new Vector2(0.84f, 0.85f);
            sliderRt.offsetMin = Vector2.zero;
            sliderRt.offsetMax = Vector2.zero;

            valueText = MenuUiFactory.CreateText("Pct", entry.Button.transform, "100%", 21f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, MenuUiFactory.AccentGold);
            valueText.rectTransform.anchorMin = new Vector2(0.84f, 0f);
            valueText.rectTransform.anchorMax = new Vector2(1f, 1f);
            valueText.rectTransform.offsetMin = Vector2.zero;
            valueText.rectTransform.offsetMax = new Vector2(-18f, 0f);

            UnityEngine.UI.Slider capturedSlider = slider;
            capturedSlider.onValueChanged.AddListener(v =>
            {
                if (_working != null)
                {
                    onValueChanged?.Invoke(v);
                }
            });

            entry.OnHorizontalStep = dir =>
            {
                capturedSlider.value = Mathf.Clamp01(capturedSlider.value + dir * 0.05f);
            };
            entry.OnSubmit = () =>
            {
                float next = capturedSlider.value >= 0.99f ? 0f : Mathf.Clamp01(capturedSlider.value + 0.1f);
                capturedSlider.value = next;
            };

            navigationGroup.AddItem(entry);
        }

        private void CreateCyclerRow(
            Transform parent,
            Sprite whiteSprite,
            string title,
            float x,
            float y,
            float width,
            out TextMeshProUGUI valueText,
            Action<int> onStep)
        {
            MenuItemEntry entry = MenuUiFactory.CreateMenuButton($"Opt_{title}", parent, title, whiteSprite, 21f, TextAlignmentOptions.MidlineLeft);
            PlaceTopLeftRect(entry.Button.GetComponent<RectTransform>(), x, y, width, 50f);

            valueText = MenuUiFactory.CreateText("Val", entry.Button.transform, "< ... >", 21f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, MenuUiFactory.AccentGold);
            MenuUiFactory.StretchFull(valueText.rectTransform, 20f, 4f, 20f, 4f);

            entry.OnHorizontalStep = dir => onStep?.Invoke(dir);
            entry.OnSubmit = () => onStep?.Invoke(1);
            navigationGroup.AddItem(entry);
        }

        public void Open()
        {
            if (_bindRows.Count == 0 && whiteSprite != null)
            {
                BuildUI(whiteSprite);
            }
            _working = SettingsManager.Current.Clone();
            _resolutionIndex = SettingsManager.FindResolutionIndex(_working.resolutionWidth, _working.resolutionHeight);
            _listeningRow = null;
            _openCooldownFrames = 2;

            gameObject.SetActive(true);
            RefreshWidgets();
            navigationGroup?.SetInputLocked(true);
            navigationGroup?.SetSelectedIndex(0);
        }

        public void Close(bool saveChanges = true)
        {
            _listeningRow = null;
            if (saveChanges && _working != null)
            {
                SettingsManager.Save(_working);
                SettingsManager.ApplySettings(_working, applyResolution: true);
            }

            gameObject.SetActive(false);
            OnClosed?.Invoke();
        }

        public void ResetDefaults()
        {
            _working = SettingsManager.ResetToDefaults(applyAndSave: true).Clone();
            _resolutionIndex = SettingsManager.FindResolutionIndex(_working.resolutionWidth, _working.resolutionHeight);
            RefreshWidgets();
        }

        private void Update()
        {
            if (_openCooldownFrames > 0)
            {
                _openCooldownFrames--;
                if (_openCooldownFrames == 0)
                {
                    navigationGroup?.SetInputLocked(_listeningRow != null);
                }
                return;
            }

            if (_listeningRow == null)
            {
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (kb.escapeKey.wasPressedThisFrame)
            {
                _listeningRow = null;
                navigationGroup?.SetInputLocked(false);
                RefreshWidgets();
                return;
            }

            foreach (KeyControl kc in kb.allKeys)
            {
                if (kc != null && kc.wasPressedThisFrame && kc.keyCode != Key.None && kc.keyCode != Key.Escape)
                {
                    _working.GetBindingsForPlayer(_listeningRow.PlayerIndex).SetKey(_listeningRow.ButtonType, kc.keyCode);
                    _listeningRow = null;
                    navigationGroup?.SetInputLocked(false);
                    RefreshWidgets();
                    break;
                }
            }
        }

        private void BeginRebind(KeyBindRow row)
        {
            _listeningRow = row;
            navigationGroup?.SetInputLocked(true);
            row.ValueText.text = "PRESSIONE TECLA... (ESC CANCELA)";
            row.ValueText.color = MenuUiFactory.AccentCyan;
        }

        private void CycleDevice(int playerIndex, int step)
        {
            int enumCount = Enum.GetValues(typeof(InputAssignmentMode)).Length;
            int current = (int)_working.GetDeviceModeForPlayer(playerIndex);
            int next = ((current + step) % enumCount + enumCount) % enumCount;
            _working.SetDeviceModeForPlayer(playerIndex, (InputAssignmentMode)next);
            RefreshWidgets();
        }

        private void RefreshWidgets()
        {
            if (_working == null) return;

            if (_masterSlider != null)
            {
                _masterSlider.SetValueWithoutNotify(_working.masterVolume);
                _masterVal.text = $"{Mathf.RoundToInt(_working.masterVolume * 100f)}%";
            }
            if (_musicSlider != null)
            {
                _musicSlider.SetValueWithoutNotify(_working.musicVolume);
                _musicVal.text = $"{Mathf.RoundToInt(_working.musicVolume * 100f)}%";
            }
            if (_sfxSlider != null)
            {
                _sfxSlider.SetValueWithoutNotify(_working.sfxVolume);
                _sfxVal.text = $"{Mathf.RoundToInt(_working.sfxVolume * 100f)}%";
            }

            if (_resolutionVal != null)
            {
                _resolutionVal.text = $"<  {_working.resolutionWidth} x {_working.resolutionHeight}  >";
            }
            if (_fullscreenVal != null)
            {
                _fullscreenVal.text = _working.fullscreen ? "<  ATIVADA  >" : "<  DESATIVADA  >";
            }
            if (_p1DeviceVal != null)
            {
                _p1DeviceVal.text = $"<  {SettingsManager.FormatDeviceModeLabel(_working.p1DeviceMode)}  >";
            }
            if (_p2DeviceVal != null)
            {
                _p2DeviceVal.text = $"<  {SettingsManager.FormatDeviceModeLabel(_working.p2DeviceMode)}  >";
            }

            for (int i = 0; i < _bindRows.Count; i++)
            {
                KeyBindRow row = _bindRows[i];
                if (_listeningRow == row) continue;

                Key k = _working.GetBindingsForPlayer(row.PlayerIndex).GetKey(row.ButtonType, row.PlayerIndex);
                row.ValueText.text = $"[{k}]";
                row.ValueText.color = MenuUiFactory.AccentGold;
            }
        }

        private static void PlaceTopLeftRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
