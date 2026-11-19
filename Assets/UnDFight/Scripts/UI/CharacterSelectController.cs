using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnDFight
{
    /// <summary>
    /// MKX-styled Character Select, Stage Select, and Arcade Tower Ladder controller with official matte UnDF identity:
    /// - Top 65% stays completely open for 3D P1 & P2 fighter models (blue #3D8AC7 & crimson #991B1B rim lights).
    /// - Bottom fighter roster bar is 100% data-driven: 1 clean row up to 8 fighters, auto-wraps to 2 rows for 9+.
    /// - Stage Select keeps 3D fighters visible above a panoramic bottom stage strip (1 row <= 4, 2 rows for 5+)
    ///   with a hexagonal [?] Random button and [Q/E] weather toggle, while updating the 3D stage backdrop behind fighters.
    /// - Arcade Tower Ladder features clean UnDF emblem + "TORRE ARCADE" header, left challenger card, and right 7-rung ladder.
    /// - Preserves strict lock/unlock rules for secret boss Ibaneis and exact RosterGridPanel/Grid child ordering.
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
            public UnityEngine.UI.Image LockIcon;
            public UnityEngine.UI.Image[] BorderImages;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI BadgeText;
        }

        private sealed class StageCardView
        {
            public int Index;
            public bool IsRandom;
            public StageData Stage;
            public UnityEngine.UI.Button Button;
            public UnityEngine.UI.Image PreviewImage;
            public UnityEngine.UI.Image OuterFrame;
            public UnityEngine.UI.Image[] BorderImages;
            public TextMeshProUGUI LabelText;
        }

        public const string LockedUnlockHintMessage = "DERROTE O CHEFE FINAL NO MODO TORRE PARA DESBLOQUEAR";

        [Header("Data & Scene Flow")]
        [SerializeField] private FighterRoster fighterRoster;
        [SerializeField] private StageRoster stageRoster;
        [SerializeField] private string fallbackFightSceneName = "Training";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private float stageStepTransitionSeconds = 0.35f;

        [Header("Theme Sprites")]
        [SerializeField] private Sprite whiteSprite;
        [SerializeField] private Sprite undfEmblemSprite;
        [SerializeField] private Sprite hexBadgeSprite;
        [SerializeField] private Sprite diamondSprite;
        [SerializeField] private Sprite lockIconSprite;

        [Header("3D Preview Anchors & Backdrop")]
        [SerializeField] private Transform p1PreviewAnchor;
        [SerializeField] private Transform p2PreviewAnchor;
        [SerializeField] private MeshRenderer stageBackdropRenderer;

        [Header("UI References")]
        [SerializeField] private GameObject fighterNamesStrip;
        [SerializeField] private GameObject rosterGridPanel;
        [SerializeField] private Transform gridContainer;
        [SerializeField] private GameObject stageSelectPanel;
        [SerializeField] private Transform stageCardsGridContainer;
        [SerializeField] private Transform stageRandomSlotContainer;
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
        private readonly List<StageCardView> _stageCardViews = new List<StageCardView>();
        private int _p1Index;
        private int _p2Index;
        private bool _p1Confirmed;
        private bool _p2Confirmed;
        private float _stageTransitionTimer = -1f;
        private SelectStep _currentStep = SelectStep.FighterPick;
        private int _lastStageNavIndex = -1;
        private bool _weatherManuallyChosenInStageSelect;
        private int _currentRosterColumns = 8;

        private GameObject _p1PreviewInstance;
        private GameObject _p2PreviewInstance;
        private Animator _p1PreviewAnimator;
        private Animator _p2PreviewAnimator;
        private FighterData _p1SpawnedData;
        private FighterData _p2SpawnedData;
        private Material _runtimeBackdropMat;

        // Dynamic Arcade Tower Ladder overlay & Difficulty Selector Pill
        private GameObject _towerLadderPanel;
        private TextMeshProUGUI _towerChallengerName;
        private TextMeshProUGUI _towerChallengerMeta;
        private TextMeshProUGUI _towerChallengerDifficulty;
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
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

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

        /// <summary>
        /// Computes 100% data-driven fighter roster layout:
        /// - Up to 8 fighters: 1 clean horizontal row leaving 65%+ top space open for 3D models.
        /// - 9+ fighters: automatically wraps into 2 centered rows with scaled cells so models stay clear.
        /// </summary>
        public static void ComputeRosterGridLayout(
            int fighterCount,
            out int columns,
            out int rows,
            out Vector2 cellSize,
            out Vector2 spacing,
            out Vector2 panelSize)
        {
            int count = Mathf.Max(1, fighterCount);
            if (count <= 8)
            {
                rows = 1;
                columns = count;
                spacing = new Vector2(10f, 8f);
                float maxGridW = 1560f;
                float cellW = Mathf.Clamp((maxGridW - (columns - 1) * spacing.x) / columns, 132f, 186f);
                float cellH = 108f;
                cellSize = new Vector2(Mathf.Floor(cellW), cellH);
                float totalW = columns * cellSize.x + (columns - 1) * spacing.x + 28f;
                panelSize = new Vector2(totalW, cellH + 26f);
            }
            else
            {
                rows = 2;
                columns = Mathf.CeilToInt(count / 2f);
                spacing = new Vector2(8f, 8f);
                float maxGridW = 1620f;
                float cellW = Mathf.Clamp((maxGridW - (columns - 1) * spacing.x) / columns, 108f, 174f);
                float cellH = 84f;
                cellSize = new Vector2(Mathf.Floor(cellW), cellH);
                float totalW = columns * cellSize.x + (columns - 1) * spacing.x + 28f;
                panelSize = new Vector2(totalW, rows * cellH + spacing.y + 26f);
            }
        }

        /// <summary>
        /// Computes 100% data-driven panoramic stage bar layout:
        /// - Up to 4 stages: 1 panoramic horizontal row alongside the hexagonal [?] Random button.
        /// - 5+ stages: automatically adapts into 2 centered rows without encroaching on 3D fighters.
        /// </summary>
        public static void ComputeStageGridLayout(
            int stageCount,
            out int columns,
            out int rows,
            out Vector2 cellSize,
            out Vector2 spacing,
            out Vector2 panelSize)
        {
            int count = Mathf.Max(1, stageCount);
            if (count <= 4)
            {
                rows = 1;
                columns = count;
                spacing = new Vector2(14f, 10f);
                float maxGridW = 1310f;
                float cellW = Mathf.Clamp((maxGridW - (columns - 1) * spacing.x) / columns, 236f, 314f);
                float cellH = 132f;
                cellSize = new Vector2(Mathf.Floor(cellW), cellH);
                float gridW = columns * cellSize.x + (columns - 1) * spacing.x;
                panelSize = new Vector2(gridW + 178f, 214f);
            }
            else
            {
                rows = 2;
                columns = Mathf.CeilToInt(count / 2f);
                spacing = new Vector2(10f, 10f);
                float maxGridW = 1350f;
                float cellW = Mathf.Clamp((maxGridW - (columns - 1) * spacing.x) / columns, 184f, 266f);
                float cellH = 88f;
                cellSize = new Vector2(Mathf.Floor(cellW), cellH);
                float gridW = columns * cellSize.x + (columns - 1) * spacing.x;
                panelSize = new Vector2(gridW + 178f, 268f);
            }
        }

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

        private void OnDestroy()
        {
            if (_runtimeBackdropMat != null)
            {
                if (Application.isPlaying) Destroy(_runtimeBackdropMat);
                else DestroyImmediate(_runtimeBackdropMat);
                _runtimeBackdropMat = null;
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

        public void ConfigureThemeSprites(
            Sprite emblem,
            Sprite hexBadge,
            Sprite diamond,
            Sprite lockIcon,
            MeshRenderer backdropRenderer = null)
        {
            if (emblem != null) undfEmblemSprite = emblem;
            if (hexBadge != null) hexBadgeSprite = hexBadge;
            if (diamond != null) diamondSprite = diamond;
            if (lockIcon != null) lockIconSprite = lockIcon;
            if (backdropRenderer != null) stageBackdropRenderer = backdropRenderer;
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

            for (int i = canvasRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = canvasRoot.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            _towerLadderPanel = null;
            _difficultyBar = null;

            // 1. Minimalist Top Header (leaves top 65% open for 3D fighters)
            BuildTopHeader(canvasRoot);

            // 2. MKX Fighter Names Strip aligned right above the left/right edges of the bottom roster bar
            BuildFighterNamesStrip(canvasRoot);

            // 3. Step 1: Data-Driven Bottom Roster Bar (RosterGridPanel/Grid)
            BuildRosterBar(canvasRoot);

            // 4. Step 2: Panoramic Bottom Stage Select Bar (keeps 3D P1 & P2 visible above)
            BuildStageSelectBar(canvasRoot);

            // 5. Clean Matte Footer Bar
            RectTransform footerBar = MenuUiFactory.CreatePanel(
                "SelectFooterBar",
                canvasRoot,
                new Color(0.043f, 0.055f, 0.078f, 0.92f),
                whiteSprite);
            footerBar.anchorMin = new Vector2(0f, 0f);
            footerBar.anchorMax = new Vector2(1f, 0f);
            footerBar.pivot = new Vector2(0.5f, 0f);
            footerBar.anchoredPosition = Vector2.zero;
            footerBar.sizeDelta = new Vector2(0f, 44f);

            RectTransform footerTopBorder = MenuUiFactory.CreatePanel("FooterBorder", footerBar, MenuUiFactory.SlateBorderDark, whiteSprite);
            footerTopBorder.anchorMin = new Vector2(0f, 1f);
            footerTopBorder.anchorMax = new Vector2(1f, 1f);
            footerTopBorder.pivot = new Vector2(0.5f, 1f);
            footerTopBorder.sizeDelta = new Vector2(0f, 1.5f);

            footerHintText = MenuUiFactory.CreateText(
                "SelectFooter",
                footerBar,
                "P1: WASD + J/ENTER (K/ESC VOLTA)   •   P2: SETAS + NUM4/ENTER (NUM5 VOLTA)   •   MOUSE & GAMEPAD",
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 2.5f);
            footerHintText.enableAutoSizing = true;
            footerHintText.fontSizeMin = 10.5f;
            footerHintText.fontSizeMax = 14f;
            footerHintText.textWrappingMode = TextWrappingModes.NoWrap;
            MenuUiFactory.StretchFull(footerHintText.rectTransform, 24f, 2f, 24f, 2f);

            RebuildRosterGrid();
            RebuildStageList();
            EnterFighterPickStep();
            _built = true;
        }

        private void BuildTopHeader(Transform canvasRoot)
        {
            RectTransform topHeader = MenuUiFactory.CreatePanel(
                "TopHeader",
                canvasRoot,
                new Color(0.043f, 0.055f, 0.078f, 0.84f),
                whiteSprite);
            topHeader.anchorMin = new Vector2(0f, 1f);
            topHeader.anchorMax = new Vector2(1f, 1f);
            topHeader.pivot = new Vector2(0.5f, 1f);
            topHeader.anchoredPosition = Vector2.zero;
            topHeader.sizeDelta = new Vector2(0f, 68f);

            RectTransform bottomLine = MenuUiFactory.CreatePanel("HeaderBottomBorder", topHeader, MenuUiFactory.SlateBorderDark, whiteSprite);
            bottomLine.anchorMin = new Vector2(0f, 0f);
            bottomLine.anchorMax = new Vector2(1f, 0f);
            bottomLine.pivot = new Vector2(0.5f, 0f);
            bottomLine.sizeDelta = new Vector2(0f, 1.5f);

            // Left: P1 badge + compact vitals summary
            RectTransform p1Pill = MenuUiFactory.CreatePanel("P1TopBadge", topHeader, MenuUiFactory.UnDFRoyal, whiteSprite);
            p1Pill.anchorMin = new Vector2(0f, 0.5f);
            p1Pill.anchorMax = new Vector2(0f, 0.5f);
            p1Pill.pivot = new Vector2(0f, 0.5f);
            p1Pill.anchoredPosition = new Vector2(36f, 0f);
            p1Pill.sizeDelta = new Vector2(138f, 34f);
            MenuUiFactory.AddBorder(p1Pill, MenuUiFactory.UnDFBlue, whiteSprite, 1.2f);

            p1HeaderText = MenuUiFactory.CreateText(
                "Header",
                p1Pill,
                "JOGADOR 1",
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextActive,
                characterSpacing: 3f);
            p1HeaderText.enableAutoSizing = true;
            p1HeaderText.fontSizeMin = 10f;
            p1HeaderText.fontSizeMax = 14f;
            MenuUiFactory.StretchFull(p1HeaderText.rectTransform, 6f, 2f, 6f, 2f);

            p1HeightText = MenuUiFactory.CreateText(
                "P1CompactHeight",
                topHeader,
                "1,81 M",
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.UnDFSky,
                characterSpacing: 1.5f);
            p1HeightText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            p1HeightText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            p1HeightText.rectTransform.pivot = new Vector2(0f, 0.5f);
            p1HeightText.rectTransform.anchoredPosition = new Vector2(188f, 9f);
            p1HeightText.rectTransform.sizeDelta = new Vector2(440f, 22f);

            p1WeightText = MenuUiFactory.CreateText(
                "P1CompactWeight",
                topHeader,
                "92 KG • PESADO • BRUTAMONTES",
                13f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 1.2f);
            p1WeightText.enableAutoSizing = true;
            p1WeightText.fontSizeMin = 10f;
            p1WeightText.fontSizeMax = 13f;
            p1WeightText.textWrappingMode = TextWrappingModes.NoWrap;
            p1WeightText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            p1WeightText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            p1WeightText.rectTransform.pivot = new Vector2(0f, 0.5f);
            p1WeightText.rectTransform.anchoredPosition = new Vector2(188f, -10f);
            p1WeightText.rectTransform.sizeDelta = new Vector2(440f, 20f);

            // Center: UnDF Emblem + SELEÇÃO DE LUTADOR
            var centerGo = new GameObject("HeaderCenter", typeof(RectTransform));
            centerGo.transform.SetParent(topHeader, false);
            var centerRt = centerGo.GetComponent<RectTransform>();
            centerRt.anchorMin = new Vector2(0.5f, 0.5f);
            centerRt.anchorMax = new Vector2(0.5f, 0.5f);
            centerRt.pivot = new Vector2(0.5f, 0.5f);
            centerRt.sizeDelta = new Vector2(560f, 48f);

            if (undfEmblemSprite != null)
            {
                var embGo = new GameObject("HeaderEmblem", typeof(RectTransform));
                embGo.transform.SetParent(centerRt, false);
                var embRt = embGo.GetComponent<RectTransform>();
                embRt.anchorMin = new Vector2(0.5f, 0.5f);
                embRt.anchorMax = new Vector2(0.5f, 0.5f);
                embRt.pivot = new Vector2(0.5f, 0.5f);
                embRt.anchoredPosition = new Vector2(-210f, 0f);
                embRt.sizeDelta = new Vector2(58f, 34f);
                var embImg = embGo.AddComponent<UnityEngine.UI.Image>();
                embImg.sprite = undfEmblemSprite;
                embImg.preserveAspect = true;
                embImg.raycastTarget = false;
            }

            modeBannerText = MenuUiFactory.CreateText(
                "ModeTitle",
                centerRt,
                "SELEÇÃO DE LUTADOR",
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextActive,
                characterSpacing: 5.5f);
            modeBannerText.enableAutoSizing = true;
            modeBannerText.fontSizeMin = 15f;
            modeBannerText.fontSizeMax = 22f;
            modeBannerText.textWrappingMode = TextWrappingModes.NoWrap;
            MenuUiFactory.StretchFull(modeBannerText.rectTransform, undfEmblemSprite != null ? 48f : 10f, 4f, 10f, 4f);

            // Right: P2 / CPU badge + compact vitals summary
            RectTransform p2Pill = MenuUiFactory.CreatePanel("P2TopBadge", topHeader, MenuUiFactory.CrimsonDark, whiteSprite);
            p2Pill.anchorMin = new Vector2(1f, 0.5f);
            p2Pill.anchorMax = new Vector2(1f, 0.5f);
            p2Pill.pivot = new Vector2(1f, 0.5f);
            p2Pill.anchoredPosition = new Vector2(-36f, 0f);
            p2Pill.sizeDelta = new Vector2(178f, 34f);
            MenuUiFactory.AddBorder(p2Pill, MenuUiFactory.CrimsonActive, whiteSprite, 1.2f);

            p2HeaderText = MenuUiFactory.CreateText(
                "Header",
                p2Pill,
                "JOGADOR 2",
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextActive,
                characterSpacing: 2.5f);
            p2HeaderText.enableAutoSizing = true;
            p2HeaderText.fontSizeMin = 9.5f;
            p2HeaderText.fontSizeMax = 14f;
            MenuUiFactory.StretchFull(p2HeaderText.rectTransform, 6f, 2f, 6f, 2f);

            p2HeightText = MenuUiFactory.CreateText(
                "P2CompactHeight",
                topHeader,
                "1,79 M",
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 1.5f);
            p2HeightText.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            p2HeightText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            p2HeightText.rectTransform.pivot = new Vector2(1f, 0.5f);
            p2HeightText.rectTransform.anchoredPosition = new Vector2(-228f, 9f);
            p2HeightText.rectTransform.sizeDelta = new Vector2(420f, 22f);

            p2WeightText = MenuUiFactory.CreateText(
                "P2CompactWeight",
                topHeader,
                "87 KG • MÉDIO • BRIGUENTO",
                13f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineRight,
                MenuUiFactory.TextInactive,
                characterSpacing: 1.2f);
            p2WeightText.enableAutoSizing = true;
            p2WeightText.fontSizeMin = 10f;
            p2WeightText.fontSizeMax = 13f;
            p2WeightText.textWrappingMode = TextWrappingModes.NoWrap;
            p2WeightText.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            p2WeightText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            p2WeightText.rectTransform.pivot = new Vector2(1f, 0.5f);
            p2WeightText.rectTransform.anchoredPosition = new Vector2(-228f, -10f);
            p2WeightText.rectTransform.sizeDelta = new Vector2(420f, 20f);
        }

        private void BuildFighterNamesStrip(Transform canvasRoot)
        {
            var stripGo = new GameObject("FighterNamesStrip", typeof(RectTransform));
            stripGo.transform.SetParent(canvasRoot, false);
            var stripRt = stripGo.GetComponent<RectTransform>();
            stripRt.anchorMin = new Vector2(0.5f, 0f);
            stripRt.anchorMax = new Vector2(0.5f, 0f);
            stripRt.pivot = new Vector2(0.5f, 0f);
            stripRt.anchoredPosition = new Vector2(0f, 198f);
            stripRt.sizeDelta = new Vector2(1590f, 88f);
            fighterNamesStrip = stripGo;

            // Left: P1 Name + thin #3D8AC7 underline + P1 Status / Unlock Hint
            p1NameText = MenuUiFactory.CreateText(
                "P1FighterName",
                stripRt,
                "LUCAS",
                44f,
                FontStyles.Bold,
                TextAlignmentOptions.BottomLeft,
                MenuUiFactory.TextActive,
                characterSpacing: 6f);
            p1NameText.enableAutoSizing = true;
            p1NameText.fontSizeMin = 24f;
            p1NameText.fontSizeMax = 44f;
            p1NameText.textWrappingMode = TextWrappingModes.NoWrap;
            p1NameText.rectTransform.anchorMin = new Vector2(0f, 0f);
            p1NameText.rectTransform.anchorMax = new Vector2(0.46f, 1f);
            p1NameText.rectTransform.offsetMin = new Vector2(4f, 26f);
            p1NameText.rectTransform.offsetMax = new Vector2(-8f, -2f);

            RectTransform p1Line = MenuUiFactory.CreatePanel("P1NameLine", stripRt, MenuUiFactory.UnDFBlue, whiteSprite);
            p1Line.anchorMin = new Vector2(0f, 0f);
            p1Line.anchorMax = new Vector2(0.36f, 0f);
            p1Line.pivot = new Vector2(0f, 0f);
            p1Line.anchoredPosition = new Vector2(4f, 23f);
            p1Line.sizeDelta = new Vector2(0f, 2.5f);

            p1StatusText = MenuUiFactory.CreateText(
                "P1Status",
                stripRt,
                "SELECIONANDO...",
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.TopLeft,
                MenuUiFactory.UnDFSky,
                characterSpacing: 2.5f);
            p1StatusText.enableAutoSizing = true;
            p1StatusText.fontSizeMin = 10.5f;
            p1StatusText.fontSizeMax = 15f;
            p1StatusText.textWrappingMode = TextWrappingModes.NoWrap;
            p1StatusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            p1StatusText.rectTransform.anchorMax = new Vector2(0.49f, 0f);
            p1StatusText.rectTransform.pivot = new Vector2(0f, 0f);
            p1StatusText.rectTransform.anchoredPosition = new Vector2(4f, 0f);
            p1StatusText.rectTransform.sizeDelta = new Vector2(0f, 22f);

            // Right: P2 Name + thin #B91C1C underline + P2 Status
            p2NameText = MenuUiFactory.CreateText(
                "P2FighterName",
                stripRt,
                "TIAGO",
                44f,
                FontStyles.Bold,
                TextAlignmentOptions.BottomRight,
                MenuUiFactory.TextActive,
                characterSpacing: 6f);
            p2NameText.enableAutoSizing = true;
            p2NameText.fontSizeMin = 24f;
            p2NameText.fontSizeMax = 44f;
            p2NameText.textWrappingMode = TextWrappingModes.NoWrap;
            p2NameText.rectTransform.anchorMin = new Vector2(0.54f, 0f);
            p2NameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            p2NameText.rectTransform.offsetMin = new Vector2(8f, 26f);
            p2NameText.rectTransform.offsetMax = new Vector2(-4f, -2f);

            RectTransform p2Line = MenuUiFactory.CreatePanel("P2NameLine", stripRt, MenuUiFactory.CrimsonActive, whiteSprite);
            p2Line.anchorMin = new Vector2(0.64f, 0f);
            p2Line.anchorMax = new Vector2(1f, 0f);
            p2Line.pivot = new Vector2(1f, 0f);
            p2Line.anchoredPosition = new Vector2(-4f, 23f);
            p2Line.sizeDelta = new Vector2(0f, 2.5f);

            p2StatusText = MenuUiFactory.CreateText(
                "P2Status",
                stripRt,
                "SELECIONANDO...",
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.TopRight,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 2.5f);
            p2StatusText.enableAutoSizing = true;
            p2StatusText.fontSizeMin = 10.5f;
            p2StatusText.fontSizeMax = 15f;
            p2StatusText.textWrappingMode = TextWrappingModes.NoWrap;
            p2StatusText.rectTransform.anchorMin = new Vector2(0.51f, 0f);
            p2StatusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            p2StatusText.rectTransform.pivot = new Vector2(1f, 0f);
            p2StatusText.rectTransform.anchoredPosition = new Vector2(-4f, 0f);
            p2StatusText.rectTransform.sizeDelta = new Vector2(0f, 22f);
        }

        private void BuildRosterBar(Transform canvasRoot)
        {
            RectTransform gridPanel = MenuUiFactory.CreatePanel(
                "RosterGridPanel",
                canvasRoot,
                new Color(0.043f, 0.055f, 0.078f, 0.90f),
                whiteSprite);
            gridPanel.anchorMin = new Vector2(0.5f, 0f);
            gridPanel.anchorMax = new Vector2(0.5f, 0f);
            gridPanel.pivot = new Vector2(0.5f, 0f);
            gridPanel.anchoredPosition = new Vector2(0f, 54f);
            gridPanel.sizeDelta = new Vector2(1590f, 134f);
            MenuUiFactory.AddBorder(gridPanel, MenuUiFactory.SlateBorderDark, whiteSprite, 1.5f);
            rosterGridPanel = gridPanel.gameObject;

            RectTransform gridAccent = MenuUiFactory.CreatePanel("GridTopAccent", gridPanel, MenuUiFactory.UnDFBlue, whiteSprite);
            gridAccent.anchorMin = new Vector2(0f, 1f);
            gridAccent.anchorMax = new Vector2(1f, 1f);
            gridAccent.pivot = new Vector2(0.5f, 1f);
            gridAccent.sizeDelta = new Vector2(0f, 2.5f);

            var gridGo = new GameObject("Grid", typeof(RectTransform));
            gridGo.transform.SetParent(gridPanel, false);
            MenuUiFactory.StretchFull(gridGo.GetComponent<RectTransform>(), 14f, 12f, 14f, 14f);

            var glg = gridGo.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            glg.cellSize = new Vector2(184f, 108f);
            glg.spacing = new Vector2(10f, 8f);
            glg.childAlignment = TextAnchor.MiddleCenter;
            glg.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 8;
            gridContainer = gridGo.transform;
        }

        private void BuildStageSelectBar(Transform canvasRoot)
        {
            RectTransform stageBox = MenuUiFactory.CreatePanel(
                "StageSelectPanel",
                canvasRoot,
                new Color(0.043f, 0.055f, 0.078f, 0.90f),
                whiteSprite,
                raycastTarget: true);
            stageBox.anchorMin = new Vector2(0.5f, 0f);
            stageBox.anchorMax = new Vector2(0.5f, 0f);
            stageBox.pivot = new Vector2(0.5f, 0f);
            stageBox.anchoredPosition = new Vector2(0f, 54f);
            stageBox.sizeDelta = new Vector2(1510f, 214f);
            MenuUiFactory.AddBorder(stageBox, MenuUiFactory.SlateBorderDark, whiteSprite, 1.5f);
            stageSelectPanel = stageBox.gameObject;

            RectTransform topLine = MenuUiFactory.CreatePanel("StageTopLine", stageBox, MenuUiFactory.UnDFBlue, whiteSprite);
            topLine.anchorMin = new Vector2(0f, 1f);
            topLine.anchorMax = new Vector2(1f, 1f);
            topLine.pivot = new Vector2(0.5f, 1f);
            topLine.sizeDelta = new Vector2(0f, 2.5f);

            // Hidden/subtle preview banner reference (for headless test compatibility; visual 3D backdrop lives behind fighters)
            RectTransform hiddenPreview = MenuUiFactory.CreatePanel("StagePreviewCard", stageBox, Color.clear, whiteSprite);
            hiddenPreview.anchorMin = new Vector2(0f, 1f);
            hiddenPreview.anchorMax = new Vector2(0f, 1f);
            hiddenPreview.sizeDelta = Vector2.zero;
            stagePreviewBanner = hiddenPreview.GetComponent<UnityEngine.UI.Image>();
            stagePreviewBanner.enabled = false;

            // Left: Selected Stage Name + Subtitle aligned above the left edge of the panoramic bar
            stagePreviewTitle = MenuUiFactory.CreateText(
                "StagePreviewTitle",
                stageBox,
                "FAVELA CARIOCA",
                30f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.TextActive,
                characterSpacing: 5f);
            stagePreviewTitle.enableAutoSizing = true;
            stagePreviewTitle.fontSizeMin = 18f;
            stagePreviewTitle.fontSizeMax = 30f;
            stagePreviewTitle.textWrappingMode = TextWrappingModes.NoWrap;
            stagePreviewTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            stagePreviewTitle.rectTransform.anchorMax = new Vector2(0.60f, 1f);
            stagePreviewTitle.rectTransform.pivot = new Vector2(0f, 1f);
            stagePreviewTitle.rectTransform.anchoredPosition = new Vector2(22f, -8f);
            stagePreviewTitle.rectTransform.sizeDelta = new Vector2(0f, 36f);

            stagePreviewSubtitle = MenuUiFactory.CreateText(
                "StagePreviewSub",
                stageBox,
                "Bar do Jorge • Pôr do Sol",
                14f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 2f);
            stagePreviewSubtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            stagePreviewSubtitle.rectTransform.anchorMax = new Vector2(0.60f, 1f);
            stagePreviewSubtitle.rectTransform.pivot = new Vector2(0f, 1f);
            stagePreviewSubtitle.rectTransform.anchoredPosition = new Vector2(22f, -42f);
            stagePreviewSubtitle.rectTransform.sizeDelta = new Vector2(0f, 22f);

            // Right: Compact Weather Badge aligned above the right edge
            EnsureWeatherSelectorBar();

            // Panoramic Stage Cards Grid (Left/Center) + Hexagonal [?] Random Slot (Right)
            var cardsGridGo = new GameObject("StageCardsGrid", typeof(RectTransform));
            cardsGridGo.transform.SetParent(stageBox, false);
            var cardsGridRt = cardsGridGo.GetComponent<RectTransform>();
            cardsGridRt.anchorMin = new Vector2(0f, 0f);
            cardsGridRt.anchorMax = new Vector2(1f, 1f);
            cardsGridRt.offsetMin = new Vector2(20f, 14f);
            cardsGridRt.offsetMax = new Vector2(-156f, -68f);

            var sglg = cardsGridGo.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            sglg.cellSize = new Vector2(314f, 132f);
            sglg.spacing = new Vector2(14f, 10f);
            sglg.childAlignment = TextAnchor.MiddleCenter;
            sglg.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            sglg.constraintCount = 4;
            stageCardsGridContainer = cardsGridRt;

            var randomSlotGo = new GameObject("StageRandomSlot", typeof(RectTransform));
            randomSlotGo.transform.SetParent(stageBox, false);
            var randomSlotRt = randomSlotGo.GetComponent<RectTransform>();
            randomSlotRt.anchorMin = new Vector2(1f, 0f);
            randomSlotRt.anchorMax = new Vector2(1f, 1f);
            randomSlotRt.offsetMin = new Vector2(-148f, 14f);
            randomSlotRt.offsetMax = new Vector2(-18f, -68f);
            stageRandomSlotContainer = randomSlotRt;

            stageNavigation = stageBox.gameObject.AddComponent<MenuNavigationGroup>();
            stageNavigation.HorizontalPrimary = true;
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
            RectTransform bar = MenuUiFactory.CreatePanel(
                "DifficultySelectorBar",
                transform,
                new Color(0.071f, 0.094f, 0.133f, 0.92f),
                sprite,
                raycastTarget: true);
            bar.anchorMin = new Vector2(1f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 1f);
            bar.anchoredPosition = new Vector2(-36f, -78f);
            bar.sizeDelta = new Vector2(356f, 36f);
            MenuUiFactory.AddBorder(bar, MenuUiFactory.SlateBorderMid, sprite, 1.2f);
            _difficultyBar = bar.gameObject;

            UnityEngine.UI.Button prevBtn = CreateArrowButton("DiffPrevBtn", bar, "<", isLeft: true, sprite);
            UnityEngine.UI.Button nextBtn = CreateArrowButton("DiffNextBtn", bar, ">", isLeft: false, sprite);
            prevBtn.onClick.AddListener(() => CycleDifficultyAndRefresh(-1));
            nextBtn.onClick.AddListener(() => CycleDifficultyAndRefresh(1));

            _difficultyBarLabel = MenuUiFactory.CreateText(
                "DifficultyLabel",
                bar,
                "DIFICULDADE: MÉDIO",
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.UnDFSky,
                characterSpacing: 2.5f);
            _difficultyBarLabel.enableAutoSizing = true;
            _difficultyBarLabel.fontSizeMin = 10f;
            _difficultyBarLabel.fontSizeMax = 14f;
            MenuUiFactory.StretchFull(_difficultyBarLabel.rectTransform, 44f, 2f, 44f, 2f);
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
                _towerChallengerName = existing.Find("ChallengerColumn/ChallengerCard/ChallengerName")?.GetComponent<TextMeshProUGUI>();
                _towerChallengerMeta = existing.Find("ChallengerColumn/ChallengerCard/ChallengerMeta")?.GetComponent<TextMeshProUGUI>();
                _towerChallengerDifficulty = existing.Find("ChallengerColumn/ChallengerCard/DifficultyRow/DiffVal")?.GetComponent<TextMeshProUGUI>();
                _towerLadderRowsContainer = existing.Find("LadderColumn/TowerRows");
                _towerLadderNavigation = existing.GetComponent<MenuNavigationGroup>();
                return;
            }

            Sprite sprite = ResolveWhiteSprite();

            // Clean matte 2-column Arcade Tower panel
            RectTransform box = MenuUiFactory.CreatePanel("TowerLadderPanel", transform, MenuUiFactory.ObsidianBg, sprite, raycastTarget: true);
            box.anchorMin = new Vector2(0.5f, 0.50f);
            box.anchorMax = new Vector2(0.5f, 0.50f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(1180f, 670f);
            MenuUiFactory.AddBorder(box, MenuUiFactory.SlateBorderDark, sprite, 2f);
            _towerLadderPanel = box.gameObject;

            RectTransform topLine = MenuUiFactory.CreatePanel("TowerTopLine", box, MenuUiFactory.UnDFBlue, sprite);
            topLine.anchorMin = new Vector2(0f, 1f);
            topLine.anchorMax = new Vector2(1f, 1f);
            topLine.pivot = new Vector2(0.5f, 1f);
            topLine.sizeDelta = new Vector2(0f, 3.5f);

            // Clean Header: ONLY UnDF Emblem + "TORRE ARCADE" (no extra 7 LUTAS / CHEFE text in header!)
            var hdrGo = new GameObject("TowerHeader", typeof(RectTransform));
            hdrGo.transform.SetParent(box, false);
            var hdrRt = hdrGo.GetComponent<RectTransform>();
            hdrRt.anchorMin = new Vector2(0f, 1f);
            hdrRt.anchorMax = new Vector2(1f, 1f);
            hdrRt.pivot = new Vector2(0.5f, 1f);
            hdrRt.anchoredPosition = new Vector2(0f, -14f);
            hdrRt.sizeDelta = new Vector2(-64f, 56f);

            if (undfEmblemSprite != null)
            {
                var embGo = new GameObject("TowerEmblem", typeof(RectTransform));
                embGo.transform.SetParent(hdrRt, false);
                var embRt = embGo.GetComponent<RectTransform>();
                embRt.anchorMin = new Vector2(0f, 0.5f);
                embRt.anchorMax = new Vector2(0f, 0.5f);
                embRt.pivot = new Vector2(0f, 0.5f);
                embRt.anchoredPosition = new Vector2(0f, 0f);
                embRt.sizeDelta = new Vector2(74f, 44f);
                var embImg = embGo.AddComponent<UnityEngine.UI.Image>();
                embImg.sprite = undfEmblemSprite;
                embImg.preserveAspect = true;
                embImg.raycastTarget = false;
            }

            float titleX = undfEmblemSprite != null ? 92f : 0f;
            TextMeshProUGUI title = MenuUiFactory.CreateText(
                "TowerHeading",
                hdrRt,
                "TORRE ARCADE",
                32f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.TextActive,
                characterSpacing: 7f);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(titleX, 0f);
            title.rectTransform.offsetMax = Vector2.zero;

            RectTransform hdrDivider = MenuUiFactory.CreatePanel("TowerHeaderDivider", box, MenuUiFactory.SlateBorderDark, sprite);
            hdrDivider.anchorMin = new Vector2(0f, 1f);
            hdrDivider.anchorMax = new Vector2(1f, 1f);
            hdrDivider.pivot = new Vector2(0.5f, 1f);
            hdrDivider.anchoredPosition = new Vector2(0f, -78f);
            hdrDivider.sizeDelta = new Vector2(-64f, 1.5f);

            // LEFT COLUMN: Sober Challenger Card + Action Buttons
            var leftColGo = new GameObject("ChallengerColumn", typeof(RectTransform));
            leftColGo.transform.SetParent(box, false);
            var leftColRt = leftColGo.GetComponent<RectTransform>();
            leftColRt.anchorMin = new Vector2(0f, 0f);
            leftColRt.anchorMax = new Vector2(0f, 1f);
            leftColRt.pivot = new Vector2(0f, 0.5f);
            leftColRt.offsetMin = new Vector2(32f, 28f);
            leftColRt.offsetMax = new Vector2(442f, -96f);

            RectTransform challCard = MenuUiFactory.CreatePanel("ChallengerCard", leftColRt, MenuUiFactory.PanelDarkBg, sprite);
            challCard.anchorMin = new Vector2(0f, 1f);
            challCard.anchorMax = new Vector2(1f, 1f);
            challCard.pivot = new Vector2(0.5f, 1f);
            challCard.anchoredPosition = Vector2.zero;
            challCard.sizeDelta = new Vector2(0f, 360f);
            MenuUiFactory.AddBorder(challCard, MenuUiFactory.SlateBorderMid, sprite, 1.5f);

            RectTransform challTop = MenuUiFactory.CreatePanel("ChallengerAccent", challCard, MenuUiFactory.UnDFRoyal, sprite);
            challTop.anchorMin = new Vector2(0f, 1f);
            challTop.anchorMax = new Vector2(1f, 1f);
            challTop.pivot = new Vector2(0.5f, 1f);
            challTop.sizeDelta = new Vector2(0f, 3f);

            TextMeshProUGUI challTag = MenuUiFactory.CreateText(
                "ChallengerTag",
                challCard,
                "DESAFIANTE",
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.UnDFSky,
                characterSpacing: 5f);
            challTag.rectTransform.anchorMin = new Vector2(0f, 1f);
            challTag.rectTransform.anchorMax = new Vector2(1f, 1f);
            challTag.rectTransform.pivot = new Vector2(0.5f, 1f);
            challTag.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            challTag.rectTransform.sizeDelta = new Vector2(-44f, 24f);

            _towerChallengerName = MenuUiFactory.CreateText(
                "ChallengerName",
                challCard,
                "LUCAS",
                42f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.TextActive,
                characterSpacing: 5f);
            _towerChallengerName.enableAutoSizing = true;
            _towerChallengerName.fontSizeMin = 24f;
            _towerChallengerName.fontSizeMax = 42f;
            _towerChallengerName.rectTransform.anchorMin = new Vector2(0f, 1f);
            _towerChallengerName.rectTransform.anchorMax = new Vector2(1f, 1f);
            _towerChallengerName.rectTransform.pivot = new Vector2(0.5f, 1f);
            _towerChallengerName.rectTransform.anchoredPosition = new Vector2(0f, -52f);
            _towerChallengerName.rectTransform.sizeDelta = new Vector2(-44f, 54f);

            _towerChallengerMeta = MenuUiFactory.CreateText(
                "ChallengerMeta",
                challCard,
                "1,81 M  •  92 KG\nPESADO  •  BRUTAMONTES",
                16f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 2f);
            _towerChallengerMeta.lineSpacing = 12f;
            _towerChallengerMeta.rectTransform.anchorMin = new Vector2(0f, 1f);
            _towerChallengerMeta.rectTransform.anchorMax = new Vector2(1f, 1f);
            _towerChallengerMeta.rectTransform.pivot = new Vector2(0.5f, 1f);
            _towerChallengerMeta.rectTransform.anchoredPosition = new Vector2(0f, -120f);
            _towerChallengerMeta.rectTransform.sizeDelta = new Vector2(-44f, 76f);

            // Difficulty row inside challenger card
            RectTransform diffRow = MenuUiFactory.CreatePanel("DifficultyRow", challCard, MenuUiFactory.ObsidianBg, sprite, raycastTarget: true);
            diffRow.anchorMin = new Vector2(0f, 0f);
            diffRow.anchorMax = new Vector2(1f, 0f);
            diffRow.pivot = new Vector2(0.5f, 0f);
            diffRow.anchoredPosition = new Vector2(0f, 24f);
            diffRow.sizeDelta = new Vector2(-44f, 54f);
            MenuUiFactory.AddBorder(diffRow, MenuUiFactory.SlateBorderDark, sprite, 1.2f);

            UnityEngine.UI.Button diffPrev = CreateArrowButton("TowerDiffPrev", diffRow, "<", isLeft: true, sprite);
            UnityEngine.UI.Button diffNext = CreateArrowButton("TowerDiffNext", diffRow, ">", isLeft: false, sprite);
            diffPrev.onClick.AddListener(() => CycleDifficultyAndRefresh(-1));
            diffNext.onClick.AddListener(() => CycleDifficultyAndRefresh(1));

            _towerChallengerDifficulty = MenuUiFactory.CreateText(
                "DiffVal",
                diffRow,
                "DIFICULDADE: MÉDIO",
                14.5f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.UnDFSky,
                characterSpacing: 2.5f);
            _towerChallengerDifficulty.enableAutoSizing = true;
            _towerChallengerDifficulty.fontSizeMin = 11f;
            _towerChallengerDifficulty.fontSizeMax = 14.5f;
            MenuUiFactory.StretchFull(_towerChallengerDifficulty.rectTransform, 44f, 4f, 44f, 4f);

            // Action Buttons below Challenger Card
            _towerLadderNavigation = box.gameObject.AddComponent<MenuNavigationGroup>();
            MenuItemEntry startBtn = MenuUiFactory.CreateMenuButton(
                "TowerStartBtn",
                leftColRt,
                "INICIAR PRÓXIMA LUTA",
                sprite,
                19f,
                TextAlignmentOptions.Center,
                characterSpacing: 4f);
            startBtn.NormalBg = new Color(0.118f, 0.255f, 0.440f, 0.96f);
            startBtn.SelectedBg = new Color(0.118f, 0.337f, 0.600f, 1.0f);
            startBtn.NormalAccent = MenuUiFactory.UnDFBlue;
            startBtn.SelectedAccent = MenuUiFactory.UnDFSky;
            startBtn.NormalLabelColor = MenuUiFactory.TextActive;
            startBtn.SelectedLabelColor = MenuUiFactory.TextActive;
            RectTransform startRt = startBtn.Button.GetComponent<RectTransform>();
            startRt.anchorMin = new Vector2(0f, 0f);
            startRt.anchorMax = new Vector2(1f, 0f);
            startRt.pivot = new Vector2(0.5f, 0f);
            startRt.anchoredPosition = new Vector2(0f, 76f);
            startRt.sizeDelta = new Vector2(0f, 64f);
            startBtn.OnSubmit = StartCurrentTowerMatch;
            _towerLadderNavigation.AddItem(startBtn);

            MenuItemEntry backBtn = MenuUiFactory.CreateMenuButton(
                "TowerBackBtn",
                leftColRt,
                "VOLTAR",
                sprite,
                18f,
                TextAlignmentOptions.Center,
                characterSpacing: 5f);
            RectTransform backRt = backBtn.Button.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 0f);
            backRt.anchorMax = new Vector2(1f, 0f);
            backRt.pivot = new Vector2(0.5f, 0f);
            backRt.anchoredPosition = new Vector2(0f, 0f);
            backRt.sizeDelta = new Vector2(0f, 56f);
            backBtn.OnSubmit = ReturnToFighterPickFromTowerLadder;
            _towerLadderNavigation.AddItem(backBtn);

            _towerLadderNavigation.OnCancel = ReturnToFighterPickFromTowerLadder;

            // RIGHT COLUMN: 7-Rung Vertical Ladder
            var rightColGo = new GameObject("LadderColumn", typeof(RectTransform));
            rightColGo.transform.SetParent(box, false);
            var rightColRt = rightColGo.GetComponent<RectTransform>();
            rightColRt.anchorMin = new Vector2(0f, 0f);
            rightColRt.anchorMax = new Vector2(1f, 1f);
            rightColRt.offsetMin = new Vector2(468f, 28f);
            rightColRt.offsetMax = new Vector2(-32f, -96f);

            var rowsGo = new GameObject("TowerRows", typeof(RectTransform));
            rowsGo.transform.SetParent(rightColRt, false);
            var rowsRt = rowsGo.GetComponent<RectTransform>();
            MenuUiFactory.StretchFull(rowsRt, 0f, 0f, 0f, 0f);
            _towerLadderRowsContainer = rowsRt;

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
            FighterData p1 = run.PlayerFighter;
            if (_towerChallengerName != null)
            {
                _towerChallengerName.text = p1 != null ? p1.fighterName.ToUpperInvariant() : "P1";
            }
            if (_towerChallengerMeta != null && p1 != null)
            {
                string h = p1.heightMeters.ToString("0.00").Replace('.', ',');
                string w = FighterScaling.GetWeightClassLabel(p1.WeightCategory);
                string a = FighterScaling.GetArchetypeLabel(p1.archetype);
                _towerChallengerMeta.text = $"{h} M  •  {Mathf.RoundToInt(p1.weightKg)} KG\n{w}  •  {a}";
            }
            if (_towerChallengerDifficulty != null)
            {
                string diffLabel = GameSessionState.GetCpuDifficultyLabel(run.BaseDifficulty);
                _towerChallengerDifficulty.text = $"DIFICULDADE: {diffLabel}";
            }

            int total = run.TotalMatches;
            float rowHeight = 68f;
            float rowSpacing = 78f;

            // Render from top (7 - Boss Ibaneis) down to bottom (1 - Match 1)
            for (int visualRow = 0; visualRow < total; visualRow++)
            {
                int matchIdx = total - 1 - visualRow;
                ArcadeTowerStageEntry entry = run.Matches[matchIdx];
                bool defeated = matchIdx < run.CurrentMatchIndex;
                bool isCurrent = matchIdx == run.CurrentMatchIndex;

                Color bgColor;
                Color borderColor;
                Color textColor;
                Color subColor;

                if (defeated)
                {
                    bgColor = MenuUiFactory.VictoryMutedBg;
                    borderColor = MenuUiFactory.VictoryMutedBorder;
                    textColor = MenuUiFactory.VictoryMutedText;
                    subColor = MenuUiFactory.VictoryMutedText;
                }
                else if (isCurrent)
                {
                    bgColor = entry.isBossMatch
                        ? new Color(0.28f, 0.08f, 0.09f, 0.96f)
                        : new Color(0.118f, 0.337f, 0.600f, 0.92f);
                    borderColor = entry.isBossMatch ? MenuUiFactory.CrimsonActive : MenuUiFactory.UnDFSky;
                    textColor = MenuUiFactory.TextActive;
                    subColor = entry.isBossMatch ? MenuUiFactory.TextSubtitle : MenuUiFactory.UnDFSky;
                }
                else if (entry.isBossMatch)
                {
                    bgColor = MenuUiFactory.CrimsonSurface;
                    borderColor = MenuUiFactory.CrimsonDark;
                    textColor = MenuUiFactory.TextActive;
                    subColor = new Color(0.90f, 0.62f, 0.62f, 1f);
                }
                else
                {
                    bgColor = MenuUiFactory.PanelDarkBg;
                    borderColor = MenuUiFactory.SlateBorderDark;
                    textColor = MenuUiFactory.TextSubtitle;
                    subColor = MenuUiFactory.TextInactive;
                }

                RectTransform rowRt = MenuUiFactory.CreatePanel($"LadderStep_{matchIdx + 1}", _towerLadderRowsContainer, bgColor, sprite);
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -visualRow * rowSpacing);
                rowRt.sizeDelta = new Vector2(0f, rowHeight);
                MenuUiFactory.AddBorder(rowRt, borderColor, sprite, (entry.isBossMatch || isCurrent) ? 2f : 1.2f);

                // Left rung number pill
                Color numBg = entry.isBossMatch
                    ? MenuUiFactory.CrimsonDark
                    : (isCurrent ? MenuUiFactory.UnDFBlue : (defeated ? MenuUiFactory.VictoryMutedBorder : MenuUiFactory.PanelElevatedBg));
                RectTransform numBox = MenuUiFactory.CreatePanel("RungNum", rowRt, numBg, sprite);
                numBox.anchorMin = new Vector2(0f, 0f);
                numBox.anchorMax = new Vector2(0f, 1f);
                numBox.pivot = new Vector2(0f, 0.5f);
                numBox.anchoredPosition = Vector2.zero;
                numBox.sizeDelta = new Vector2(54f, 0f);

                TextMeshProUGUI numTxt = MenuUiFactory.CreateText("NumLabel", numBox, $"{matchIdx + 1}", 22f, FontStyles.Bold, TextAlignmentOptions.Center, MenuUiFactory.TextActive);
                MenuUiFactory.StretchFull(numTxt.rectTransform, 2f, 2f, 2f, 2f);

                // Active diamond indicator
                if (isCurrent)
                {
                    RectTransform dia = MenuUiFactory.CreatePanel(
                        "CurrentMarker",
                        rowRt,
                        entry.isBossMatch ? MenuUiFactory.CrimsonActive : MenuUiFactory.UnDFSky,
                        diamondSprite != null ? diamondSprite : sprite);
                    dia.anchorMin = new Vector2(1f, 0.5f);
                    dia.anchorMax = new Vector2(1f, 0.5f);
                    dia.pivot = new Vector2(1f, 0.5f);
                    dia.anchoredPosition = new Vector2(-18f, 0f);
                    dia.sizeDelta = new Vector2(14f, 14f);
                    if (diamondSprite == null) dia.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }

                string oppName = entry.opponent != null ? entry.opponent.fighterName.ToUpperInvariant() : "???";
                string weatherLabel = entry.GetWeatherDisplayLabel(stageRoster);
                string prefix = defeated ? "[OK] " : string.Empty;
                string titleLine = entry.isBossMatch
                    ? $"{prefix}CHEFE FINAL: {oppName}"
                    : $"{prefix}{oppName}";
                string metaLine = entry.isBossMatch
                    ? $"{entry.stageDisplayName}"
                    : $"{entry.ladderLabel}  •  {entry.stageDisplayName}  •  {weatherLabel}";

                TextMeshProUGUI mainTxt = MenuUiFactory.CreateText("Label", rowRt, titleLine, 20f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor, characterSpacing: 3f);
                mainTxt.enableAutoSizing = true;
                mainTxt.fontSizeMin = 13f;
                mainTxt.fontSizeMax = 20f;
                mainTxt.textWrappingMode = TextWrappingModes.NoWrap;
                mainTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                mainTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
                mainTxt.rectTransform.offsetMin = new Vector2(70f, 0f);
                mainTxt.rectTransform.offsetMax = new Vector2(-46f, -4f);

                TextMeshProUGUI subTxt = MenuUiFactory.CreateText("SubLabel", rowRt, metaLine, 12.5f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, subColor, characterSpacing: 2f);
                subTxt.enableAutoSizing = true;
                subTxt.fontSizeMin = 10f;
                subTxt.fontSizeMax = 12.5f;
                subTxt.textWrappingMode = TextWrappingModes.NoWrap;
                subTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
                subTxt.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                subTxt.rectTransform.offsetMin = new Vector2(70f, 5f);
                subTxt.rectTransform.offsetMax = new Vector2(-46f, 0f);
            }
        }

        private void EnsureWeatherSelectorBar()
        {
            if (stageSelectPanel == null)
            {
                return;
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
                RectTransform bar = MenuUiFactory.CreatePanel(
                    "WeatherSelectorBar",
                    stageSelectPanel.transform,
                    MenuUiFactory.PanelDarkBg,
                    sprite,
                    raycastTarget: true);
                bar.anchorMin = new Vector2(1f, 1f);
                bar.anchorMax = new Vector2(1f, 1f);
                bar.pivot = new Vector2(1f, 1f);
                bar.anchoredPosition = new Vector2(-20f, -14f);
                bar.sizeDelta = new Vector2(430f, 40f);
                MenuUiFactory.AddBorder(bar, MenuUiFactory.SlateBorderMid, sprite, 1.5f);
                weatherSelectorBar = bar.gameObject;

                weatherPrevButton = CreateArrowButton("WeatherPrevBtn", bar, "Q", isLeft: true, sprite);
                weatherNextButton = CreateArrowButton("WeatherNextBtn", bar, "E", isLeft: false, sprite);

                weatherLabelText = MenuUiFactory.CreateText(
                    "WeatherLabel",
                    bar,
                    "CLIMA: PÔR DO SOL",
                    15f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Center,
                    MenuUiFactory.UnDFSky,
                    characterSpacing: 3f);
                weatherLabelText.enableAutoSizing = true;
                weatherLabelText.fontSizeMin = 11f;
                weatherLabelText.fontSizeMax = 15f;
                weatherLabelText.textWrappingMode = TextWrappingModes.NoWrap;
                MenuUiFactory.StretchFull(weatherLabelText.rectTransform, 48f, 2f, 48f, 2f);
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
            RectTransform rt = MenuUiFactory.CreatePanel(btnName, parent, MenuUiFactory.PanelElevatedBg, sprite, raycastTarget: true);
            rt.anchorMin = new Vector2(isLeft ? 0f : 1f, 0f);
            rt.anchorMax = new Vector2(isLeft ? 0f : 1f, 1f);
            rt.pivot = new Vector2(isLeft ? 0f : 1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(42f, 0f);

            var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = rt.GetComponent<UnityEngine.UI.Image>();
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            TextMeshProUGUI arrowTxt = MenuUiFactory.CreateText(
                "Symbol",
                rt,
                symbol,
                16f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.UnDFSky,
                characterSpacing: 0f);
            MenuUiFactory.StretchFull(arrowTxt.rectTransform, 2f, 2f, 2f, 2f);
            return btn;
        }

        public void RebuildStageList()
        {
            _stageChoices.Clear();
            _stageCardViews.Clear();
            if (stageNavigation == null || stageSelectPanel == null)
            {
                return;
            }

            EnsureWeatherSelectorBar();
            stageNavigation.HorizontalPrimary = true;

            if (stageCardsGridContainer == null)
            {
                stageCardsGridContainer = stageSelectPanel.transform.Find("StageCardsGrid");
            }
            if (stageRandomSlotContainer == null)
            {
                stageRandomSlotContainer = stageSelectPanel.transform.Find("StageRandomSlot");
            }

            ClearChildren(stageCardsGridContainer);
            ClearChildren(stageRandomSlotContainer);
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

            // Apply 100% data-driven stage grid layout (1 row <= 4 stages, 2 rows for 5+)
            ComputeStageGridLayout(_stageChoices.Count, out int cols, out int rows, out Vector2 cellSize, out Vector2 spacing, out Vector2 panelSize);
            RectTransform stageBoxRt = stageSelectPanel.GetComponent<RectTransform>();
            if (stageBoxRt != null)
            {
                stageBoxRt.sizeDelta = panelSize;
            }

            if (stageCardsGridContainer != null)
            {
                var glg = stageCardsGridContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                if (glg != null)
                {
                    glg.cellSize = cellSize;
                    glg.spacing = spacing;
                    glg.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                    glg.constraintCount = cols;
                }
            }

            // Panoramic Stage Cards
            for (int i = 0; i < _stageChoices.Count; i++)
            {
                StageData stage = _stageChoices[i];
                StageCardView view = CreateStagePanoramicCard(i, stage, sprite);
                _stageCardViews.Add(view);

                int capturedIdx = i;
                var entry = new MenuItemEntry
                {
                    Button = view.Button,
                    Background = null,
                    Label = view.LabelText,
                    NormalLabelColor = MenuUiFactory.TextSubtitle,
                    SelectedLabelColor = MenuUiFactory.TextActive,
                    OnSubmit = () => SubmitStageChoice(capturedIdx)
                };
                stageNavigation.AddItem(entry);
            }

            // Hexagonal [?] Random Stage Button
            int randomIdx = _stageChoices.Count;
            StageCardView randomView = CreateRandomHexStageButton(randomIdx, sprite);
            _stageCardViews.Add(randomView);
            var randomEntry = new MenuItemEntry
            {
                Button = randomView.Button,
                Background = null,
                Label = randomView.LabelText,
                NormalLabelColor = MenuUiFactory.TextSubtitle,
                SelectedLabelColor = MenuUiFactory.TextActive,
                OnSubmit = () => SubmitStageChoice(randomIdx)
            };
            stageNavigation.AddItem(randomEntry);

            stageNavigation.OnCancel = ReturnToFighterPickFromStageSelect;
            _lastStageNavIndex = -1;
            UpdateStagePreviewFromIndex();
        }

        private StageCardView CreateStagePanoramicCard(int index, StageData stage, Sprite fallbackSprite)
        {
            var go = new GameObject($"StageBtn_{index}", typeof(RectTransform));
            go.transform.SetParent(stageCardsGridContainer != null ? stageCardsGridContainer : stageSelectPanel.transform, false);

            var bg = go.AddComponent<UnityEngine.UI.Image>();
            bg.sprite = stage.previewImage != null ? stage.previewImage : fallbackSprite;
            bg.color = stage.previewImage != null ? Color.white : stage.previewColor;
            bg.preserveAspect = false;
            bg.raycastTarget = true;

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            // Bottom matte scrim for readable stage title
            RectTransform scrim = MenuUiFactory.CreatePanel("BottomScrim", go.transform, new Color(0.043f, 0.055f, 0.078f, 0.84f), fallbackSprite);
            scrim.anchorMin = new Vector2(0f, 0f);
            scrim.anchorMax = new Vector2(1f, 0f);
            scrim.pivot = new Vector2(0.5f, 0f);
            scrim.sizeDelta = new Vector2(0f, 34f);

            TextMeshProUGUI label = MenuUiFactory.CreateText(
                "Label",
                scrim,
                stage.displayName.ToUpperInvariant(),
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 2.5f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = 15f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            MenuUiFactory.StretchFull(label.rectTransform, 8f, 2f, 8f, 2f);

            MenuUiFactory.AddBorder(go.transform, MenuUiFactory.SlateBorderDark, fallbackSprite, 2f);
            var borders = CollectBorderImages(go.transform);

            return new StageCardView
            {
                Index = index,
                IsRandom = false,
                Stage = stage,
                Button = btn,
                PreviewImage = bg,
                BorderImages = borders,
                LabelText = label
            };
        }

        private StageCardView CreateRandomHexStageButton(int index, Sprite fallbackSprite)
        {
            Transform parent = stageRandomSlotContainer != null ? stageRandomSlotContainer : stageSelectPanel.transform;
            var go = new GameObject($"StageBtn_{index}_RandomHex", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 6f);
            rt.sizeDelta = new Vector2(104f, 104f);

            Sprite hex = hexBadgeSprite != null ? hexBadgeSprite : fallbackSprite;
            var outerImg = go.AddComponent<UnityEngine.UI.Image>();
            outerImg.sprite = hex;
            outerImg.color = MenuUiFactory.SlateBorderMid;
            outerImg.raycastTarget = true;

            RectTransform innerRt = MenuUiFactory.CreatePanel("HexInner", go.transform, MenuUiFactory.PanelDarkBg, hex);
            MenuUiFactory.StretchFull(innerRt, 4f, 4f, 4f, 4f);

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = outerImg;
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            TextMeshProUGUI qMark = MenuUiFactory.CreateText("QuestionMark", innerRt, "?", 42f, FontStyles.Bold, TextAlignmentOptions.Center, MenuUiFactory.TextActive);
            MenuUiFactory.StretchFull(qMark.rectTransform, 0f, 4f, 0f, 4f);

            TextMeshProUGUI subLabel = MenuUiFactory.CreateText(
                "Label",
                go.transform,
                "ALEATÓRIO",
                12f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextInactive,
                characterSpacing: 2f);
            subLabel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            subLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            subLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            subLabel.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            subLabel.rectTransform.sizeDelta = new Vector2(124f, 20f);

            return new StageCardView
            {
                Index = index,
                IsRandom = true,
                Stage = null,
                Button = btn,
                OuterFrame = outerImg,
                LabelText = subLabel
            };
        }

        public void RebuildRosterGrid()
        {
            _cards.Clear();
            if (gridContainer == null || fighterRoster == null)
            {
                return;
            }

            ClearChildren(gridContainer);

            // Apply 100% data-driven scalable layout (1 row <= 8 fighters, 2 rows for 9+)
            ComputeRosterGridLayout(fighterRoster.Count, out int cols, out int rows, out Vector2 cellSize, out Vector2 spacing, out Vector2 panelSize);
            _currentRosterColumns = cols;

            var glg = gridContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (glg != null)
            {
                glg.cellSize = cellSize;
                glg.spacing = spacing;
                glg.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = cols;
            }

            if (rosterGridPanel != null)
            {
                var panelRt = rosterGridPanel.GetComponent<RectTransform>();
                if (panelRt != null)
                {
                    panelRt.sizeDelta = panelSize;
                }
            }

            if (fighterNamesStrip != null)
            {
                var stripRt = fighterNamesStrip.GetComponent<RectTransform>();
                if (stripRt != null)
                {
                    stripRt.anchoredPosition = new Vector2(0f, 54f + panelSize.y + 8f);
                    stripRt.sizeDelta = new Vector2(panelSize.x, 86f);
                }
            }

            // Strictly populate children 0 .. fighterRoster.Count - 1 in exact FighterRoster order
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
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            bool isBoss = data != null && data.isSecretBoss;
            Color cardBorderColor = isBoss ? MenuUiFactory.CrimsonDark : MenuUiFactory.SlateBorderDark;
            MenuUiFactory.AddBorder(go.transform, cardBorderColor, sprite, 1.5f);
            var borderImages = CollectBorderImages(go.transform);

            // Base selection strips (MKX style: P1 UnDF blue #3D8AC7, P2 crimson #B91C1C)
            RectTransform p1Rim = MenuUiFactory.CreatePanel("P1Rim", go.transform, MenuUiFactory.UnDFBlue, sprite);
            p1Rim.anchorMin = new Vector2(0f, 0f);
            p1Rim.anchorMax = new Vector2(1f, 0f);
            p1Rim.pivot = new Vector2(0.5f, 0f);
            p1Rim.sizeDelta = new Vector2(0f, 24f);

            RectTransform p2Rim = MenuUiFactory.CreatePanel("P2Rim", go.transform, MenuUiFactory.CrimsonActive, sprite);
            p2Rim.anchorMin = new Vector2(0f, 0f);
            p2Rim.anchorMax = new Vector2(1f, 0f);
            p2Rim.pivot = new Vector2(0.5f, 0f);
            p2Rim.sizeDelta = new Vector2(0f, 24f);

            // Sober matte Lock icon for locked fighters
            UnityEngine.UI.Image lockImg = null;
            if (lockIconSprite != null)
            {
                RectTransform lockRt = MenuUiFactory.CreatePanel("LockIcon", go.transform, new Color(0.58f, 0.64f, 0.72f, 0.85f), lockIconSprite);
                lockRt.anchorMin = new Vector2(0.5f, 1f);
                lockRt.anchorMax = new Vector2(0.5f, 1f);
                lockRt.pivot = new Vector2(0.5f, 1f);
                lockRt.anchoredPosition = new Vector2(0f, -8f);
                lockRt.sizeDelta = new Vector2(22f, 22f);
                lockImg = lockRt.GetComponent<UnityEngine.UI.Image>();
                lockImg.preserveAspect = true;
                lockImg.enabled = false;
            }

            // Preserve exact Name auto-sizing bounds (13..24, NoWrap) verified by automated tests
            TextMeshProUGUI nameTmp = MenuUiFactory.CreateText(
                "Name",
                go.transform,
                data.fighterName.ToUpperInvariant(),
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextActive,
                characterSpacing: 3.5f);
            nameTmp.enableAutoSizing = true;
            nameTmp.fontSizeMin = 13f;
            nameTmp.fontSizeMax = 24f;
            nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
            MenuUiFactory.StretchFull(nameTmp.rectTransform, 8f, 26f, 8f, 12f);

            // Badge in the bottom strip
            TextMeshProUGUI badgeTmp = MenuUiFactory.CreateText(
                "Badge",
                go.transform,
                "",
                12.5f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextActive,
                characterSpacing: 2.5f);
            badgeTmp.enableAutoSizing = true;
            badgeTmp.fontSizeMin = 9.5f;
            badgeTmp.fontSizeMax = 12.5f;
            badgeTmp.textWrappingMode = TextWrappingModes.NoWrap;
            badgeTmp.rectTransform.anchorMin = new Vector2(0f, 0f);
            badgeTmp.rectTransform.anchorMax = new Vector2(1f, 0f);
            badgeTmp.rectTransform.pivot = new Vector2(0.5f, 0f);
            badgeTmp.rectTransform.sizeDelta = new Vector2(-12f, 24f);

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
                LockIcon = lockImg,
                BorderImages = borderImages,
                NameText = nameTmp,
                BadgeText = badgeTmp
            };
        }

        private static UnityEngine.UI.Image[] CollectBorderImages(Transform root)
        {
            var list = new List<UnityEngine.UI.Image>(4);
            foreach (string edgeName in new[] { "Border_Top", "Border_Bottom", "Border_Left", "Border_Right" })
            {
                Transform edge = root.Find(edgeName);
                if (edge != null)
                {
                    var img = edge.GetComponent<UnityEngine.UI.Image>();
                    if (img != null) list.Add(img);
                }
            }
            return list.ToArray();
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform c = parent.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }
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
                if (_inputCooldownFrames <= 0)
                {
                    int weatherStep = ReadWeatherToggleKeys();
                    if (weatherStep != 0)
                    {
                        StepWeatherSelection(weatherStep);
                    }
                }

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

        private static int ReadWeatherToggleKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) return -1;
                if (kb.eKey.wasPressedThisFrame) return 1;
            }

            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
            {
                Gamepad pad = pads[i];
                if (pad == null || !pad.added) continue;
                if (pad.leftShoulder.wasPressedThisFrame) return -1;
                if (pad.rightShoulder.wasPressedThisFrame) return 1;
            }
            return 0;
        }

        private void UpdateArcadeTowerInput()
        {
            ReadMenuStep(0, allowArrowsToo: true, _currentRosterColumns, fighterRoster.Count > 8, ref _p1RepeatCooldown, out int moveStep, out bool confirm, out bool cancel);

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
            ReadMenuStep(0, allowArrowsToo: true, _currentRosterColumns, fighterRoster.Count > 8, ref _p1RepeatCooldown, out int moveStep, out bool confirm, out bool cancel);

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
            bool multiRow = fighterRoster.Count > 8;
            ReadMenuStep(0, allowArrowsToo: false, _currentRosterColumns, multiRow, ref _p1RepeatCooldown, out int p1Move, out bool p1Confirm, out bool p1Cancel);
            ReadMenuStep(1, allowArrowsToo: false, _currentRosterColumns, multiRow, ref _p2RepeatCooldown, out int p2Move, out bool p2Confirm, out bool p2Cancel);

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

            if (fighterNamesStrip != null) fighterNamesStrip.SetActive(true);
            if (rosterGridPanel != null) rosterGridPanel.SetActive(true);
            if (stageSelectPanel != null) stageSelectPanel.SetActive(false);
            if (_towerLadderPanel != null) _towerLadderPanel.SetActive(false);
            SetStageBackdropVisible(false, null, Color.white);
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

            if (fighterNamesStrip != null) fighterNamesStrip.SetActive(false);
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
                stageNavigation.SetSelectedIndex(initialStageIdx, notifyAudio: false);
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
            if (fighterNamesStrip != null) fighterNamesStrip.SetActive(false);
            if (rosterGridPanel != null) rosterGridPanel.SetActive(false);
            if (stageSelectPanel != null) stageSelectPanel.SetActive(false);
            SetStageBackdropVisible(false, null, Color.white);
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

            // Highlight selected panoramic card or hexagonal [?] button
            for (int i = 0; i < _stageCardViews.Count; i++)
            {
                StageCardView view = _stageCardViews[i];
                bool selected = i == idx;

                if (view.IsRandom)
                {
                    if (view.OuterFrame != null)
                    {
                        view.OuterFrame.color = selected ? MenuUiFactory.UnDFBlue : MenuUiFactory.SlateBorderMid;
                    }
                    if (view.LabelText != null)
                    {
                        view.LabelText.color = selected ? MenuUiFactory.UnDFSky : MenuUiFactory.TextInactive;
                    }
                }
                else
                {
                    if (view.BorderImages != null)
                    {
                        Color bCol = selected ? MenuUiFactory.UnDFBlue : MenuUiFactory.SlateBorderDark;
                        for (int b = 0; b < view.BorderImages.Length; b++)
                        {
                            if (view.BorderImages[b] != null) view.BorderImages[b].color = bCol;
                        }
                    }
                    if (view.PreviewImage != null && view.Stage != null)
                    {
                        Color baseCol = view.Stage.previewImage != null ? Color.white : view.Stage.previewColor;
                        if (selected && view.Stage.supportsWeatherVariants)
                        {
                            baseCol = ApplyWeatherPreviewTint(baseCol, weather);
                        }
                        else if (!selected)
                        {
                            baseCol *= 0.62f;
                            baseCol.a = 1f;
                        }
                        view.PreviewImage.color = baseCol;
                    }
                    if (view.LabelText != null)
                    {
                        view.LabelText.color = selected ? MenuUiFactory.TextActive : MenuUiFactory.TextInactive;
                    }
                }
            }

            if (idx >= 0 && idx < _stageChoices.Count)
            {
                StageData stage = _stageChoices[idx];
                bool supportsWeather = stage.supportsWeatherVariants;
                StageWeatherVariant effectiveWeather = supportsWeather ? weather : stage.defaultWeather;

                stagePreviewTitle.text = stage.displayName.ToUpperInvariant();
                if (supportsWeather)
                {
                    string weatherLabel = GameSessionState.GetWeatherLabel(effectiveWeather, stage);
                    stagePreviewSubtitle.text = $"{stage.subtitle}  •  {weatherLabel}";
                }
                else
                {
                    stagePreviewSubtitle.text = $"{stage.subtitle}  •  ILUMINAÇÃO FIXA";
                }

                Color tint = supportsWeather
                    ? ApplyWeatherPreviewTint(new Color(0.40f, 0.44f, 0.50f, 1f), effectiveWeather)
                    : new Color(0.36f, 0.40f, 0.46f, 1f);
                SetStageBackdropVisible(true, stage.previewImage != null ? stage.previewImage.texture : null, tint);

                if (stagePreviewBanner != null)
                {
                    stagePreviewBanner.sprite = stage.previewImage != null ? stage.previewImage : ResolveWhiteSprite();
                }

                UpdateWeatherSelectorVisuals(supportsWeather, effectiveWeather, stage, isRandom: false);
            }
            else
            {
                stagePreviewTitle.text = "ARENA ALEATÓRIA";
                string weatherLabel = GameSessionState.GetWeatherLabel(weather, null);
                stagePreviewSubtitle.text = _weatherManuallyChosenInStageSelect
                    ? $"Arena aleatória  •  Clima: {weatherLabel}"
                    : "Arena e clima escolhidos aleatoriamente";

                SetStageBackdropVisible(true, null, new Color(0.08f, 0.12f, 0.19f, 1f));
                UpdateWeatherSelectorVisuals(supportsWeather: true, weather, stage: null, isRandom: true);
            }

            UpdateModeHeader();
        }

        private void SetStageBackdropVisible(bool visible, Texture tex, Color tint)
        {
            if (stageBackdropRenderer == null)
            {
                return;
            }

            stageBackdropRenderer.gameObject.SetActive(visible);
            if (!visible) return;

            if (_runtimeBackdropMat == null)
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
                if (unlit != null)
                {
                    _runtimeBackdropMat = new Material(unlit);
                    stageBackdropRenderer.sharedMaterial = _runtimeBackdropMat;
                }
            }

            if (_runtimeBackdropMat != null)
            {
                Texture useTex = tex != null ? tex : Texture2D.whiteTexture;
                if (_runtimeBackdropMat.HasProperty(BaseMapId)) _runtimeBackdropMat.SetTexture(BaseMapId, useTex);
                if (_runtimeBackdropMat.HasProperty(MainTexId)) _runtimeBackdropMat.SetTexture(MainTexId, useTex);
                if (_runtimeBackdropMat.HasProperty(BaseColorId)) _runtimeBackdropMat.SetColor(BaseColorId, tint);
                if (_runtimeBackdropMat.HasProperty(ColorId)) _runtimeBackdropMat.SetColor(ColorId, tint);
            }
        }

        private static Color ApplyWeatherPreviewTint(Color baseColor, StageWeatherVariant weather)
        {
            return weather switch
            {
                StageWeatherVariant.Night => new Color(baseColor.r * 0.50f, baseColor.g * 0.58f, baseColor.b * 0.92f, baseColor.a),
                StageWeatherVariant.RainyNight => new Color(baseColor.r * 0.38f, baseColor.g * 0.48f, baseColor.b * 0.80f, baseColor.a),
                _ => baseColor
            };
        }

        private void UpdateWeatherSelectorVisuals(bool supportsWeather, StageWeatherVariant weather, StageData stage, bool isRandom)
        {
            if (weatherLabelText != null)
            {
                if (!supportsWeather)
                {
                    weatherLabelText.text = "ILUMINAÇÃO FIXA";
                    weatherLabelText.color = MenuUiFactory.TextInactive;
                }
                else
                {
                    string wLabel = (isRandom && !_weatherManuallyChosenInStageSelect)
                        ? "ALEATÓRIO"
                        : GameSessionState.GetWeatherLabel(weather, stage);
                    weatherLabelText.text = $"CLIMA: {wLabel}";
                    weatherLabelText.color = weather switch
                    {
                        StageWeatherVariant.Night => MenuUiFactory.UnDFSky,
                        StageWeatherVariant.RainyNight => MenuUiFactory.UnDFBlue,
                        _ => MenuUiFactory.TextActive
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
                txt.color = active ? MenuUiFactory.UnDFSky : new Color(0.36f, 0.42f, 0.48f, 0.55f);
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
                    _difficultyBarLabel.text = $"{prefix} • {GameSessionState.GetCpuDifficultyLabel(GameSessionState.SelectedCpuDifficulty)} (TAB)";
                }
            }
        }

        private void UpdateTowerSummarySlotOnP2()
        {
            ClearPreviewAnchor(p2PreviewAnchor, ref _p2PreviewInstance, ref _p2PreviewAnimator, ref _p2SpawnedData);

            if (p2HeaderText != null) p2HeaderText.text = "TORRE ARCADE";
            if (p2NameText != null) p2NameText.text = "TORRE ARCADE";
            if (p2HeightText != null) p2HeightText.text = $"DIFICULDADE: {GameSessionState.GetCpuDifficultyLabel(GameSessionState.SelectedCpuDifficulty)}";
            if (p2WeightText != null) p2WeightText.text = "DESAFIO PROGRESSIVO";
            if (p2StatusText != null)
            {
                p2StatusText.text = "ESCOLHA SEU DESAFIANTE";
                p2StatusText.color = MenuUiFactory.UnDFSky;
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
            if (modeBannerText != null)
            {
                modeBannerText.text = _currentStep switch
                {
                    SelectStep.StageSelect => "SELEÇÃO DE ARENA",
                    SelectStep.TowerLadder => "TORRE ARCADE",
                    _ => "SELEÇÃO DE LUTADOR"
                };
            }

            if (footerHintText != null)
            {
                if (_currentStep == SelectStep.TowerLadder)
                {
                    footerHintText.text = "W/S OU SETAS: NAVEGAR   •   TAB / Y: DIFICULDADE   •   ENTER / J: INICIAR PRÓXIMA LUTA   •   ESC / K: VOLTAR";
                }
                else if (_currentStep == SelectStep.StageSelect)
                {
                    bool selectedSupportsWeather = true;
                    if (stageNavigation != null && stageNavigation.SelectedIndex >= 0 && stageNavigation.SelectedIndex < _stageChoices.Count)
                    {
                        StageData st = _stageChoices[stageNavigation.SelectedIndex];
                        selectedSupportsWeather = st != null && st.supportsWeatherVariants;
                    }

                    string weatherHint = selectedSupportsWeather
                        ? "[Q / E] ALTERNAR CLIMA  •  DIA / NOITE / NOITE CHUVOSA"
                        : "[Q / E] ILUMINAÇÃO FIXA";
                    footerHintText.text = $"{weatherHint}   •   ENTER / J CONFIRMAR   •   ESC / K VOLTAR";
                }
                else if (GameSessionState.CurrentMode == GameMode.ArcadeTower)
                {
                    footerHintText.text = "A/D OU SETAS: ESCOLHER LUTADOR   •   TAB / Y: DIFICULDADE   •   ENTER / J CONFIRMAR   •   ESC / K VOLTAR";
                }
                else
                {
                    footerHintText.text = GameSessionState.CurrentMode == GameMode.VersusCpu
                        ? "P1: WASD + J/ENTER   •   TAB / Y: DIFICULDADE CPU   •   ESC / K: MENU PRINCIPAL"
                        : "P1: WASD + J/ENTER   •   P2: SETAS + NUM4/ENTER   •   ESC / K: MENU PRINCIPAL";
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

                if (c.P1Outline != null)
                {
                    c.P1Outline.enabled = isP1 && unlocked;
                    c.P1Outline.color = MenuUiFactory.UnDFBlue;
                }
                if (c.P2Outline != null)
                {
                    c.P2Outline.enabled = isP2 && !isP1 && unlocked;
                    c.P2Outline.color = MenuUiFactory.CrimsonActive;
                }
                if (c.LockIcon != null)
                {
                    c.LockIcon.enabled = !unlocked;
                }

                if (c.BorderImages != null)
                {
                    Color borderCol;
                    if (!unlocked)
                    {
                        borderCol = (isP1 || isP2) ? MenuUiFactory.CrimsonDark : MenuUiFactory.SlateBorderDark;
                    }
                    else if (isP1)
                    {
                        borderCol = MenuUiFactory.UnDFBlue;
                    }
                    else if (isP2)
                    {
                        borderCol = MenuUiFactory.CrimsonActive;
                    }
                    else if (isBoss)
                    {
                        borderCol = MenuUiFactory.CrimsonDark;
                    }
                    else
                    {
                        borderCol = MenuUiFactory.SlateBorderDark;
                    }

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
                        c.NameText.color = MenuUiFactory.TextInactive;
                    }
                    else
                    {
                        c.NameText.text = c.Data != null ? c.Data.fighterName.ToUpperInvariant() : string.Empty;
                        c.NameText.color = (isP1 || isP2) ? MenuUiFactory.TextActive : MenuUiFactory.TextSubtitle;
                    }
                }

                if (c.Background != null)
                {
                    if (!unlocked)
                    {
                        c.Background.color = (isP1 || isP2)
                            ? new Color(0.11f, 0.08f, 0.10f, 0.96f)
                            : MenuUiFactory.ObsidianBg;
                    }
                    else if (isP1)
                    {
                        c.Background.color = new Color(0.10f, 0.19f, 0.31f, 0.96f);
                    }
                    else if (isP2)
                    {
                        c.Background.color = new Color(0.22f, 0.08f, 0.09f, 0.96f);
                    }
                    else if (isBoss)
                    {
                        c.Background.color = MenuUiFactory.CrimsonSurface;
                    }
                    else
                    {
                        c.Background.color = MenuUiFactory.PanelDarkBg;
                    }
                }

                if (c.BadgeText != null)
                {
                    if (!unlocked)
                    {
                        c.BadgeText.text = "BLOQUEADO";
                        c.BadgeText.color = new Color(0.84f, 0.34f, 0.34f, 0.96f);
                    }
                    else if (isP1 && isP2)
                    {
                        c.BadgeText.text = "P1 + P2";
                        c.BadgeText.color = MenuUiFactory.TextActive;
                    }
                    else if (isP1)
                    {
                        c.BadgeText.text = _p1Confirmed ? "P1 PRONTO" : "P1";
                        c.BadgeText.color = MenuUiFactory.TextActive;
                    }
                    else if (isP2)
                    {
                        c.BadgeText.text = _p2Confirmed ? "P2 PRONTO" : "P2";
                        c.BadgeText.color = MenuUiFactory.TextActive;
                    }
                    else if (isBoss)
                    {
                        c.BadgeText.text = "CHEFE FINAL";
                        c.BadgeText.color = new Color(0.92f, 0.56f, 0.56f, 0.96f);
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
                    hdrTmp.text = "P1 • JOGADOR 1";
                }
                else if (GameSessionState.CurrentMode == GameMode.ArcadeTower)
                {
                    hdrTmp.text = "1º DESAFIO";
                }
                else
                {
                    hdrTmp.text = GameSessionState.CurrentMode == GameMode.VersusCpu
                        ? $"CPU • {GameSessionState.GetCpuDifficultyLabel(GameSessionState.SelectedCpuDifficulty)}"
                        : (GameSessionState.CurrentMode == GameMode.Training ? "P2 • TREINO" : "P2 • JOGADOR 2");
                }
            }

            if (!unlocked)
            {
                ClearPreviewAnchor(anchor, ref instance, ref animator, ref spawnedData);

                if (nameTmp != null) nameTmp.text = "???";
                if (heightTmp != null) heightTmp.text = "BLOQUEADO";
                if (weightTmp != null)
                {
                    weightTmp.text = data.isSecretBoss ? "CHEFE FINAL SECRETO" : "LUTADOR BLOQUEADO";
                }
                if (statusTmp != null)
                {
                    statusTmp.text = LockedUnlockHintMessage;
                    statusTmp.color = new Color(0.90f, 0.44f, 0.42f, 1f);
                }
                return;
            }

            if (nameTmp != null) nameTmp.text = data.fighterName.ToUpperInvariant();
            if (heightTmp != null) heightTmp.text = $"ALTURA: {data.heightMeters.ToString("0.00").Replace('.', ',')} M";
            if (weightTmp != null)
            {
                string weightLabel = FighterScaling.GetWeightClassLabel(data.WeightCategory);
                string styleLabel = FighterScaling.GetArchetypeLabel(data.archetype);
                weightTmp.text = $"{Mathf.RoundToInt(data.weightKg)} KG • {weightLabel} • {styleLabel}";
            }
            if (statusTmp != null)
            {
                bool waitingForP1 = slotIndex == 1 && GameSessionState.P1SelectsBothFighters && !_p1Confirmed;
                if (slotIndex == 1 && GameSessionState.CurrentMode == GameMode.ArcadeTower)
                {
                    statusTmp.text = "LUTA 1/7";
                    statusTmp.color = MenuUiFactory.CrimsonActive;
                }
                else
                {
                    statusTmp.text = confirmed ? "CONFIRMADO" : (waitingForP1 ? "AGUARDANDO P1..." : "SELECIONANDO...");
                    statusTmp.color = confirmed
                        ? (slotIndex == 0 ? MenuUiFactory.UnDFSky : new Color(0.96f, 0.62f, 0.62f, 1f))
                        : (waitingForP1 ? MenuUiFactory.TextInactive : MenuUiFactory.TextSubtitle);
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

        private static void ReadMenuStep(
            int playerIndex,
            bool allowArrowsToo,
            int columns,
            bool multiRow,
            ref float cooldown,
            out int moveStep,
            out bool confirm,
            out bool cancel)
        {
            moveStep = 0;
            confirm = false;
            cancel = false;

            float dirX = 0f;
            float dirY = 0f;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (playerIndex == 0)
                {
                    if (kb.dKey.isPressed || (allowArrowsToo && kb.rightArrowKey.isPressed)) dirX += 1f;
                    if (kb.aKey.isPressed || (allowArrowsToo && kb.leftArrowKey.isPressed)) dirX -= 1f;
                    if (kb.sKey.isPressed || (allowArrowsToo && kb.downArrowKey.isPressed)) dirY += 1f;
                    if (kb.wKey.isPressed || (allowArrowsToo && kb.upArrowKey.isPressed)) dirY -= 1f;

                    if (kb.jKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) confirm = true;
                    if (kb.kKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) cancel = true;
                }
                else
                {
                    if (kb.rightArrowKey.isPressed) dirX += 1f;
                    if (kb.leftArrowKey.isPressed) dirX -= 1f;
                    if (kb.downArrowKey.isPressed) dirY += 1f;
                    if (kb.upArrowKey.isPressed) dirY -= 1f;

                    if (kb.numpad4Key.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) confirm = true;
                    if (kb.numpad5Key.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) cancel = true;
                }
            }

            var pads = Gamepad.all;
            Gamepad pad = playerIndex < pads.Count ? pads[playerIndex] : null;
            if (pad != null && pad.added)
            {
                float gx = Mathf.Abs(pad.dpad.x.ReadValue()) > 0.3f ? pad.dpad.x.ReadValue() : pad.leftStick.x.ReadValue();
                float gy = Mathf.Abs(pad.dpad.y.ReadValue()) > 0.3f ? pad.dpad.y.ReadValue() : pad.leftStick.y.ReadValue();
                if (Mathf.Abs(gx) > 0.45f && Mathf.Approximately(dirX, 0f)) dirX = Mathf.Sign(gx);
                if (Mathf.Abs(gy) > 0.45f && Mathf.Approximately(dirY, 0f)) dirY = -Mathf.Sign(gy);
                if (pad.buttonSouth.wasPressedThisFrame) confirm = true;
                if (pad.buttonEast.wasPressedThisFrame) cancel = true;
            }

            int desiredStep = 0;
            if (Mathf.Abs(dirX) > 0.2f)
            {
                desiredStep = dirX > 0f ? 1 : -1;
            }
            else if (Mathf.Abs(dirY) > 0.2f)
            {
                int rowStride = multiRow ? Mathf.Max(1, columns) : 1;
                desiredStep = dirY > 0f ? rowStride : -rowStride;
            }

            if (desiredStep != 0)
            {
                if (cooldown <= 0f)
                {
                    moveStep = desiredStep;
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
