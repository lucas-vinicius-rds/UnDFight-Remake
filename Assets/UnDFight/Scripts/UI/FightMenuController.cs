using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnDFight
{
    /// <summary>
    /// MKX-styled in-fight Pause Menu (Esc / Gamepad Start), automatic MatchOver post-fight menu,
    /// and shared Options modal inside combat scenes, supporting Change Stage and Change Fighters.
    /// </summary>
    public class FightMenuController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private MatchManager matchManager;
        [SerializeField] private CombatVfxController vfxController;
        [SerializeField] private Sprite whiteSprite;

        [Header("UI Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private MenuNavigationGroup pauseNavigation;
        [SerializeField] private GameObject matchOverPanel;
        [SerializeField] private MenuNavigationGroup matchOverNavigation;
        [SerializeField] private OptionsPanelController optionsPanel;

        private TextMeshProUGUI _hitboxButtonLabel;
        private bool _isPaused;
        private int _escCooldownFrames;

        public bool IsPaused => _isPaused;
        public bool IsMatchOverMenuOpen => matchOverPanel != null && matchOverPanel.activeSelf;

        private void Awake()
        {
            if (simulation == null) simulation = FindAnyObjectByType<FightSimulation>();
            if (matchManager == null) matchManager = FindAnyObjectByType<MatchManager>();
            if (vfxController == null) vfxController = FindAnyObjectByType<CombatVfxController>();

            if (transform.Find("PausePanel") == null && whiteSprite != null)
            {
                BuildUI(transform, whiteSprite);
            }
            else
            {
                BindExistingPanels();
            }

            SetPauseMenuVisible(false);
            SetMatchOverMenuVisible(false);
            if (optionsPanel != null)
            {
                optionsPanel.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (matchManager != null)
            {
                matchManager.OnMatchStateChanged -= HandleMatchStateChanged;
                matchManager.OnMatchStateChanged += HandleMatchStateChanged;
            }
        }

        private void OnDisable()
        {
            if (matchManager != null)
            {
                matchManager.OnMatchStateChanged -= HandleMatchStateChanged;
            }
        }

        public void Configure(FightSimulation sim, MatchManager mm, CombatVfxController vfx, Sprite sprite)
        {
            simulation = sim;
            matchManager = mm;
            vfxController = vfx;
            whiteSprite = sprite;
        }

        public void BuildUI(Transform canvasRoot, Sprite sprite)
        {
            whiteSprite = sprite;

            // Pause Panel (MKX obsidian/bronze/amber)
            RectTransform pauseOverlay = MenuUiFactory.CreatePanel("PausePanel", canvasRoot, new Color(0.02f, 0.022f, 0.03f, 0.78f), whiteSprite, raycastTarget: true);
            MenuUiFactory.StretchFull(pauseOverlay, 0f, 0f, 0f, 0f);
            pausePanel = pauseOverlay.gameObject;

            RectTransform pauseBox = MenuUiFactory.CreatePanel("PauseBox", pauseOverlay, MenuUiFactory.ObsidianBg, whiteSprite, raycastTarget: true);
            pauseBox.anchorMin = new Vector2(0.5f, 0.5f);
            pauseBox.anchorMax = new Vector2(0.5f, 0.5f);
            pauseBox.pivot = new Vector2(0.5f, 0.5f);
            pauseBox.sizeDelta = new Vector2(580f, 640f);
            MenuUiFactory.AddBorder(pauseBox, MenuUiFactory.BronzeBorder, whiteSprite, 2f);

            RectTransform pauseTop = MenuUiFactory.CreatePanel("PauseAccent", pauseBox, MenuUiFactory.AccentGold, whiteSprite);
            pauseTop.anchorMin = new Vector2(0f, 1f);
            pauseTop.anchorMax = new Vector2(1f, 1f);
            pauseTop.pivot = new Vector2(0.5f, 1f);
            pauseTop.sizeDelta = new Vector2(0f, 4f);

            TextMeshProUGUI pauseTitle = MenuUiFactory.CreateText("PauseTitle", pauseBox, "PAUSADO", 42f, FontStyles.Bold, TextAlignmentOptions.Center, MenuUiFactory.AccentGold, characterSpacing: 8f);
            pauseTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            pauseTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            pauseTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            pauseTitle.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            pauseTitle.rectTransform.sizeDelta = new Vector2(-40f, 58f);

            pauseNavigation = pauseBox.gameObject.AddComponent<MenuNavigationGroup>();
            string[] pauseLabels =
            {
                "CONTINUAR",
                "REINICIAR LUTA",
                "HITBOXES (F1): OFF",
                "CONFIGURAÇÕES",
                "TROCAR MAPA",
                "TROCAR PERSONAGENS",
                "MENU PRINCIPAL"
            };

            for (int i = 0; i < pauseLabels.Length; i++)
            {
                MenuItemEntry entry = MenuUiFactory.CreateMenuButton($"PauseBtn_{i}", pauseBox, pauseLabels[i], whiteSprite, 23f, TextAlignmentOptions.Center, characterSpacing: 5f);
                RectTransform rt = entry.Button.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -96f - i * 72f);
                rt.sizeDelta = new Vector2(476f, 58f);
                pauseNavigation.AddItem(entry);
            }

            // MatchOver Post-Fight Menu Panel (positioned cleanly below the winner banner)
            RectTransform moPanel = MenuUiFactory.CreatePanel("MatchOverPanel", canvasRoot, MenuUiFactory.ObsidianBg, whiteSprite, raycastTarget: true);
            moPanel.anchorMin = new Vector2(0.5f, 0.39f);
            moPanel.anchorMax = new Vector2(0.5f, 0.39f);
            moPanel.pivot = new Vector2(0.5f, 0.5f);
            moPanel.sizeDelta = new Vector2(500f, 318f);
            matchOverPanel = moPanel.gameObject;
            MenuUiFactory.AddBorder(moPanel, MenuUiFactory.BronzeBorder, whiteSprite, 2f);

            RectTransform moLine = MenuUiFactory.CreatePanel("MOAccent", moPanel, MenuUiFactory.AccentGold, whiteSprite);
            moLine.anchorMin = new Vector2(0f, 1f);
            moLine.anchorMax = new Vector2(1f, 1f);
            moLine.pivot = new Vector2(0.5f, 1f);
            moLine.sizeDelta = new Vector2(0f, 4f);

            matchOverNavigation = moPanel.gameObject.AddComponent<MenuNavigationGroup>();
            string[] moLabels =
            {
                "REVANCHE",
                "TROCAR MAPA",
                "TROCAR PERSONAGENS",
                "MENU PRINCIPAL"
            };

            for (int i = 0; i < moLabels.Length; i++)
            {
                MenuItemEntry entry = MenuUiFactory.CreateMenuButton($"MatchOverBtn_{i}", moPanel, moLabels[i], whiteSprite, 23f, TextAlignmentOptions.Center, characterSpacing: 5f);
                RectTransform rt = entry.Button.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -22f - i * 70f);
                rt.sizeDelta = new Vector2(424f, 56f);
                matchOverNavigation.AddItem(entry);
            }

            // Shared Options Modal
            var optGo = new GameObject("OptionsPanel", typeof(RectTransform));
            optGo.transform.SetParent(canvasRoot, false);
            optionsPanel = optGo.AddComponent<OptionsPanelController>();
            optionsPanel.SetWhiteSprite(whiteSprite);
            optionsPanel.BuildUI(whiteSprite);
            optGo.SetActive(false);

            BindExistingPanels();
        }

        private void BindExistingPanels()
        {
            if (pauseNavigation != null)
            {
                var btns = pauseNavigation.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                pauseNavigation.ClearItems();
                for (int i = 0; i < btns.Length; i++)
                {
                    int idx = i;
                    var label = btns[i].transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                    if (idx == 2) _hitboxButtonLabel = label;

                    pauseNavigation.AddItem(new MenuItemEntry
                    {
                        Button = btns[i],
                        Background = btns[i].GetComponent<UnityEngine.UI.Image>(),
                        AccentBar = btns[i].transform.Find("AccentBar")?.GetComponent<UnityEngine.UI.Image>(),
                        AccentGlow = btns[i].transform.Find("AccentGlow")?.GetComponent<UnityEngine.UI.Image>(),
                        Label = label,
                        OnSubmit = () => ExecutePauseAction(idx)
                    });
                }
                pauseNavigation.OnCancel = () => SetPaused(false);
            }

            if (matchOverNavigation != null)
            {
                var btns = matchOverNavigation.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                matchOverNavigation.ClearItems();
                for (int i = 0; i < btns.Length; i++)
                {
                    int idx = i;
                    matchOverNavigation.AddItem(new MenuItemEntry
                    {
                        Button = btns[i],
                        Background = btns[i].GetComponent<UnityEngine.UI.Image>(),
                        AccentBar = btns[i].transform.Find("AccentBar")?.GetComponent<UnityEngine.UI.Image>(),
                        AccentGlow = btns[i].transform.Find("AccentGlow")?.GetComponent<UnityEngine.UI.Image>(),
                        Label = btns[i].transform.Find("Label")?.GetComponent<TextMeshProUGUI>(),
                        OnSubmit = () => ExecuteMatchOverAction(idx)
                    });
                }
            }

            if (optionsPanel != null)
            {
                optionsPanel.OnClosed -= HandleOptionsClosed;
                optionsPanel.OnClosed += HandleOptionsClosed;
            }
        }

        private void Update()
        {
            if (_escCooldownFrames > 0)
            {
                _escCooldownFrames--;
                return;
            }

            if (optionsPanel != null && optionsPanel.IsOpen)
            {
                return;
            }

            if (IsPauseInputPressed())
            {
                if (matchManager == null || matchManager.Phase != MatchPhase.MatchOver)
                {
                    SetPaused(!_isPaused);
                }
            }
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            simulation?.SetPaused(paused);
            RefreshHitboxLabel();
            SetPauseMenuVisible(paused);
            if (paused)
            {
                pauseNavigation?.SetSelectedIndex(0);
                pauseNavigation?.SetInputLocked(false);
            }
        }

        public void OpenOptionsFromPause()
        {
            if (optionsPanel == null) return;
            pauseNavigation?.SetInputLocked(true);
            optionsPanel.Open();
        }

        private void HandleOptionsClosed()
        {
            _escCooldownFrames = 2;
            pauseNavigation?.SetInputLocked(false);
            RefreshHitboxLabel();
        }

        private void HandleMatchStateChanged(MatchManager manager)
        {
            bool showMatchOver = manager != null && manager.Phase == MatchPhase.MatchOver;
            SetMatchOverMenuVisible(showMatchOver);
            if (showMatchOver)
            {
                matchOverNavigation?.SetSelectedIndex(0);
            }
        }

        private void ExecutePauseAction(int index)
        {
            switch (index)
            {
                case 0: // Continuar
                    SetPaused(false);
                    break;
                case 1: // Reiniciar Luta
                    SetPaused(false);
                    matchManager?.RestartMatch();
                    break;
                case 2: // Alternar Hitboxes (F1)
                    if (vfxController != null)
                    {
                        vfxController.SetDebugBoxesVisible(!vfxController.ShowDebugBoxes);
                        RefreshHitboxLabel();
                    }
                    break;
                case 3: // Configurações
                    OpenOptionsFromPause();
                    break;
                case 4: // Trocar Mapa
                    SetPaused(false);
                    matchManager?.GoToStageSelect();
                    break;
                case 5: // Trocar Personagens
                    SetPaused(false);
                    matchManager?.GoToCharacterSelect();
                    break;
                case 6: // Menu Principal
                    SetPaused(false);
                    matchManager?.GoToMainMenu();
                    break;
            }
        }

        private void ExecuteMatchOverAction(int index)
        {
            switch (index)
            {
                case 0: // Revanche
                    SetMatchOverMenuVisible(false);
                    matchManager?.RestartMatch();
                    break;
                case 1: // Trocar Mapa
                    matchManager?.GoToStageSelect();
                    break;
                case 2: // Trocar Personagens
                    matchManager?.GoToCharacterSelect();
                    break;
                case 3: // Menu Principal
                    matchManager?.GoToMainMenu();
                    break;
            }
        }

        private void RefreshHitboxLabel()
        {
            if (_hitboxButtonLabel == null) return;
            bool on = vfxController != null && vfxController.ShowDebugBoxes;
            _hitboxButtonLabel.text = on ? "HITBOXES (F1): ON" : "HITBOXES (F1): OFF";
        }

        private void SetPauseMenuVisible(bool visible)
        {
            if (pausePanel != null) pausePanel.SetActive(visible);
        }

        private void SetMatchOverMenuVisible(bool visible)
        {
            if (matchOverPanel != null) matchOverPanel.SetActive(visible);
        }

        private static bool IsPauseInputPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                return true;
            }

            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
            {
                if (pads[i] != null && pads[i].added && pads[i].startButton.wasPressedThisFrame)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
