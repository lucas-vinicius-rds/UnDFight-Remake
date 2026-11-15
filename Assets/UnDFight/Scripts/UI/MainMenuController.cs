using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnDFight
{
    /// <summary>
    /// MKX-inspired Main Menu controller:
    /// Root column has 3 primary entries (LUTAR, CONFIGURAÇÕES, SAIR DO JOGO);
    /// LUTAR transitions in-place to Fight Modes (PLAYER VS PLAYER, PLAYER VS CPU, TREINO, VOLTAR);
    /// CONFIGURAÇÕES opens the dark/amber Options modal.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public const string CharacterSelectSceneName = "CharacterSelect";

        [SerializeField] private MenuNavigationGroup rootNavigation;
        [SerializeField] private MenuNavigationGroup fightSubmenuNavigation;
        [SerializeField] private GameObject rootMenuContainer;
        [SerializeField] private GameObject fightSubmenuContainer;
        [SerializeField] private TextMeshProUGUI sectionHeadingText;
        [SerializeField] private OptionsPanelController optionsPanel;
        [SerializeField] private FighterRoster fighterRoster;
        [SerializeField] private Sprite whiteSprite;

        private bool _built;

        public bool IsFightSubmenuOpen => fightSubmenuContainer != null && fightSubmenuContainer.activeSelf;

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
                    if (sectionHeadingText == null) sectionHeadingText = panel.Find("SectionHeading")?.GetComponent<TextMeshProUGUI>();
                }
                if (optionsPanel == null) optionsPanel = GetComponentInChildren<OptionsPanelController>(true);
                BindAllMenus();
            }

            _built = true;
        }

        public void BuildUI(Transform canvasRoot, Sprite sprite)
        {
            whiteSprite = sprite;

            // Subtle left-side dark gradient rail framing the MKX navigation over the live 3D diorama
            RectTransform leftVignette = MenuUiFactory.CreatePanel("LeftBackdrop", canvasRoot, new Color(0.02f, 0.022f, 0.03f, 0.65f), whiteSprite);
            leftVignette.anchorMin = new Vector2(0f, 0f);
            leftVignette.anchorMax = new Vector2(0f, 1f);
            leftVignette.pivot = new Vector2(0f, 0.5f);
            leftVignette.anchoredPosition = Vector2.zero;
            leftVignette.sizeDelta = new Vector2(690f, 0f);

            // Main Left Column MKX Panel
            RectTransform menuBox = MenuUiFactory.CreatePanel("MainMenuPanel", canvasRoot, MenuUiFactory.ObsidianBg, whiteSprite);
            menuBox.anchorMin = new Vector2(0f, 0.5f);
            menuBox.anchorMax = new Vector2(0f, 0.5f);
            menuBox.pivot = new Vector2(0f, 0.5f);
            menuBox.anchoredPosition = new Vector2(84f, 0f);
            menuBox.sizeDelta = new Vector2(560f, 700f);
            MenuUiFactory.AddBorder(menuBox, MenuUiFactory.BronzeBorder, whiteSprite, 2f);

            RectTransform topBar = MenuUiFactory.CreatePanel("HeaderAccent", menuBox, MenuUiFactory.AccentGold, whiteSprite);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 4f);

            TextMeshProUGUI title = MenuUiFactory.CreateText(
                "GameTitle",
                menuBox,
                "UNDFIGHT",
                68f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.AccentGold,
                characterSpacing: 11f);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            title.rectTransform.sizeDelta = new Vector2(-68f, 82f);

            TextMeshProUGUI subtitle = MenuUiFactory.CreateText(
                "Subtitle",
                menuBox,
                "REMAKE 2.5D  •  ARENA DE COMBATE",
                17f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                new Color(0.68f, 0.62f, 0.52f, 1f),
                characterSpacing: 6f);
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -118f);
            subtitle.rectTransform.sizeDelta = new Vector2(-68f, 28f);

            RectTransform divider = MenuUiFactory.CreatePanel("HeaderDivider", menuBox, MenuUiFactory.BronzeBorder, whiteSprite);
            divider.anchorMin = new Vector2(0f, 1f);
            divider.anchorMax = new Vector2(1f, 1f);
            divider.pivot = new Vector2(0.5f, 1f);
            divider.anchoredPosition = new Vector2(0f, -162f);
            divider.sizeDelta = new Vector2(-68f, 2f);

            sectionHeadingText = MenuUiFactory.CreateText(
                "SectionHeading",
                menuBox,
                "MENU PRINCIPAL",
                16f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                MenuUiFactory.AccentAmber,
                characterSpacing: 7f);
            sectionHeadingText.rectTransform.anchorMin = new Vector2(0f, 1f);
            sectionHeadingText.rectTransform.anchorMax = new Vector2(1f, 1f);
            sectionHeadingText.rectTransform.pivot = new Vector2(0.5f, 1f);
            sectionHeadingText.rectTransform.anchoredPosition = new Vector2(0f, -182f);
            sectionHeadingText.rectTransform.sizeDelta = new Vector2(-68f, 28f);

            // Root Menu Container (3 items)
            var rootGroupGo = new GameObject("RootMenuGroup", typeof(RectTransform));
            rootGroupGo.transform.SetParent(menuBox, false);
            MenuUiFactory.StretchFull(rootGroupGo.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            rootMenuContainer = rootGroupGo;
            rootNavigation = rootGroupGo.AddComponent<MenuNavigationGroup>();

            string[] rootLabels =
            {
                "LUTAR",
                "CONFIGURAÇÕES",
                "SAIR DO JOGO"
            };
            CreateColumnButtons(rootGroupGo.transform, rootNavigation, "RootBtn", rootLabels, -234f, 82f, 98f, 28f);

            // Fight Modes Submenu Container (4 items)
            var fightGroupGo = new GameObject("FightSubmenuGroup", typeof(RectTransform));
            fightGroupGo.transform.SetParent(menuBox, false);
            MenuUiFactory.StretchFull(fightGroupGo.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            fightSubmenuContainer = fightGroupGo;
            fightSubmenuNavigation = fightGroupGo.AddComponent<MenuNavigationGroup>();

            string[] fightLabels =
            {
                "PLAYER VS PLAYER",
                "PLAYER VS CPU",
                "TREINO",
                "VOLTAR"
            };
            CreateColumnButtons(fightGroupGo.transform, fightSubmenuNavigation, "FightBtn", fightLabels, -234f, 74f, 88f, 25f);
            fightGroupGo.SetActive(false);

            // Bottom subtle MKX bar
            RectTransform footerBar = MenuUiFactory.CreatePanel("FooterBar", canvasRoot, new Color(0.043f, 0.047f, 0.063f, 0.84f), whiteSprite);
            footerBar.anchorMin = new Vector2(0f, 0f);
            footerBar.anchorMax = new Vector2(1f, 0f);
            footerBar.pivot = new Vector2(0.5f, 0f);
            footerBar.anchoredPosition = Vector2.zero;
            footerBar.sizeDelta = new Vector2(0f, 44f);

            RectTransform footerBorder = MenuUiFactory.CreatePanel("FooterTopBorder", footerBar, MenuUiFactory.BronzeBorder, whiteSprite);
            footerBorder.anchorMin = new Vector2(0f, 1f);
            footerBorder.anchorMax = new Vector2(1f, 1f);
            footerBorder.pivot = new Vector2(0.5f, 1f);
            footerBorder.sizeDelta = new Vector2(0f, 1.5f);

            TextMeshProUGUI footer = MenuUiFactory.CreateText(
                "MainFooter",
                footerBar,
                "WASD / SETAS / D-PAD: NAVEGAR   •   ENTER / J / A: CONFIRMAR   •   ESC / K / B: VOLTAR",
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Color(0.68f, 0.66f, 0.62f, 1f),
                characterSpacing: 4f);
            MenuUiFactory.StretchFull(footer.rectTransform, 24f, 2f, 24f, 2f);

            // Options Modal
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

        private void CreateColumnButtons(
            Transform parent,
            MenuNavigationGroup nav,
            string prefix,
            string[] labels,
            float startY,
            float buttonHeight,
            float stepY,
            float fontSize)
        {
            nav.ClearItems();
            for (int i = 0; i < labels.Length; i++)
            {
                MenuItemEntry entry = MenuUiFactory.CreateMenuButton($"{prefix}_{i}", parent, labels[i], whiteSprite, fontSize, TextAlignmentOptions.MidlineLeft, characterSpacing: 6f);
                RectTransform rt = entry.Button.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, startY - i * stepY);
                rt.sizeDelta = new Vector2(-68f, buttonHeight);
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
                    rootNavigation.AddItem(BuildEntryFromButton(btns[i], () => ExecuteRootAction(idx)));
                }
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

            if (optionsPanel != null)
            {
                optionsPanel.OnClosed -= HandleOptionsClosed;
                optionsPanel.OnClosed += HandleOptionsClosed;
            }
        }

        private static MenuItemEntry BuildEntryFromButton(UnityEngine.UI.Button btn, System.Action onSubmit)
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

        private void ExecuteRootAction(int index)
        {
            switch (index)
            {
                case 0:
                    OpenFightSubmenu();
                    break;
                case 1:
                    OpenOptions();
                    break;
                case 2:
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
            rootNavigation?.SetSelectedIndex(0);
            rootNavigation?.SetInputLocked(false);
        }

        public void StartMode(GameMode mode)
        {
            GameSessionState.SelectMode(mode);
            GameSessionState.OpenStageSelectOnCharacterSelect = false;
            if (fighterRoster != null)
            {
                GameSessionState.SelectFighters(null, null);
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
