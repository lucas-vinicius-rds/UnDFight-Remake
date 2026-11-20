using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// MKX-inspired 2.5D combat HUD with official matte UnDF identity:
    /// - Ultra-clean top bar with tapered blade health bars (UI_BladeBarLeft/Right), silver-matte fill
    ///   transitioning to crimson below 28%, dark crimson delayed damage trail, floating archetype/name,
    ///   diamond round pips, boxless floating timer, and subtle central Logo_UnDF_Emblem.
    /// - Bottom-corner EX/SUPER meters with data-driven meterSegmentCount (default 2, ready for 3-bar Ultimates),
    ///   compact circular medallions above each bar, segment dividers, and official UnDF blue signature base.
    /// - MKX combo/string counter with #8BC9EE vertical bar, unscaledDeltaTime pop animation, and "STRING • X DANO".
    /// - Matte graphite stage interaction prompts with #3D8AC7 left edge and #1E5699 key tag.
    /// </summary>
    public class FightHUD : MonoBehaviour
    {
        private sealed class StagePromptWidget
        {
            public RectTransform Root;
            public UnityEngine.UI.Image Border;
            public UnityEngine.UI.Image Background;
            public UnityEngine.UI.Image LeftAccent;
            public UnityEngine.UI.Image KeyTagBg;
            public TextMeshProUGUI KeyTagLabel;
            public TextMeshProUGUI Label;
            public string FullPromptText;
        }

        private sealed class MeterMedallionView
        {
            public UnityEngine.UI.Image Ring;
            public UnityEngine.UI.Image Fill;
            public TextMeshProUGUI Label;
        }

        [Header("Simulation & Match")]
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private MatchManager matchManager;
        [SerializeField] private StageInteractableManager stageInteractableManager;
        [SerializeField] private FightMenuController fightMenuController;

        [Header("Theme Sprites")]
        [SerializeField] private Sprite whiteSprite;
        [SerializeField] private Sprite bladeBarLeftSprite;
        [SerializeField] private Sprite bladeBarRightSprite;
        [SerializeField] private Sprite diamondSprite;
        [SerializeField] private Sprite circleBadgeSprite;
        [SerializeField] private Sprite undfEmblemSprite;
        [SerializeField] private Sprite softSweepSprite;

        [Header("P1 Widgets (Top Left)")]
        [SerializeField] private UnityEngine.UI.Image p1HealthFill;
        [SerializeField] private UnityEngine.UI.Image p1DamageTrailFill;
        [SerializeField] private TextMeshProUGUI p1NameText;
        [SerializeField] private TextMeshProUGUI p1NameShadow;
        [SerializeField] private TextMeshProUGUI p1ArchetypeText;
        [SerializeField] private TextMeshProUGUI p1ArchetypeShadow;
        [SerializeField] private UnityEngine.UI.Image p1ArchetypeDiamond;
        [SerializeField] private UnityEngine.UI.Image[] p1RoundPips;
        [SerializeField] private TextMeshProUGUI p1CalloutText;

        [Header("P2 Widgets (Top Right)")]
        [SerializeField] private UnityEngine.UI.Image p2HealthFill;
        [SerializeField] private UnityEngine.UI.Image p2DamageTrailFill;
        [SerializeField] private TextMeshProUGUI p2NameText;
        [SerializeField] private TextMeshProUGUI p2NameShadow;
        [SerializeField] private TextMeshProUGUI p2ArchetypeText;
        [SerializeField] private TextMeshProUGUI p2ArchetypeShadow;
        [SerializeField] private UnityEngine.UI.Image p2ArchetypeDiamond;
        [SerializeField] private UnityEngine.UI.Image[] p2RoundPips;
        [SerializeField] private TextMeshProUGUI p2CalloutText;

        [Header("Bottom-Corner EX / SUPER Meters (Data-Driven)")]
        [SerializeField, Min(1)] private int meterSegmentCount = 2;
        [SerializeField] private RectTransform p1EnergyMeterRoot;
        [SerializeField] private RectTransform p2EnergyMeterRoot;
        [SerializeField] private UnityEngine.UI.Image p1SuperFill;
        [SerializeField] private TextMeshProUGUI p1SuperText;
        [SerializeField] private TextMeshProUGUI p1SuperTextShadow;
        [SerializeField] private UnityEngine.UI.Image p2SuperFill;
        [SerializeField] private TextMeshProUGUI p2SuperText;
        [SerializeField] private TextMeshProUGUI p2SuperTextShadow;

        [Header("Combo / String Counter")]
        [SerializeField] private GameObject p1ComboContainer;
        [SerializeField] private TextMeshProUGUI p1ComboNumberText;
        [SerializeField] private TextMeshProUGUI p1ComboDetailText;
        [SerializeField] private TextMeshProUGUI p1ComboText;
        [SerializeField] private GameObject p2ComboContainer;
        [SerializeField] private TextMeshProUGUI p2ComboNumberText;
        [SerializeField] private TextMeshProUGUI p2ComboDetailText;
        [SerializeField] private TextMeshProUGUI p2ComboText;

        [Header("Center Timer, Emblem & Banner")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI timerShadow;
        [SerializeField] private UnityEngine.UI.Image centerEmblemImage;
        [SerializeField] private GameObject bannerContainer;
        [SerializeField] private TextMeshProUGUI bannerText;
        [SerializeField] private TextMeshProUGUI bannerTextShadow;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI subtitleTextShadow;

        [Header("Visual Styling (MKX Matte + Official UnDF)")]
        [SerializeField] private Color healthyColor = new Color(0.929f, 0.949f, 0.969f, 1.0f);       // #EDF2F7 silver-white matte
        [SerializeField] private Color lowHealthColor = new Color(0.863f, 0.149f, 0.149f, 1.0f);     // #DC2626 crimson < 28%
        [SerializeField] private Color damageTrailColor = new Color(0.600f, 0.106f, 0.106f, 0.94f);  // #991B1B dark crimson trail
        [SerializeField] private Color roundWonColor = new Color(0.545f, 0.788f, 0.933f, 1.0f);      // #8BC9EE UnDF sky diamond
        [SerializeField] private Color roundEmptyColor = new Color(0.043f, 0.055f, 0.078f, 0.85f);   // Matte dark center inside steel outline
        [SerializeField] private Color superChargingColor = new Color(0.118f, 0.337f, 0.600f, 1.0f); // #1E5699 UnDF royal
        [SerializeField] private Color superExReadyColor = new Color(0.239f, 0.541f, 0.780f, 1.0f);  // #3D8AC7 UnDF blue
        [SerializeField] private Color superReadyColorA = new Color(0.239f, 0.541f, 0.780f, 1.0f);   // #3D8AC7
        [SerializeField] private Color superReadyColorB = new Color(0.545f, 0.788f, 0.933f, 1.0f);   // #8BC9EE
        [SerializeField] private float lowHealthThreshold = 0.28f;
        [SerializeField] private float trailHoldSeconds = 0.28f;
        [SerializeField] private float trailDecaySpeed = 0.75f;
        [SerializeField] private float calloutDurationSeconds = 0.65f;
        [SerializeField] private float comboDisplayDurationSeconds = 1.55f;

        private float _p1TrailRatio = 1f;
        private float _p2TrailRatio = 1f;
        private float _p1TrailHoldTimer;
        private float _p2TrailHoldTimer;
        private float _p1PrevRatio = 1f;
        private float _p2PrevRatio = 1f;

        private float _p1CalloutTimer;
        private float _p2CalloutTimer;
        private float _p1ComboTimer;
        private float _p2ComboTimer;
        private float _p1ComboPopTimer;
        private float _p2ComboPopTimer;
        private int _p1ComboDamageAccum;
        private int _p2ComboDamageAccum;

        private Sprite _fallbackWhiteSprite;
        private StagePromptWidget _p1StagePrompt;
        private StagePromptWidget _p2StagePrompt;
        private RectTransform _towerBadgeRoot;
        private UnityEngine.UI.Image _towerBadgeBorder;
        private UnityEngine.UI.Image _towerBadgeBg;
        private TextMeshProUGUI _towerBadgeLabel;

        private bool _searchedFightMenu;
        private readonly List<MeterMedallionView> _p1Medallions = new List<MeterMedallionView>();
        private readonly List<MeterMedallionView> _p2Medallions = new List<MeterMedallionView>();

        public bool IsTowerBadgeVisible => _towerBadgeRoot != null && _towerBadgeRoot.gameObject.activeSelf;
        public string TowerBadgeText => _towerBadgeLabel != null ? _towerBadgeLabel.text : string.Empty;
        public RectTransform P1EnergyMeterRoot => p1EnergyMeterRoot;
        public RectTransform P2EnergyMeterRoot => p2EnergyMeterRoot;
        public UnityEngine.UI.Image CenterEmblemImage => centerEmblemImage;
        public GameObject BannerContainer => bannerContainer;
        public bool IsBannerVisible => bannerContainer != null && bannerContainer.activeSelf;

        public void SetFightMenuController(FightMenuController controller)
        {
            fightMenuController = controller;
            _searchedFightMenu = true;
        }

        public int MeterSegmentCount
        {
            get => Mathf.Max(1, meterSegmentCount);
            set
            {
                int next = Mathf.Max(1, value);
                if (meterSegmentCount != next || p1EnergyMeterRoot == null || p2EnergyMeterRoot == null)
                {
                    meterSegmentCount = next;
                    RebuildBottomEnergyMeters();
                }
            }
        }

        public int GetMeterDividerCount(int playerIndex)
        {
            RectTransform root = playerIndex == 0 ? p1EnergyMeterRoot : p2EnergyMeterRoot;
            if (root == null) return 0;
            Transform barFrame = root.Find("EnergyBarFrame");
            if (barFrame == null) return 0;
            int count = 0;
            for (int i = 0; i < barFrame.childCount; i++)
            {
                if (barFrame.GetChild(i).name.StartsWith("SegmentDivider_", System.StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        public int GetMeterMedallionCount(int playerIndex)
        {
            RectTransform root = playerIndex == 0 ? p1EnergyMeterRoot : p2EnergyMeterRoot;
            if (root == null) return 0;
            Transform row = root.Find("MedallionsRow");
            if (row == null) return 0;
            int count = 0;
            for (int i = 0; i < row.childCount; i++)
            {
                if (row.GetChild(i).name.StartsWith("Medallion_", System.StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        public void ConfigureThemeSprites(
            Sprite white,
            Sprite bladeLeft,
            Sprite bladeRight,
            Sprite diamond,
            Sprite circleBadge,
            Sprite emblem,
            Sprite softSweep = null)
        {
            if (white != null) whiteSprite = white;
            if (bladeLeft != null) bladeBarLeftSprite = bladeLeft;
            if (bladeRight != null) bladeBarRightSprite = bladeRight;
            if (diamond != null) diamondSprite = diamond;
            if (circleBadge != null) circleBadgeSprite = circleBadge;
            if (emblem != null) undfEmblemSprite = emblem;
            if (softSweep != null) softSweepSprite = softSweep;
        }

        public void Configure(FightSimulation sim, MatchManager manager = null)
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
            }
            simulation = sim;
            matchManager = manager;
            if (simulation != null && isActiveAndEnabled)
            {
                simulation.OnCombatHit += HandleCombatHit;
                simulation.OnSuperFlashTriggered += HandleSuperFlashTriggered;
            }
            EnsureSuperAndComboWidgets();
            EnsureStagePromptWidgets();
            EnsureTowerBadgeWidget();
            RefreshImmediate();
        }

        public void Configure(
            FightSimulation sim,
            MatchManager manager,
            UnityEngine.UI.Image p1Fill,
            UnityEngine.UI.Image p1Trail,
            TextMeshProUGUI p1Name,
            UnityEngine.UI.Image[] p1Pips,
            TextMeshProUGUI p1Callout,
            UnityEngine.UI.Image p2Fill,
            UnityEngine.UI.Image p2Trail,
            TextMeshProUGUI p2Name,
            UnityEngine.UI.Image[] p2Pips,
            TextMeshProUGUI p2Callout,
            TextMeshProUGUI timer,
            GameObject bannerObj,
            TextMeshProUGUI banner,
            TextMeshProUGUI subtitle)
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
            }

            simulation = sim;
            matchManager = manager;
            p1HealthFill = p1Fill;
            p1DamageTrailFill = p1Trail;
            p1NameText = p1Name;
            p1RoundPips = p1Pips;
            p1CalloutText = p1Callout;
            p2HealthFill = p2Fill;
            p2DamageTrailFill = p2Trail;
            p2NameText = p2Name;
            p2RoundPips = p2Pips;
            p2CalloutText = p2Callout;
            timerText = timer;
            bannerContainer = bannerObj;
            bannerText = banner;
            subtitleText = subtitle;

            if (simulation != null && isActiveAndEnabled)
            {
                simulation.OnCombatHit += HandleCombatHit;
                simulation.OnSuperFlashTriggered += HandleSuperFlashTriggered;
            }
            EnsureSuperAndComboWidgets();
            EnsureStagePromptWidgets();
            EnsureTowerBadgeWidget();
            RefreshImmediate();
        }

        public void ApplyDefaultMkxUnDFPalette()
        {
            healthyColor = new Color(0.929f, 0.949f, 0.969f, 1.0f);       // #EDF2F7 silver-white matte
            lowHealthColor = new Color(0.863f, 0.149f, 0.149f, 1.0f);     // #DC2626 crimson < 28%
            damageTrailColor = new Color(0.600f, 0.106f, 0.106f, 0.94f);  // #991B1B dark crimson trail
            roundWonColor = MenuUiFactory.UnDFSky;                        // #8BC9EE UnDF sky diamond
            roundEmptyColor = new Color(0.043f, 0.055f, 0.078f, 0.85f);   // Matte dark center inside steel outline
            superChargingColor = MenuUiFactory.UnDFRoyal;                 // #1E5699
            superExReadyColor = MenuUiFactory.UnDFBlue;                   // #3D8AC7
            superReadyColorA = MenuUiFactory.UnDFBlue;                    // #3D8AC7
            superReadyColorB = MenuUiFactory.UnDFSky;                     // #8BC9EE
            lowHealthThreshold = 0.28f;
        }

        /// <summary>
        /// Builds the full MKX + official UnDF combat HUD hierarchy cleanly inside the given canvasRoot.
        /// Used both by UiThemeSetupEditor (across all 4 arenas) and Edit Mode tests.
        /// </summary>
        public void BuildMkxUI(Transform canvasRoot)
        {
            ApplyDefaultMkxUnDFPalette();

            for (int i = canvasRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = canvasRoot.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            _p1StagePrompt = null;
            _p2StagePrompt = null;
            _towerBadgeRoot = null;
            p1EnergyMeterRoot = null;
            p2EnergyMeterRoot = null;
            _p1Medallions.Clear();
            _p2Medallions.Clear();

            Sprite solid = GetHudSprite();
            Sprite bladeLeft = bladeBarLeftSprite != null ? bladeBarLeftSprite : solid;
            Sprite bladeRight = bladeBarRightSprite != null ? bladeBarRightSprite : solid;
            Sprite diamond = diamondSprite != null ? diamondSprite : solid;

            // 1. TOP COMBAT BAR (ultra-clean, no heavy container boxes)
            var topBarGo = new GameObject("TopCombatBar", typeof(RectTransform));
            topBarGo.transform.SetParent(canvasRoot, false);
            var topBarRt = (RectTransform)topBarGo.transform;
            topBarRt.anchorMin = new Vector2(0f, 1f);
            topBarRt.anchorMax = new Vector2(1f, 1f);
            topBarRt.pivot = new Vector2(0.5f, 1f);
            topBarRt.anchoredPosition = Vector2.zero;
            topBarRt.sizeDelta = new Vector2(0f, 160f);

            // Center Timer + Official UnDF Emblem (NO heavy box around timer!)
            var centerGo = new GameObject("CenterTimerGroup", typeof(RectTransform));
            centerGo.transform.SetParent(topBarRt, false);
            var centerRt = (RectTransform)centerGo.transform;
            centerRt.anchorMin = new Vector2(0.5f, 1f);
            centerRt.anchorMax = new Vector2(0.5f, 1f);
            centerRt.pivot = new Vector2(0.5f, 1f);
            centerRt.anchoredPosition = new Vector2(0f, -10f);
            centerRt.sizeDelta = new Vector2(146f, 108f);

            timerShadow = CreateShadowedLabel(
                centerRt,
                "TimerText",
                "99",
                56f,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextActive,
                characterSpacing: 2f,
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(0f, -2f),
                sizeDelta: new Vector2(0f, 64f),
                out timerText);

            var embGo = new GameObject("CenterUnDFEmblem", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            embGo.transform.SetParent(centerRt, false);
            var embRt = (RectTransform)embGo.transform;
            embRt.anchorMin = new Vector2(0.5f, 1f);
            embRt.anchorMax = new Vector2(0.5f, 1f);
            embRt.pivot = new Vector2(0.5f, 1f);
            embRt.anchoredPosition = new Vector2(0f, -68f);
            embRt.sizeDelta = new Vector2(54f, 32f);
            centerEmblemImage = embGo.GetComponent<UnityEngine.UI.Image>();
            centerEmblemImage.sprite = undfEmblemSprite != null ? undfEmblemSprite : solid;
            centerEmblemImage.preserveAspect = true;
            centerEmblemImage.color = undfEmblemSprite != null ? new Color(1f, 1f, 1f, 0.92f) : new Color(0.239f, 0.541f, 0.780f, 0.75f);
            centerEmblemImage.raycastTarget = false;

            // Left P1 & Right P2 Health Groups
            BuildTopFighterSide(
                topBarRt,
                isLeft: true,
                bladeSprite: bladeLeft,
                solidSprite: solid,
                diamondSpriteRef: diamond,
                out p1HealthFill,
                out p1DamageTrailFill,
                out p1NameText,
                out p1NameShadow,
                out p1ArchetypeText,
                out p1ArchetypeShadow,
                out p1ArchetypeDiamond,
                out p1RoundPips,
                out p1CalloutText,
                out p1ComboContainer,
                out p1ComboNumberText,
                out p1ComboDetailText,
                out p1ComboText);

            BuildTopFighterSide(
                topBarRt,
                isLeft: false,
                bladeSprite: bladeRight,
                solidSprite: solid,
                diamondSpriteRef: diamond,
                out p2HealthFill,
                out p2DamageTrailFill,
                out p2NameText,
                out p2NameShadow,
                out p2ArchetypeText,
                out p2ArchetypeShadow,
                out p2ArchetypeDiamond,
                out p2RoundPips,
                out p2CalloutText,
                out p2ComboContainer,
                out p2ComboNumberText,
                out p2ComboDetailText,
                out p2ComboText);

            // 2. BOTTOM-CORNER EX / SUPER ENERGY METERS (Data-Driven)
            RebuildBottomEnergyMeters();

            // 3. CENTRAL ROUND / KO / TIME OVER BANNER (Clean MKX cinematic soft sweep, zero full-width hard box)
            Sprite sweepSprite = softSweepSprite != null ? softSweepSprite : solid;
            RectTransform bannerRt = MenuUiFactory.CreatePanel(
                "BannerPanel",
                canvasRoot,
                new Color(0.031f, 0.043f, 0.067f, softSweepSprite != null ? 0.90f : 0.78f),
                sweepSprite);
            bannerRt.anchorMin = new Vector2(0.16f, 0.52f);
            bannerRt.anchorMax = new Vector2(0.84f, 0.52f);
            bannerRt.pivot = new Vector2(0.5f, 0.5f);
            bannerRt.sizeDelta = new Vector2(0f, 130f);
            bannerContainer = bannerRt.gameObject;

            // Subtle short centered UnDF blue hairlines fading cleanly to transparent at both ends
            RectTransform bTop = MenuUiFactory.CreatePanel("BannerTopAccent", bannerRt, MenuUiFactory.UnDFBlue, sweepSprite);
            bTop.anchorMin = new Vector2(0.5f, 1f);
            bTop.anchorMax = new Vector2(0.5f, 1f);
            bTop.pivot = new Vector2(0.5f, 1f);
            bTop.anchoredPosition = new Vector2(0f, -12f);
            bTop.sizeDelta = new Vector2(340f, 2.5f);

            RectTransform bBot = MenuUiFactory.CreatePanel("BannerBottomAccent", bannerRt, MenuUiFactory.UnDFBlue, sweepSprite);
            bBot.anchorMin = new Vector2(0.5f, 0f);
            bBot.anchorMax = new Vector2(0.5f, 0f);
            bBot.pivot = new Vector2(0.5f, 0f);
            bBot.anchoredPosition = new Vector2(0f, 12f);
            bBot.sizeDelta = new Vector2(340f, 2.5f);

            bannerTextShadow = MenuUiFactory.CreateText(
                "BannerText_Shadow",
                bannerRt,
                "ROUND 1",
                58f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.015f, 0.022f, 0.035f, 0.92f),
                characterSpacing: 9f);
            bannerTextShadow.rectTransform.anchorMin = new Vector2(0f, 0.28f);
            bannerTextShadow.rectTransform.anchorMax = new Vector2(1f, 0.94f);
            bannerTextShadow.rectTransform.offsetMin = new Vector2(3f, -3f);
            bannerTextShadow.rectTransform.offsetMax = new Vector2(3f, -3f);

            bannerText = MenuUiFactory.CreateText(
                "BannerText",
                bannerRt,
                "ROUND 1",
                58f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextActive,
                characterSpacing: 9f);
            bannerText.rectTransform.anchorMin = new Vector2(0f, 0.28f);
            bannerText.rectTransform.anchorMax = new Vector2(1f, 0.94f);
            bannerText.rectTransform.offsetMin = Vector2.zero;
            bannerText.rectTransform.offsetMax = Vector2.zero;

            subtitleTextShadow = MenuUiFactory.CreateText(
                "SubtitleText_Shadow",
                bannerRt,
                string.Empty,
                21f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.015f, 0.022f, 0.035f, 0.88f),
                characterSpacing: 5f);
            subtitleTextShadow.rectTransform.anchorMin = new Vector2(0f, 0.08f);
            subtitleTextShadow.rectTransform.anchorMax = new Vector2(1f, 0.34f);
            subtitleTextShadow.rectTransform.offsetMin = new Vector2(2f, -2f);
            subtitleTextShadow.rectTransform.offsetMax = new Vector2(2f, -2f);
            subtitleTextShadow.gameObject.SetActive(false);

            subtitleText = MenuUiFactory.CreateText(
                "SubtitleText",
                bannerRt,
                string.Empty,
                21f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.UnDFSky,
                characterSpacing: 5f);
            subtitleText.rectTransform.anchorMin = new Vector2(0f, 0.08f);
            subtitleText.rectTransform.anchorMax = new Vector2(1f, 0.34f);
            subtitleText.rectTransform.offsetMin = Vector2.zero;
            subtitleText.rectTransform.offsetMax = Vector2.zero;
            subtitleText.gameObject.SetActive(false);
            bannerContainer.SetActive(false);

            // 4. STAGE INTERACTION PROMPTS & TOWER PROGRESS BADGE
            EnsureStagePromptWidgets();
            EnsureTowerBadgeWidget();
            RefreshImmediate();
        }

        private void BuildTopFighterSide(
            Transform topBarParent,
            bool isLeft,
            Sprite bladeSprite,
            Sprite solidSprite,
            Sprite diamondSpriteRef,
            out UnityEngine.UI.Image healthFill,
            out UnityEngine.UI.Image trailFill,
            out TextMeshProUGUI nameTxt,
            out TextMeshProUGUI nameShd,
            out TextMeshProUGUI archTxt,
            out TextMeshProUGUI archShd,
            out UnityEngine.UI.Image archDiamond,
            out UnityEngine.UI.Image[] roundPips,
            out TextMeshProUGUI calloutTxt,
            out GameObject comboContainer,
            out TextMeshProUGUI comboNumberTxt,
            out TextMeshProUGUI comboDetailTxt,
            out TextMeshProUGUI comboSummaryTxt)
        {
            string prefix = isLeft ? "P1" : "P2";

            var groupGo = new GameObject($"{prefix}_Group", typeof(RectTransform));
            groupGo.transform.SetParent(topBarParent, false);
            var groupRt = (RectTransform)groupGo.transform;
            if (isLeft)
            {
                groupRt.anchorMin = new Vector2(0f, 1f);
                groupRt.anchorMax = new Vector2(0.5f, 1f);
                groupRt.pivot = new Vector2(0f, 1f);
                groupRt.offsetMin = new Vector2(46f, -156f);
                groupRt.offsetMax = new Vector2(-84f, 0f);
            }
            else
            {
                groupRt.anchorMin = new Vector2(0.5f, 1f);
                groupRt.anchorMax = new Vector2(1f, 1f);
                groupRt.pivot = new Vector2(1f, 1f);
                groupRt.offsetMin = new Vector2(84f, -156f);
                groupRt.offsetMax = new Vector2(-46f, 0f);
            }

            // Tapered Blade Health Bar (thin, crisp metallic outline + dark translucent backing)
            RectTransform barOuter = MenuUiFactory.CreatePanel($"{prefix}_HealthBorder", groupRt, new Color(0.35f, 0.42f, 0.50f, 0.80f), bladeSprite);
            barOuter.anchorMin = new Vector2(0f, 1f);
            barOuter.anchorMax = new Vector2(1f, 1f);
            barOuter.pivot = new Vector2(0.5f, 1f);
            barOuter.anchoredPosition = new Vector2(0f, -24f);
            barOuter.sizeDelta = new Vector2(0f, 24f);

            RectTransform barBg = MenuUiFactory.CreatePanel($"{prefix}_HealthBg", barOuter, new Color(0.039f, 0.051f, 0.071f, 0.84f), bladeSprite);
            MenuUiFactory.StretchFull(barBg, 2f, 2f, 2f, 2f);

            // Delayed Damage Trail (#991B1B dark crimson)
            RectTransform trailRt = MenuUiFactory.CreatePanel($"{prefix}_DamageTrail", barBg, damageTrailColor, bladeSprite);
            MenuUiFactory.StretchFull(trailRt, 0f, 0f, 0f, 0f);
            trailFill = trailRt.GetComponent<UnityEngine.UI.Image>();
            trailFill.type = UnityEngine.UI.Image.Type.Filled;
            trailFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            trailFill.fillOrigin = isLeft ? (int)UnityEngine.UI.Image.OriginHorizontal.Right : (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            trailFill.fillAmount = 1f;

            // Main Health Fill (#EDF2F7 silver-white matte -> #DC2626 below 28%)
            RectTransform fillRt = MenuUiFactory.CreatePanel($"{prefix}_HealthFill", barBg, healthyColor, bladeSprite);
            MenuUiFactory.StretchFull(fillRt, 0f, 0f, 0f, 0f);
            healthFill = fillRt.GetComponent<UnityEngine.UI.Image>();
            healthFill.type = UnityEngine.UI.Image.Type.Filled;
            healthFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            healthFill.fillOrigin = isLeft ? (int)UnityEngine.UI.Image.OriginHorizontal.Right : (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            healthFill.fillAmount = 1f;

            // Subtle feathered dark scrim behind archetype/name for legibility over bright sky (zero hard box)
            if (softSweepSprite != null)
            {
                RectTransform sideScrim = MenuUiFactory.CreatePanel(
                    $"{prefix}_NameLegibilityScrim",
                    groupRt,
                    new Color(0.024f, 0.033f, 0.052f, 0.56f),
                    softSweepSprite);
                sideScrim.anchorMin = new Vector2(isLeft ? 0f : 1f, 1f);
                sideScrim.anchorMax = new Vector2(isLeft ? 0f : 1f, 1f);
                sideScrim.pivot = new Vector2(isLeft ? 0f : 1f, 1f);
                sideScrim.anchoredPosition = new Vector2(isLeft ? -36f : 36f, -44f);
                sideScrim.sizeDelta = new Vector2(440f, 68f);
            }

            // Outer Edge Below Bar: Mini Diamond + Discreet Uppercase Archetype (with soft shadow)
            Color sideAccent = isLeft ? MenuUiFactory.UnDFSky : MenuUiFactory.CrimsonActive;
            RectTransform archDiaRt = MenuUiFactory.CreatePanel($"{prefix}_ArchetypeDiamond", groupRt, sideAccent, diamondSpriteRef);
            archDiaRt.anchorMin = new Vector2(isLeft ? 0f : 1f, 1f);
            archDiaRt.anchorMax = new Vector2(isLeft ? 0f : 1f, 1f);
            archDiaRt.pivot = new Vector2(isLeft ? 0f : 1f, 1f);
            archDiaRt.anchoredPosition = new Vector2(isLeft ? 10f : -10f, -55f);
            archDiaRt.sizeDelta = new Vector2(9f, 9f);
            if (diamondSprite == null) archDiaRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            archDiamond = archDiaRt.GetComponent<UnityEngine.UI.Image>();

            archShd = CreateShadowedLabel(
                groupRt,
                $"{prefix}_Archetype",
                isLeft ? "BRUTAMONTES" : "BRIGUENTO DE RUA",
                12.5f,
                isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 3f,
                anchorMin: new Vector2(isLeft ? 0f : 1f, 1f),
                anchorMax: new Vector2(isLeft ? 0f : 1f, 1f),
                pivot: new Vector2(isLeft ? 0f : 1f, 1f),
                anchoredPos: new Vector2(isLeft ? 26f : -26f, -50f),
                sizeDelta: new Vector2(440f, 18f),
                out archTxt);

            // Floating Fighter Name with Soft Shadow aligned strictly under the outer edge (zero box behind name)
            nameShd = CreateShadowedLabel(
                groupRt,
                $"{prefix}_Name",
                isLeft ? "LUCAS" : "TIAGO",
                27f,
                isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight,
                MenuUiFactory.TextActive,
                characterSpacing: 4.5f,
                anchorMin: new Vector2(isLeft ? 0f : 1f, 1f),
                anchorMax: new Vector2(isLeft ? 0f : 1f, 1f),
                pivot: new Vector2(isLeft ? 0f : 1f, 1f),
                anchoredPos: new Vector2(isLeft ? 10f : -10f, -68f),
                sizeDelta: new Vector2(460f, 34f),
                out nameTxt);

            // Inner Edge Below Bar: Thin Metallic Hairline + Diamond Round Win Pips
            RectTransform innerLine = MenuUiFactory.CreatePanel($"{prefix}_InnerHairline", groupRt, MenuUiFactory.SlateBorderMid, solidSprite);
            innerLine.anchorMin = new Vector2(isLeft ? 1f : 0f, 1f);
            innerLine.anchorMax = new Vector2(isLeft ? 1f : 0f, 1f);
            innerLine.pivot = new Vector2(isLeft ? 1f : 0f, 1f);
            innerLine.anchoredPosition = new Vector2(isLeft ? -8f : 8f, -52f);
            innerLine.sizeDelta = new Vector2(104f, 1.5f);

            roundPips = new UnityEngine.UI.Image[2];
            for (int r = 0; r < 2; r++)
            {
                float xOffset = isLeft ? (-22f - r * 28f) : (22f + r * 28f);
                RectTransform pipOutline = MenuUiFactory.CreatePanel($"{prefix}_RoundPipFrame_{r}", groupRt, MenuUiFactory.SlateBorderLight, diamondSpriteRef);
                pipOutline.anchorMin = new Vector2(isLeft ? 1f : 0f, 1f);
                pipOutline.anchorMax = new Vector2(isLeft ? 1f : 0f, 1f);
                pipOutline.pivot = new Vector2(0.5f, 0.5f);
                pipOutline.anchoredPosition = new Vector2(xOffset, -68f);
                pipOutline.sizeDelta = new Vector2(17f, 17f);
                if (diamondSprite == null) pipOutline.localRotation = Quaternion.Euler(0f, 0f, 45f);

                RectTransform pipFill = MenuUiFactory.CreatePanel($"{prefix}_RoundPip_{r}", pipOutline, roundEmptyColor, diamondSpriteRef);
                MenuUiFactory.StretchFull(pipFill, 2.5f, 2.5f, 2.5f, 2.5f);
                roundPips[r] = pipFill.GetComponent<UnityEngine.UI.Image>();
            }

            // Short Combat Feedback Callout (HIT! / BLOCK! / EX! / ARMOR! / TECH! / SUPER!)
            calloutTxt = MenuUiFactory.CreateText(
                $"{prefix}_Callout",
                groupRt,
                string.Empty,
                24f,
                FontStyles.Bold,
                isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight,
                MenuUiFactory.UnDFSky,
                characterSpacing: 4f);
            calloutTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            calloutTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            calloutTxt.rectTransform.pivot = new Vector2(0.5f, 1f);
            calloutTxt.rectTransform.anchoredPosition = new Vector2(isLeft ? 20f : -20f, -112f);
            calloutTxt.rectTransform.sizeDelta = new Vector2(-40f, 34f);

            // MKX Combo / String Counter Widget (vertical #8BC9EE bar + bold pop number + HITS + STRING • X DANO)
            var comboGo = new GameObject($"{prefix}_ComboWidget", typeof(RectTransform));
            comboGo.transform.SetParent(groupRt, false);
            var comboRt = (RectTransform)comboGo.transform;
            comboRt.anchorMin = new Vector2(isLeft ? 0f : 1f, 1f);
            comboRt.anchorMax = new Vector2(isLeft ? 0f : 1f, 1f);
            comboRt.pivot = new Vector2(isLeft ? 0f : 1f, 1f);
            comboRt.anchoredPosition = new Vector2(isLeft ? 18f : -18f, -162f);
            comboRt.sizeDelta = new Vector2(360f, 76f);
            comboContainer = comboGo;

            RectTransform vBar = MenuUiFactory.CreatePanel("ComboAccentBar", comboRt, MenuUiFactory.UnDFSky, solidSprite);
            vBar.anchorMin = new Vector2(isLeft ? 0f : 1f, 0f);
            vBar.anchorMax = new Vector2(isLeft ? 0f : 1f, 1f);
            vBar.pivot = new Vector2(isLeft ? 0f : 1f, 0.5f);
            vBar.anchoredPosition = Vector2.zero;
            vBar.sizeDelta = new Vector2(4f, -8f);

            comboNumberTxt = MenuUiFactory.CreateText(
                "ComboNumber",
                comboRt,
                "3",
                46f,
                FontStyles.Bold,
                isLeft ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight,
                MenuUiFactory.TextActive,
                characterSpacing: 2f);
            comboNumberTxt.rectTransform.anchorMin = new Vector2(0f, 0.34f);
            comboNumberTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            comboNumberTxt.rectTransform.offsetMin = new Vector2(isLeft ? 16f : 0f, 0f);
            comboNumberTxt.rectTransform.offsetMax = new Vector2(isLeft ? 0f : -16f, 0f);

            TextMeshProUGUI hitsTag = MenuUiFactory.CreateText(
                "HitsLabel",
                comboRt,
                "HITS",
                15f,
                FontStyles.Bold,
                isLeft ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight,
                MenuUiFactory.UnDFSky,
                characterSpacing: 4f);
            hitsTag.rectTransform.anchorMin = new Vector2(0f, 0.36f);
            hitsTag.rectTransform.anchorMax = new Vector2(1f, 0.85f);
            hitsTag.rectTransform.offsetMin = new Vector2(isLeft ? 64f : 0f, 2f);
            hitsTag.rectTransform.offsetMax = new Vector2(isLeft ? 0f : -64f, 0f);

            comboDetailTxt = MenuUiFactory.CreateText(
                "ComboDetail",
                comboRt,
                string.Empty,
                14f,
                FontStyles.Bold,
                isLeft ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 2.5f);
            comboDetailTxt.enableAutoSizing = true;
            comboDetailTxt.fontSizeMin = 10.5f;
            comboDetailTxt.fontSizeMax = 14f;
            comboDetailTxt.textWrappingMode = TextWrappingModes.NoWrap;
            comboDetailTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            comboDetailTxt.rectTransform.anchorMax = new Vector2(1f, 0.34f);
            comboDetailTxt.rectTransform.offsetMin = new Vector2(isLeft ? 16f : 0f, 0f);
            comboDetailTxt.rectTransform.offsetMax = new Vector2(isLeft ? 0f : -16f, 0f);

            // Hidden compatibility summary text mirror (keeps p1ComboText / p2ComboText valid for tests/callers)
            comboSummaryTxt = MenuUiFactory.CreateText($"{prefix}_ComboText", comboRt, string.Empty, 14f, FontStyles.Bold, TextAlignmentOptions.Center, Color.clear);
            comboSummaryTxt.enabled = false;

            comboContainer.SetActive(false);
        }

        public void RebuildBottomEnergyMeters()
        {
            int segments = Mathf.Max(1, meterSegmentCount);
            Sprite solid = GetHudSprite();
            Sprite circle = circleBadgeSprite != null ? circleBadgeSprite : solid;

            BuildSingleBottomEnergyMeter(
                isLeft: true,
                segments,
                solid,
                circle,
                ref p1EnergyMeterRoot,
                ref p1SuperFill,
                ref p1SuperText,
                ref p1SuperTextShadow,
                _p1Medallions);

            BuildSingleBottomEnergyMeter(
                isLeft: false,
                segments,
                solid,
                circle,
                ref p2EnergyMeterRoot,
                ref p2SuperFill,
                ref p2SuperText,
                ref p2SuperTextShadow,
                _p2Medallions);
        }

        private void BuildSingleBottomEnergyMeter(
            bool isLeft,
            int segments,
            Sprite solidSprite,
            Sprite circleSprite,
            ref RectTransform meterRoot,
            ref UnityEngine.UI.Image superFill,
            ref TextMeshProUGUI superText,
            ref TextMeshProUGUI superTextShadow,
            List<MeterMedallionView> medallions)
        {
            string rootName = isLeft ? "P1_EnergyMeter" : "P2_EnergyMeter";
            Transform existing = transform.Find(rootName);
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            medallions.Clear();

            var rootGo = new GameObject(rootName, typeof(RectTransform));
            rootGo.transform.SetParent(transform, false);
            meterRoot = (RectTransform)rootGo.transform;
            meterRoot.anchorMin = new Vector2(isLeft ? 0f : 1f, 0f);
            meterRoot.anchorMax = new Vector2(isLeft ? 0f : 1f, 0f);
            meterRoot.pivot = new Vector2(isLeft ? 0f : 1f, 0f);
            meterRoot.anchoredPosition = new Vector2(isLeft ? 48f : -48f, 30f);
            meterRoot.sizeDelta = new Vector2(436f, 62f);

            // Subtle feathered dark scrim behind bottom-corner energy meter for legibility over bright sand/floor
            if (softSweepSprite != null)
            {
                RectTransform meterScrim = MenuUiFactory.CreatePanel(
                    "MeterLegibilityScrim",
                    meterRoot,
                    new Color(0.020f, 0.028f, 0.044f, 0.68f),
                    softSweepSprite);
                meterScrim.anchorMin = new Vector2(0.5f, 0.5f);
                meterScrim.anchorMax = new Vector2(0.5f, 0.5f);
                meterScrim.pivot = new Vector2(0.5f, 0.5f);
                meterScrim.anchoredPosition = new Vector2(0f, 4f);
                meterScrim.sizeDelta = new Vector2(580f, 108f);
            }

            // Top row: Data-driven circular medallions (EX, S, [ULT]) + compact shadowed status label
            var medRowGo = new GameObject("MedallionsRow", typeof(RectTransform));
            medRowGo.transform.SetParent(meterRoot, false);
            var medRowRt = (RectTransform)medRowGo.transform;
            medRowRt.anchorMin = new Vector2(0f, 1f);
            medRowRt.anchorMax = new Vector2(1f, 1f);
            medRowRt.pivot = new Vector2(0.5f, 1f);
            medRowRt.anchoredPosition = Vector2.zero;
            medRowRt.sizeDelta = new Vector2(0f, 30f);

            string[] labels = BuildMedallionLabels(segments);
            const float medSize = 26f;
            const float medStep = 34f;
            for (int i = 0; i < segments; i++)
            {
                float x = isLeft ? (14f + i * medStep) : (-14f - i * medStep);
                RectTransform ringRt = MenuUiFactory.CreatePanel($"Medallion_{i}", medRowRt, MenuUiFactory.SlateBorderMid, circleSprite);
                ringRt.anchorMin = new Vector2(isLeft ? 0f : 1f, 0.5f);
                ringRt.anchorMax = new Vector2(isLeft ? 0f : 1f, 0.5f);
                ringRt.pivot = new Vector2(0.5f, 0.5f);
                ringRt.anchoredPosition = new Vector2(x, 0f);
                ringRt.sizeDelta = new Vector2(medSize, medSize);

                RectTransform innerRt = MenuUiFactory.CreatePanel("Inner", ringRt, MenuUiFactory.ObsidianBg, circleSprite);
                MenuUiFactory.StretchFull(innerRt, 2f, 2f, 2f, 2f);

                TextMeshProUGUI lbl = MenuUiFactory.CreateText("Tag", innerRt, labels[i], 10f, FontStyles.Bold, TextAlignmentOptions.Center, MenuUiFactory.TextInactive, characterSpacing: 0.5f);
                lbl.enableAutoSizing = true;
                lbl.fontSizeMin = 7.5f;
                lbl.fontSizeMax = 10f;
                MenuUiFactory.StretchFull(lbl.rectTransform, 1f, 1f, 1f, 1f);

                medallions.Add(new MeterMedallionView
                {
                    Ring = ringRt.GetComponent<UnityEngine.UI.Image>(),
                    Fill = innerRt.GetComponent<UnityEngine.UI.Image>(),
                    Label = lbl
                });
            }

            float statusOffset = 18f + segments * medStep;
            superTextShadow = MenuUiFactory.CreateText(
                isLeft ? "P1_SuperText_Shadow" : "P2_SuperText_Shadow",
                medRowRt,
                "ESPECIAL 0%",
                13f,
                FontStyles.Bold,
                isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight,
                new Color(0.015f, 0.022f, 0.035f, 0.92f),
                characterSpacing: 2.5f);
            superTextShadow.rectTransform.anchorMin = new Vector2(0f, 0f);
            superTextShadow.rectTransform.anchorMax = new Vector2(1f, 1f);
            superTextShadow.rectTransform.offsetMin = new Vector2((isLeft ? statusOffset : 4f) + 1.5f, -1.5f);
            superTextShadow.rectTransform.offsetMax = new Vector2((isLeft ? -4f : -statusOffset) + 1.5f, -1.5f);

            superText = MenuUiFactory.CreateText(
                isLeft ? "P1_SuperText" : "P2_SuperText",
                medRowRt,
                "ESPECIAL 0%",
                13f,
                FontStyles.Bold,
                isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 2.5f);
            superText.rectTransform.anchorMin = new Vector2(0f, 0f);
            superText.rectTransform.anchorMax = new Vector2(1f, 1f);
            superText.rectTransform.offsetMin = new Vector2(isLeft ? statusOffset : 4f, 0f);
            superText.rectTransform.offsetMax = new Vector2(isLeft ? -4f : -statusOffset, 0f);

            // Middle: Segmented Energy Bar Frame
            RectTransform barFrame = MenuUiFactory.CreatePanel("EnergyBarFrame", meterRoot, MenuUiFactory.SlateBorderDark, solidSprite);
            barFrame.anchorMin = new Vector2(0f, 0f);
            barFrame.anchorMax = new Vector2(1f, 0f);
            barFrame.pivot = new Vector2(0.5f, 0f);
            barFrame.anchoredPosition = new Vector2(0f, 8f);
            barFrame.sizeDelta = new Vector2(0f, 16f);

            RectTransform barBg = MenuUiFactory.CreatePanel("EnergyBarBg", barFrame, new Color(0.043f, 0.055f, 0.078f, 0.90f), solidSprite);
            MenuUiFactory.StretchFull(barBg, 1.5f, 1.5f, 1.5f, 1.5f);

            RectTransform fillRt = MenuUiFactory.CreatePanel(isLeft ? "P1_SuperFill" : "P2_SuperFill", barBg, superChargingColor, solidSprite);
            MenuUiFactory.StretchFull(fillRt, 0f, 0f, 0f, 0f);
            superFill = fillRt.GetComponent<UnityEngine.UI.Image>();
            superFill.type = UnityEngine.UI.Image.Type.Filled;
            superFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            superFill.fillOrigin = isLeft ? (int)UnityEngine.UI.Image.OriginHorizontal.Left : (int)UnityEngine.UI.Image.OriginHorizontal.Right;
            superFill.fillAmount = 0f;

            // Data-driven segment dividers (N - 1 dividers for N segments)
            for (int d = 1; d < segments; d++)
            {
                float frac = (float)d / segments;
                RectTransform divRt = MenuUiFactory.CreatePanel($"SegmentDivider_{d}", barFrame, new Color(0.043f, 0.055f, 0.078f, 0.98f), solidSprite);
                divRt.anchorMin = new Vector2(frac, 0f);
                divRt.anchorMax = new Vector2(frac, 1f);
                divRt.pivot = new Vector2(0.5f, 0.5f);
                divRt.anchoredPosition = Vector2.zero;
                divRt.sizeDelta = new Vector2(3f, 0f);
            }

            // Bottom: Official UnDF Institutional Blue Signature Base (#1E5699 -> #3D8AC7 -> #8BC9EE)
            var sigGo = new GameObject("UnDFSignatureBase", typeof(RectTransform));
            sigGo.transform.SetParent(meterRoot, false);
            var sigRt = (RectTransform)sigGo.transform;
            sigRt.anchorMin = new Vector2(0f, 0f);
            sigRt.anchorMax = new Vector2(1f, 0f);
            sigRt.pivot = new Vector2(0.5f, 0f);
            sigRt.anchoredPosition = Vector2.zero;
            sigRt.sizeDelta = new Vector2(0f, 4f);

            Color[] undfBands = isLeft
                ? new[] { MenuUiFactory.UnDFRoyal, MenuUiFactory.UnDFBlue, MenuUiFactory.UnDFSky }
                : new[] { MenuUiFactory.UnDFSky, MenuUiFactory.UnDFBlue, MenuUiFactory.UnDFRoyal };

            for (int b = 0; b < 3; b++)
            {
                RectTransform band = MenuUiFactory.CreatePanel($"UnDFBand_{b}", sigRt, undfBands[b], solidSprite);
                band.anchorMin = new Vector2(b / 3f, 0f);
                band.anchorMax = new Vector2((b + 1) / 3f, 1f);
                band.offsetMin = Vector2.zero;
                band.offsetMax = Vector2.zero;
            }
        }

        private static string[] BuildMedallionLabels(int segments)
        {
            if (segments == 1) return new[] { "S" };
            if (segments == 2) return new[] { "EX", "S" };
            if (segments == 3) return new[] { "EX", "S", "ULT" };

            var arr = new string[segments];
            for (int i = 0; i < segments; i++)
            {
                arr[i] = i == segments - 1 ? "ULT" : (i == 0 ? "EX" : $"B{i + 1}");
            }
            return arr;
        }

        private static TextMeshProUGUI CreateShadowedLabel(
            Transform parent,
            string name,
            string initialText,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color,
            float characterSpacing,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            out TextMeshProUGUI mainLabel)
        {
            TextMeshProUGUI shadow = MenuUiFactory.CreateText(
                $"{name}_Shadow",
                parent,
                initialText,
                fontSize,
                FontStyles.Bold,
                alignment,
                new Color(0.015f, 0.022f, 0.035f, 0.90f),
                characterSpacing);
            shadow.rectTransform.anchorMin = anchorMin;
            shadow.rectTransform.anchorMax = anchorMax;
            shadow.rectTransform.pivot = pivot;
            shadow.rectTransform.anchoredPosition = anchoredPos + new Vector2(2f, -2f);
            shadow.rectTransform.sizeDelta = sizeDelta;

            mainLabel = MenuUiFactory.CreateText(
                name,
                parent,
                initialText,
                fontSize,
                FontStyles.Bold,
                alignment,
                color,
                characterSpacing);
            mainLabel.rectTransform.anchorMin = anchorMin;
            mainLabel.rectTransform.anchorMax = anchorMax;
            mainLabel.rectTransform.pivot = pivot;
            mainLabel.rectTransform.anchoredPosition = anchoredPos;
            mainLabel.rectTransform.sizeDelta = sizeDelta;

            return shadow;
        }

        private void Awake()
        {
            if (simulation == null) simulation = FindAnyObjectByType<FightSimulation>();
            if (matchManager == null) matchManager = FindAnyObjectByType<MatchManager>();
            if (stageInteractableManager == null) stageInteractableManager = FindAnyObjectByType<StageInteractableManager>();
            if (fightMenuController == null)
            {
                fightMenuController = FindAnyObjectByType<FightMenuController>();
                _searchedFightMenu = true;
            }
            EnsureSuperAndComboWidgets();
            EnsureStagePromptWidgets();
            EnsureTowerBadgeWidget();
        }

        private void OnEnable()
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnCombatHit += HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
                simulation.OnSuperFlashTriggered += HandleSuperFlashTriggered;
            }
            EnsureSuperAndComboWidgets();
            EnsureStagePromptWidgets();
            EnsureTowerBadgeWidget();
            RefreshImmediate();
        }

        private void OnDisable()
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
            }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float unscaledDt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            UpdateFighterBars(dt);
            UpdateRoundAndTimerUI();
            UpdateCallouts(dt, unscaledDt);
            UpdateStageInteractionPrompts();
        }

        public bool IsStagePromptVisible(int playerIndex)
        {
            StagePromptWidget widget = playerIndex == 0 ? _p1StagePrompt : _p2StagePrompt;
            return widget != null && widget.Root != null && widget.Root.gameObject.activeSelf;
        }

        public string GetStagePromptText(int playerIndex)
        {
            StagePromptWidget widget = playerIndex == 0 ? _p1StagePrompt : _p2StagePrompt;
            if (widget == null) return string.Empty;
            if (!string.IsNullOrEmpty(widget.FullPromptText)) return widget.FullPromptText;
            return widget.Label != null ? widget.Label.text : string.Empty;
        }

        public void RefreshStagePromptsNow()
        {
            EnsureStagePromptWidgets();
            UpdateStageInteractionPrompts();
        }

        public void RefreshImmediate()
        {
            _p1PrevRatio = GetHealthRatio(simulation != null ? simulation.Fighter1 : null);
            _p2PrevRatio = GetHealthRatio(simulation != null ? simulation.Fighter2 : null);
            _p1TrailRatio = _p1PrevRatio;
            _p2TrailRatio = _p2PrevRatio;
            _p1TrailHoldTimer = 0f;
            _p2TrailHoldTimer = 0f;

            UpdateFighterIdentity();
            ApplyHealthWidgets(p1HealthFill, p1DamageTrailFill, _p1PrevRatio, _p1TrailRatio);
            ApplyHealthWidgets(p2HealthFill, p2DamageTrailFill, _p2PrevRatio, _p2TrailRatio);
            UpdateSingleSuperBar(simulation != null ? simulation.Fighter1 : null, p1SuperFill, p1SuperText, p1SuperTextShadow, _p1Medallions);
            UpdateSingleSuperBar(simulation != null ? simulation.Fighter2 : null, p2SuperFill, p2SuperText, p2SuperTextShadow, _p2Medallions);
            UpdateRoundAndTimerUI();

            if (p1CalloutText != null) p1CalloutText.text = string.Empty;
            if (p2CalloutText != null) p2CalloutText.text = string.Empty;
            if (p1ComboText != null) p1ComboText.text = string.Empty;
            if (p2ComboText != null) p2ComboText.text = string.Empty;
            if (p1ComboContainer != null) p1ComboContainer.SetActive(false);
            if (p2ComboContainer != null) p2ComboContainer.SetActive(false);
            _p1ComboTimer = 0f;
            _p2ComboTimer = 0f;
            _p1ComboDamageAccum = 0;
            _p2ComboDamageAccum = 0;
            UpdateStageInteractionPrompts();
            UpdateTowerBadgeUI();
        }

        private void HandleSuperFlashTriggered(FighterController caster, int freezeTicks)
        {
            if (simulation == null || caster == null)
            {
                return;
            }

            bool casterIsP1 = caster == simulation.Fighter1;
            TextMeshProUGUI targetCallout = casterIsP1 ? p1CalloutText : p2CalloutText;
            if (targetCallout != null)
            {
                targetCallout.text = "SUPER!";
                targetCallout.color = MenuUiFactory.UnDFSky;
                if (casterIsP1) _p1CalloutTimer = calloutDurationSeconds;
                else _p2CalloutTimer = calloutDurationSeconds;
            }
        }

        private void HandleCombatHit(CombatHitEvent hit)
        {
            if (simulation == null || hit.Defender == null)
            {
                return;
            }

            bool defenderIsP1 = hit.Defender == simulation.Fighter1;
            TextMeshProUGUI targetCallout = defenderIsP1 ? p1CalloutText : p2CalloutText;
            if (targetCallout != null)
            {
                if (hit.WasArmorAbsorbed)
                {
                    targetCallout.text = "ARMOR!";
                    targetCallout.color = MenuUiFactory.TextActive;
                }
                else if (hit.Outcome == HitOutcomeType.ThrowTech)
                {
                    targetCallout.text = "TECH!";
                    targetCallout.color = MenuUiFactory.UnDFSky;
                }
                else if (hit.Outcome == HitOutcomeType.Blocked)
                {
                    targetCallout.text = "BLOCK!";
                    targetCallout.color = MenuUiFactory.UnDFBlue;
                }
                else if (hit.Outcome == HitOutcomeType.KnockDown && hit.Defender.CurrentHealth <= 0)
                {
                    targetCallout.text = "K.O.!";
                    targetCallout.color = lowHealthColor;
                }
                else if (hit.IsSpecial)
                {
                    string moveLabel = hit.Move != null && !string.IsNullOrWhiteSpace(hit.Move.moveName)
                        ? hit.Move.moveName.ToUpperInvariant() + "!"
                        : "ESPECIAL!";
                    targetCallout.text = moveLabel;
                    targetCallout.color = MenuUiFactory.UnDFSky;
                }
                else if (hit.IsExMove)
                {
                    targetCallout.text = "EX!";
                    targetCallout.color = MenuUiFactory.UnDFSky;
                }
                else
                {
                    targetCallout.text = hit.WasTrade ? "TRADE HIT!" : "HIT!";
                    targetCallout.color = MenuUiFactory.TextActive;
                }

                if (defenderIsP1) _p1CalloutTimer = calloutDurationSeconds;
                else _p2CalloutTimer = calloutDurationSeconds;
            }

            // Track combo damage and show MKX combo/string widget from 2 hits onward or named string
            string comboStringLabel = !string.IsNullOrWhiteSpace(hit.ComboStringName)
                ? hit.ComboStringName.ToUpperInvariant()
                : (hit.Attacker != null && !string.IsNullOrWhiteSpace(hit.Attacker.ActiveComboStringName)
                    ? hit.Attacker.ActiveComboStringName.ToUpperInvariant()
                    : string.Empty);

            if (hit.Outcome != HitOutcomeType.Blocked &&
                hit.Outcome != HitOutcomeType.ThrowTech &&
                hit.Attacker != null)
            {
                bool attackerIsP1 = hit.Attacker == simulation.Fighter1;
                if (hit.ComboCount <= 1)
                {
                    if (attackerIsP1) _p1ComboDamageAccum = hit.DamageDealt;
                    else _p2ComboDamageAccum = hit.DamageDealt;
                }
                else
                {
                    if (attackerIsP1) _p1ComboDamageAccum += hit.DamageDealt;
                    else _p2ComboDamageAccum += hit.DamageDealt;
                }

                int totalDmg = hit.ComboDamage > 0
                    ? hit.ComboDamage
                    : (attackerIsP1 ? _p1ComboDamageAccum : _p2ComboDamageAccum);

                if (hit.ComboCount >= 2 || !string.IsNullOrEmpty(comboStringLabel))
                {
                    GameObject container = attackerIsP1 ? p1ComboContainer : p2ComboContainer;
                    TextMeshProUGUI numTxt = attackerIsP1 ? p1ComboNumberText : p2ComboNumberText;
                    TextMeshProUGUI detTxt = attackerIsP1 ? p1ComboDetailText : p2ComboDetailText;
                    TextMeshProUGUI legacyComboTxt = attackerIsP1 ? p1ComboText : p2ComboText;

                    if (container != null) container.SetActive(true);
                    if (numTxt != null)
                    {
                        numTxt.text = Mathf.Max(1, hit.ComboCount).ToString();
                        numTxt.color = hit.IsSpecial ? MenuUiFactory.UnDFSky : MenuUiFactory.TextActive;
                    }
                    if (detTxt != null)
                    {
                        detTxt.text = !string.IsNullOrEmpty(comboStringLabel)
                            ? $"{comboStringLabel} \u2022 {totalDmg} DANO"
                            : $"{totalDmg} DANO";
                        detTxt.color = MenuUiFactory.TextSubtitle;
                    }
                    if (legacyComboTxt != null)
                    {
                        legacyComboTxt.text = !string.IsNullOrEmpty(comboStringLabel)
                            ? $"{hit.ComboCount} HITS \u2022 {comboStringLabel} \u2022 {totalDmg} DANO"
                            : $"{hit.ComboCount} HITS \u2022 {totalDmg} DANO";
                    }

                    if (attackerIsP1)
                    {
                        _p1ComboTimer = comboDisplayDurationSeconds;
                        _p1ComboPopTimer = 0.16f;
                    }
                    else
                    {
                        _p2ComboTimer = comboDisplayDurationSeconds;
                        _p2ComboPopTimer = 0.16f;
                    }
                }
            }
        }

        private void UpdateFighterBars(float dt)
        {
            FighterController p1 = simulation != null ? simulation.Fighter1 : null;
            FighterController p2 = simulation != null ? simulation.Fighter2 : null;

            UpdateSingleHealthBar(p1, p1HealthFill, p1DamageTrailFill, ref _p1PrevRatio, ref _p1TrailRatio, ref _p1TrailHoldTimer, dt);
            UpdateSingleHealthBar(p2, p2HealthFill, p2DamageTrailFill, ref _p2PrevRatio, ref _p2TrailRatio, ref _p2TrailHoldTimer, dt);
            UpdateSingleSuperBar(p1, p1SuperFill, p1SuperText, p1SuperTextShadow, _p1Medallions);
            UpdateSingleSuperBar(p2, p2SuperFill, p2SuperText, p2SuperTextShadow, _p2Medallions);
            UpdateFighterIdentity();
        }

        private void UpdateSingleHealthBar(
            FighterController fighter,
            UnityEngine.UI.Image fill,
            UnityEngine.UI.Image trail,
            ref float prevRatio,
            ref float trailRatio,
            ref float holdTimer,
            float dt)
        {
            float ratio = GetHealthRatio(fighter);

            if (ratio > trailRatio)
            {
                trailRatio = ratio;
                holdTimer = 0f;
            }
            else if (ratio < prevRatio - 0.0001f)
            {
                holdTimer = trailHoldSeconds;
            }

            prevRatio = ratio;

            if (holdTimer > 0f)
            {
                holdTimer -= dt;
            }
            else if (trailRatio > ratio)
            {
                trailRatio = Mathf.MoveTowards(trailRatio, ratio, trailDecaySpeed * dt);
            }

            ApplyHealthWidgets(fill, trail, ratio, trailRatio);
        }

        private void ApplyHealthWidgets(UnityEngine.UI.Image fill, UnityEngine.UI.Image trail, float ratio, float trailRatio)
        {
            if (fill != null)
            {
                fill.fillAmount = ratio;
                fill.color = ratio < lowHealthThreshold ? lowHealthColor : healthyColor;
            }
            if (trail != null)
            {
                trail.fillAmount = trailRatio;
                trail.color = damageTrailColor;
            }
        }

        private void UpdateFighterIdentity()
        {
            if (simulation == null) return;

            UpdateSideIdentity(simulation.Fighter1, isP1: true, p1NameText, p1NameShadow, p1ArchetypeText, p1ArchetypeShadow, p1ArchetypeDiamond);
            UpdateSideIdentity(simulation.Fighter2, isP1: false, p2NameText, p2NameShadow, p2ArchetypeText, p2ArchetypeShadow, p2ArchetypeDiamond);
        }

        private static void UpdateSideIdentity(
            FighterController fighter,
            bool isP1,
            TextMeshProUGUI nameTxt,
            TextMeshProUGUI nameShd,
            TextMeshProUGUI archTxt,
            TextMeshProUGUI archShd,
            UnityEngine.UI.Image archDiamond)
        {
            if (fighter == null || fighter.Data == null) return;

            string upperName = fighter.Data.fighterName.ToUpperInvariant();
            if (nameTxt != null && nameTxt.text != upperName) nameTxt.text = upperName;
            if (nameShd != null && nameShd.text != upperName) nameShd.text = upperName;

            string arch = FighterScaling.GetArchetypeLabel(fighter.Data.archetype);
            if (archTxt != null && archTxt.text != arch) archTxt.text = arch;
            if (archShd != null && archShd.text != arch) archShd.text = arch;

            if (archDiamond != null)
            {
                bool useCrimson = !isP1 || fighter.Data.isSecretBoss;
                archDiamond.color = useCrimson ? MenuUiFactory.CrimsonActive : MenuUiFactory.UnDFSky;
            }
        }

        private void UpdateRoundAndTimerUI()
        {
            if (matchManager == null)
            {
                return;
            }

            if (timerText != null)
            {
                string val;
                Color col;
                if (matchManager.IsTrainingMode)
                {
                    val = "\u221E";
                    col = MenuUiFactory.TextActive;
                }
                else
                {
                    int sec = matchManager.RemainingSeconds;
                    val = sec.ToString("00");
                    col = sec <= 15 ? lowHealthColor : MenuUiFactory.TextActive;
                }

                if (timerText.text != val) timerText.text = val;
                timerText.color = col;
                if (timerShadow != null && timerShadow.text != val) timerShadow.text = val;
            }

            UpdatePips(p1RoundPips, matchManager.P1RoundsWon);
            UpdatePips(p2RoundPips, matchManager.P2RoundsWon);

            bool suppressBanner = ShouldSuppressBanner();
            bool hasBanner = !suppressBanner && !string.IsNullOrEmpty(matchManager.BannerText);
            if (bannerContainer != null && bannerContainer.activeSelf != hasBanner)
            {
                bannerContainer.SetActive(hasBanner);
            }
            if (bannerText != null)
            {
                bannerText.text = matchManager.BannerText;
            }
            if (bannerTextShadow != null)
            {
                bannerTextShadow.text = matchManager.BannerText;
            }

            bool hasSubtitle = !string.IsNullOrEmpty(matchManager.SubtitleText);
            if (subtitleText != null)
            {
                subtitleText.gameObject.SetActive(hasSubtitle);
                subtitleText.text = matchManager.SubtitleText;
            }
            if (subtitleTextShadow != null)
            {
                subtitleTextShadow.gameObject.SetActive(hasSubtitle);
                subtitleTextShadow.text = matchManager.SubtitleText;
            }

            UpdateTowerBadgeUI();
        }

        private bool ShouldSuppressBanner()
        {
            if (matchManager != null && matchManager.Phase == MatchPhase.MatchOver)
            {
                return true;
            }

            if (fightMenuController == null && !_searchedFightMenu)
            {
                fightMenuController = FindAnyObjectByType<FightMenuController>();
                _searchedFightMenu = true;
            }

            return fightMenuController != null && fightMenuController.IsAnyModalOpen;
        }

        private void UpdatePips(UnityEngine.UI.Image[] pips, int roundsWon)
        {
            if (pips == null) return;
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                pips[i].color = i < roundsWon ? roundWonColor : roundEmptyColor;
            }
        }

        private void UpdateCallouts(float dt, float unscaledDt)
        {
            UpdateSingleCallout(p1CalloutText, ref _p1CalloutTimer, dt, calloutDurationSeconds);
            UpdateSingleCallout(p2CalloutText, ref _p2CalloutTimer, dt, calloutDurationSeconds);
            UpdateComboWidget(p1ComboContainer, p1ComboNumberText, p1ComboDetailText, p1ComboText, ref _p1ComboTimer, ref _p1ComboPopTimer, unscaledDt);
            UpdateComboWidget(p2ComboContainer, p2ComboNumberText, p2ComboDetailText, p2ComboText, ref _p2ComboTimer, ref _p2ComboPopTimer, unscaledDt);
        }

        private static void UpdateSingleCallout(TextMeshProUGUI callout, ref float timer, float dt, float totalDuration)
        {
            if (callout == null) return;

            if (timer > 0f)
            {
                timer -= dt;
                float fadeWindow = Mathf.Max(0.15f, totalDuration * 0.35f);
                float alpha = Mathf.Clamp01(timer / fadeWindow);
                Color c = callout.color;
                callout.color = new Color(c.r, c.g, c.b, alpha);
                if (timer <= 0f)
                {
                    callout.text = string.Empty;
                }
            }
        }

        private void UpdateComboWidget(
            GameObject container,
            TextMeshProUGUI numTxt,
            TextMeshProUGUI detTxt,
            TextMeshProUGUI legacyTxt,
            ref float timer,
            ref float popTimer,
            float unscaledDt)
        {
            if (popTimer > 0f)
            {
                popTimer = Mathf.Max(0f, popTimer - unscaledDt);
                if (numTxt != null)
                {
                    float t = popTimer / 0.16f;
                    float scale = 1f + 0.28f * t;
                    numTxt.rectTransform.localScale = new Vector3(scale, scale, 1f);
                }
            }
            else if (numTxt != null && numTxt.rectTransform.localScale != Vector3.one)
            {
                numTxt.rectTransform.localScale = Vector3.one;
            }

            if (timer > 0f)
            {
                timer -= unscaledDt;
                float fadeWindow = Mathf.Max(0.2f, comboDisplayDurationSeconds * 0.30f);
                float alpha = Mathf.Clamp01(timer / fadeWindow);

                if (numTxt != null)
                {
                    Color c = numTxt.color;
                    numTxt.color = new Color(c.r, c.g, c.b, alpha);
                }
                if (detTxt != null)
                {
                    Color c = detTxt.color;
                    detTxt.color = new Color(c.r, c.g, c.b, alpha);
                }
                if (timer <= 0f)
                {
                    if (container != null) container.SetActive(false);
                    if (legacyTxt != null) legacyTxt.text = string.Empty;
                }
            }
        }

        private void UpdateSingleSuperBar(
            FighterController fighter,
            UnityEngine.UI.Image superFill,
            TextMeshProUGUI superText,
            TextMeshProUGUI superTextShadow,
            List<MeterMedallionView> medallions)
        {
            float ratio = fighter != null ? fighter.SuperNormalized : 0f;
            bool isSuperReady = fighter != null && fighter.CanUseSpecial;
            bool isExReady = fighter != null && fighter.CanUseEx && !isSuperReady;

            if (superFill != null)
            {
                superFill.fillAmount = ratio;
                if (isSuperReady)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                    superFill.color = Color.Lerp(superReadyColorA, superReadyColorB, pulse);
                }
                else if (isExReady)
                {
                    superFill.color = superExReadyColor;
                }
                else
                {
                    superFill.color = superChargingColor;
                }
            }

            int segments = Mathf.Max(1, meterSegmentCount);
            int filledSegments = Mathf.FloorToInt(ratio * segments + 0.0001f);
            for (int i = 0; i < medallions.Count; i++)
            {
                MeterMedallionView m = medallions[i];
                bool lit = i < filledSegments;
                if (m.Ring != null) m.Ring.color = lit ? MenuUiFactory.UnDFSky : MenuUiFactory.SlateBorderDark;
                if (m.Fill != null) m.Fill.color = lit ? MenuUiFactory.UnDFRoyal : MenuUiFactory.ObsidianBg;
                if (m.Label != null) m.Label.color = lit ? MenuUiFactory.TextActive : MenuUiFactory.TextInactive;
            }

            if (superText != null)
            {
                string label;
                Color labelColor;
                if (isSuperReady)
                {
                    label = "SUPER PRONTO";
                    labelColor = MenuUiFactory.UnDFSky;
                }
                else if (isExReady)
                {
                    int pct = fighter != null ? fighter.SuperGauge : 50;
                    label = $"EX PRONTO \u2022 {pct}%";
                    labelColor = MenuUiFactory.UnDFBlue;
                }
                else
                {
                    int pct = fighter != null ? fighter.SuperGauge : 0;
                    label = $"ESPECIAL {pct}%";
                    labelColor = MenuUiFactory.TextSubtitle;
                }

                superText.text = label;
                superText.color = labelColor;
                if (superTextShadow != null && superTextShadow.text != label)
                {
                    superTextShadow.text = label;
                }
            }
        }

        /// <summary>
        /// Ensures bottom-corner EX/SUPER energy meters and combo counter widgets exist and are wired,
        /// both for Edit Mode serialization and runtime fallback.
        /// </summary>
        public void EnsureSuperAndComboWidgets()
        {
            if (p1EnergyMeterRoot == null)
            {
                Transform existingP1 = transform.Find("P1_EnergyMeter");
                if (existingP1 != null) p1EnergyMeterRoot = (RectTransform)existingP1;
            }
            if (p2EnergyMeterRoot == null)
            {
                Transform existingP2 = transform.Find("P2_EnergyMeter");
                if (existingP2 != null) p2EnergyMeterRoot = (RectTransform)existingP2;
            }

            if (p1EnergyMeterRoot == null || p2EnergyMeterRoot == null)
            {
                RebuildBottomEnergyMeters();
                return;
            }

            if (_p1Medallions.Count == 0 || p1SuperFill == null || p1SuperText == null || p1SuperTextShadow == null)
            {
                RebindMeterReferences(p1EnergyMeterRoot, isLeft: true, ref p1SuperFill, ref p1SuperText, ref p1SuperTextShadow, _p1Medallions);
            }
            if (_p2Medallions.Count == 0 || p2SuperFill == null || p2SuperText == null || p2SuperTextShadow == null)
            {
                RebindMeterReferences(p2EnergyMeterRoot, isLeft: false, ref p2SuperFill, ref p2SuperText, ref p2SuperTextShadow, _p2Medallions);
            }
        }

        private static void RebindMeterReferences(
            RectTransform root,
            bool isLeft,
            ref UnityEngine.UI.Image superFill,
            ref TextMeshProUGUI superText,
            ref TextMeshProUGUI superTextShadow,
            List<MeterMedallionView> medallions)
        {
            if (root == null) return;
            string fillName = isLeft ? "P1_SuperFill" : "P2_SuperFill";
            string textName = isLeft ? "P1_SuperText" : "P2_SuperText";
            string shadowName = isLeft ? "P1_SuperText_Shadow" : "P2_SuperText_Shadow";

            Transform fillTr = FindDescendant(root, fillName);
            if (fillTr != null) superFill = fillTr.GetComponent<UnityEngine.UI.Image>();
            Transform textTr = FindDescendant(root, textName);
            if (textTr != null) superText = textTr.GetComponent<TextMeshProUGUI>();
            Transform shadowTr = FindDescendant(root, shadowName);
            if (shadowTr != null) superTextShadow = shadowTr.GetComponent<TextMeshProUGUI>();

            medallions.Clear();
            Transform row = root.Find("MedallionsRow");
            if (row != null)
            {
                for (int i = 0; i < row.childCount; i++)
                {
                    Transform child = row.GetChild(i);
                    if (!child.name.StartsWith("Medallion_", System.StringComparison.Ordinal)) continue;
                    Transform inner = child.Find("Inner");
                    medallions.Add(new MeterMedallionView
                    {
                        Ring = child.GetComponent<UnityEngine.UI.Image>(),
                        Fill = inner != null ? inner.GetComponent<UnityEngine.UI.Image>() : null,
                        Label = inner != null ? inner.Find("Tag")?.GetComponent<TextMeshProUGUI>() : null
                    });
                }
            }
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName) return child;
                Transform nested = FindDescendant(child, targetName);
                if (nested != null) return nested;
            }
            return null;
        }

        private Sprite GetHudSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            if (_fallbackWhiteSprite != null) return _fallbackWhiteSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _fallbackWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return _fallbackWhiteSprite;
        }

        private static float GetHealthRatio(FighterController fighter)
        {
            if (fighter == null || fighter.MaxHealth <= 0)
            {
                return 1f;
            }
            return Mathf.Clamp01(fighter.CurrentHealth / (float)fighter.MaxHealth);
        }

        private void EnsureStagePromptWidgets()
        {
            Sprite solid = GetHudSprite();
            TMP_FontAsset fontAsset = p1NameText != null ? p1NameText.font : (p2NameText != null ? p2NameText.font : null);

            if (_p1StagePrompt == null)
            {
                _p1StagePrompt = CreateOrReusePromptWidget("StagePrompt_P1", MenuUiFactory.UnDFBlue, MenuUiFactory.UnDFRoyal, solid, fontAsset);
            }
            if (_p2StagePrompt == null)
            {
                _p2StagePrompt = CreateOrReusePromptWidget("StagePrompt_P2", MenuUiFactory.CrimsonActive, MenuUiFactory.CrimsonDark, solid, fontAsset);
            }
        }

        private StagePromptWidget CreateOrReusePromptWidget(
            string widgetName,
            Color edgeAccent,
            Color keyTagColor,
            Sprite sprite,
            TMP_FontAsset fontAsset)
        {
            Transform existing = transform.Find(widgetName);
            if (existing != null)
            {
                var existingBorder = existing.GetComponent<UnityEngine.UI.Image>();
                Transform bgTr = existing.Find("Bg");
                var existingBg = bgTr != null ? bgTr.GetComponent<UnityEngine.UI.Image>() : null;
                Transform leftAcc = bgTr != null ? bgTr.Find("LeftAccent") : null;
                Transform keyTag = bgTr != null ? bgTr.Find("KeyTag") : null;
                Transform labelTr = bgTr != null ? bgTr.Find("Label") : existing.Find("Label");
                return new StagePromptWidget
                {
                    Root = (RectTransform)existing,
                    Border = existingBorder,
                    Background = existingBg,
                    LeftAccent = leftAcc != null ? leftAcc.GetComponent<UnityEngine.UI.Image>() : null,
                    KeyTagBg = keyTag != null ? keyTag.GetComponent<UnityEngine.UI.Image>() : null,
                    KeyTagLabel = keyTag != null ? keyTag.Find("KeyText")?.GetComponent<TextMeshProUGUI>() : null,
                    Label = labelTr != null ? labelTr.GetComponent<TextMeshProUGUI>() : null
                };
            }

            // Matte graphite prompt with thin steel border, #3D8AC7 left strip, and #1E5699 key tag
            var rootGo = new GameObject(widgetName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            rootGo.transform.SetParent(transform, false);
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(268f, 34f);

            var borderImg = rootGo.GetComponent<UnityEngine.UI.Image>();
            borderImg.sprite = sprite;
            borderImg.color = MenuUiFactory.SlateBorderMid;
            borderImg.raycastTarget = false;

            RectTransform bgRt = MenuUiFactory.CreatePanel("Bg", rootRt, new Color(0.043f, 0.055f, 0.078f, 0.92f), sprite);
            MenuUiFactory.StretchFull(bgRt, 1.5f, 1.5f, 1.5f, 1.5f);

            RectTransform leftStripRt = MenuUiFactory.CreatePanel("LeftAccent", bgRt, edgeAccent, sprite);
            leftStripRt.anchorMin = new Vector2(0f, 0f);
            leftStripRt.anchorMax = new Vector2(0f, 1f);
            leftStripRt.pivot = new Vector2(0f, 0.5f);
            leftStripRt.sizeDelta = new Vector2(4f, 0f);

            RectTransform keyTagRt = MenuUiFactory.CreatePanel("KeyTag", bgRt, keyTagColor, sprite);
            keyTagRt.anchorMin = new Vector2(0f, 0.5f);
            keyTagRt.anchorMax = new Vector2(0f, 0.5f);
            keyTagRt.pivot = new Vector2(0f, 0.5f);
            keyTagRt.anchoredPosition = new Vector2(10f, 0f);
            keyTagRt.sizeDelta = new Vector2(76f, 21f);

            TextMeshProUGUI keyTagTmp = MenuUiFactory.CreateText("KeyText", keyTagRt, "DEF + LP", 11.5f, FontStyles.Bold, TextAlignmentOptions.Center, MenuUiFactory.TextActive, characterSpacing: 1f);
            if (fontAsset != null) keyTagTmp.font = fontAsset;
            MenuUiFactory.StretchFull(keyTagTmp.rectTransform, 2f, 1f, 2f, 1f);

            TextMeshProUGUI tmp = MenuUiFactory.CreateText("Label", bgRt, "ARREMESSAR", 13f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, MenuUiFactory.TextActive, characterSpacing: 1.5f);
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10f;
            tmp.fontSizeMax = 13f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            MenuUiFactory.StretchFull(tmp.rectTransform, 94f, 2f, 8f, 2f);

            rootGo.SetActive(false);

            return new StagePromptWidget
            {
                Root = rootRt,
                Border = borderImg,
                Background = bgRt.GetComponent<UnityEngine.UI.Image>(),
                LeftAccent = leftStripRt.GetComponent<UnityEngine.UI.Image>(),
                KeyTagBg = keyTagRt.GetComponent<UnityEngine.UI.Image>(),
                KeyTagLabel = keyTagTmp,
                Label = tmp,
                FullPromptText = "[DEF + LP] ARREMESSAR"
            };
        }

        private void UpdateStageInteractionPrompts()
        {
            if (_p1StagePrompt == null && _p2StagePrompt == null)
            {
                return;
            }

            if (simulation == null || (matchManager != null && !matchManager.InputsAllowed) || simulation.IsPaused)
            {
                SetPromptVisible(_p1StagePrompt, false);
                SetPromptVisible(_p2StagePrompt, false);
                return;
            }

            if (stageInteractableManager == null)
            {
                stageInteractableManager = FindAnyObjectByType<StageInteractableManager>();
            }

            StageThrowableProp p1Prop = FindPromptPropForFighter(simulation.Fighter1);
            bool hideP2ForCpu = simulation.CpuAi != null && simulation.CpuAi.IsEnabled;
            StageThrowableProp p2Prop = hideP2ForCpu ? null : FindPromptPropForFighter(simulation.Fighter2);

            bool sameProp = p1Prop != null && p1Prop == p2Prop;
            UpdateSinglePromptWidget(_p1StagePrompt, p1Prop, isP1: true, extraOffsetY: sameProp ? 22f : 0f);
            UpdateSinglePromptWidget(_p2StagePrompt, p2Prop, isP1: false, extraOffsetY: sameProp ? -20f : 0f);
        }

        private StageThrowableProp FindPromptPropForFighter(FighterController fighter)
        {
            if (fighter == null || !fighter.CanAttemptStageInteract())
            {
                return null;
            }

            return stageInteractableManager != null
                ? stageInteractableManager.FindNearestReadyThrowable(fighter)
                : StageInteractableManager.FindNearestReadyThrowableInScene(fighter);
        }

        private void UpdateSinglePromptWidget(StagePromptWidget widget, StageThrowableProp prop, bool isP1, float extraOffsetY)
        {
            if (widget == null || widget.Root == null)
            {
                return;
            }

            if (prop == null)
            {
                SetPromptVisible(widget, false);
                return;
            }

            SetPromptVisible(widget, true);
            string actionUpper = prop.PromptLabel.ToUpperInvariant();
            widget.FullPromptText = $"[DEF + LP] {actionUpper}";

            if (widget.LeftAccent != null)
            {
                widget.LeftAccent.color = isP1 ? MenuUiFactory.UnDFBlue : MenuUiFactory.CrimsonActive;
            }
            if (widget.KeyTagBg != null)
            {
                widget.KeyTagBg.color = isP1 ? MenuUiFactory.UnDFRoyal : MenuUiFactory.CrimsonDark;
            }
            if (widget.Label != null)
            {
                widget.Label.text = actionUpper;
                widget.Label.color = MenuUiFactory.TextActive;
            }

            Camera cam = Camera.main;
            var canvasRt = transform as RectTransform;
            if (cam != null && canvasRt != null)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(prop.GetPromptWorldPosition());
                if (screenPos.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPos, null, out Vector2 localPt))
                {
                    Rect rect = canvasRt.rect;
                    float halfW = widget.Root.sizeDelta.x * 0.5f + 16f;
                    float halfH = widget.Root.sizeDelta.y * 0.5f + 16f;
                    float clampedX = Mathf.Clamp(localPt.x, rect.xMin + halfW, rect.xMax - halfW);
                    float clampedY = Mathf.Clamp(localPt.y + 28f + extraOffsetY, rect.yMin + halfH, rect.yMax - 150f);
                    widget.Root.anchoredPosition = new Vector2(clampedX, clampedY);
                    return;
                }
            }

            widget.Root.anchoredPosition = new Vector2(isP1 ? -260f : 260f, -150f + extraOffsetY);
        }

        private static void SetPromptVisible(StagePromptWidget widget, bool visible)
        {
            if (widget != null && widget.Root != null && widget.Root.gameObject.activeSelf != visible)
            {
                widget.Root.gameObject.SetActive(visible);
            }
        }

        private void EnsureTowerBadgeWidget()
        {
            if (_towerBadgeRoot != null)
            {
                return;
            }

            Transform existing = transform.Find("TowerProgressBadge");
            if (existing != null)
            {
                _towerBadgeRoot = (RectTransform)existing;
                _towerBadgeBorder = existing.GetComponent<UnityEngine.UI.Image>();
                Transform bgTr = existing.Find("Bg");
                _towerBadgeBg = bgTr != null ? bgTr.GetComponent<UnityEngine.UI.Image>() : null;
                Transform labelTr = bgTr != null ? bgTr.Find("Label") : existing.Find("Label");
                _towerBadgeLabel = labelTr != null ? labelTr.GetComponent<TextMeshProUGUI>() : null;
                return;
            }

            Sprite solid = GetHudSprite();
            TMP_FontAsset fontAsset = p1NameText != null ? p1NameText.font : (p2NameText != null ? p2NameText.font : null);

            var rootGo = new GameObject("TowerProgressBadge", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            rootGo.transform.SetParent(transform, false);
            _towerBadgeRoot = (RectTransform)rootGo.transform;
            _towerBadgeRoot.anchorMin = new Vector2(0.5f, 1f);
            _towerBadgeRoot.anchorMax = new Vector2(0.5f, 1f);
            _towerBadgeRoot.pivot = new Vector2(0.5f, 1f);
            _towerBadgeRoot.anchoredPosition = new Vector2(0f, -112f);
            _towerBadgeRoot.sizeDelta = new Vector2(188f, 24f);

            _towerBadgeBorder = rootGo.GetComponent<UnityEngine.UI.Image>();
            _towerBadgeBorder.sprite = solid;
            _towerBadgeBorder.color = MenuUiFactory.UnDFBlue;
            _towerBadgeBorder.raycastTarget = false;

            RectTransform bgRt = MenuUiFactory.CreatePanel("Bg", _towerBadgeRoot, new Color(0.043f, 0.055f, 0.078f, 0.88f), solid);
            MenuUiFactory.StretchFull(bgRt, 1.2f, 1.2f, 1.2f, 1.2f);
            _towerBadgeBg = bgRt.GetComponent<UnityEngine.UI.Image>();

            _towerBadgeLabel = MenuUiFactory.CreateText("Label", bgRt, "TORRE 1/7", 12.5f, FontStyles.Bold, TextAlignmentOptions.Center, MenuUiFactory.UnDFSky, characterSpacing: 2.5f);
            if (fontAsset != null) _towerBadgeLabel.font = fontAsset;
            _towerBadgeLabel.textWrappingMode = TextWrappingModes.NoWrap;
            MenuUiFactory.StretchFull(_towerBadgeLabel.rectTransform, 8f, 1f, 8f, 1f);

            rootGo.SetActive(false);
        }

        private void UpdateTowerBadgeUI()
        {
            if (_towerBadgeRoot == null)
            {
                EnsureTowerBadgeWidget();
            }
            if (_towerBadgeRoot == null)
            {
                return;
            }

            ArcadeTowerRun run = GameSessionState.ActiveTowerRun;
            bool show = GameSessionState.CurrentMode == GameMode.ArcadeTower &&
                        run != null &&
                        run.CurrentMatch != null;

            if (_towerBadgeRoot.gameObject.activeSelf != show)
            {
                _towerBadgeRoot.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            int step = run.CurrentMatchIndex + 1;
            int total = Mathf.Max(1, run.TotalMatches);
            bool isBoss = run.IsCurrentMatchBoss;

            if (isBoss)
            {
                _towerBadgeRoot.sizeDelta = new Vector2(308f, 26f);
                if (_towerBadgeBorder != null) _towerBadgeBorder.color = MenuUiFactory.CrimsonActive;
                if (_towerBadgeBg != null) _towerBadgeBg.color = MenuUiFactory.CrimsonSurface;
                if (_towerBadgeLabel != null)
                {
                    _towerBadgeLabel.text = $"CHEFE FINAL \u2022 TORRE {step}/{total}";
                    _towerBadgeLabel.color = MenuUiFactory.TextActive;
                }
            }
            else
            {
                _towerBadgeRoot.sizeDelta = new Vector2(188f, 24f);
                if (_towerBadgeBorder != null) _towerBadgeBorder.color = MenuUiFactory.UnDFBlue;
                if (_towerBadgeBg != null) _towerBadgeBg.color = new Color(0.043f, 0.055f, 0.078f, 0.88f);
                if (_towerBadgeLabel != null)
                {
                    _towerBadgeLabel.text = $"TORRE {step}/{total}";
                    _towerBadgeLabel.color = MenuUiFactory.UnDFSky;
                }
            }
        }
    }
}
