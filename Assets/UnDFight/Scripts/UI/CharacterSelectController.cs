using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnDFight
{
    /// <summary>
    /// MKX-styled Character Select, Stage Select, and Arcade Tower Ladder controller:
    /// - Locked fighters (e.g. secret boss Ibaneis before clearing the Tower) show "???" / "BLOQUEADO",
    ///   hide their 3D preview, and cannot be confirmed until unlocked via ProgressionManager.
    /// - Standard modes: FighterPick -> StageSelect (with Weather selector).
    /// - ArcadeTower mode: P1 picks 1 fighter + Tower difficulty -> TowerLadderPanel (7 matches) -> Start Fight.
    /// </summary>
    public class CharacterSelectController : MonoBehaviour
    {
        public enum SelectStep
        {
            FighterPick = 0,
            StageSelect = 1,
            TowerLadder = 2
        }

        private sealed class RosterCardView
        {
            public int Index;
            public FighterData Data;
            public UnityEngine.UI.Button Button;
            public UnityEngine.UI.Image Background;
            public UnityEngine.UI.Image P1Outline;
            public UnityEngine.UI.Image P2Outline;
            public UnityEngine.UI.Image[] BorderImages;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI BadgeText;
        }

        public const string LockedUnlockHintMessage = "DERROTE O CHEFE FINAL NO MODO TORRE PARA DESBLOQUEAR";

        [Header("Data & Scene Flow")]
        [SerializeField] private FighterRoster fighterRoster;
        [SerializeField] private StageRoster stageRoster;
        [SerializeField] private string fallbackFightSceneName = "Training";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private float stageStepTransitionSeconds = 0.35f;
        [SerializeField] private Sprite whiteSprite;

        [Header("3D Preview Anchors")]
        [SerializeField] private Transform p1PreviewAnchor;
        [SerializeField] private Transform p2PreviewAnchor;

        [Header("UI References")]
        [SerializeField] private GameObject rosterGridPanel;
        [SerializeField] private Transform gridContainer;
        [SerializeField] private GameObject stageSelectPanel;
        [SerializeField] private MenuNavigationGroup stageNavigation;
        [SerializeField] private UnityEngine.UI.Image stagePreviewBanner;
        [SerializeField] private TextMeshProUGUI stagePreviewTitle;
        [SerializeField] private TextMeshProUGUI stagePreviewSubtitle;
        [SerializeField] private GameObject weatherSelectorBar;
        [SerializeField] private UnityEngine.UI.Button weatherPrevButton;
        [SerializeField] private UnityEngine.UI.Button weatherNextButton;
        [SerializeField] private TextMeshProUGUI weatherLabelText;
        [SerializeField] private TextMeshProUGUI modeBannerText;
        [SerializeField] private TextMeshProUGUI footerHintText;
        [SerializeField] private TextMeshProUGUI p1HeaderText;
        [SerializeField] private TextMeshProUGUI p1NameText;
        [SerializeField] private TextMeshProUGUI p1HeightText;
        [SerializeField] private TextMeshProUGUI p1WeightText;
        [SerializeField] private TextMeshProUGUI p1StatusText;
        [SerializeField] private TextMeshProUGUI p2HeaderText;
        [SerializeField] private TextMeshProUGUI p2NameText;
        [SerializeField] private TextMeshProUGUI p2HeightText;
        [SerializeField] private TextMeshProUGUI p2WeightText;
        [SerializeField] private TextMeshProUGUI p2StatusText;

        private readonly List<RosterCardView> _cards = new List<RosterCardView>();
        private readonly List<StageData> _stageChoices = new List<StageData>();
        private int _p1Index;
        private int _p2Index;
        private bool _p1Confirmed;
        private bool _p2Confirmed;
        private float _stageTransitionTimer = -1f;
        private SelectStep _currentStep = SelectStep.FighterPick;
        private int _lastStageNavIndex = -1;
        private bool _weatherManuallyChosenInStageSelect;

        private GameObject _p1PreviewInstance;
        private GameObject _p2PreviewInstance;
        private Animator _p1PreviewAnimator;
        private Animator _p2PreviewAnimator;
        private FighterData _p1SpawnedData;
        private FighterData _p2SpawnedData;

        // Dynamic Arcade Tower Ladder overlay & Difficulty Selector Pill
        private GameObject _towerLadderPanel;
        private TextMeshProUGUI _towerLadderSubtitle;
        private Transform _towerLadderRowsContainer;
        private MenuNavigationGroup _towerLadderNavigation;
        private GameObject _difficultyBar;
        private TextMeshProUGUI _difficultyBarLabel;
        private Sprite _runtimeFallbackSprite;

        private float _p1RepeatCooldown;
        private float _p2RepeatCooldown;
        private int _inputCooldownFrames;
        private bool _built;

        private static readonly int StateParamHash = Animator.StringToHash("State");

        public int P1Index => _p1Index;
        public int P2Index => _p2Index;
        public bool P1Confirmed => _p1Confirmed;
        public bool P2Confirmed => _p2Confirmed;
        public SelectStep CurrentStep => _currentStep;
        public TextMeshProUGUI WeatherLabelText => weatherLabelText;
        public TextMeshProUGUI P1NameText => p1NameText;
        public TextMeshProUGUI P1StatusText => p1StatusText;
        public GameObject TowerLadderPanel => _towerLadderPanel;
        public bool IsTowerLadderOpen => _towerLadderPanel != null && _towerLadderPanel.activeSelf;
        public bool HasP1PreviewInstance => _p1PreviewInstance != null;
        public bool HasP2PreviewInstance => _p2PreviewInstance != null;

        private void Awake()
        {
            if (gridContainer == null || stageSelectPanel == null)
            {
                if (whiteSprite != null && transform.Find("TopHeader") == null)
                {
                    BuildUI(transform, whiteSprite);
                }
            }
            else
            {
                EnsureRuntimeStageOptions();
            }
        }

        private void Start()
        {
            if (!_built && gridContainer != null)
            {
                RebuildRosterGrid();
                RebuildStageList();
                _built = true;
            }

            if (GameSessionState.OpenStageSelectOnCharacterSelect && GameSessionState.CurrentMode != GameMode.ArcadeTower)
            {
                GameSessionState.OpenStageSelectOnCharacterSelect = false;
                RestoreSessionFightersAndOpenStageSelect();
            }
            else
            {
                GameSessionState.OpenStageSelectOnCharacterSelect = false;
                _p1Index = FindFirstUnlockedIndex(0);
                _p2Index = FindFirstUnlockedIndex(fighterRoster != null && fighterRoster.Count > 1 ? 1 : 0);
                _p1Confirmed = false;
                _p2Confirmed = false;
                _stageTransitionTimer = -1f;
                EnterFighterPickStep();
            }
        }

        public void ConfigureReferences(
            FighterRoster roster,
            StageRoster stages,
            Sprite sprite,
            Transform p1Anchor,
            Transform p2Anchor)
        {
            fighterRoster = roster;
            stageRoster = stages;
            whiteSprite = sprite;
            p1PreviewAnchor = p1Anchor;
            p2PreviewAnchor = p2Anchor;
        }

        private void EnsureRuntimeStageOptions()
        {
            if (_stageChoices.Count == 0 && stageNavigation != null)
            {
                RebuildStageList();
            }
        }

        public void BuildUI(Transform canvasRoot, Sprite sprite)
        {
            whiteSprite = sprite;

            // Top MKX Header Banner
            RectTransform topHeader = MenuUiFactory.CreatePanel("TopHeader", canvasRoot, MenuUiFactory.ObsidianBg, whiteSprite);
            topHeader.anchorMin = new Vector2(0f, 1f);
            topHeader.anchorMax = new Vector2(1f, 1f);
            topHeader.pivot = new Vector2(0.5f, 1f);
            topHeader.anchoredPosition = new Vector2(0f, -14f);
            topHeader.sizeDelta = new Vector2(-80f, 70f);
            MenuUiFactory.AddBorder(topHeader, MenuUiFactory.BronzeBorder, whiteSprite, 1.5f);

            RectTransform topGold = MenuUiFactory.CreatePanel("TopGold", topHeader, MenuUiFactory.AccentGold, whiteSprite);
            topGold.anchorMin = new Vector2(0f, 1f);
            topGold.anchorMax = new Vector2(1f, 1f);
            topGold.pivot = new Vector2(0.5f, 1f);
            topGold.sizeDelta = new Vector2(0f, 3f);

            modeBannerText = MenuUiFactory.CreateText(
                "ModeTitle",
                topHeader,
                "ESCOLHA DOS LUTADORES",
                30f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.AccentGold,
                characterSpacing: 7f);
            modeBannerText.enableAutoSizing = true;
            modeBannerText.fontSizeMin = 18f;
            modeBannerText.fontSizeMax = 30f;
            MenuUiFactory.StretchFull(modeBannerText.rectTransform, 16f, 4f, 16f, 4f);

            // P1 & P2 MKX Info Cards
            CreateInfoCard(canvasRoot, true, out p1HeaderText, out p1NameText, out p1HeightText, out p1WeightText, out p1StatusText);
            CreateInfoCard(canvasRoot, false, out p2HeaderText, out p2NameText, out p2HeightText, out p2WeightText, out p2StatusText);

            // Step 1: Roster Grid Container (Bottom Center)
            RectTransform gridPanel = MenuUiFactory.CreatePanel("RosterGridPanel", canvasRoot, MenuUiFactory.ObsidianBg, whiteSprite);
            gridPanel.anchorMin = new Vector2(0.5f, 0f);
            gridPanel.anchorMax = new Vector2(0.5f, 0f);
            gridPanel.pivot = new Vector2(0.5f, 0f);
            gridPanel.anchoredPosition = new Vector2(0f, 62f);
            gridPanel.sizeDelta = new Vector2(920f, 224f);
            MenuUiFactory.AddBorder(gridPanel, MenuUiFactory.BronzeBorder, whiteSprite, 2f);
            rosterGridPanel = gridPanel.gameObject;

            RectTransform gridAccent = MenuUiFactory.CreatePanel("GridTopAccent", gridPanel, MenuUiFactory.AccentAmber, whiteSprite);
            gridAccent.anchorMin = new Vector2(0f, 1f);
            gridAccent.anchorMax = new Vector2(1f, 1f);
            gridAccent.pivot = new Vector2(0.5f, 1f);
            gridAccent.sizeDelta = new Vector2(0f, 3f);

            var gridGo = new GameObject("Grid", typeof(RectTransform));
            gridGo.transform.SetParent(gridPanel, false);
            MenuUiFactory.StretchFull(gridGo.GetComponent<RectTransform>(), 20f, 16f, 20f, 18f);

            var glg = gridGo.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            glg.cellSize = new Vector2(204f, 90f);
            glg.spacing = new Vector2(16f, 12f);
            glg.childAlignment = TextAnchor.MiddleCenter;
            glg.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;
            gridContainer = gridGo.transform;

            // Step 2: Center Stage Select Overlay Panel (between left & right confirmed fighters)
            BuildStageSelectOverlay(canvasRoot);

            // Footer Bar
            RectTransform footerBar = MenuUiFactory.CreatePanel("SelectFooterBar", canvasRoot, MenuUiFactory.ObsidianBg, whiteSprite);
            footerBar.anchorMin = new Vector2(0f, 0f);
            footerBar.anchorMax = new Vector2(1f, 0f);
            footerBar.pivot = new Vector2(0.5f, 0f);
            footerBar.anchoredPosition = Vector2.zero;
            footerBar.sizeDelta = new Vector2(0f, 44f);
            MenuUiFactory.AddBorder(footerBar, MenuUiFactory.BronzeBorder, whiteSprite, 1f);

            footerHintText = MenuUiFactory.CreateText(
                "SelectFooter",
                footerBar,
                "P1: WASD + J/ENTER (K/ESC VOLTA)   •   P2: SETAS + NUM4/ENTER (NUM5 VOLTA)   •   MOUSE & GAMEPAD",
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.70f, 0.67f, 0.62f, 1f),
                characterSpacing: 3.5f);
            MenuUiFactory.StretchFull(footerHintText.rectTransform, 20f, 2f, 20f, 2f);

            RebuildRosterGrid();
            RebuildStageList();
            EnterFighterPickStep();
            _built = true;
        }

        private void BuildStageSelectOverlay(Transform canvasRoot)
        {
            RectTransform stageBox = MenuUiFactory.CreatePanel("StageSelectPanel", canvasRoot, MenuUiFactory.ObsidianBg, whiteSprite, raycastTarget: true);
            stageBox.anchorMin = new Vector2(0.5f, 0.50f);
            stageBox.anchorMax = new Vector2(0.5f, 0.50f);
            stageBox.pivot = new Vector2(0.5f, 0.5f);
            stageBox.sizeDelta = new Vector2(620f, 640f);
            MenuUiFactory.AddBorder(stageBox, MenuUiFactory.BronzeBorder, whiteSprite, 2f);
            stageSelectPanel = stageBox.gameObject;

            RectTransform topLine = MenuUiFactory.CreatePanel("StageTopLine", stageBox, MenuUiFactory.AccentGold, whiteSprite);
            topLine.anchorMin = new Vector2(0f, 1f);
            topLine.anchorMax = new Vector2(1f, 1f);
            topLine.pivot = new Vector2(0.5f, 1f);
            topLine.sizeDelta = new Vector2(0f, 4f);

            TextMeshProUGUI title = MenuUiFactory.CreateText(
                "StageHeading",
                stageBox,
                "SELECIONE A ARENA",
                30f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.AccentGold,
                characterSpacing: 7f);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            title.rectTransform.sizeDelta = new Vector2(-40f, 44f);

            // Visual Stage Preview Card inside StageSelectPanel
            RectTransform previewCard = MenuUiFactory.CreatePanel("StagePreviewCard", stageBox, new Color(0.55f, 0.28f, 0.12f, 1f), whiteSprite);
            previewCard.anchorMin = new Vector2(0.5f, 1f);
            previewCard.anchorMax = new Vector2(0.5f, 1f);
            previewCard.pivot = new Vector2(0.5f, 1f);
            previewCard.anchoredPosition = new Vector2(0f, -74f);
            previewCard.sizeDelta = new Vector2(548f, 152f);
            MenuUiFactory.AddBorder(previewCard, MenuUiFactory.AccentAmber, whiteSprite, 2f);
            stagePreviewBanner = previewCard.GetComponent<UnityEngine.UI.Image>();

            // Dark scrim for crisp high-contrast text over any preview tint
            RectTransform scrim = MenuUiFactory.CreatePanel("Scrim", previewCard, new Color(0.03f, 0.035f, 0.05f, 0.68f), whiteSprite);
            MenuUiFactory.StretchFull(scrim, 0f, 0f, 0f, 0f);

            stagePreviewTitle = MenuUiFactory.CreateText(
                "StagePreviewTitle",
                previewCard,
                "FAVELA CARIOCA",
                34f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                Color.white,
                characterSpacing: 6f);
            stagePreviewTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            stagePreviewTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            stagePreviewTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            stagePreviewTitle.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            stagePreviewTitle.rectTransform.sizeDelta = new Vector2(-32f, 48f);

            stagePreviewSubtitle = MenuUiFactory.CreateText(
                "StagePreviewSub",
                previewCard,
                "Bar do Jorge • Pôr do Sol",
                20f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Color(1.0f, 0.86f, 0.54f, 1f),
                characterSpacing: 2f);
            stagePreviewSubtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            stagePreviewSubtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            stagePreviewSubtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            stagePreviewSubtitle.rectTransform.anchoredPosition = new Vector2(0f, -92f);
            stagePreviewSubtitle.rectTransform.sizeDelta = new Vector2(-32f, 34f);

            stageNavigation = stageBox.gameObject.AddComponent<MenuNavigationGroup>();
            EnsureWeatherSelectorBar();
            stageSelectPanel.SetActive(false);
        }

        private void EnsureDifficultyBar()
        {
            if (_difficultyBar != null)
            {
                return;
            }

            Transform existing = transform.Find("DifficultySelectorBar");
            if (existing != null)
            {
                _difficultyBar = existing.gameObject;
                _difficultyBarLabel = existing.Find("DifficultyLabel")?.GetComponent<TextMeshProUGUI>();
                return;
            }

            Sprite sprite = ResolveWhiteSprite();
            RectTransform bar = MenuUiFactory.CreatePanel("DifficultySelectorBar", transform, MenuUiFactory.ObsidianBg, sprite, raycastTarget: true);
            bar.anchorMin = new Vector2(0.5f, 0f);
            bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 294f);
            bar.sizeDelta = new Vector2(460f, 40f);
            MenuUiFactory.AddBorder(bar, MenuUiFactory.BronzeBorder, sprite, 1.5f);
            _difficultyBar = bar.gameObject;

            UnityEngine.UI.Button prevBtn = CreateArrowButton("DiffPrevBtn", bar, "<", isLeft: true, sprite);
            UnityEngine.UI.Button nextBtn = CreateArrowButton("DiffNextBtn", bar, ">", isLeft: false, sprite);
            prevBtn.onClick.AddListener(() => CycleDifficultyAndRefresh(-1));
            nextBtn.onClick.AddListener(() => CycleDifficultyAndRefresh(1));

            _difficultyBarLabel = MenuUiFactory.CreateText(
                "DifficultyLabel",
                bar,
                "DIFICULDADE: MÉDIO",
                17f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.AccentGold,
                characterSpacing: 3.5f);
            MenuUiFactory.StretchFull(_difficultyBarLabel.rectTransform, 58f, 2f, 58f, 2f);
            _difficultyBar.SetActive(false);
        }

        private void EnsureTowerLadderPanel()
        {
            if (_towerLadderPanel != null)
            {
                return;
            }

            Transform existing = transform.Find("TowerLadderPanel");
            if (existing != null)
            {
                _towerLadderPanel = existing.gameObject;
                _towerLadderSubtitle = existing.Find("TowerSubtitle")?.GetComponent<TextMeshProUGUI>();
                _towerLadderRowsContainer = existing.Find("TowerRows");
                _towerLadderNavigation = existing.GetComponent<MenuNavigationGroup>();
                return;
            }

            Sprite sprite = ResolveWhiteSprite();
            RectTransform box = MenuUiFactory.CreatePanel("TowerLadderPanel", transform, MenuUiFactory.ObsidianBg, sprite, raycastTarget: true);
            box.anchorMin = new Vector2(0.5f, 0.50f);
            box.anchorMax = new Vector2(0.5f, 0.50f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(680f, 650f);
            MenuUiFactory.AddBorder(box, MenuUiFactory.BronzeBorder, sprite, 2f);
            _towerLadderPanel = box.gameObject;

            RectTransform topLine = MenuUiFactory.CreatePanel("TowerTopLine", box, MenuUiFactory.AccentGold, sprite);
            topLine.anchorMin = new Vector2(0f, 1f);
            topLine.anchorMax = new Vector2(1f, 1f);
            topLine.pivot = new Vector2(0.5f, 1f);
            topLine.sizeDelta = new Vector2(0f, 4f);

            TextMeshProUGUI title = MenuUiFactory.CreateText(
                "TowerHeading",
                box,
                "TORRE ARCADE • 7 LUTAS",
                30f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.AccentGold,
                characterSpacing: 6f);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            title.rectTransform.sizeDelta = new Vector2(-36f, 42f);

            _towerLadderSubtitle = MenuUiFactory.CreateText(
                "TowerSubtitle",
                box,
                "LUTADOR: LUCAS  •  DIFICULDADE: MÉDIO",
                16f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.86f, 0.80f, 0.70f, 1f),
                characterSpacing: 3f);
            _towerLadderSubtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _towerLadderSubtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _towerLadderSubtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            _towerLadderSubtitle.rectTransform.anchoredPosition = new Vector2(0f, -60f);
            _towerLadderSubtitle.rectTransform.sizeDelta = new Vector2(-36f, 28f);

            var rowsGo = new GameObject("TowerRows", typeof(RectTransform));
            rowsGo.transform.SetParent(box, false);
            var rowsRt = (RectTransform)rowsGo.transform;
            rowsRt.anchorMin = new Vector2(0.5f, 1f);
            rowsRt.anchorMax = new Vector2(0.5f, 1f);
            rowsRt.pivot = new Vector2(0.5f, 1f);
            rowsRt.anchoredPosition = new Vector2(0f, -98f);
            rowsRt.sizeDelta = new Vector2(616f, 386f);
            _towerLadderRowsContainer = rowsRt;

            _towerLadderNavigation = box.gameObject.AddComponent<MenuNavigationGroup>();
            MenuItemEntry startBtn = MenuUiFactory.CreateMenuButton("TowerStartBtn", box, "INICIAR LUTA", sprite, 22f, TextAlignmentOptions.Center, characterSpacing: 6f);
            RectTransform startRt = startBtn.Button.GetComponent<RectTransform>();
            startRt.anchorMin = new Vector2(0.5f, 0f);
            startRt.anchorMax = new Vector2(0.5f, 0f);
            startRt.pivot = new Vector2(0.5f, 0f);
            startRt.anchoredPosition = new Vector2(0f, 82f);
            startRt.sizeDelta = new Vector2(548f, 54f);
            startBtn.OnSubmit = StartCurrentTowerMatch;
            _towerLadderNavigation.AddItem(startBtn);

            MenuItemEntry backBtn = MenuUiFactory.CreateMenuButton("TowerBackBtn", box, "VOLTAR", sprite, 20f, TextAlignmentOptions.Center, characterSpacing: 6f);
            RectTransform backRt = backBtn.Button.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0.5f, 0f);
            backRt.anchorMax = new Vector2(0.5f, 0f);
            backRt.pivot = new Vector2(0.5f, 0f);
            backRt.anchoredPosition = new Vector2(0f, 18f);
            backRt.sizeDelta = new Vector2(548f, 52f);
            backBtn.OnSubmit = ReturnToFighterPickFromTowerLadder;
            _towerLadderNavigation.AddItem(backBtn);

            _towerLadderNavigation.OnCancel = ReturnToFighterPickFromTowerLadder;
            _towerLadderPanel.SetActive(false);
        }

        private void PopulateTowerLadderRows(ArcadeTowerRun run)
        {
            if (_towerLadderRowsContainer == null || run == null)
            {
                return;
            }

            for (int i = _towerLadderRowsContainer.childCount - 1; i >= 0; i--)
            {
                Transform c = _towerLadderRowsContainer.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            Sprite sprite = ResolveWhiteSprite();
            string p1Name = run.PlayerFighter != null ? run.PlayerFighter.fighterName.ToUpperInvariant() : "P1";
            string diffLabel = GameSessionState.GetCpuDifficultyLabel(run.BaseDifficulty);
            if (_towerLadderSubtitle != null)
            {
                _towerLadderSubtitle.text = $"LUTADOR: {p1Name}   •   DIFICULDADE: {diffLabel} (TAB ALTERA)";
            }

            int total = run.TotalMatches;
            float rowHeight = 48f;
            float rowSpacing = 54f;

            // Render from top (7 - Boss) down to bottom (1 - Match 1)
            for (int visualRow = 0; visualRow < total; visualRow++)
            {
                int matchIdx = total - 1 - visualRow;
                ArcadeTowerStageEntry entry = run.Matches[matchIdx];
                bool isCurrent = matchIdx == run.CurrentMatchIndex;

                Color bgColor = entry.isBossMatch
                    ? new Color(0.22f, 0.07f, 0.06f, 0.96f)
                    : (isCurrent ? new Color(0.22f, 0.15f, 0.06f, 0.96f) : MenuUiFactory.PanelDarkBg);
                Color borderColor = entry.isBossMatch
                    ? new Color(0.92f, 0.28f, 0.18f, 0.98f)
                    : (isCurrent ? MenuUiFactory.AccentGold : MenuUiFactory.BronzeBorder);

                RectTransform rowRt = MenuUiFactory.CreatePanel($"LadderStep_{matchIdx + 1}", _towerLadderRowsContainer, bgColor, sprite);
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -visualRow * rowSpacing);
                rowRt.sizeDelta = new Vector2(0f, rowHeight);
                MenuUiFactory.AddBorder(rowRt, borderColor, sprite, (entry.isBossMatch || isCurrent) ? 2f : 1.2f);

                string oppName = entry.opponent != null ? entry.opponent.fighterName.ToUpperInvariant() : "???";
                string weatherLabel = entry.GetWeatherDisplayLabel(stageRoster);
                string marker = isCurrent ? "► " : "  ";
                string line = entry.isBossMatch
                    ? $"{marker}{matchIdx + 1} • CHEFE FINAL: {oppName} • {entry.stageDisplayName}"
                    : $"{marker}{matchIdx + 1} • {entry.ladderLabel}: {oppName} • {entry.stageDisplayName} ({weatherLabel})";

                Color textColor = entry.isBossMatch
                    ? new Color(1.0f, 0.88f, 0.36f, 1f)
                    : (isCurrent ? MenuUiFactory.AccentGold : new Color(0.86f, 0.86f, 0.84f, 1f));

                TextMeshProUGUI txt = MenuUiFactory.CreateText("Label", rowRt, line, 16f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor, characterSpacing: 1.5f);
                txt.enableAutoSizing = true;
                txt.fontSizeMin = 11f;
                txt.fontSizeMax = 16f;
                txt.textWrappingMode = TextWrappingModes.NoWrap;
                MenuUiFactory.StretchFull(txt.rectTransform, 14f, 2f, 14f, 2f);
            }
        }

        private void EnsureWeatherSelectorBar()
        {
            if (stageSelectPanel == null)
            {
                return;
            }

            RectTransform stageBox = stageSelectPanel.GetComponent<RectTransform>();
            if (stageBox != null && stageBox.sizeDelta.y < 620f)
            {
                stageBox.sizeDelta = new Vector2(Mathf.Max(620f, stageBox.sizeDelta.x), 640f);
            }

            Transform existing = stageSelectPanel.transform.Find("WeatherSelectorBar");
            if (existing != null)
            {
                weatherSelectorBar = existing.gameObject;
                weatherPrevButton = existing.Find("WeatherPrevBtn")?.GetComponent<UnityEngine.UI.Button>();
                weatherNextButton = existing.Find("WeatherNextBtn")?.GetComponent<UnityEngine.UI.Button>();
                weatherLabelText = existing.Find("WeatherLabel")?.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                Sprite sprite = ResolveWhiteSprite();
                RectTransform bar = MenuUiFactory.CreatePanel("WeatherSelectorBar", stageSelectPanel.transform, MenuUiFactory.PanelDarkBg, sprite, raycastTarget: true);
                bar.anchorMin = new Vector2(0.5f, 1f);
                bar.anchorMax = new Vector2(0.5f, 1f);
                bar.pivot = new Vector2(0.5f, 1f);
                bar.anchoredPosition = new Vector2(0f, -238f);
                bar.sizeDelta = new Vector2(548f, 44f);
                MenuUiFactory.AddBorder(bar, MenuUiFactory.BronzeBorder, sprite, 1.5f);
                weatherSelectorBar = bar.gameObject;

                weatherPrevButton = CreateArrowButton("WeatherPrevBtn", bar, "<", isLeft: true, sprite);
                weatherNextButton = CreateArrowButton("WeatherNextBtn", bar, ">", isLeft: false, sprite);

                weatherLabelText = MenuUiFactory.CreateText(
                    "WeatherLabel",
                    bar,
                    "CLIMA: PÔR DO SOL",
                    18f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Center,
                    MenuUiFactory.AccentGold,
                    characterSpacing: 4f);
                MenuUiFactory.StretchFull(weatherLabelText.rectTransform, 60f, 2f, 60f, 2f);
            }

            if (weatherPrevButton != null)
            {
                weatherPrevButton.onClick.RemoveAllListeners();
                weatherPrevButton.onClick.AddListener(() => StepWeatherSelection(-1));
            }
            if (weatherNextButton != null)
            {
                weatherNextButton.onClick.RemoveAllListeners();
                weatherNextButton.onClick.AddListener(() => StepWeatherSelection(1));
            }
        }

        private UnityEngine.UI.Button CreateArrowButton(string btnName, Transform parent, string symbol, bool isLeft, Sprite sprite)
        {
            RectTransform rt = MenuUiFactory.CreatePanel(btnName, parent, new Color(0.14f, 0.12f, 0.10f, 0.92f), sprite, raycastTarget: true);
            rt.anchorMin = new Vector2(isLeft ? 0f : 1f, 0f);
            rt.anchorMax = new Vector2(isLeft ? 0f : 1f, 1f);
            rt.pivot = new Vector2(isLeft ? 0f : 1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(54f, 0f);

            var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = rt.GetComponent<UnityEngine.UI.Image>();
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            TextMeshProUGUI arrowTxt = MenuUiFactory.CreateText(
                "Symbol",
                rt,
                symbol,
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.AccentGold,
                characterSpacing: 0f);
            MenuUiFactory.StretchFull(arrowTxt.rectTransform, 2f, 2f, 2f, 2f);
            return btn;
        }

        private void RebuildStageList()
        {
            _stageChoices.Clear();
            if (stageNavigation == null || stageSelectPanel == null)
            {
                return;
            }

            EnsureWeatherSelectorBar();

            Transform box = stageSelectPanel.transform;
            for (int i = box.childCount - 1; i >= 0; i--)
            {
                Transform child = box.GetChild(i);
                if (child.name.StartsWith("StageBtn_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            stageNavigation.ClearItems();

            if (stageRoster != null)
            {
                for (int i = 0; i < stageRoster.Count; i++)
                {
                    StageData s = stageRoster.GetStage(i);
                    if (s != null) _stageChoices.Add(s);
                }
            }

            Sprite sprite = ResolveWhiteSprite();
            int totalRows = _stageChoices.Count + 1; // +1 for ALEATÓRIO
            float rowHeight = totalRows > 4 ? 52f : 56f;
            float rowSpacing = totalRows > 4 ? 62f : 66f;
            for (int i = 0; i < totalRows; i++)
            {
                bool isRandom = i >= _stageChoices.Count;
                string label = isRandom ? "ALEATÓRIO" : _stageChoices[i].displayName.ToUpperInvariant();
                MenuItemEntry entry = MenuUiFactory.CreateMenuButton($"StageBtn_{i}", box, label, sprite, 22f, TextAlignmentOptions.Center, characterSpacing: 6f);
                RectTransform rt = entry.Button.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -294f - i * rowSpacing);
                rt.sizeDelta = new Vector2(548f, rowHeight);

                int capturedIdx = i;
                entry.OnSubmit = () => SubmitStageChoice(capturedIdx);
                entry.OnHorizontalStep = dir => StepWeatherSelection(dir);
                stageNavigation.AddItem(entry);
            }

            stageNavigation.OnCancel = ReturnToFighterPickFromStageSelect;
            _lastStageNavIndex = -1;
            UpdateStagePreviewFromIndex();
        }

        private void CreateInfoCard(
            Transform canvasRoot,
            bool isLeft,
            out TextMeshProUGUI hdr,
            out TextMeshProUGUI nameTmp,
            out TextMeshProUGUI heightTmp,
            out TextMeshProUGUI weightTmp,
            out TextMeshProUGUI statusTmp)
        {
            string side = isLeft ? "P1" : "P2";
            Color accent = isLeft ? MenuUiFactory.AccentGold : MenuUiFactory.AccentCrimson;
            TextAlignmentOptions align = isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;

            RectTransform card = MenuUiFactory.CreatePanel($"{side}_InfoCard", canvasRoot, MenuUiFactory.ObsidianBg, whiteSprite);
            card.anchorMin = new Vector2(isLeft ? 0f : 1f, 0f);
            card.anchorMax = new Vector2(isLeft ? 0f : 1f, 0f);
            card.pivot = new Vector2(isLeft ? 0f : 1f, 0f);
            card.anchoredPosition = new Vector2(isLeft ? 44f : -44f, 62f);
            card.sizeDelta = new Vector2(400f, 224f);
            MenuUiFactory.AddBorder(card, MenuUiFactory.BronzeBorder, whiteSprite, 2f);

            RectTransform bar = MenuUiFactory.CreatePanel("Accent", card, accent, whiteSprite);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, 4f);

            hdr = MenuUiFactory.CreateText("Header", card, isLeft ? "JOGADOR 1" : "JOGADOR 2", 18f, FontStyles.Bold, align, accent, characterSpacing: 5f);
            PlaceRow(hdr.rectTransform, -14f, 26f);

            nameTmp = MenuUiFactory.CreateText("FighterName", card, "LUCAS", 36f, FontStyles.Bold, align, Color.white, characterSpacing: 6f);
            nameTmp.enableAutoSizing = true;
            nameTmp.fontSizeMin = 20f;
            nameTmp.fontSizeMax = 36f;
            nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
            PlaceRow(nameTmp.rectTransform, -44f, 44f);

            heightTmp = MenuUiFactory.CreateText("Height", card, "ALTURA: 1,81 M", 19f, FontStyles.Normal, align, new Color(0.80f, 0.78f, 0.74f, 1f), characterSpacing: 2f);
            PlaceRow(heightTmp.rectTransform, -96f, 28f);

            weightTmp = MenuUiFactory.CreateText("Weight", card, "92 KG • PESADO • BRUTAMONTES", 16f, FontStyles.Normal, align, new Color(0.80f, 0.78f, 0.74f, 1f), characterSpacing: 1.2f);
            weightTmp.enableAutoSizing = true;
            weightTmp.fontSizeMin = 11f;
            weightTmp.fontSizeMax = 16f;
            weightTmp.textWrappingMode = TextWrappingModes.NoWrap;
            PlaceRow(weightTmp.rectTransform, -126f, 28f);

            statusTmp = MenuUiFactory.CreateText("Status", card, "SELECIONANDO...", 20f, FontStyles.Bold, align, MenuUiFactory.AccentAmber, characterSpacing: 4f);
            PlaceRow(statusTmp.rectTransform, -162f, 48f);
        }

        private static void PlaceRow(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-40f, h);
        }

        public void RebuildRosterGrid()
        {
            _cards.Clear();
            if (gridContainer == null || fighterRoster == null)
            {
                return;
            }

            for (int i = gridContainer.childCount - 1; i >= 0; i--)
            {
                Transform c = gridContainer.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            for (int i = 0; i < fighterRoster.Count; i++)
            {
                FighterData data = fighterRoster.GetFighter(i);
                if (data == null) continue;

                RosterCardView card = CreateFighterCard(i, data);
                _cards.Add(card);
            }
        }

        private RosterCardView CreateFighterCard(int index, FighterData data)
        {
            Sprite sprite = ResolveWhiteSprite();
            var go = new GameObject($"Card_{data.fighterName}", typeof(RectTransform));
            go.transform.SetParent(gridContainer, false);

            var bg = go.AddComponent<UnityEngine.UI.Image>();
            bg.sprite = sprite;
            bg.color = MenuUiFactory.PanelDarkBg;
            bg.raycastTarget = true;

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            bool isBoss = data != null && data.isSecretBoss;
            Color cardBorderColor = isBoss ? new Color(0.85f, 0.36f, 0.14f, 0.95f) : MenuUiFactory.BronzeBorder;
            MenuUiFactory.AddBorder(go.transform, cardBorderColor, sprite, isBoss ? 2.0f : 1.5f);

            var borderImages = new List<UnityEngine.UI.Image>(4);
            foreach (string edgeName in new[] { "Border_Top", "Border_Bottom", "Border_Left", "Border_Right" })
            {
                Transform edge = go.transform.Find(edgeName);
                if (edge != null)
                {
                    var img = edge.GetComponent<UnityEngine.UI.Image>();
                    if (img != null) borderImages.Add(img);
                }
            }

            RectTransform p1Rim = MenuUiFactory.CreatePanel("P1Rim", go.transform, MenuUiFactory.AccentGold, sprite);
            p1Rim.anchorMin = new Vector2(0f, 1f);
            p1Rim.anchorMax = new Vector2(1f, 1f);
            p1Rim.pivot = new Vector2(0.5f, 1f);
            p1Rim.sizeDelta = new Vector2(0f, 5f);

            RectTransform p2Rim = MenuUiFactory.CreatePanel("P2Rim", go.transform, MenuUiFactory.AccentCrimson, sprite);
            p2Rim.anchorMin = new Vector2(0f, 0f);
            p2Rim.anchorMax = new Vector2(1f, 0f);
            p2Rim.pivot = new Vector2(0.5f, 0f);
            p2Rim.sizeDelta = new Vector2(0f, 5f);

            Color nameColor = isBoss ? new Color(1.0f, 0.90f, 0.60f, 1.0f) : Color.white;
            TextMeshProUGUI nameTmp = MenuUiFactory.CreateText("Name", go.transform, data.fighterName.ToUpperInvariant(), 24f, FontStyles.Bold, TextAlignmentOptions.Center, nameColor, characterSpacing: 5f);
            nameTmp.enableAutoSizing = true;
            nameTmp.fontSizeMin = 13f;
            nameTmp.fontSizeMax = 24f;
            nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
            MenuUiFactory.StretchFull(nameTmp.rectTransform, 8f, 22f, 8f, 10f);

            TextMeshProUGUI badgeTmp = MenuUiFactory.CreateText("Badge", go.transform, "", 14f, FontStyles.Bold, TextAlignmentOptions.Bottom, MenuUiFactory.AccentGold, characterSpacing: 3f);
            MenuUiFactory.StretchFull(badgeTmp.rectTransform, 8f, 8f, 8f, 44f);

            int capturedIndex = index;
            btn.onClick.AddListener(() => HandleCardClicked(capturedIndex));

            return new RosterCardView
            {
                Index = index,
                Data = data,
                Button = btn,
                Background = bg,
                P1Outline = p1Rim.GetComponent<UnityEngine.UI.Image>(),
                P2Outline = p2Rim.GetComponent<UnityEngine.UI.Image>(),
                BorderImages = borderImages.ToArray(),
                NameText = nameTmp,
                BadgeText = badgeTmp
            };
        }

        public bool IsFighterUnlocked(FighterData data)
        {
            return ProgressionManager.IsFighterUnlocked(data);
        }

        public bool IsIndexUnlocked(int rosterIndex)
        {
            if (fighterRoster == null || fighterRoster.Count == 0)
            {
                return false;
            }
            return IsFighterUnlocked(fighterRoster.GetFighter(rosterIndex));
        }

        public void SetP1Index(int index)
        {
            _p1Index = WrapIndex(index);
            RefreshAll();
        }

        public void SetP2Index(int index)
        {
            _p2Index = WrapIndex(index);
            RefreshAll();
        }

        public bool TryConfirmP1()
        {
            if (!IsIndexUnlocked(_p1Index))
            {
                FightAudioManager.Instance?.PlayUiBack();
                RefreshAll();
                return false;
            }

            _p1Confirmed = true;
            FightAudioManager.Instance?.PlayUiConfirm();

            if (GameSessionState.CurrentMode == GameMode.ArcadeTower)
            {
                BuildActiveTowerRunAndEnterLadder();
                return true;
            }

            RefreshAll();
            return true;
        }

        public bool TryConfirmP2()
        {
            if (!IsIndexUnlocked(_p2Index))
            {
                FightAudioManager.Instance?.PlayUiBack();
                RefreshAll();
                return false;
            }

            _p2Confirmed = true;
            FightAudioManager.Instance?.PlayUiConfirm();
            RefreshAll();
            return true;
        }

        private void BuildActiveTowerRunAndEnterLadder()
        {
            FighterData p1Data = fighterRoster != null ? fighterRoster.GetFighter(_p1Index) : null;
            if (p1Data == null || !IsFighterUnlocked(p1Data))
            {
                _p1Confirmed = false;
                RefreshAll();
                return;
            }

            ArcadeTowerRun run = ArcadeTowerRun.BuildRun(
                p1Data,
                fighterRoster,
                stageRoster,
                GameSessionState.SelectedCpuDifficulty);
            run.ApplyCurrentMatchToSession();

            if (run.CurrentMatch != null && run.CurrentMatch.opponent != null && fighterRoster != null)
            {
                int oppIdx = fighterRoster.IndexOf(run.CurrentMatch.opponent);
                if (oppIdx >= 0) _p2Index = oppIdx;
            }

            EnterTowerLadderStep();
        }

        private void CycleDifficultyAndRefresh(int direction)
        {
            GameSessionState.CycleCpuDifficulty(direction);
            FightAudioManager.Instance?.PlayUiMove();

            if (_currentStep == SelectStep.TowerLadder && GameSessionState.CurrentMode == GameMode.ArcadeTower)
            {
                FighterData p1Data = fighterRoster != null ? fighterRoster.GetFighter(_p1Index) : null;
                if (p1Data != null)
                {
                    ArcadeTowerRun rebuilt = ArcadeTowerRun.BuildRun(
                        p1Data,
                        fighterRoster,
                        stageRoster,
                        GameSessionState.SelectedCpuDifficulty);
                    rebuilt.ApplyCurrentMatchToSession();
                    if (rebuilt.CurrentMatch != null && rebuilt.CurrentMatch.opponent != null && fighterRoster != null)
                    {
                        int oppIdx = fighterRoster.IndexOf(rebuilt.CurrentMatch.opponent);
                        if (oppIdx >= 0) _p2Index = oppIdx;
                    }
                    PopulateTowerLadderRows(rebuilt);
                }
            }

            RefreshAll();
        }

        private void Update()
        {
            if (_inputCooldownFrames > 0)
            {
                _inputCooldownFrames--;
            }

            bool supportsDifficultyCycle =
                GameSessionState.CurrentMode == GameMode.VersusCpu ||
                GameSessionState.CurrentMode == GameMode.ArcadeTower;

            if (supportsDifficultyCycle && _inputCooldownFrames <= 0 && IsDifficultyCyclePressed())
            {
                CycleDifficultyAndRefresh(1);
            }

            if (_stageTransitionTimer >= 0f)
            {
                _stageTransitionTimer -= Time.unscaledDeltaTime;
                if (_stageTransitionTimer <= 0f)
                {
                    _stageTransitionTimer = -1f;
                    EnterStageSelectStep();
                }
                return;
            }

            if (_currentStep == SelectStep.StageSelect)
            {
                if (stageNavigation != null && stageNavigation.SelectedIndex != _lastStageNavIndex)
                {
                    _lastStageNavIndex = stageNavigation.SelectedIndex;
                    UpdateStagePreviewFromIndex();
                }
                return;
            }

            if (_currentStep == SelectStep.TowerLadder)
            {
                return;
            }

            if (_inputCooldownFrames > 0 || fighterRoster == null || fighterRoster.Count == 0)
            {
                return;
            }

            if (GameSessionState.CurrentMode == GameMode.ArcadeTower)
            {
                UpdateArcadeTowerInput();
            }
            else if (GameSessionState.P1SelectsBothFighters)
            {
                UpdateSingleChooserInput();
            }
            else
            {
                UpdateDualPlayerInput();
            }
        }

        private void UpdateArcadeTowerInput()
        {
            ReadMenuStep(0, allowArrowsToo: true, ref _p1RepeatCooldown, out int moveStep, out bool confirm, out bool cancel);

            if (moveStep != 0)
            {
                _p1Index = WrapIndex(_p1Index + moveStep);
                FightAudioManager.Instance?.PlayUiMove();
                RefreshAll();
            }

            if (confirm)
            {
                TryConfirmP1();
            }
            else if (cancel)
            {
                GameSessionState.ClearTowerRun();
                FightAudioManager.Instance?.PlayUiBack();
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        private void UpdateSingleChooserInput()
        {
            ReadMenuStep(0, allowArrowsToo: true, ref _p1RepeatCooldown, out int moveStep, out bool confirm, out bool cancel);

            if (!_p1Confirmed)
            {
                if (moveStep != 0)
                {
                    _p1Index = WrapIndex(_p1Index + moveStep);
                    FightAudioManager.Instance?.PlayUiMove();
                    RefreshAll();
                }
                if (confirm)
                {
                    TryConfirmP1();
                }
                else if (cancel)
                {
                    FightAudioManager.Instance?.PlayUiBack();
                    SceneManager.LoadScene(mainMenuSceneName);
                }
            }
            else if (!_p2Confirmed)
            {
                if (moveStep != 0)
                {
                    _p2Index = WrapIndex(_p2Index + moveStep);
                    FightAudioManager.Instance?.PlayUiMove();
                    RefreshAll();
                }
                if (confirm)
                {
                    if (TryConfirmP2())
                    {
                        BeginStageStepTransition();
                    }
                }
                else if (cancel)
                {
                    _p1Confirmed = false;
                    FightAudioManager.Instance?.PlayUiBack();
                    RefreshAll();
                }
            }
        }

        private void UpdateDualPlayerInput()
        {
            ReadMenuStep(0, allowArrowsToo: false, ref _p1RepeatCooldown, out int p1Move, out bool p1Confirm, out bool p1Cancel);
            ReadMenuStep(1, allowArrowsToo: false, ref _p2RepeatCooldown, out int p2Move, out bool p2Confirm, out bool p2Cancel);

            bool changed = false;

            if (!_p1Confirmed)
            {
                if (p1Move != 0) { _p1Index = WrapIndex(_p1Index + p1Move); FightAudioManager.Instance?.PlayUiMove(); changed = true; }
                if (p1Confirm)
                {
                    if (IsIndexUnlocked(_p1Index))
                    {
                        _p1Confirmed = true;
                        FightAudioManager.Instance?.PlayUiConfirm();
                    }
                    else
                    {
                        FightAudioManager.Instance?.PlayUiBack();
                    }
                    changed = true;
                }
                else if (p1Cancel) { FightAudioManager.Instance?.PlayUiBack(); SceneManager.LoadScene(mainMenuSceneName); return; }
            }
            else if (p1Cancel)
            {
                _p1Confirmed = false;
                FightAudioManager.Instance?.PlayUiBack();
                changed = true;
            }

            if (!_p2Confirmed)
            {
                if (p2Move != 0) { _p2Index = WrapIndex(_p2Index + p2Move); FightAudioManager.Instance?.PlayUiMove(); changed = true; }
                if (p2Confirm)
                {
                    if (IsIndexUnlocked(_p2Index))
                    {
                        _p2Confirmed = true;
                        FightAudioManager.Instance?.PlayUiConfirm();
                    }
                    else
                    {
                        FightAudioManager.Instance?.PlayUiBack();
                    }
                    changed = true;
                }
            }
            else if (p2Cancel)
            {
                _p2Confirmed = false;
                FightAudioManager.Instance?.PlayUiBack();
                changed = true;
            }

            if (changed)
            {
                RefreshAll();
                if (_p1Confirmed && _p2Confirmed)
                {
                    BeginStageStepTransition();
                }
            }
        }

        private void HandleCardClicked(int cardIndex)
        {
            if (_currentStep != SelectStep.FighterPick || _stageTransitionTimer >= 0f) return;

            if (GameSessionState.CurrentMode == GameMode.ArcadeTower)
            {
                _p1Index = cardIndex;
                TryConfirmP1();
                return;
            }

            if (!_p1Confirmed)
            {
                _p1Index = cardIndex;
                TryConfirmP1();
            }
            else if (!_p2Confirmed)
            {
                _p2Index = cardIndex;
                if (TryConfirmP2())
                {
                    BeginStageStepTransition();
                }
            }
        }

        private void BeginStageStepTransition()
        {
            SaveCurrentFightersToSession();
            _stageTransitionTimer = stageStepTransitionSeconds;
        }

        private void SaveCurrentFightersToSession()
        {
            FighterData p1Data = fighterRoster != null ? fighterRoster.GetFighter(_p1Index) : null;
            FighterData p2Data = fighterRoster != null ? fighterRoster.GetFighter(_p2Index) : null;
            GameSessionState.SelectFighters(p1Data, p2Data);
        }

        public void ConfirmBothAndOpenStageSelect(int p1Index, int p2Index)
        {
            _p1Index = WrapIndex(p1Index);
            _p2Index = WrapIndex(p2Index);
            if (!IsIndexUnlocked(_p1Index) || !IsIndexUnlocked(_p2Index))
            {
                _p1Confirmed = false;
                _p2Confirmed = false;
                RefreshAll();
                return;
            }

            _p1Confirmed = true;
            _p2Confirmed = true;
            _stageTransitionTimer = -1f;
            SaveCurrentFightersToSession();
            EnterStageSelectStep();
        }

        public void RestoreSessionFightersAndOpenStageSelect()
        {
            int idx1 = fighterRoster != null && GameSessionState.SelectedFighterP1 != null
                ? fighterRoster.IndexOf(GameSessionState.SelectedFighterP1)
                : 0;
            int idx2 = fighterRoster != null && GameSessionState.SelectedFighterP2 != null
                ? fighterRoster.IndexOf(GameSessionState.SelectedFighterP2)
                : (fighterRoster != null && fighterRoster.Count > 1 ? 1 : 0);

            _p1Index = idx1 >= 0 ? idx1 : FindFirstUnlockedIndex(0);
            _p2Index = idx2 >= 0 ? idx2 : FindFirstUnlockedIndex(1);
            _p1Confirmed = true;
            _p2Confirmed = true;
            _stageTransitionTimer = -1f;
            EnterStageSelectStep();
        }

        public void EnterFighterPickStep()
        {
            _currentStep = SelectStep.FighterPick;
            _stageTransitionTimer = -1f;
            _inputCooldownFrames = 2;

            if (rosterGridPanel != null) rosterGridPanel.SetActive(true);
            if (stageSelectPanel != null) stageSelectPanel.SetActive(false);
            if (_towerLadderPanel != null) _towerLadderPanel.SetActive(false);
            stageNavigation?.SetInputLocked(true);
            _towerLadderNavigation?.SetInputLocked(true);

            RefreshAll();
        }

        public void EnterStageSelectStep()
        {
            _currentStep = SelectStep.StageSelect;
            _stageTransitionTimer = -1f;
            _inputCooldownFrames = 2;
            _weatherManuallyChosenInStageSelect = false;

            if (rosterGridPanel != null) rosterGridPanel.SetActive(false);
            if (_towerLadderPanel != null) _towerLadderPanel.SetActive(false);
            if (stageSelectPanel != null) stageSelectPanel.SetActive(true);
            _towerLadderNavigation?.SetInputLocked(true);

            EnsureRuntimeStageOptions();
            EnsureWeatherSelectorBar();
            if (stageNavigation != null)
            {
                int initialStageIdx = 0;
                if (!string.IsNullOrWhiteSpace(GameSessionState.SelectedStageSceneName))
                {
                    for (int i = 0; i < _stageChoices.Count; i++)
                    {
                        if (string.Equals(_stageChoices[i].sceneName, GameSessionState.SelectedStageSceneName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            initialStageIdx = i;
                            break;
                        }
                    }
                }
                stageNavigation.SetSelectedIndex(initialStageIdx);
                stageNavigation.SetInputLocked(false);
                _lastStageNavIndex = stageNavigation.SelectedIndex;
                UpdateStagePreviewFromIndex();
            }

            RefreshAll();
        }

        public void EnterTowerLadderStep()
        {
            _currentStep = SelectStep.TowerLadder;
            _stageTransitionTimer = -1f;
            _inputCooldownFrames = 2;

            EnsureTowerLadderPanel();
            if (rosterGridPanel != null) rosterGridPanel.SetActive(false);
            if (stageSelectPanel != null) stageSelectPanel.SetActive(false);
            stageNavigation?.SetInputLocked(true);

            if (_towerLadderPanel != null)
            {
                PopulateTowerLadderRows(GameSessionState.ActiveTowerRun);
                _towerLadderPanel.SetActive(true);
            }

            if (_towerLadderNavigation != null)
            {
                _towerLadderNavigation.SetSelectedIndex(0, notifyAudio: false);
                _towerLadderNavigation.SetInputLocked(false);
            }

            RefreshAll();
        }

        public void StartCurrentTowerMatch()
        {
            ArcadeTowerRun run = GameSessionState.ActiveTowerRun;
            if (run == null || run.CurrentMatch == null)
            {
                EnterFighterPickStep();
                return;
            }

            run.ApplyCurrentMatchToSession();
            SceneManager.LoadScene(run.CurrentMatch.stageSceneName);
        }

        public void ReturnToFighterPickFromTowerLadder()
        {
            _p1Confirmed = false;
            _p2Confirmed = false;
            GameSessionState.ClearTowerRun();
            EnterFighterPickStep();
        }

        private void ReturnToFighterPickFromStageSelect()
        {
            _p2Confirmed = false;
            if (GameSessionState.P1SelectsBothFighters)
            {
                _p1Confirmed = true;
            }
            EnterFighterPickStep();
        }

        public void StepWeatherSelection(int direction)
        {
            if (_currentStep != SelectStep.StageSelect || stageNavigation == null)
            {
                return;
            }

            int idx = stageNavigation.SelectedIndex;
            bool isRandom = idx < 0 || idx >= _stageChoices.Count;
            if (!isRandom)
            {
                StageData stage = _stageChoices[idx];
                if (stage == null || !stage.supportsWeatherVariants)
                {
                    return;
                }
            }

            GameSessionState.CycleWeather(direction);
            _weatherManuallyChosenInStageSelect = true;
            FightAudioManager.Instance?.PlayUiMove();
            UpdateStagePreviewFromIndex();
        }

        private void UpdateStagePreviewFromIndex()
        {
            if (stageNavigation == null || stagePreviewTitle == null || stagePreviewSubtitle == null)
            {
                return;
            }

            int idx = stageNavigation.SelectedIndex;
            StageWeatherVariant weather = GameSessionState.SelectedWeather;

            if (idx >= 0 && idx < _stageChoices.Count)
            {
                StageData stage = _stageChoices[idx];
                bool supportsWeather = stage.supportsWeatherVariants;
                StageWeatherVariant effectiveWeather = supportsWeather ? weather : stage.defaultWeather;

                stagePreviewTitle.text = stage.displayName.ToUpperInvariant();
                if (supportsWeather)
                {
                    string weatherLabel = GameSessionState.GetWeatherLabel(effectiveWeather, stage);
                    stagePreviewSubtitle.text = $"{stage.subtitle} • {weatherLabel}";
                }
                else
                {
                    stagePreviewSubtitle.text = stage.subtitle;
                }

                if (stagePreviewBanner != null)
                {
                    stagePreviewBanner.sprite = stage.previewImage != null ? stage.previewImage : ResolveWhiteSprite();
                    Color baseCol = stage.previewImage != null ? Color.white : stage.previewColor;
                    stagePreviewBanner.color = supportsWeather
                        ? ApplyWeatherPreviewTint(baseCol, effectiveWeather)
                        : baseCol;
                }

                UpdateWeatherSelectorVisuals(supportsWeather, effectiveWeather, stage, isRandom: false);
            }
            else
            {
                stagePreviewTitle.text = "ALEATÓRIO";
                string weatherLabel = GameSessionState.GetWeatherLabel(weather, null);
                stagePreviewSubtitle.text = _weatherManuallyChosenInStageSelect
                    ? $"Arena aleatória • Clima: {weatherLabel}"
                    : "Arena e clima aleatórios para o combate";
                if (stagePreviewBanner != null)
                {
                    stagePreviewBanner.sprite = ResolveWhiteSprite();
                    stagePreviewBanner.color = ApplyWeatherPreviewTint(new Color(0.28f, 0.22f, 0.14f, 1f), weather);
                }

                UpdateWeatherSelectorVisuals(supportsWeather: true, weather, stage: null, isRandom: true);
            }
        }

        private static Color ApplyWeatherPreviewTint(Color baseColor, StageWeatherVariant weather)
        {
            return weather switch
            {
                StageWeatherVariant.Night => new Color(baseColor.r * 0.45f, baseColor.g * 0.52f, baseColor.b * 0.88f, baseColor.a),
                StageWeatherVariant.RainyNight => new Color(baseColor.r * 0.34f, baseColor.g * 0.44f, baseColor.b * 0.76f, baseColor.a),
                _ => baseColor
            };
        }

        private void UpdateWeatherSelectorVisuals(bool supportsWeather, StageWeatherVariant weather, StageData stage, bool isRandom)
        {
            if (weatherLabelText != null)
            {
                if (!supportsWeather)
                {
                    weatherLabelText.text = "CLIMA FIXO";
                    weatherLabelText.color = new Color(0.50f, 0.48f, 0.46f, 0.90f);
                }
                else
                {
                    string wLabel = (isRandom && !_weatherManuallyChosenInStageSelect)
                        ? "ALEATÓRIO"
                        : GameSessionState.GetWeatherLabel(weather, stage);
                    weatherLabelText.text = $"CLIMA: {wLabel}";
                    weatherLabelText.color = weather switch
                    {
                        StageWeatherVariant.Night => new Color(0.64f, 0.80f, 1.0f, 1f),
                        StageWeatherVariant.RainyNight => new Color(0.48f, 0.84f, 0.98f, 1f),
                        _ => MenuUiFactory.AccentGold
                    };
                }
            }

            SetWeatherArrowInteractive(weatherPrevButton, supportsWeather);
            SetWeatherArrowInteractive(weatherNextButton, supportsWeather);
        }

        private static void SetWeatherArrowInteractive(UnityEngine.UI.Button btn, bool active)
        {
            if (btn == null) return;
            btn.interactable = active;
            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.color = active ? MenuUiFactory.AccentGold : new Color(0.36f, 0.34f, 0.32f, 0.65f);
            }
        }

        public void SubmitStageChoice(int index)
        {
            SaveCurrentFightersToSession();

            string sceneToLoad = fallbackFightSceneName;
            if (index >= 0 && index < _stageChoices.Count)
            {
                StageData chosen = _stageChoices[index];
                GameSessionState.SelectStage(chosen);
                if (!chosen.supportsWeatherVariants)
                {
                    GameSessionState.SelectWeather(chosen.defaultWeather);
                }
                sceneToLoad = GameSessionState.ResolveStageSceneName(stageRoster, fallbackFightSceneName);
            }
            else if (_stageChoices.Count > 0)
            {
                int randomIdx = Random.Range(0, _stageChoices.Count);
                StageData chosen = _stageChoices[randomIdx];
                GameSessionState.SelectStage(chosen);
                if (!chosen.supportsWeatherVariants)
                {
                    GameSessionState.SelectWeather(chosen.defaultWeather);
                }
                else if (!_weatherManuallyChosenInStageSelect)
                {
                    GameSessionState.SelectWeather((StageWeatherVariant)Random.Range(0, 3));
                }
                sceneToLoad = GameSessionState.ResolveStageSceneName(stageRoster, fallbackFightSceneName);
            }

            SceneManager.LoadScene(sceneToLoad);
        }

        public void RefreshAll()
        {
            UpdateModeHeader();
            UpdateDifficultyBarVisuals();
            UpdateCardsVisuals();
            UpdateFighterSlot(0, _p1Index, _p1Confirmed, p1PreviewAnchor, true, ref _p1PreviewInstance, ref _p1PreviewAnimator, ref _p1SpawnedData, p1HeaderText, p1NameText, p1HeightText, p1WeightText, p1StatusText);

            if (GameSessionState.CurrentMode == GameMode.ArcadeTower && _currentStep == SelectStep.FighterPick)
            {
                UpdateTowerSummarySlotOnP2();
            }
            else
            {
                UpdateFighterSlot(1, _p2Index, _p2Confirmed, p2PreviewAnchor, false, ref _p2PreviewInstance, ref _p2PreviewAnimator, ref _p2SpawnedData, p2HeaderText, p2NameText, p2HeightText, p2WeightText, p2StatusText);
            }
        }

        private void UpdateDifficultyBarVisuals()
        {
            bool showDiffBar = _currentStep == SelectStep.FighterPick &&
                               (GameSessionState.CurrentMode == GameMode.ArcadeTower ||
                                GameSessionState.CurrentMode == GameMode.VersusCpu);

            if (showDiffBar)
            {
                EnsureDifficultyBar();
            }

            if (_difficultyBar != null)
            {
                _difficultyBar.SetActive(showDiffBar);
                if (showDiffBar && _difficultyBarLabel != null)
                {
                    string prefix = GameSessionState.CurrentMode == GameMode.ArcadeTower ? "TORRE" : "CPU";
                    _difficultyBarLabel.text = $"{prefix} • DIFICULDADE: {GameSessionState.GetCpuDifficultyLabel(GameSessionState.SelectedCpuDifficulty)}";
                }
            }
        }

        private void UpdateTowerSummarySlotOnP2()
        {
            ClearPreviewAnchor(p2PreviewAnchor, ref _p2PreviewInstance, ref _p2PreviewAnimator, ref _p2SpawnedData);

            if (p2HeaderText != null) p2HeaderText.text = "MODO TORRE ARCADE";
            if (p2NameText != null) p2NameText.text = "7 LUTAS";
            if (p2HeightText != null) p2HeightText.text = $"DIFICULDADE: {GameSessionState.GetCpuDifficultyLabel(GameSessionState.SelectedCpuDifficulty)}";
            if (p2WeightText != null) p2WeightText.text = "CHEFE FINAL • BLOCO DE CELAS";
            if (p2StatusText != null)
            {
                p2StatusText.enableAutoSizing = true;
                p2StatusText.fontSizeMin = 11f;
                p2StatusText.fontSizeMax = 15f;
                p2StatusText.textWrappingMode = TextWrappingModes.Normal;
                p2StatusText.text = "ESCOLHA SEU LUTADOR PARA ESCALAR A TORRE";
                p2StatusText.color = MenuUiFactory.AccentGold;
            }
        }

        private static bool IsDifficultyCyclePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
            {
                return true;
            }

            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
            {
                Gamepad pad = pads[i];
                if (pad != null && pad.added && pad.buttonNorth.wasPressedThisFrame)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateModeHeader()
        {
            string diffLabel = GameSessionState.GetCpuDifficultyLabel(GameSessionState.SelectedCpuDifficulty);
            string modeName = GameSessionState.CurrentMode switch
            {
                GameMode.ArcadeTower => $"TORRE ARCADE (DIFICULDADE: {diffLabel})",
                GameMode.VersusCpu => $"PLAYER VS CPU (DIFICULDADE: {diffLabel})",
                GameMode.Training => "TREINO",
                _ => "PLAYER VS PLAYER"
            };

            if (modeBannerText != null)
            {
                if (_currentStep == SelectStep.TowerLadder)
                {
                    modeBannerText.text = $"ESCADA DA TORRE   •   {modeName}";
                }
                else if (_currentStep == SelectStep.StageSelect)
                {
                    modeBannerText.text = $"SELECIONE A ARENA   •   {modeName}";
                }
                else if (GameSessionState.CurrentMode == GameMode.VersusCpu || GameSessionState.CurrentMode == GameMode.ArcadeTower)
                {
                    modeBannerText.text = $"MODO: {modeName}";
                }
                else
                {
                    modeBannerText.text = $"ESCOLHA DOS LUTADORES   •   {modeName}";
                }
            }

            if (footerHintText != null)
            {
                if (_currentStep == SelectStep.TowerLadder)
                {
                    footerHintText.text = "W/S OU ↑/↓: NAVEGAR   •   TAB / Y: DIFICULDADE   •   ENTER / J / A: INICIAR LUTA   •   ESC / K / B: VOLTAR";
                }
                else if (_currentStep == SelectStep.StageSelect)
                {
                    footerHintText.text = GameSessionState.CurrentMode == GameMode.VersusCpu
                        ? "W/S OU ↑/↓: ARENA   •   A/D OU ←/→: CLIMA   •   TAB: DIFICULDADE CPU   •   ENTER / J: LUTAR   •   ESC / K: VOLTAR"
                        : "W/S OU ↑/↓: ARENA   •   A/D OU ←/→: CLIMA   •   ENTER / J / A: INICIAR LUTA   •   ESC / K / B: VOLTAR";
                }
                else if (GameSessionState.CurrentMode == GameMode.ArcadeTower)
                {
                    footerHintText.text = "WASD / SETAS + J/ENTER: ESCOLHER LUTADOR   •   TAB / Y: DIFICULDADE DA TORRE   •   ESC / K: MENU";
                }
                else
                {
                    footerHintText.text = GameSessionState.CurrentMode == GameMode.VersusCpu
                        ? "P1: WASD + J/ENTER (K/ESC VOLTA)   •   TAB / BOTÃO Y: DIFICULDADE CPU   •   MOUSE & GAMEPAD"
                        : "P1: WASD + J/ENTER (K/ESC VOLTA)   •   P2: SETAS + NUM4/ENTER (NUM5 VOLTA)   •   MOUSE & GAMEPAD";
                }
            }
        }

        private void UpdateCardsVisuals()
        {
            bool hideP2Cursor = GameSessionState.CurrentMode == GameMode.ArcadeTower && _currentStep == SelectStep.FighterPick;

            for (int i = 0; i < _cards.Count; i++)
            {
                RosterCardView c = _cards[i];
                bool isP1 = i == _p1Index;
                bool isP2 = !hideP2Cursor && i == _p2Index;
                bool isBoss = c.Data != null && c.Data.isSecretBoss;
                bool unlocked = IsFighterUnlocked(c.Data);

                if (c.P1Outline != null) c.P1Outline.enabled = isP1;
                if (c.P2Outline != null) c.P2Outline.enabled = isP2;

                if (c.BorderImages != null)
                {
                    Color borderCol = !unlocked
                        ? new Color(0.24f, 0.24f, 0.28f, 0.90f)
                        : (isBoss ? new Color(0.85f, 0.36f, 0.14f, 0.95f) : MenuUiFactory.BronzeBorder);
                    for (int b = 0; b < c.BorderImages.Length; b++)
                    {
                        if (c.BorderImages[b] != null) c.BorderImages[b].color = borderCol;
                    }
                }

                if (c.NameText != null)
                {
                    if (!unlocked)
                    {
                        c.NameText.text = "???";
                        c.NameText.color = new Color(0.48f, 0.48f, 0.52f, 0.95f);
                    }
                    else
                    {
                        c.NameText.text = c.Data != null ? c.Data.fighterName.ToUpperInvariant() : string.Empty;
                        c.NameText.color = isBoss ? new Color(1.0f, 0.90f, 0.60f, 1.0f) : Color.white;
                    }
                }

                if (c.Background != null)
                {
                    if (!unlocked)
                    {
                        c.Background.color = (isP1 || isP2)
                            ? new Color(0.11f, 0.095f, 0.115f, 0.96f)
                            : new Color(0.052f, 0.055f, 0.072f, 0.95f);
                    }
                    else if (isP1 || isP2)
                    {
                        c.Background.color = isBoss
                            ? new Color(0.24f, 0.12f, 0.08f, 0.96f)
                            : new Color(0.18f, 0.145f, 0.095f, 0.96f);
                    }
                    else
                    {
                        c.Background.color = isBoss
                            ? new Color(0.14f, 0.075f, 0.065f, 0.96f)
                            : MenuUiFactory.PanelDarkBg;
                    }
                }

                if (c.BadgeText != null)
                {
                    if (!unlocked)
                    {
                        c.BadgeText.text = "BLOQUEADO";
                        c.BadgeText.color = new Color(0.84f, 0.32f, 0.26f, 0.96f);
                    }
                    else if (isP1 && isP2)
                    {
                        c.BadgeText.text = "P1 + P2";
                        c.BadgeText.color = MenuUiFactory.AccentGold;
                    }
                    else if (isP1)
                    {
                        c.BadgeText.text = _p1Confirmed ? "P1 PRONTO" : "P1";
                        c.BadgeText.color = MenuUiFactory.AccentGold;
                    }
                    else if (isP2)
                    {
                        c.BadgeText.text = _p2Confirmed ? "P2 PRONTO" : "P2";
                        c.BadgeText.color = MenuUiFactory.AccentGold;
                    }
                    else if (isBoss)
                    {
                        c.BadgeText.text = "CHEFE FINAL";
                        c.BadgeText.color = new Color(0.96f, 0.52f, 0.22f, 0.95f);
                    }
                    else
                    {
                        c.BadgeText.text = string.Empty;
                    }
                }
            }
        }

        private void UpdateFighterSlot(
            int slotIndex,
            int rosterIndex,
            bool confirmed,
            Transform anchor,
            bool faceRight,
            ref GameObject instance,
            ref Animator animator,
            ref FighterData spawnedData,
            TextMeshProUGUI hdrTmp,
            TextMeshProUGUI nameTmp,
            TextMeshProUGUI heightTmp,
            TextMeshProUGUI weightTmp,
            TextMeshProUGUI statusTmp)
        {
            FighterData data = fighterRoster != null ? fighterRoster.GetFighter(rosterIndex) : null;
            if (data == null) return;

            bool unlocked = IsFighterUnlocked(data);

            if (hdrTmp != null)
            {
                if (slotIndex == 0)
                {
                    hdrTmp.text = "JOGADOR 1";
                }
                else if (GameSessionState.CurrentMode == GameMode.ArcadeTower)
                {
                    hdrTmp.text = "1º OPONENTE (TORRE)";
                }
                else
                {
                    hdrTmp.text = GameSessionState.CurrentMode == GameMode.VersusCpu
                        ? $"P2 (CPU - {GameSessionState.GetCpuDifficultyLabel(GameSessionState.SelectedCpuDifficulty)})"
                        : (GameSessionState.CurrentMode == GameMode.Training ? "P2 (TREINO)" : "JOGADOR 2");
                }
            }

            if (!unlocked)
            {
                ClearPreviewAnchor(anchor, ref instance, ref animator, ref spawnedData);

                if (nameTmp != null) nameTmp.text = "???";
                if (heightTmp != null) heightTmp.text = "BLOQUEADO";
                if (weightTmp != null)
                {
                    weightTmp.enableAutoSizing = true;
                    weightTmp.fontSizeMin = 11f;
                    weightTmp.fontSizeMax = 16f;
                    weightTmp.textWrappingMode = TextWrappingModes.NoWrap;
                    weightTmp.text = data.isSecretBoss ? "CHEFE FINAL SECRETO" : "LUTADOR BLOQUEADO";
                }
                if (statusTmp != null)
                {
                    statusTmp.enableAutoSizing = true;
                    statusTmp.fontSizeMin = 10f;
                    statusTmp.fontSizeMax = 14f;
                    statusTmp.textWrappingMode = TextWrappingModes.Normal;
                    statusTmp.characterSpacing = 1.5f;
                    statusTmp.text = LockedUnlockHintMessage;
                    statusTmp.color = new Color(0.95f, 0.44f, 0.32f, 1f);
                }
                return;
            }

            if (nameTmp != null) nameTmp.text = data.fighterName.ToUpperInvariant();
            if (heightTmp != null) heightTmp.text = $"ALTURA: {data.heightMeters.ToString("0.00").Replace('.', ',')} M";
            if (weightTmp != null)
            {
                string weightLabel = FighterScaling.GetWeightClassLabel(data.WeightCategory);
                string styleLabel = FighterScaling.GetArchetypeLabel(data.archetype);
                weightTmp.enableAutoSizing = true;
                weightTmp.fontSizeMin = 11f;
                weightTmp.fontSizeMax = 16f;
                weightTmp.textWrappingMode = TextWrappingModes.NoWrap;
                weightTmp.text = $"{Mathf.RoundToInt(data.weightKg)} KG • {weightLabel} • {styleLabel}";
            }
            if (statusTmp != null)
            {
                bool waitingForP1 = slotIndex == 1 && GameSessionState.P1SelectsBothFighters && !_p1Confirmed;
                statusTmp.enableAutoSizing = false;
                statusTmp.fontSize = 20f;
                statusTmp.characterSpacing = 4f;
                statusTmp.textWrappingMode = TextWrappingModes.NoWrap;
                if (slotIndex == 1 && GameSessionState.CurrentMode == GameMode.ArcadeTower)
                {
                    statusTmp.text = "LUTA 1/7";
                    statusTmp.color = MenuUiFactory.AccentCrimson;
                }
                else
                {
                    statusTmp.text = confirmed ? "CONFIRMADO" : (waitingForP1 ? "AGUARDANDO P1..." : "SELECIONANDO...");
                    statusTmp.color = confirmed ? MenuUiFactory.AccentGold : (waitingForP1 ? new Color(0.55f, 0.52f, 0.48f, 1f) : new Color(0.92f, 0.90f, 0.86f, 1f));
                }
            }

            if (anchor != null && data.prefab != null && spawnedData != data)
            {
                ClearPreviewAnchor(anchor, ref instance, ref animator, ref spawnedData);

                instance = Instantiate(data.prefab, anchor);
                instance.name = $"Preview_P{slotIndex + 1}_{data.fighterName}";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(0f, faceRight ? 90f : -90f, 0f);
                instance.transform.localScale = Vector3.one * data.VisualScale;

                var fc = instance.GetComponent<FighterController>();
                if (fc != null) fc.enabled = false;
                var ih = instance.GetComponent<PlayerInputHandler>();
                if (ih != null) ih.enabled = false;
                foreach (FighterDebrisPusher pusher in instance.GetComponentsInChildren<FighterDebrisPusher>(true))
                {
                    pusher.enabled = false;
                }
                foreach (Collider col in instance.GetComponentsInChildren<Collider>(true))
                {
                    col.enabled = false;
                }
                foreach (Rigidbody rb in instance.GetComponentsInChildren<Rigidbody>(true))
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.detectCollisions = false;
                }

                animator = instance.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.enabled = true;
                    animator.applyRootMotion = false;
                    Transform visual = animator.transform;
                    visual.localRotation = Quaternion.identity;
                    visual.localScale = Vector3.one;
                }

                spawnedData = data;
            }

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(StateParamHash, (int)(confirmed ? FighterState.Victory : FighterState.Idle));
            }
        }

        private static void ClearPreviewAnchor(Transform anchor, ref GameObject instance, ref Animator animator, ref FighterData spawnedData)
        {
            if (anchor != null)
            {
                for (int i = anchor.childCount - 1; i >= 0; i--)
                {
                    Transform existingChild = anchor.GetChild(i);
                    if (Application.isPlaying) Destroy(existingChild.gameObject);
                    else DestroyImmediate(existingChild.gameObject);
                }
            }
            instance = null;
            animator = null;
            spawnedData = null;
        }

        private int FindFirstUnlockedIndex(int preferredIndex)
        {
            if (fighterRoster == null || fighterRoster.Count == 0)
            {
                return 0;
            }

            int wrappedPreferred = WrapIndex(preferredIndex);
            if (IsIndexUnlocked(wrappedPreferred))
            {
                return wrappedPreferred;
            }

            for (int i = 0; i < fighterRoster.Count; i++)
            {
                if (IsIndexUnlocked(i))
                {
                    return i;
                }
            }
            return 0;
        }

        private Sprite ResolveWhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            if (rosterGridPanel != null)
            {
                var img = rosterGridPanel.GetComponent<UnityEngine.UI.Image>();
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

        private int WrapIndex(int idx)
        {
            int count = fighterRoster != null ? fighterRoster.Count : 0;
            if (count <= 0) return 0;
            return ((idx % count) + count) % count;
        }

        private static void ReadMenuStep(int playerIndex, bool allowArrowsToo, ref float cooldown, out int moveStep, out bool confirm, out bool cancel)
        {
            moveStep = 0;
            confirm = false;
            cancel = false;

            float dir = 0f;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (playerIndex == 0)
                {
                    if (kb.dKey.isPressed || (allowArrowsToo && kb.rightArrowKey.isPressed)) dir += 1f;
                    if (kb.aKey.isPressed || (allowArrowsToo && kb.leftArrowKey.isPressed)) dir -= 1f;
                    if (kb.sKey.isPressed || (allowArrowsToo && kb.downArrowKey.isPressed)) dir += 1f;
                    if (kb.wKey.isPressed || (allowArrowsToo && kb.upArrowKey.isPressed)) dir -= 1f;

                    if (kb.jKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) confirm = true;
                    if (kb.kKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) cancel = true;
                }
                else
                {
                    if (kb.rightArrowKey.isPressed || kb.downArrowKey.isPressed) dir += 1f;
                    if (kb.leftArrowKey.isPressed || kb.upArrowKey.isPressed) dir -= 1f;

                    if (kb.numpad4Key.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) confirm = true;
                    if (kb.numpad5Key.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) cancel = true;
                }
            }

            var pads = Gamepad.all;
            Gamepad pad = playerIndex < pads.Count ? pads[playerIndex] : null;
            if (pad != null && pad.added)
            {
                float gx = Mathf.Abs(pad.dpad.x.ReadValue()) > 0.3f ? pad.dpad.x.ReadValue() : pad.leftStick.x.ReadValue();
                if (Mathf.Abs(gx) > 0.45f && Mathf.Approximately(dir, 0f)) dir = Mathf.Sign(gx);
                if (pad.buttonSouth.wasPressedThisFrame) confirm = true;
                if (pad.buttonEast.wasPressedThisFrame) cancel = true;
            }

            if (Mathf.Abs(dir) > 0.2f)
            {
                if (cooldown <= 0f)
                {
                    moveStep = dir > 0f ? 1 : -1;
                    cooldown = 0.22f;
                }
                else
                {
                    cooldown -= Time.unscaledDeltaTime;
                }
            }
            else
            {
                cooldown = 0f;
            }
        }
    }
}
