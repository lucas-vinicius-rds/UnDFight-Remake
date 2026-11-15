using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnDFight
{
    /// <summary>
    /// Controls MainMenu.unity: mode selection (VS CPU, Local Versus, Training), Options JSON modal, and Quit.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public const string CharacterSelectSceneName = "CharacterSelect";

        [SerializeField] private MenuNavigationGroup mainNavigation;
        [SerializeField] private OptionsPanelController optionsPanel;
        [SerializeField] private FighterRoster fighterRoster;
        [SerializeField] private Sprite whiteSprite;

        private bool _built;

        private void Awake()
        {
            SettingsManager.ApplySettings(SettingsManager.Current, applyResolution: false);
            if (!_built)
            {
                EnsureRuntimeWiring();
            }
        }

        public void Configure(
            MenuNavigationGroup nav,
            OptionsPanelController options,
            FighterRoster roster,
            Sprite sprite)
        {
            mainNavigation = nav;
            optionsPanel = options;
            fighterRoster = roster;
            whiteSprite = sprite;
            BindMainButtons();
        }

        private void EnsureRuntimeWiring()
        {
            if (mainNavigation == null)
            {
                mainNavigation = GetComponentInChildren<MenuNavigationGroup>(true);
            }
            if (optionsPanel == null)
            {
                optionsPanel = GetComponentInChildren<OptionsPanelController>(true);
            }

            if (transform.Find("MainMenuPanel") == null && whiteSprite != null)
            {
                BuildUI(transform, whiteSprite);
            }
            else if (mainNavigation != null)
            {
                BindMainButtons();
            }

            _built = true;
        }

        public void BuildUI(Transform canvasRoot, Sprite sprite)
        {
            whiteSprite = sprite;

            // Left showcase container
            RectTransform menuBox = MenuUiFactory.CreatePanel("MainMenuPanel", canvasRoot, new Color(0.06f, 0.08f, 0.13f, 0.90f), whiteSprite);
            menuBox.anchorMin = new Vector2(0f, 0.5f);
            menuBox.anchorMax = new Vector2(0f, 0.5f);
            menuBox.pivot = new Vector2(0f, 0.5f);
            menuBox.anchoredPosition = new Vector2(110f, 0f);
            menuBox.sizeDelta = new Vector2(620f, 720f);

            RectTransform topBar = MenuUiFactory.CreatePanel("HeaderAccent", menuBox, MenuUiFactory.AccentGold, whiteSprite);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 6f);

            TextMeshProUGUI title = MenuUiFactory.CreateText("GameTitle", menuBox, "UNDFIGHT", 72f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, MenuUiFactory.AccentGold);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            title.rectTransform.sizeDelta = new Vector2(-72f, 84f);

            TextMeshProUGUI subtitle = MenuUiFactory.CreateText("Subtitle", menuBox, "REMAKE 2.5D  •  SIMULAÇÃO 60 HZ", 21f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, MenuUiFactory.AccentCyan);
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -118f);
            subtitle.rectTransform.sizeDelta = new Vector2(-72f, 34f);

            if (mainNavigation == null)
            {
                mainNavigation = menuBox.gameObject.AddComponent<MenuNavigationGroup>();
            }
            mainNavigation.ClearItems();

            string[] labels =
            {
                "LUTAR (VS CPU)",
                "VERSUS LOCAL",
                "TREINO",
                "OPÇÕES",
                "SAIR"
            };

            for (int i = 0; i < labels.Length; i++)
            {
                MenuItemEntry entry = MenuUiFactory.CreateMenuButton($"Btn_{i}", menuBox, labels[i], whiteSprite, 28f, TextAlignmentOptions.MidlineLeft);
                RectTransform rt = entry.Button.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -190f - i * 86f);
                rt.sizeDelta = new Vector2(-72f, 70f);
                mainNavigation.AddItem(entry);
            }

            TextMeshProUGUI footer = MenuUiFactory.CreateText(
                "MainFooter",
                canvasRoot,
                "WASD / Setas / D-Pad: Navegar  |  Enter / J / A: Confirmar  |  Mouse: Selecionar",
                20f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Color(0.76f, 0.82f, 0.90f, 1f));
            footer.rectTransform.anchorMin = new Vector2(0f, 0f);
            footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            footer.rectTransform.pivot = new Vector2(0.5f, 0f);
            footer.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            footer.rectTransform.sizeDelta = new Vector2(0f, 40f);

            // Right stage card info / hero callout
            RectTransform heroPanel = MenuUiFactory.CreatePanel("HeroInfoPanel", canvasRoot, new Color(0.06f, 0.08f, 0.13f, 0.72f), whiteSprite);
            heroPanel.anchorMin = new Vector2(1f, 0.5f);
            heroPanel.anchorMax = new Vector2(1f, 0.5f);
            heroPanel.pivot = new Vector2(1f, 0.5f);
            heroPanel.anchoredPosition = new Vector2(-110f, 0f);
            heroPanel.sizeDelta = new Vector2(540f, 340f);

            TextMeshProUGUI heroTitle = MenuUiFactory.CreateText("HeroHeading", heroPanel, "MODOS DE JOGO", 28f, FontStyles.Bold, TextAlignmentOptions.TopLeft, MenuUiFactory.AccentGold);
            MenuUiFactory.StretchFull(heroTitle.rectTransform, 32f, 32f, 32f, 28f);

            TextMeshProUGUI heroBody = MenuUiFactory.CreateText(
                "HeroBody",
                heroPanel,
                "• <b>LUTAR (VS CPU)</b>: Escolha seu lutador e o adversário.\n\n" +
                "• <b>VERSUS LOCAL</b>: 2 jogadores no mesmo teclado ou gamepads.\n\n" +
                "• <b>TREINO</b>: Timer infinito e regeneração automática de vida.\n\n" +
                "• <b>OPÇÕES</b>: Volumes, vídeo e remapeamento em JSON.",
                21f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                new Color(0.88f, 0.91f, 0.96f, 1f));
            MenuUiFactory.StretchFull(heroBody.rectTransform, 32f, 28f, 32f, 84f);

            // Options Modal
            var optGo = new GameObject("OptionsPanel", typeof(RectTransform));
            optGo.transform.SetParent(canvasRoot, false);
            optionsPanel = optGo.AddComponent<OptionsPanelController>();
            optionsPanel.BuildUI(whiteSprite);
            optGo.SetActive(false);

            BindMainButtons();
            _built = true;
        }

        private void BindMainButtons()
        {
            if (mainNavigation == null) return;

            // Attach actions cleanly by button name created above
            var buttons = mainNavigation.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            mainNavigation.ClearItems();

            for (int i = 0; i < buttons.Length; i++)
            {
                UnityEngine.UI.Button btn = buttons[i];
                var bg = btn.GetComponent<UnityEngine.UI.Image>();
                var accent = btn.transform.Find("AccentBar")?.GetComponent<UnityEngine.UI.Image>();
                var label = btn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                int index = i;

                var entry = new MenuItemEntry
                {
                    Button = btn,
                    Background = bg,
                    AccentBar = accent,
                    Label = label,
                    OnSubmit = () => ExecuteMainAction(index)
                };
                mainNavigation.AddItem(entry);
            }

            if (optionsPanel != null)
            {
                optionsPanel.OnClosed -= HandleOptionsClosed;
                optionsPanel.OnClosed += HandleOptionsClosed;
            }
        }

        private void ExecuteMainAction(int index)
        {
            switch (index)
            {
                case 0:
                    StartMode(GameMode.VersusCpu);
                    break;
                case 1:
                    StartMode(GameMode.LocalVersus);
                    break;
                case 2:
                    StartMode(GameMode.Training);
                    break;
                case 3:
                    OpenOptions();
                    break;
                case 4:
                    QuitGame();
                    break;
            }
        }

        public void StartMode(GameMode mode)
        {
            GameSessionState.SelectMode(mode);
            if (fighterRoster != null)
            {
                GameSessionState.SelectFighters(null, null);
            }
            SceneManager.LoadScene(CharacterSelectSceneName);
        }

        public void OpenOptions()
        {
            if (optionsPanel == null) return;
            mainNavigation?.SetInputLocked(true);
            optionsPanel.Open();
        }

        private void HandleOptionsClosed()
        {
            mainNavigation?.SetInputLocked(false);
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
