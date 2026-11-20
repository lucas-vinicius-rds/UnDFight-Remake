using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnDFight
{
    /// <summary>
    /// MKX-inspired Main Menu controller with official matte UnDF identity:
    /// - Top-left UnDF emblem + UnDFIGHT wordmark + institutional subtitle (clean top-right).
    /// - Upper center trio of MKX arched feature cards (TORRE ARCADE, COMBATE RÁPIDO, ARENAS).
    /// - Lower center clean 6-item list with soft #1E5699 sweep + flanking #8BC9EE diamonds.
    /// - Clean footer with controls legend, dynamic 1-line mode description, and bottom-right UnDF seal.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public const string CharacterSelectSceneName = "CharacterSelect";

        public static readonly string[] RootMenuLabels =
        {
            "MODO TORRE",
            "VS JOGADOR",
            "VS CPU",
            "TREINO",
            "OPÇÕES",
            "SAIR"
        };

        private static readonly string[] RootMenuDescriptions =
        {
            "ESCALE A TORRE ARCADE DE 7 LUTAS E DESAFIE O CHEFE FINAL",
            "COMBATE LOCAL PARA 2 JOGADORES NO MESMO DISPOSITIVO",
            "ENFRENTE A INTELIGÊNCIA ARTIFICIAL NA ARENA DE SUA ESCOLHA",
            "PRATIQUE COMBOS, HITBOXES E MOVIMENTOS SEM LIMITE DE TEMPO",
            "AJUSTE ÁUDIO, VÍDEO, DISPOSITIVOS E CONTROLES DE COMBATE",
            "ENCERRAR O JOGO E RETORNAR À ÁREA DE TRABALHO"
        };

        [SerializeField] private MenuNavigationGroup rootNavigation;
        [SerializeField] private MenuNavigationGroup fightSubmenuNavigation;
        [SerializeField] private GameObject rootMenuContainer;
        [SerializeField] private GameObject fightSubmenuContainer;
        [SerializeField] private TextMeshProUGUI sectionHeadingText;
        [SerializeField] private TextMeshProUGUI modeDescriptionText;
        [SerializeField] private UnityEngine.UI.Button featureCardTowerButton;
        [SerializeField] private UnityEngine.UI.Button featureCardQuickFightButton;
        [SerializeField] private UnityEngine.UI.Button featureCardArenasButton;
        [SerializeField] private OptionsPanelController optionsPanel;
        [SerializeField] private FighterRoster fighterRoster;
        [SerializeField] private Sprite whiteSprite;
        [SerializeField] private Sprite undfEmblemSprite;
        [SerializeField] private Sprite archedCardSprite;
        [SerializeField] private Sprite diamondSprite;
        [SerializeField] private Sprite softSweepSprite;
        [SerializeField] private Sprite vignetteSprite;
        [SerializeField] private Sprite circleBadgeSprite;

        private bool _built;

        public bool IsFightSubmenuOpen => fightSubmenuContainer != null && fightSubmenuContainer.activeSelf;
        public TextMeshProUGUI ModeDescriptionText => modeDescriptionText;
        public MenuNavigationGroup RootNavigation => rootNavigation;

        private void Awake()
        {
            SettingsManager.ApplySettings(SettingsManager.Current, applyResolution: false);
            if (!_built)
            {
                EnsureRuntimeWiring();
            }

            if (optionsPanel != null)
            {
                optionsPanel.gameObject.SetActive(false);
            }
            BackToRootMenu();
        }

        public void Configure(
            MenuNavigationGroup rootNav,
            MenuNavigationGroup fightNav,
            OptionsPanelController options,
            FighterRoster roster,
            Sprite sprite)
        {
            rootNavigation = rootNav;
            fightSubmenuNavigation = fightNav;
            optionsPanel = options;
            fighterRoster = roster;
            whiteSprite = sprite;
            BindAllMenus();
        }

        public void ConfigureThemeSprites(
            Sprite white,
            Sprite emblem,
            Sprite archedCard,
            Sprite diamond,
            Sprite softSweep,
            Sprite vignette = null,
            Sprite circleBadge = null)
        {
            if (white != null) whiteSprite = white;
            if (emblem != null) undfEmblemSprite = emblem;
            if (archedCard != null) archedCardSprite = archedCard;
            if (diamond != null) diamondSprite = diamond;
            if (softSweep != null) softSweepSprite = softSweep;
            if (vignette != null) vignetteSprite = vignette;
            if (circleBadge != null) circleBadgeSprite = circleBadge;
        }

        private void EnsureRuntimeWiring()
        {
            if (transform.Find("MainMenuPanel") == null && whiteSprite != null)
            {
                BuildUI(transform, whiteSprite);
            }
            else
            {
                Transform panel = transform.Find("MainMenuPanel");
                if (panel != null)
                {
                    if (rootMenuContainer == null) rootMenuContainer = panel.Find("RootMenuGroup")?.gameObject;
                    if (fightSubmenuContainer == null) fightSubmenuContainer = panel.Find("FightSubmenuGroup")?.gameObject;
                    if (rootNavigation == null && rootMenuContainer != null) rootNavigation = rootMenuContainer.GetComponent<MenuNavigationGroup>();
                    if (fightSubmenuNavigation == null && fightSubmenuContainer != null) fightSubmenuNavigation = fightSubmenuContainer.GetComponent<MenuNavigationGroup>();

                    EnsureRootButtonsUpToDate();
                }

                Transform footer = transform.Find("FooterBar");
                if (modeDescriptionText == null && footer != null)
                {
                    modeDescriptionText = footer.Find("ModeDescription")?.GetComponent<TextMeshProUGUI>();
                }

                Transform cards = transform.Find("TopFeatureCards");
                if (cards != null)
                {
                    if (featureCardTowerButton == null) featureCardTowerButton = cards.Find("FeatureCard_Tower")?.GetComponent<UnityEngine.UI.Button>();
                    if (featureCardQuickFightButton == null) featureCardQuickFightButton = cards.Find("FeatureCard_QuickFight")?.GetComponent<UnityEngine.UI.Button>();
                    if (featureCardArenasButton == null) featureCardArenasButton = cards.Find("FeatureCard_Arenas")?.GetComponent<UnityEngine.UI.Button>();
                }

                if (optionsPanel == null) optionsPanel = GetComponentInChildren<OptionsPanelController>(true);
                BindAllMenus();
            }

            _built = true;
        }

        public void EnsureRootButtonsUpToDate()
        {
            if (rootMenuContainer == null || rootNavigation == null)
            {
                return;
            }

            var existingButtons = rootMenuContainer.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            bool matchesExpected = existingButtons.Length == RootMenuLabels.Length;
            if (matchesExpected)
            {
                for (int i = 0; i < existingButtons.Length; i++)
                {
                    var lbl = existingButtons[i].transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                    if (lbl == null || lbl.text != RootMenuLabels[i])
                    {
                        matchesExpected = false;
                        break;
                    }
                }
            }

            if (!matchesExpected)
            {
                for (int i = rootMenuContainer.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = rootMenuContainer.transform.GetChild(i);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }

                CreateMkxCleanColumnButtons(
                    rootMenuContainer.transform,
                    rootNavigation,
                    "RootBtn",
                    RootMenuLabels);
            }

            if (fightSubmenuContainer != null)
            {
                fightSubmenuContainer.SetActive(false);
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

            // 1. Seamless vertical matte vignette over the 3D Arena_UnDF atmospheric backdrop (no hard horizontal bands)
            if (vignetteSprite != null)
            {
                RectTransform fullVignette = MenuUiFactory.CreatePanel(
                    "BackgroundVignette",
                    canvasRoot,
                    new Color(0.043f, 0.055f, 0.078f, 1.0f),
                    vignetteSprite);
                MenuUiFactory.StretchFull(fullVignette, 0f, 0f, 0f, 0f);
            }
            else
            {
                RectTransform fullVignette = MenuUiFactory.CreatePanel(
                    "BackgroundVignette",
                    canvasRoot,
                    new Color(0.043f, 0.055f, 0.078f, 0.68f),
                    whiteSprite);
                MenuUiFactory.StretchFull(fullVignette, 0f, 0f, 0f, 0f);
            }

            // 2. Top-Left Official UnDF Brand Header (Top-Right intentionally kept clean!)
            var brandGo = new GameObject("BrandHeader", typeof(RectTransform));
            brandGo.transform.SetParent(canvasRoot, false);
            var brandRt = brandGo.GetComponent<RectTransform>();
            brandRt.anchorMin = new Vector2(0f, 1f);
            brandRt.anchorMax = new Vector2(0f, 1f);
            brandRt.pivot = new Vector2(0f, 1f);
            brandRt.anchoredPosition = new Vector2(56f, -32f);
            brandRt.sizeDelta = new Vector2(620f, 84f);

            if (undfEmblemSprite != null)
            {
                var emblemGo = new GameObject("UnDFEmblem", typeof(RectTransform));
                emblemGo.transform.SetParent(brandRt, false);
                var emblemRt = emblemGo.GetComponent<RectTransform>();
                emblemRt.anchorMin = new Vector2(0f, 0.5f);
                emblemRt.anchorMax = new Vector2(0f, 0.5f);
                emblemRt.pivot = new Vector2(0f, 0.5f);
                emblemRt.anchoredPosition = new Vector2(0f, 2f);
                emblemRt.sizeDelta = new Vector2(96f, 56f);
                var emblemImg = emblemGo.AddComponent<UnityEngine.UI.Image>();
                emblemImg.sprite = undfEmblemSprite;
                emblemImg.preserveAspect = true;
                emblemImg.raycastTarget = false;
            }

            float titleOffsetX = undfEmblemSprite != null ? 112f : 0f;
            TextMeshProUGUI wordmark = MenuUiFactory.CreateText(
                "GameTitle",
                brandRt,
                "<color=#8BC9EE>UnD</color><color=#F1F5F9>FIGHT</color>",
                46f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.TextActive,
                characterSpacing: 2.5f);
            wordmark.rectTransform.anchorMin = new Vector2(0f, 1f);
            wordmark.rectTransform.anchorMax = new Vector2(1f, 1f);
            wordmark.rectTransform.pivot = new Vector2(0f, 1f);
            wordmark.rectTransform.anchoredPosition = new Vector2(titleOffsetX, -2f);
            wordmark.rectTransform.sizeDelta = new Vector2(-titleOffsetX, 52f);

            TextMeshProUGUI subtitle = MenuUiFactory.CreateText(
                "Subtitle",
                brandRt,
                "UNIVERSIDADE DO DISTRITO FEDERAL",
                12.5f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 3.8f);
            subtitle.rectTransform.anchorMin = new Vector2(0f, 0f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 0f);
            subtitle.rectTransform.pivot = new Vector2(0f, 0f);
            subtitle.rectTransform.anchoredPosition = new Vector2(titleOffsetX + 2f, 4f);
            subtitle.rectTransform.sizeDelta = new Vector2(-titleOffsetX, 22f);

            // 3. Upper Center Trio of MKX Arched Feature Cards
            var cardsGo = new GameObject("TopFeatureCards", typeof(RectTransform));
            cardsGo.transform.SetParent(canvasRoot, false);
            var cardsRt = cardsGo.GetComponent<RectTransform>();
            cardsRt.anchorMin = new Vector2(0.5f, 1f);
            cardsRt.anchorMax = new Vector2(0.5f, 1f);
            cardsRt.pivot = new Vector2(0.5f, 1f);
            cardsRt.anchoredPosition = new Vector2(0f, -142f);
            cardsRt.sizeDelta = new Vector2(960f, 268f);

            featureCardTowerButton = BuildArchedFeatureCard(
                "FeatureCard_Tower",
                cardsRt,
                new Vector2(-318f, -12f),
                new Vector2(272f, 220f),
                MenuUiFactory.SlateBorderMid,
                new Color(0.071f, 0.094f, 0.133f, 0.90f),
                "TORRE ARCADE",
                "7 DESAFIOS",
                MenuUiFactory.UnDFBlue,
                includeEmblem: false);

            featureCardQuickFightButton = BuildArchedFeatureCard(
                "FeatureCard_QuickFight",
                cardsRt,
                new Vector2(0f, 0f),
                new Vector2(320f, 252f),
                MenuUiFactory.UnDFBlue,
                new Color(0.094f, 0.125f, 0.176f, 0.95f),
                "COMBATE RÁPIDO",
                "VS CPU",
                MenuUiFactory.UnDFSky,
                includeEmblem: true);

            featureCardArenasButton = BuildArchedFeatureCard(
                "FeatureCard_Arenas",
                cardsRt,
                new Vector2(318f, -12f),
                new Vector2(272f, 220f),
                MenuUiFactory.SlateBorderMid,
                new Color(0.071f, 0.094f, 0.133f, 0.90f),
                "ARENAS",
                "SELEÇÃO DE MAPAS",
                MenuUiFactory.SlateBorderLight,
                includeEmblem: false);

            // 4. Lower Center Clean MKX Vertical Menu List (6 items, zero heavy boxes)
            var menuPanelGo = new GameObject("MainMenuPanel", typeof(RectTransform));
            menuPanelGo.transform.SetParent(canvasRoot, false);
            var menuRt = menuPanelGo.GetComponent<RectTransform>();
            menuRt.anchorMin = new Vector2(0.5f, 0f);
            menuRt.anchorMax = new Vector2(0.5f, 0f);
            menuRt.pivot = new Vector2(0.5f, 0f);
            menuRt.anchoredPosition = new Vector2(0f, 92f);
            menuRt.sizeDelta = new Vector2(620f, 384f);

            // Subtle feathered dark scrim behind vertical menu list for crisp legibility over glass lobby
            if (softSweepSprite != null)
            {
                RectTransform listScrim = MenuUiFactory.CreatePanel(
                    "MenuListScrim",
                    menuRt,
                    new Color(0.024f, 0.033f, 0.052f, 0.72f),
                    softSweepSprite);
                listScrim.anchorMin = new Vector2(0.5f, 0.5f);
                listScrim.anchorMax = new Vector2(0.5f, 0.5f);
                listScrim.pivot = new Vector2(0.5f, 0.5f);
                listScrim.anchoredPosition = Vector2.zero;
                listScrim.sizeDelta = new Vector2(920f, 440f);
            }

            var rootGroupGo = new GameObject("RootMenuGroup", typeof(RectTransform));
            rootGroupGo.transform.SetParent(menuRt, false);
            MenuUiFactory.StretchFull(rootGroupGo.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            rootMenuContainer = rootGroupGo;
            rootNavigation = rootGroupGo.AddComponent<MenuNavigationGroup>();

            CreateMkxCleanColumnButtons(rootGroupGo.transform, rootNavigation, "RootBtn", RootMenuLabels);

            // Legacy FightSubmenuGroup kept inactive for backward compatibility
            var fightGroupGo = new GameObject("FightSubmenuGroup", typeof(RectTransform));
            fightGroupGo.transform.SetParent(menuRt, false);
            MenuUiFactory.StretchFull(fightGroupGo.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            fightSubmenuContainer = fightGroupGo;
            fightSubmenuNavigation = fightGroupGo.AddComponent<MenuNavigationGroup>();
            fightGroupGo.SetActive(false);

            // 5. Clean Matte Footer Bar (Left controls, Center symmetric description at X=0.50, Right UnDF seal)
            RectTransform footerBar = MenuUiFactory.CreatePanel(
                "FooterBar",
                canvasRoot,
                new Color(0.043f, 0.055f, 0.078f, 0.92f),
                whiteSprite);
            footerBar.anchorMin = new Vector2(0f, 0f);
            footerBar.anchorMax = new Vector2(1f, 0f);
            footerBar.pivot = new Vector2(0.5f, 0f);
            footerBar.anchoredPosition = Vector2.zero;
            footerBar.sizeDelta = new Vector2(0f, 56f);

            RectTransform footerBorder = MenuUiFactory.CreatePanel("FooterTopBorder", footerBar, MenuUiFactory.SlateBorderDark, whiteSprite);
            footerBorder.anchorMin = new Vector2(0f, 1f);
            footerBorder.anchorMax = new Vector2(1f, 1f);
            footerBorder.pivot = new Vector2(0.5f, 1f);
            footerBorder.sizeDelta = new Vector2(0f, 1.5f);

            TextMeshProUGUI controlsLegend = MenuUiFactory.CreateText(
                "MainFooter",
                footerBar,
                "ENTER / J CONFIRMAR  •  ESC / K VOLTAR",
                13.5f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.TextInactive,
                characterSpacing: 1.8f);
            controlsLegend.enableAutoSizing = true;
            controlsLegend.fontSizeMin = 10f;
            controlsLegend.fontSizeMax = 13.5f;
            controlsLegend.textWrappingMode = TextWrappingModes.NoWrap;
            controlsLegend.rectTransform.anchorMin = new Vector2(0f, 0f);
            controlsLegend.rectTransform.anchorMax = new Vector2(0.24f, 1f);
            controlsLegend.rectTransform.offsetMin = new Vector2(28f, 4f);
            controlsLegend.rectTransform.offsetMax = new Vector2(-8f, -4f);

            modeDescriptionText = MenuUiFactory.CreateText(
                "ModeDescription",
                footerBar,
                RootMenuDescriptions[0],
                14.5f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextSubtitle,
                characterSpacing: 2.2f);
            modeDescriptionText.enableAutoSizing = true;
            modeDescriptionText.fontSizeMin = 11f;
            modeDescriptionText.fontSizeMax = 14.5f;
            modeDescriptionText.textWrappingMode = TextWrappingModes.NoWrap;
            modeDescriptionText.rectTransform.anchorMin = new Vector2(0.24f, 0f);
            modeDescriptionText.rectTransform.anchorMax = new Vector2(0.76f, 1f);
            modeDescriptionText.rectTransform.offsetMin = new Vector2(8f, 4f);
            modeDescriptionText.rectTransform.offsetMax = new Vector2(-8f, -4f);

            // Bottom-Right Circular Dark Institutional UnDF Seal
            Sprite sealSprite = circleBadgeSprite != null ? circleBadgeSprite : whiteSprite;
            RectTransform sealOuter = MenuUiFactory.CreatePanel("FooterUnDFSeal", footerBar, MenuUiFactory.UnDFBlue, sealSprite);
            sealOuter.anchorMin = new Vector2(1f, 0.5f);
            sealOuter.anchorMax = new Vector2(1f, 0.5f);
            sealOuter.pivot = new Vector2(1f, 0.5f);
            sealOuter.anchoredPosition = new Vector2(-36f, 10f);
            sealOuter.sizeDelta = new Vector2(58f, 58f);

            RectTransform sealInner = MenuUiFactory.CreatePanel("SealInner", sealOuter, MenuUiFactory.PanelDarkBg, sealSprite);
            MenuUiFactory.StretchFull(sealInner, 2.5f, 2.5f, 2.5f, 2.5f);

            if (undfEmblemSprite != null)
            {
                var sealEmblemGo = new GameObject("SealEmblem", typeof(RectTransform));
                sealEmblemGo.transform.SetParent(sealInner, false);
                MenuUiFactory.StretchFull(sealEmblemGo.GetComponent<RectTransform>(), 9f, 9f, 9f, 9f);
                var sealImg = sealEmblemGo.AddComponent<UnityEngine.UI.Image>();
                sealImg.sprite = undfEmblemSprite;
                sealImg.preserveAspect = true;
                sealImg.raycastTarget = false;
            }

            // 6. Options Modal (matte UnDF palette)
            var optGo = new GameObject("OptionsPanel", typeof(RectTransform));
            optGo.transform.SetParent(canvasRoot, false);
            optionsPanel = optGo.AddComponent<OptionsPanelController>();
            optionsPanel.SetWhiteSprite(whiteSprite);
            optionsPanel.BuildUI(whiteSprite);
            optGo.SetActive(false);

            BindAllMenus();
            BackToRootMenu();
            _built = true;
        }

        private UnityEngine.UI.Button BuildArchedFeatureCard(
            string name,
            Transform parent,
            Vector2 anchoredPos,
            Vector2 size,
            Color borderColor,
            Color fillColor,
            string title,
            string subtitle,
            Color accentColor,
            bool includeEmblem)
        {
            UnityEngine.UI.Button btn = MenuUiFactory.CreateArchedCard(
                name,
                parent,
                archedCardSprite,
                whiteSprite,
                borderColor,
                fillColor,
                size);

            RectTransform rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;

            // Subtle top accent hairline inside card
            RectTransform topBar = MenuUiFactory.CreatePanel("CardAccent", rt, accentColor, whiteSprite);
            topBar.anchorMin = new Vector2(0.14f, 1f);
            topBar.anchorMax = new Vector2(0.86f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.anchoredPosition = new Vector2(0f, -5f);
            topBar.sizeDelta = new Vector2(0f, 3f);

            if (includeEmblem && undfEmblemSprite != null)
            {
                var emblemGo = new GameObject("CardEmblem", typeof(RectTransform));
                emblemGo.transform.SetParent(rt, false);
                var embRt = emblemGo.GetComponent<RectTransform>();
                embRt.anchorMin = new Vector2(0.5f, 1f);
                embRt.anchorMax = new Vector2(0.5f, 1f);
                embRt.pivot = new Vector2(0.5f, 1f);
                embRt.anchoredPosition = new Vector2(0f, -34f);
                embRt.sizeDelta = new Vector2(142f, 82f);
                var embImg = emblemGo.AddComponent<UnityEngine.UI.Image>();
                embImg.sprite = undfEmblemSprite;
                embImg.preserveAspect = true;
                embImg.raycastTarget = false;
            }
            else if (diamondSprite != null)
            {
                RectTransform dia = MenuUiFactory.CreatePanel("CardDiamond", rt, accentColor, diamondSprite);
                dia.anchorMin = new Vector2(0.5f, 1f);
                dia.anchorMax = new Vector2(0.5f, 1f);
                dia.pivot = new Vector2(0.5f, 1f);
                dia.anchoredPosition = new Vector2(0f, -44f);
                dia.sizeDelta = new Vector2(18f, 18f);
            }

            float titleY = includeEmblem ? -136f : -92f;
            TextMeshProUGUI titleTmp = MenuUiFactory.CreateText(
                "CardTitle",
                rt,
                title,
                includeEmblem ? 23f : 21f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                MenuUiFactory.TextActive,
                characterSpacing: 4.5f);
            titleTmp.enableAutoSizing = true;
            titleTmp.fontSizeMin = 15f;
            titleTmp.fontSizeMax = includeEmblem ? 23f : 21f;
            titleTmp.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleTmp.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleTmp.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleTmp.rectTransform.anchoredPosition = new Vector2(0f, titleY);
            titleTmp.rectTransform.sizeDelta = new Vector2(-28f, 36f);

            TextMeshProUGUI subTmp = MenuUiFactory.CreateText(
                "CardSubtitle",
                rt,
                subtitle,
                12.5f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                includeEmblem ? MenuUiFactory.UnDFSky : MenuUiFactory.TextInactive,
                characterSpacing: 3f);
            subTmp.rectTransform.anchorMin = new Vector2(0f, 1f);
            subTmp.rectTransform.anchorMax = new Vector2(1f, 1f);
            subTmp.rectTransform.pivot = new Vector2(0.5f, 1f);
            subTmp.rectTransform.anchoredPosition = new Vector2(0f, titleY - 38f);
            subTmp.rectTransform.sizeDelta = new Vector2(-28f, 24f);

            return btn;
        }

        private void CreateMkxCleanColumnButtons(
            Transform parent,
            MenuNavigationGroup nav,
            string prefix,
            string[] labels)
        {
            nav.ClearItems();
            const float buttonHeight = 52f;
            const float stepY = 60f;
            const float startY = -12f;

            for (int i = 0; i < labels.Length; i++)
            {
                MenuItemEntry entry = MenuUiFactory.CreateMkxCleanMenuButton(
                    $"{prefix}_{i}",
                    parent,
                    labels[i],
                    whiteSprite,
                    softSweepSprite,
                    diamondSprite,
                    fontSize: 26f,
                    characterSpacing: 7f,
                    diamondOffsetX: 168f);

                RectTransform rt = entry.Button.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, startY - i * stepY);
                rt.sizeDelta = new Vector2(560f, buttonHeight);
                nav.AddItem(entry);
            }
        }

        private void BindAllMenus()
        {
            if (rootNavigation != null && rootMenuContainer != null)
            {
                var btns = rootMenuContainer.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                rootNavigation.ClearItems();
                for (int i = 0; i < btns.Length; i++)
                {
                    int idx = i;
                    MenuItemEntry entry = BuildEntryFromButton(btns[i], () => ExecuteRootAction(idx));
                    rootNavigation.AddItem(entry);
                }
                rootNavigation.OnSelectionChanged = HandleRootSelectionChanged;
                HandleRootSelectionChanged(rootNavigation.SelectedIndex);
            }

            if (fightSubmenuNavigation != null && fightSubmenuContainer != null)
            {
                var btns = fightSubmenuContainer.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                fightSubmenuNavigation.ClearItems();
                for (int i = 0; i < btns.Length; i++)
                {
                    int idx = i;
                    fightSubmenuNavigation.AddItem(BuildEntryFromButton(btns[i], () => ExecuteFightSubmenuAction(idx)));
                }
                fightSubmenuNavigation.OnCancel = BackToRootMenu;
            }

            BindFeatureCard(featureCardTowerButton, () => StartMode(GameMode.ArcadeTower));
            BindFeatureCard(featureCardQuickFightButton, () => StartMode(GameMode.VersusCpu));
            BindFeatureCard(featureCardArenasButton, StartArenasQuickSelect);

            if (optionsPanel != null)
            {
                optionsPanel.OnClosed -= HandleOptionsClosed;
                optionsPanel.OnClosed += HandleOptionsClosed;
            }
        }

        private static void BindFeatureCard(UnityEngine.UI.Button btn, System.Action onClick)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                FightAudioManager.Instance?.PlayUiConfirm();
                onClick?.Invoke();
            });
        }

        private void HandleRootSelectionChanged(int index)
        {
            if (modeDescriptionText == null) return;
            if (index >= 0 && index < RootMenuDescriptions.Length)
            {
                modeDescriptionText.text = RootMenuDescriptions[index];
            }
        }

        private static MenuItemEntry BuildEntryFromButton(UnityEngine.UI.Button btn, System.Action onSubmit)
        {
            UnityEngine.UI.Image sweep = btn.transform.Find("SelectionSweep")?.GetComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.Image leftDia = btn.transform.Find("DiamondLeft")?.GetComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.Image rightDia = btn.transform.Find("DiamondRight")?.GetComponent<UnityEngine.UI.Image>();
            bool isMkxClean = sweep != null || leftDia != null || rightDia != null;

            return new MenuItemEntry
            {
                Button = btn,
                Background = btn.GetComponent<UnityEngine.UI.Image>(),
                AccentBar = btn.transform.Find("AccentBar")?.GetComponent<UnityEngine.UI.Image>(),
                AccentGlow = btn.transform.Find("AccentGlow")?.GetComponent<UnityEngine.UI.Image>(),
                SweepImage = sweep,
                LeftDiamond = leftDia,
                RightDiamond = rightDia,
                Label = btn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>(),
                UseMkxCleanStyle = isMkxClean,
                NormalBg = isMkxClean ? new Color(0f, 0f, 0f, 0f) : MenuUiFactory.PanelDarkBg,
                SelectedBg = isMkxClean ? new Color(0f, 0f, 0f, 0f) : new Color(0.118f, 0.215f, 0.345f, 0.96f),
                NormalLabelColor = MenuUiFactory.TextInactive,
                SelectedLabelColor = MenuUiFactory.TextActive,
                OnSubmit = onSubmit
            };
        }

        public void ExecuteRootAction(int index)
        {
            switch (index)
            {
                case 0:
                    StartMode(GameMode.ArcadeTower);
                    break;
                case 1:
                    StartMode(GameMode.LocalVersus);
                    break;
                case 2:
                    StartMode(GameMode.VersusCpu);
                    break;
                case 3:
                    StartMode(GameMode.Training);
                    break;
                case 4:
                    OpenOptions();
                    break;
                case 5:
                    QuitGame();
                    break;
            }
        }

        private void ExecuteFightSubmenuAction(int index)
        {
            switch (index)
            {
                case 0:
                    StartMode(GameMode.LocalVersus);
                    break;
                case 1:
                    StartMode(GameMode.VersusCpu);
                    break;
                case 2:
                    StartMode(GameMode.Training);
                    break;
                case 3:
                    BackToRootMenu();
                    break;
            }
        }

        public void OpenFightSubmenu()
        {
            if (rootMenuContainer != null) rootMenuContainer.SetActive(false);
            if (fightSubmenuContainer != null) fightSubmenuContainer.SetActive(true);
            if (sectionHeadingText != null) sectionHeadingText.text = "SELECIONE O MODO DE LUTA";

            rootNavigation?.SetInputLocked(true);
            fightSubmenuNavigation?.SetSelectedIndex(0);
            fightSubmenuNavigation?.SetInputLocked(false);
        }

        public void BackToRootMenu()
        {
            if (fightSubmenuContainer != null) fightSubmenuContainer.SetActive(false);
            if (rootMenuContainer != null) rootMenuContainer.SetActive(true);
            if (sectionHeadingText != null) sectionHeadingText.text = "MENU PRINCIPAL";

            fightSubmenuNavigation?.SetInputLocked(true);
            rootNavigation?.SetSelectedIndex(0, notifyAudio: false);
            rootNavigation?.SetInputLocked(false);
            HandleRootSelectionChanged(rootNavigation != null ? rootNavigation.SelectedIndex : 0);
        }

        public void StartMode(GameMode mode)
        {
            GameSessionState.ClearTowerRun();
            GameSessionState.SelectMode(mode);
            GameSessionState.OpenStageSelectOnCharacterSelect = false;
            if (fighterRoster != null)
            {
                GameSessionState.SelectFighters(null, null);
            }
            SceneManager.LoadScene(CharacterSelectSceneName);
        }

        /// <summary>
        /// Direct shortcut from the ARENAS feature card to Stage Select (VS CPU with default unlocked fighters).
        /// Pressing back on Stage Select smoothly returns to fighter selection in VS CPU mode.
        /// </summary>
        public void StartArenasQuickSelect()
        {
            GameSessionState.ClearTowerRun();
            GameSessionState.SelectMode(GameMode.VersusCpu);
            if (fighterRoster != null && fighterRoster.Count > 0)
            {
                FighterData p1 = fighterRoster.GetFighter(0);
                FighterData p2 = fighterRoster.Count > 1 ? fighterRoster.GetFighter(1) : p1;
                GameSessionState.SelectFighters(p1, p2);
                GameSessionState.OpenStageSelectOnCharacterSelect = true;
            }
            else
            {
                GameSessionState.OpenStageSelectOnCharacterSelect = false;
            }
            SceneManager.LoadScene(CharacterSelectSceneName);
        }

        public void OpenOptions()
        {
            if (optionsPanel == null) return;
            rootNavigation?.SetInputLocked(true);
            fightSubmenuNavigation?.SetInputLocked(true);
            optionsPanel.Open();
        }

        private void HandleOptionsClosed()
        {
            if (IsFightSubmenuOpen)
            {
                fightSubmenuNavigation?.SetInputLocked(false);
            }
            else
            {
                rootNavigation?.SetInputLocked(false);
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
