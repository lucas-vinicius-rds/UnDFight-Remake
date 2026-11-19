using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnDFight
{
    /// <summary>
    /// MKX-styled in-fight Pause Menu (Esc / Gamepad Start), automatic MatchOver post-fight menu
    /// (both standard modes and Arcade Tower intermediate victory / champion / defeat panels),
    /// and shared Options modal inside combat scenes.
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

        private readonly List<UnityEngine.UI.Button> _pauseButtons = new List<UnityEngine.UI.Button>();
        private RectTransform _pauseBoxRect;
        private TextMeshProUGUI _hitboxButtonLabel;
        private bool _isPaused;
        private int _escCooldownFrames;
        private Sprite _runtimeFallbackSprite;

        // Dynamic Arcade Tower post-fight modal
        private GameObject _towerMatchOverPanel;
        private RectTransform _towerMatchOverBox;
        private UnityEngine.UI.Image _towerTopAccent;
        private TextMeshProUGUI _towerTitleText;
        private TextMeshProUGUI _towerSubtitleText;
        private Transform _towerBodyContainer;
        private GameObject _towerUnlockBanner;
        private TextMeshProUGUI _towerUnlockBannerText;
        private Transform _towerButtonsContainer;
        private MenuNavigationGroup _towerMatchOverNavigation;

        public bool IsPaused => _isPaused;
        public bool IsMatchOverMenuOpen =>
            (matchOverPanel != null && matchOverPanel.activeSelf) ||
            (_towerMatchOverPanel != null && _towerMatchOverPanel.activeSelf);
        public bool IsTowerMatchOverOpen => _towerMatchOverPanel != null && _towerMatchOverPanel.activeSelf;
        public string TowerMatchOverTitle => _towerTitleText != null ? _towerTitleText.text : string.Empty;
        public bool IsBossUnlockBannerVisible => _towerUnlockBanner != null && _towerUnlockBanner.activeSelf;
        public string TowerUnlockBannerText => _towerUnlockBannerText != null ? _towerUnlockBannerText.text : string.Empty;

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
                _pauseBoxRect = pauseNavigation.GetComponent<RectTransform>();
                _pauseButtons.Clear();
                var btns = pauseNavigation.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                for (int i = 0; i < btns.Length; i++)
                {
                    _pauseButtons.Add(btns[i]);
                }
                ConfigurePauseMenuForCurrentMode();
            }

            if (matchOverNavigation != null)
            {
                var btns = matchOverNavigation.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                matchOverNavigation.ClearItems();
                for (int i = 0; i < btns.Length; i++)
                {
                    int idx = i;
                    matchOverNavigation.AddItem(BuildMenuItem(btns[i], () => ExecuteMatchOverAction(idx)));
                }
            }

            if (optionsPanel != null)
            {
                optionsPanel.OnClosed -= HandleOptionsClosed;
                optionsPanel.OnClosed += HandleOptionsClosed;
            }
        }

        private void ConfigurePauseMenuForCurrentMode()
        {
            if (pauseNavigation == null || _pauseButtons.Count == 0)
            {
                return;
            }

            bool isTower = GameSessionState.CurrentMode == GameMode.ArcadeTower;
            pauseNavigation.ClearItems();
            _hitboxButtonLabel = null;

            int visibleSlot = 0;
            for (int i = 0; i < _pauseButtons.Count; i++)
            {
                UnityEngine.UI.Button btn = _pauseButtons[i];
                if (btn == null) continue;

                // In Arcade Tower, hide "Trocar Personagens" (slot 5) and repurpose "Trocar Mapa" (slot 4) into "ABANDONAR TORRE"
                bool visible = !(isTower && i == 5);
                btn.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var label = btn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (i == 2)
                {
                    _hitboxButtonLabel = label;
                }
                else if (i == 4 && label != null)
                {
                    label.text = isTower ? "ABANDONAR TORRE" : "TROCAR MAPA";
                }
                else if (i == 5 && label != null)
                {
                    label.text = "TROCAR PERSONAGENS";
                }

                RectTransform rt = btn.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(0f, -96f - visibleSlot * 72f);
                }

                int capturedOriginalIdx = i;
                pauseNavigation.AddItem(BuildMenuItem(btn, () => ExecutePauseAction(capturedOriginalIdx)));
                visibleSlot++;
            }

            if (_pauseBoxRect != null)
            {
                _pauseBoxRect.sizeDelta = new Vector2(580f, isTower ? 568f : 640f);
            }

            pauseNavigation.OnCancel = () => SetPaused(false);
        }

        private static MenuItemEntry BuildMenuItem(UnityEngine.UI.Button btn, System.Action onSubmit)
        {
            return new MenuItemEntry
            {
                Button = btn,
                Background = btn.GetComponent<UnityEngine.UI.Image>(),
                AccentBar = btn.transform.Find("AccentBar")?.GetComponent<UnityEngine.UI.Image>(),
                AccentGlow = btn.transform.Find("AccentGlow")?.GetComponent<UnityEngine.UI.Image>(),
                Label = btn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>(),
                OnSubmit = onSubmit
            };
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
            if (paused)
            {
                ConfigurePauseMenuForCurrentMode();
            }
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

        public void HandleMatchStateChanged(MatchManager manager)
        {
            bool showMatchOver = manager != null && manager.Phase == MatchPhase.MatchOver;
            if (!showMatchOver)
            {
                SetMatchOverMenuVisible(false);
                return;
            }

            if (GameSessionState.CurrentMode == GameMode.ArcadeTower && GameSessionState.ActiveTowerRun != null)
            {
                if (matchOverPanel != null) matchOverPanel.SetActive(false);
                ShowTowerMatchOverPanel(manager, GameSessionState.ActiveTowerRun);
            }
            else
            {
                if (_towerMatchOverPanel != null) _towerMatchOverPanel.SetActive(false);
                if (matchOverPanel != null) matchOverPanel.SetActive(true);
                matchOverNavigation?.SetSelectedIndex(0);
            }
        }

        private void ShowTowerMatchOverPanel(MatchManager manager, ArcadeTowerRun run)
        {
            EnsureTowerMatchOverPanel();
            if (_towerMatchOverPanel == null)
            {
                return;
            }

            bool playerWon = manager.WinnerPlayerIndex == 0;
            bool isBossMatch = run.IsCurrentMatchBoss || !run.HasNextMatch;

            if (playerWon && isBossMatch)
            {
                // Ensures boss victory & unlock are recorded exactly once
                manager.TryRecordTowerBossVictoryIfNeeded();
                PopulateTowerChampionPanel(run);
            }
            else if (playerWon)
            {
                PopulateTowerIntermediateVictoryPanel(run);
            }
            else
            {
                PopulateTowerDefeatPanel(run);
            }

            _towerMatchOverPanel.SetActive(true);
            _towerMatchOverNavigation?.SetSelectedIndex(0, notifyAudio: false);
            _towerMatchOverNavigation?.SetInputLocked(false);
        }

        private void EnsureTowerMatchOverPanel()
        {
            if (_towerMatchOverPanel != null)
            {
                return;
            }

            Sprite sprite = ResolveWhiteSprite();
            RectTransform overlay = MenuUiFactory.CreatePanel("TowerMatchOverPanel", transform, new Color(0.02f, 0.022f, 0.032f, 0.80f), sprite, raycastTarget: true);
            MenuUiFactory.StretchFull(overlay, 0f, 0f, 0f, 0f);
            _towerMatchOverPanel = overlay.gameObject;

            _towerMatchOverBox = MenuUiFactory.CreatePanel("TowerBox", overlay, MenuUiFactory.ObsidianBg, sprite, raycastTarget: true);
            _towerMatchOverBox.anchorMin = new Vector2(0.5f, 0.5f);
            _towerMatchOverBox.anchorMax = new Vector2(0.5f, 0.5f);
            _towerMatchOverBox.pivot = new Vector2(0.5f, 0.5f);
            _towerMatchOverBox.anchoredPosition = new Vector2(0f, -20f);
            _towerMatchOverBox.sizeDelta = new Vector2(680f, 600f);
            MenuUiFactory.AddBorder(_towerMatchOverBox, MenuUiFactory.BronzeBorder, sprite, 2f);

            RectTransform topLine = MenuUiFactory.CreatePanel("TowerTopAccent", _towerMatchOverBox, MenuUiFactory.AccentGold, sprite);
            topLine.anchorMin = new Vector2(0f, 1f);
            topLine.anchorMax = new Vector2(1f, 1f);
            topLine.pivot = new Vector2(0.5f, 1f);
            topLine.sizeDelta = new Vector2(0f, 4f);
            _towerTopAccent = topLine.GetComponent<UnityEngine.UI.Image>();

            _towerTitleText = MenuUiFactory.CreateText(
                "TowerTitle",
                _towerMatchOverBox,
                "VITÓRIA NA TORRE!",
                36f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.AccentGold,
                characterSpacing: 6f);
            _towerTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _towerTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _towerTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _towerTitleText.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            _towerTitleText.rectTransform.sizeDelta = new Vector2(-40f, 46f);

            _towerSubtitleText = MenuUiFactory.CreateText(
                "TowerSubtitle",
                _towerMatchOverBox,
                string.Empty,
                17f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.84f, 0.80f, 0.72f, 1f),
                characterSpacing: 2.5f);
            _towerSubtitleText.enableAutoSizing = true;
            _towerSubtitleText.fontSizeMin = 12f;
            _towerSubtitleText.fontSizeMax = 17f;
            _towerSubtitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _towerSubtitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _towerSubtitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _towerSubtitleText.rectTransform.anchoredPosition = new Vector2(0f, -68f);
            _towerSubtitleText.rectTransform.sizeDelta = new Vector2(-44f, 30f);

            var bodyGo = new GameObject("TowerBody", typeof(RectTransform));
            bodyGo.transform.SetParent(_towerMatchOverBox, false);
            var bodyRt = (RectTransform)bodyGo.transform;
            bodyRt.anchorMin = new Vector2(0.5f, 1f);
            bodyRt.anchorMax = new Vector2(0.5f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = new Vector2(0f, -108f);
            bodyRt.sizeDelta = new Vector2(604f, 320f);
            _towerBodyContainer = bodyRt;

            // Boss Unlock celebration banner (toggled only when boss is unlocked for the first time)
            RectTransform unlockBanner = MenuUiFactory.CreatePanel("BossUnlockBanner", _towerMatchOverBox, new Color(0.24f, 0.08f, 0.05f, 0.96f), sprite);
            unlockBanner.anchorMin = new Vector2(0.5f, 1f);
            unlockBanner.anchorMax = new Vector2(0.5f, 1f);
            unlockBanner.pivot = new Vector2(0.5f, 1f);
            unlockBanner.anchoredPosition = new Vector2(0f, -264f);
            unlockBanner.sizeDelta = new Vector2(604f, 84f);
            MenuUiFactory.AddBorder(unlockBanner, MenuUiFactory.AccentGold, sprite, 2.5f);
            _towerUnlockBanner = unlockBanner.gameObject;

            _towerUnlockBannerText = MenuUiFactory.CreateText(
                "UnlockText",
                unlockBanner,
                "NOVO LUTADOR DESBLOQUEADO: IBANEIS!",
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.AccentGold,
                characterSpacing: 3.5f);
            _towerUnlockBannerText.enableAutoSizing = true;
            _towerUnlockBannerText.fontSizeMin = 14f;
            _towerUnlockBannerText.fontSizeMax = 22f;
            _towerUnlockBannerText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _towerUnlockBannerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _towerUnlockBannerText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _towerUnlockBannerText.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            _towerUnlockBannerText.rectTransform.sizeDelta = new Vector2(-24f, 36f);

            TextMeshProUGUI unlockSub = MenuUiFactory.CreateText(
                "UnlockSub",
                unlockBanner,
                "CHEFE FINAL LIBERADO EM TODOS OS MODOS DE JOGO",
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(1.0f, 0.92f, 0.72f, 1f),
                characterSpacing: 2.5f);
            unlockSub.rectTransform.anchorMin = new Vector2(0f, 0f);
            unlockSub.rectTransform.anchorMax = new Vector2(1f, 0f);
            unlockSub.rectTransform.pivot = new Vector2(0.5f, 0f);
            unlockSub.rectTransform.anchoredPosition = new Vector2(0f, 10f);
            unlockSub.rectTransform.sizeDelta = new Vector2(-24f, 24f);
            _towerUnlockBanner.SetActive(false);

            var btnGo = new GameObject("TowerButtons", typeof(RectTransform));
            btnGo.transform.SetParent(_towerMatchOverBox, false);
            var btnRt = (RectTransform)btnGo.transform;
            btnRt.anchorMin = new Vector2(0.5f, 0f);
            btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 18f);
            btnRt.sizeDelta = new Vector2(540f, 200f);
            _towerButtonsContainer = btnRt;
            _towerMatchOverNavigation = btnGo.AddComponent<MenuNavigationGroup>();

            _towerMatchOverPanel.SetActive(false);
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        private void PopulateTowerIntermediateVictoryPanel(ArcadeTowerRun run)
        {
            Sprite sprite = ResolveWhiteSprite();
            ClearContainer(_towerBodyContainer);
            ClearContainer(_towerButtonsContainer);
            _towerMatchOverNavigation.ClearItems();
            if (_towerUnlockBanner != null) _towerUnlockBanner.SetActive(false);

            _towerMatchOverBox.sizeDelta = new Vector2(680f, 590f);
            if (_towerTopAccent != null) _towerTopAccent.color = MenuUiFactory.AccentGold;
            _towerTitleText.text = "VITÓRIA NA TORRE!";
            _towerTitleText.color = MenuUiFactory.AccentGold;

            ArcadeTowerStageEntry current = run.CurrentMatch;
            ArcadeTowerStageEntry next = run.NextMatch;
            string nextOpp = next != null && next.opponent != null ? next.opponent.fighterName.ToUpperInvariant() : "???";
            string nextStage = next != null ? next.stageDisplayName : string.Empty;
            _towerSubtitleText.text = $"DEGRAU {run.CurrentMatchIndex + 1}/{run.TotalMatches} VENCIDO  •  PRÓXIMO: {nextOpp} ({nextStage})";

            // Render 7-step mini ladder (top = 7 Boss, bottom = 1)
            float rowH = 38f;
            float rowStep = 43f;
            int total = run.TotalMatches;
            for (int visualRow = 0; visualRow < total; visualRow++)
            {
                int matchIdx = total - 1 - visualRow;
                ArcadeTowerStageEntry step = run.Matches[matchIdx];
                bool defeated = matchIdx <= run.CurrentMatchIndex;
                bool isNext = matchIdx == run.CurrentMatchIndex + 1;

                Color rowBg = isNext
                    ? new Color(0.24f, 0.16f, 0.06f, 0.96f)
                    : (step.isBossMatch
                        ? new Color(0.18f, 0.06f, 0.06f, 0.94f)
                        : (defeated ? new Color(0.06f, 0.10f, 0.08f, 0.85f) : MenuUiFactory.PanelDarkBg));

                Color rowBorder = isNext
                    ? MenuUiFactory.AccentGold
                    : (step.isBossMatch ? MenuUiFactory.AccentCrimson : MenuUiFactory.BronzeBorder);

                RectTransform rowRt = MenuUiFactory.CreatePanel($"LadderRow_{matchIdx}", _towerBodyContainer, rowBg, sprite);
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -visualRow * rowStep);
                rowRt.sizeDelta = new Vector2(0f, rowH);
                MenuUiFactory.AddBorder(rowRt, rowBorder, sprite, isNext ? 2f : 1f);

                string prefix = defeated ? "[OK] " : (isNext ? "► " : "  ");
                string oppName = step.opponent != null ? step.opponent.fighterName.ToUpperInvariant() : "???";
                string weather = step.GetWeatherDisplayLabel();
                string rowLabel = step.isBossMatch
                    ? $"{prefix}{matchIdx + 1} • CHEFE FINAL: {oppName} • {step.stageDisplayName}"
                    : $"{prefix}{matchIdx + 1} • {step.ladderLabel}: {oppName} • {step.stageDisplayName} ({weather})";

                Color textColor = isNext
                    ? MenuUiFactory.AccentGold
                    : (defeated
                        ? new Color(0.52f, 0.78f, 0.56f, 0.92f)
                        : (step.isBossMatch ? new Color(1.0f, 0.78f, 0.58f, 1f) : new Color(0.80f, 0.80f, 0.78f, 1f)));

                TextMeshProUGUI txt = MenuUiFactory.CreateText("RowText", rowRt, rowLabel, 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor, characterSpacing: 1.5f);
                txt.enableAutoSizing = true;
                txt.fontSizeMin = 11f;
                txt.fontSizeMax = 15f;
                txt.textWrappingMode = TextWrappingModes.NoWrap;
                MenuUiFactory.StretchFull(txt.rectTransform, 14f, 2f, 14f, 2f);
            }

            AddTowerActionButtons(new (string label, System.Action action)[]
            {
                ("PRÓXIMA LUTA", () => AdvanceTowerToNextMatch(run)),
                ("MENU PRINCIPAL", () => matchManager?.GoToMainMenu())
            });
        }

        private void PopulateTowerChampionPanel(ArcadeTowerRun run)
        {
            Sprite sprite = ResolveWhiteSprite();
            ClearContainer(_towerBodyContainer);
            ClearContainer(_towerButtonsContainer);
            _towerMatchOverNavigation.ClearItems();

            bool unlockedNow = run.UnlockedBossThisRun;
            _towerMatchOverBox.sizeDelta = new Vector2(680f, unlockedNow ? 600f : 520f);
            if (_towerTopAccent != null) _towerTopAccent.color = MenuUiFactory.AccentGold;

            string champName = run.PlayerFighter != null ? run.PlayerFighter.fighterName.ToUpperInvariant() : "CAMPEÃO";
            string diffLabel = GameSessionState.GetCpuDifficultyLabel(run.BaseDifficulty);
            int completions = ProgressionManager.Current.towerCompletionsCount;

            _towerTitleText.text = "CAMPEÃO DA TORRE!";
            _towerTitleText.color = MenuUiFactory.AccentGold;
            _towerSubtitleText.text = $"{champName} DERROTOU O CHEFE FINAL IBANEIS!";

            RectTransform statsBox = MenuUiFactory.CreatePanel("ChampionStats", _towerBodyContainer, MenuUiFactory.PanelDarkBg, sprite);
            statsBox.anchorMin = new Vector2(0f, 1f);
            statsBox.anchorMax = new Vector2(1f, 1f);
            statsBox.pivot = new Vector2(0.5f, 1f);
            statsBox.anchoredPosition = Vector2.zero;
            statsBox.sizeDelta = new Vector2(0f, 138f);
            MenuUiFactory.AddBorder(statsBox, MenuUiFactory.BronzeBorder, sprite, 1.5f);

            string statsBody =
                $"CAMPEÃO: <color=#FFB800>{champName}</color>\n" +
                $"DIFICULDADE: <color=#FFB800>{diffLabel}</color>   •   CONTINUES USADOS: <color=#FFB800>{run.ContinuesUsed}</color>\n" +
                $"TORRES CONCLUÍDAS: <color=#FFB800>{completions}</color>";
            TextMeshProUGUI statsTmp = MenuUiFactory.CreateText("StatsText", statsBox, statsBody, 20f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, characterSpacing: 2f);
            statsTmp.lineSpacing = 14f;
            MenuUiFactory.StretchFull(statsTmp.rectTransform, 18f, 12f, 18f, 12f);

            if (_towerUnlockBanner != null)
            {
                _towerUnlockBanner.SetActive(unlockedNow);
                if (unlockedNow && _towerUnlockBannerText != null)
                {
                    string bossName = run.BossFighter != null ? run.BossFighter.fighterName.ToUpperInvariant() : "IBANEIS";
                    _towerUnlockBannerText.text = $"NOVO LUTADOR DESBLOQUEADO: {bossName}!";
                }
            }

            AddTowerActionButtons(new (string label, System.Action action)[]
            {
                ("NOVA TORRE", () =>
                {
                    GameSessionState.ClearTowerRun();
                    GameSessionState.SelectMode(GameMode.ArcadeTower);
                    GameSessionState.OpenStageSelectOnCharacterSelect = false;
                    SceneManager.LoadScene("CharacterSelect");
                }),
                ("SELEÇÃO DE LUTADORES", () =>
                {
                    GameSessionState.ClearTowerRun();
                    GameSessionState.SelectMode(GameMode.LocalVersus);
                    GameSessionState.OpenStageSelectOnCharacterSelect = false;
                    SceneManager.LoadScene("CharacterSelect");
                }),
                ("MENU PRINCIPAL", () => matchManager?.GoToMainMenu())
            });
        }

        private void PopulateTowerDefeatPanel(ArcadeTowerRun run)
        {
            Sprite sprite = ResolveWhiteSprite();
            ClearContainer(_towerBodyContainer);
            ClearContainer(_towerButtonsContainer);
            _towerMatchOverNavigation.ClearItems();
            if (_towerUnlockBanner != null) _towerUnlockBanner.SetActive(false);

            _towerMatchOverBox.sizeDelta = new Vector2(640f, 440f);
            if (_towerTopAccent != null) _towerTopAccent.color = MenuUiFactory.AccentCrimson;

            ArcadeTowerStageEntry current = run.CurrentMatch;
            string oppName = current != null && current.opponent != null ? current.opponent.fighterName.ToUpperInvariant() : "OPONENTE";
            string stageName = current != null ? current.stageDisplayName : string.Empty;

            _towerTitleText.text = "DERROTA NA TORRE";
            _towerTitleText.color = MenuUiFactory.AccentCrimson;
            _towerSubtitleText.text = $"DEGRAU {run.CurrentMatchIndex + 1}/{run.TotalMatches}  •  {oppName} VENCEU EM {stageName}";

            RectTransform infoCard = MenuUiFactory.CreatePanel("DefeatCard", _towerBodyContainer, new Color(0.12f, 0.055f, 0.06f, 0.92f), sprite);
            infoCard.anchorMin = new Vector2(0f, 1f);
            infoCard.anchorMax = new Vector2(1f, 1f);
            infoCard.pivot = new Vector2(0.5f, 1f);
            infoCard.anchoredPosition = Vector2.zero;
            infoCard.sizeDelta = new Vector2(0f, 76f);
            MenuUiFactory.AddBorder(infoCard, MenuUiFactory.AccentCrimson, sprite, 1.5f);

            TextMeshProUGUI infoTmp = MenuUiFactory.CreateText(
                "DefeatSummary",
                infoCard,
                $"CONTINUES USADOS ATÉ AGORA: {run.ContinuesUsed}   •   USE CONTINUE PARA TENTAR ESTE DEGRAU NOVAMENTE",
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.95f, 0.86f, 0.80f, 1f),
                characterSpacing: 1.5f);
            infoTmp.enableAutoSizing = true;
            infoTmp.fontSizeMin = 12f;
            infoTmp.fontSizeMax = 15f;
            MenuUiFactory.StretchFull(infoTmp.rectTransform, 14f, 8f, 14f, 8f);

            AddTowerActionButtons(new (string label, System.Action action)[]
            {
                ("TENTAR NOVAMENTE (CONTINUE)", () =>
                {
                    run.RecordContinueUsed();
                    SetMatchOverMenuVisible(false);
                    matchManager?.RestartMatch();
                }),
                ("REINICIAR TORRE", () =>
                {
                    run.ResetToFirstMatch();
                    run.ApplyCurrentMatchToSession();
                    string firstScene = run.CurrentMatch != null ? run.CurrentMatch.stageSceneName : GameSessionState.DefaultStageSceneName;
                    SceneManager.LoadScene(firstScene);
                }),
                ("MENU PRINCIPAL", () => matchManager?.GoToMainMenu())
            });
        }

        private void AddTowerActionButtons((string label, System.Action action)[] actions)
        {
            Sprite sprite = ResolveWhiteSprite();
            float btnHeight = actions.Length >= 3 ? 52f : 56f;
            float btnStep = actions.Length >= 3 ? 62f : 68f;
            float totalHeight = actions.Length * btnStep - (btnStep - btnHeight);

            var containerRt = _towerButtonsContainer as RectTransform;
            if (containerRt != null)
            {
                containerRt.sizeDelta = new Vector2(540f, totalHeight + 8f);
            }

            for (int i = 0; i < actions.Length; i++)
            {
                MenuItemEntry entry = MenuUiFactory.CreateMenuButton(
                    $"TowerActionBtn_{i}",
                    _towerButtonsContainer,
                    actions[i].label,
                    sprite,
                    fontSize: 21f,
                    alignment: TextAlignmentOptions.Center,
                    characterSpacing: 4.5f);

                RectTransform rt = entry.Button.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -i * btnStep);
                rt.sizeDelta = new Vector2(520f, btnHeight);
                entry.OnSubmit = actions[i].action;
                _towerMatchOverNavigation.AddItem(entry);
            }
        }

        private void AdvanceTowerToNextMatch(ArcadeTowerRun run)
        {
            if (run == null || !run.AdvanceToNextMatch())
            {
                matchManager?.GoToMainMenu();
                return;
            }

            run.ApplyCurrentMatchToSession();
            string nextScene = run.CurrentMatch != null ? run.CurrentMatch.stageSceneName : GameSessionState.DefaultStageSceneName;
            SceneManager.LoadScene(nextScene);
        }

        private void ExecutePauseAction(int index)
        {
            bool isTower = GameSessionState.CurrentMode == GameMode.ArcadeTower;
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
                case 4: // Trocar Mapa (or Abandonar Torre in ArcadeTower)
                    SetPaused(false);
                    if (isTower)
                    {
                        GameSessionState.ClearTowerRun();
                        matchManager?.GoToCharacterSelect();
                    }
                    else
                    {
                        matchManager?.GoToStageSelect();
                    }
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
            if (!visible && _towerMatchOverPanel != null)
            {
                _towerMatchOverPanel.SetActive(false);
            }
        }

        private Sprite ResolveWhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            if (pausePanel != null)
            {
                var img = pausePanel.GetComponent<UnityEngine.UI.Image>();
                if (img != null && img.sprite != null) return img.sprite;
            }
            if (matchOverPanel != null)
            {
                var img = matchOverPanel.GetComponent<UnityEngine.UI.Image>();
                if (img != null && img.sprite != null) return img.sprite;
            }
            if (_runtimeFallbackSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _runtimeFallbackSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            }
            return _runtimeFallbackSprite;
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
