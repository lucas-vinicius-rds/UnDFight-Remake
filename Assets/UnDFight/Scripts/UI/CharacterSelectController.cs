using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnDFight
{
    /// <summary>
    /// Dynamic FighterRoster selection screen with 3D URP live preview (Idle -> Victory, real ModelScale),
    /// independent P1/P2 cursors, mode-aware sequential picking (VS CPU / Training), and mirror match support.
    /// </summary>
    public class CharacterSelectController : MonoBehaviour
    {
        private sealed class RosterCardView
        {
            public int Index;
            public FighterData Data;
            public UnityEngine.UI.Button Button;
            public UnityEngine.UI.Image Background;
            public UnityEngine.UI.Image P1Outline;
            public UnityEngine.UI.Image P2Outline;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI BadgeText;
        }

        [Header("Data & Scene Flow")]
        [SerializeField] private FighterRoster fighterRoster;
        [SerializeField] private string fightSceneName = "Training";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private float matchLaunchDelaySeconds = 0.70f;
        [SerializeField] private Sprite whiteSprite;

        [Header("3D Preview Anchors")]
        [SerializeField] private Transform p1PreviewAnchor;
        [SerializeField] private Transform p2PreviewAnchor;

        [Header("UI References")]
        [SerializeField] private Transform gridContainer;
        [SerializeField] private TextMeshProUGUI modeBannerText;
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
        private int _p1Index;
        private int _p2Index;
        private bool _p1Confirmed;
        private bool _p2Confirmed;
        private float _launchTimer = -1f;

        private GameObject _p1PreviewInstance;
        private GameObject _p2PreviewInstance;
        private Animator _p1PreviewAnimator;
        private Animator _p2PreviewAnimator;
        private FighterData _p1SpawnedData;
        private FighterData _p2SpawnedData;

        private float _p1RepeatCooldown;
        private float _p2RepeatCooldown;
        private bool _built;

        private static readonly int StateParamHash = Animator.StringToHash("State");

        public int P1Index => _p1Index;
        public int P2Index => _p2Index;
        public bool P1Confirmed => _p1Confirmed;
        public bool P2Confirmed => _p2Confirmed;

        private void Awake()
        {
            if (gridContainer == null || _cards.Count == 0)
            {
                if (whiteSprite != null && transform.Find("TopHeader") == null)
                {
                    BuildUI(transform, whiteSprite);
                }
            }
        }

        private void Start()
        {
            if (!_built)
            {
                RebuildRosterGrid();
            }

            _p1Index = 0;
            _p2Index = fighterRoster != null && fighterRoster.Count > 1 ? 1 : 0;
            _p1Confirmed = false;
            _p2Confirmed = false;
            _launchTimer = -1f;

            RefreshAll();
        }

        public void ConfigureReferences(
            FighterRoster roster,
            Sprite sprite,
            Transform p1Anchor,
            Transform p2Anchor)
        {
            fighterRoster = roster;
            whiteSprite = sprite;
            p1PreviewAnchor = p1Anchor;
            p2PreviewAnchor = p2Anchor;
        }

        public void BuildUI(Transform canvasRoot, Sprite sprite)
        {
            whiteSprite = sprite;

            // Top Banner
            RectTransform topHeader = MenuUiFactory.CreatePanel("TopHeader", canvasRoot, new Color(0.06f, 0.08f, 0.13f, 0.90f), whiteSprite);
            topHeader.anchorMin = new Vector2(0f, 1f);
            topHeader.anchorMax = new Vector2(1f, 1f);
            topHeader.pivot = new Vector2(0.5f, 1f);
            topHeader.anchoredPosition = new Vector2(0f, -14f);
            topHeader.sizeDelta = new Vector2(-80f, 74f);

            modeBannerText = MenuUiFactory.CreateText("ModeTitle", topHeader, "SELEÇÃO DE PERSONAGENS", 34f, FontStyles.Bold, TextAlignmentOptions.Center, MenuUiFactory.AccentGold);
            MenuUiFactory.StretchFull(modeBannerText.rectTransform, 16f, 4f, 16f, 4f);

            // P1 Info Card (Bottom-Left above grid)
            CreateInfoCard(canvasRoot, true, out p1HeaderText, out p1NameText, out p1HeightText, out p1WeightText, out p1StatusText);
            // P2 Info Card (Bottom-Right above grid)
            CreateInfoCard(canvasRoot, false, out p2HeaderText, out p2NameText, out p2HeightText, out p2WeightText, out p2StatusText);

            // Roster Grid Container (Bottom Center, scales automatically for 8+ fighters)
            RectTransform gridPanel = MenuUiFactory.CreatePanel("RosterGridPanel", canvasRoot, new Color(0.06f, 0.08f, 0.13f, 0.90f), whiteSprite);
            gridPanel.anchorMin = new Vector2(0.5f, 0f);
            gridPanel.anchorMax = new Vector2(0.5f, 0f);
            gridPanel.pivot = new Vector2(0.5f, 0f);
            gridPanel.anchoredPosition = new Vector2(0f, 64f);
            gridPanel.sizeDelta = new Vector2(960f, 224f);

            var gridGo = new GameObject("Grid", typeof(RectTransform));
            gridGo.transform.SetParent(gridPanel, false);
            var gridRt = gridGo.GetComponent<RectTransform>();
            MenuUiFactory.StretchFull(gridRt, 20f, 16f, 20f, 16f);

            var glg = gridGo.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            glg.cellSize = new Vector2(210f, 92f);
            glg.spacing = new Vector2(16f, 12f);
            glg.childAlignment = TextAnchor.MiddleCenter;
            glg.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;

            gridContainer = gridGo.transform;

            TextMeshProUGUI footer = MenuUiFactory.CreateText(
                "SelectFooter",
                canvasRoot,
                "P1: WASD + J/Enter (K/Esc volta)  |  P2: Setas + Num4/Enter (Num5 volta)  |  Mouse & Gamepad ativos",
                19f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Color(0.78f, 0.84f, 0.92f, 1f));
            footer.rectTransform.anchorMin = new Vector2(0f, 0f);
            footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            footer.rectTransform.pivot = new Vector2(0.5f, 0f);
            footer.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            footer.rectTransform.sizeDelta = new Vector2(0f, 36f);

            RebuildRosterGrid();
            _built = true;
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
            Color accent = isLeft ? MenuUiFactory.AccentCyan : MenuUiFactory.AccentCrimson;
            TextAlignmentOptions align = isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;

            RectTransform card = MenuUiFactory.CreatePanel($"{side}_InfoCard", canvasRoot, new Color(0.06f, 0.08f, 0.13f, 0.88f), whiteSprite);
            card.anchorMin = new Vector2(isLeft ? 0f : 1f, 0f);
            card.anchorMax = new Vector2(isLeft ? 0f : 1f, 0f);
            card.pivot = new Vector2(isLeft ? 0f : 1f, 0f);
            card.anchoredPosition = new Vector2(isLeft ? 48f : -48f, 64f);
            card.sizeDelta = new Vector2(390f, 224f);

            RectTransform bar = MenuUiFactory.CreatePanel("Accent", card, accent, whiteSprite);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, 5f);

            hdr = MenuUiFactory.CreateText("Header", card, isLeft ? "JOGADOR 1" : "JOGADOR 2", 20f, FontStyles.Bold, align, accent);
            PlaceRow(hdr.rectTransform, -14f, 28f);

            nameTmp = MenuUiFactory.CreateText("FighterName", card, "LUCAS", 38f, FontStyles.Bold, align, Color.white);
            PlaceRow(nameTmp.rectTransform, -44f, 46f);

            heightTmp = MenuUiFactory.CreateText("Height", card, "Altura: 1,81 m", 22f, FontStyles.Normal, align, new Color(0.85f, 0.90f, 0.96f, 1f));
            PlaceRow(heightTmp.rectTransform, -96f, 30f);

            weightTmp = MenuUiFactory.CreateText("Weight", card, "Peso: 92 kg", 22f, FontStyles.Normal, align, new Color(0.85f, 0.90f, 0.96f, 1f));
            PlaceRow(weightTmp.rectTransform, -128f, 30f);

            statusTmp = MenuUiFactory.CreateText("Status", card, "SELECIONANDO...", 24f, FontStyles.Bold, align, MenuUiFactory.AccentGold);
            PlaceRow(statusTmp.rectTransform, -172f, 36f);
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
            var go = new GameObject($"Card_{data.fighterName}", typeof(RectTransform));
            go.transform.SetParent(gridContainer, false);

            var bg = go.AddComponent<UnityEngine.UI.Image>();
            bg.sprite = whiteSprite;
            bg.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            bg.raycastTarget = true;

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            RectTransform p1Rim = MenuUiFactory.CreatePanel("P1Rim", go.transform, MenuUiFactory.AccentCyan, whiteSprite);
            p1Rim.anchorMin = new Vector2(0f, 1f);
            p1Rim.anchorMax = new Vector2(1f, 1f);
            p1Rim.pivot = new Vector2(0.5f, 1f);
            p1Rim.sizeDelta = new Vector2(0f, 6f);

            RectTransform p2Rim = MenuUiFactory.CreatePanel("P2Rim", go.transform, MenuUiFactory.AccentCrimson, whiteSprite);
            p2Rim.anchorMin = new Vector2(0f, 0f);
            p2Rim.anchorMax = new Vector2(1f, 0f);
            p2Rim.pivot = new Vector2(0.5f, 0f);
            p2Rim.sizeDelta = new Vector2(0f, 6f);

            TextMeshProUGUI nameTmp = MenuUiFactory.CreateText("Name", go.transform, data.fighterName.ToUpperInvariant(), 25f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            MenuUiFactory.StretchFull(nameTmp.rectTransform, 8f, 22f, 8f, 10f);

            TextMeshProUGUI badgeTmp = MenuUiFactory.CreateText("Badge", go.transform, "", 16f, FontStyles.Bold, TextAlignmentOptions.Bottom, MenuUiFactory.AccentGold);
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
                NameText = nameTmp,
                BadgeText = badgeTmp
            };
        }

        private void Update()
        {
            if (_launchTimer >= 0f)
            {
                _launchTimer -= Time.unscaledDeltaTime;
                if (_launchTimer <= 0f)
                {
                    LaunchFightScene();
                }
                return;
            }

            if (fighterRoster == null || fighterRoster.Count == 0)
            {
                return;
            }

            if (GameSessionState.P1SelectsBothFighters)
            {
                UpdateSingleChooserInput();
            }
            else
            {
                UpdateDualPlayerInput();
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
                    RefreshAll();
                }
                if (confirm)
                {
                    _p1Confirmed = true;
                    RefreshAll();
                }
                else if (cancel)
                {
                    SceneManager.LoadScene(mainMenuSceneName);
                }
            }
            else if (!_p2Confirmed)
            {
                if (moveStep != 0)
                {
                    _p2Index = WrapIndex(_p2Index + moveStep);
                    RefreshAll();
                }
                if (confirm)
                {
                    _p2Confirmed = true;
                    RefreshAll();
                    BeginLaunchCountdown();
                }
                else if (cancel)
                {
                    _p1Confirmed = false;
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
                if (p1Move != 0) { _p1Index = WrapIndex(_p1Index + p1Move); changed = true; }
                if (p1Confirm) { _p1Confirmed = true; changed = true; }
                else if (p1Cancel) { SceneManager.LoadScene(mainMenuSceneName); return; }
            }
            else if (p1Cancel)
            {
                _p1Confirmed = false;
                changed = true;
            }

            if (!_p2Confirmed)
            {
                if (p2Move != 0) { _p2Index = WrapIndex(_p2Index + p2Move); changed = true; }
                if (p2Confirm) { _p2Confirmed = true; changed = true; }
            }
            else if (p2Cancel)
            {
                _p2Confirmed = false;
                changed = true;
            }

            if (changed)
            {
                RefreshAll();
                if (_p1Confirmed && _p2Confirmed)
                {
                    BeginLaunchCountdown();
                }
            }
        }

        private void HandleCardClicked(int cardIndex)
        {
            if (_launchTimer >= 0f) return;

            if (!_p1Confirmed)
            {
                _p1Index = cardIndex;
                _p1Confirmed = true;
            }
            else if (!_p2Confirmed)
            {
                _p2Index = cardIndex;
                _p2Confirmed = true;
                BeginLaunchCountdown();
            }

            RefreshAll();
        }

        private void BeginLaunchCountdown()
        {
            FighterData p1Data = fighterRoster.GetFighter(_p1Index);
            FighterData p2Data = fighterRoster.GetFighter(_p2Index);
            GameSessionState.SelectFighters(p1Data, p2Data);
            _launchTimer = matchLaunchDelaySeconds;
        }

        public void ConfirmBothImmediate(int p1Index, int p2Index)
        {
            _p1Index = WrapIndex(p1Index);
            _p2Index = WrapIndex(p2Index);
            _p1Confirmed = true;
            _p2Confirmed = true;
            FighterData p1Data = fighterRoster.GetFighter(_p1Index);
            FighterData p2Data = fighterRoster.GetFighter(_p2Index);
            GameSessionState.SelectFighters(p1Data, p2Data);
            RefreshAll();
        }

        public void LaunchFightScene()
        {
            SceneManager.LoadScene(fightSceneName);
        }

        public void RefreshAll()
        {
            UpdateModeHeader();
            UpdateCardsVisuals();
            UpdateFighterSlot(0, _p1Index, _p1Confirmed, p1PreviewAnchor, true, ref _p1PreviewInstance, ref _p1PreviewAnimator, ref _p1SpawnedData, p1HeaderText, p1NameText, p1HeightText, p1WeightText, p1StatusText);
            UpdateFighterSlot(1, _p2Index, _p2Confirmed, p2PreviewAnchor, false, ref _p2PreviewInstance, ref _p2PreviewAnimator, ref _p2SpawnedData, p2HeaderText, p2NameText, p2HeightText, p2WeightText, p2StatusText);
        }

        private void UpdateModeHeader()
        {
            if (modeBannerText == null) return;

            string modeName = GameSessionState.CurrentMode switch
            {
                GameMode.VersusCpu => "MODO: LUTAR (VS CPU)",
                GameMode.Training => "MODO: TREINO",
                _ => "MODO: VERSUS LOCAL"
            };
            modeBannerText.text = $"SELEÇÃO DE PERSONAGENS  •  {modeName}";
        }

        private void UpdateCardsVisuals()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                RosterCardView c = _cards[i];
                bool isP1 = i == _p1Index;
                bool isP2 = i == _p2Index;

                if (c.P1Outline != null) c.P1Outline.enabled = isP1;
                if (c.P2Outline != null) c.P2Outline.enabled = isP2;
                if (c.Background != null)
                {
                    c.Background.color = (isP1 || isP2)
                        ? new Color(0.20f, 0.26f, 0.38f, 1.0f)
                        : new Color(0.11f, 0.14f, 0.20f, 0.94f);
                }

                if (c.BadgeText != null)
                {
                    if (isP1 && isP2) c.BadgeText.text = "P1 + P2";
                    else if (isP1) c.BadgeText.text = _p1Confirmed ? "P1 PRONTO" : "P1";
                    else if (isP2) c.BadgeText.text = _p2Confirmed ? "P2 PRONTO" : "P2";
                    else c.BadgeText.text = string.Empty;
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

            if (hdrTmp != null)
            {
                hdrTmp.text = slotIndex == 0
                    ? "JOGADOR 1"
                    : (GameSessionState.CurrentMode == GameMode.VersusCpu ? "P2 (CPU)" :
                       GameSessionState.CurrentMode == GameMode.Training ? "P2 (TREINO)" : "JOGADOR 2");
            }

            if (nameTmp != null) nameTmp.text = data.fighterName.ToUpperInvariant();
            if (heightTmp != null) heightTmp.text = $"Altura: {data.heightMeters.ToString("0.00").Replace('.', ',')} m";
            if (weightTmp != null) weightTmp.text = $"Peso: {Mathf.RoundToInt(data.weightKg)} kg";
            if (statusTmp != null)
            {
                bool waitingForP1 = slotIndex == 1 && GameSessionState.P1SelectsBothFighters && !_p1Confirmed;
                statusTmp.text = confirmed ? "PRONTO!" : (waitingForP1 ? "AGUARDANDO P1..." : "SELECIONANDO...");
                statusTmp.color = confirmed ? MenuUiFactory.AccentGold : (waitingForP1 ? new Color(0.65f, 0.72f, 0.80f, 1f) : Color.white);
            }

            if (anchor != null && data.prefab != null && spawnedData != data)
            {
                if (instance != null)
                {
                    if (Application.isPlaying) Destroy(instance);
                    else DestroyImmediate(instance);
                }

                instance = Instantiate(data.prefab, anchor);
                instance.name = $"Preview_P{slotIndex + 1}_{data.fighterName}";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(0f, faceRight ? 90f : -90f, 0f);
                instance.transform.localScale = Vector3.one * data.ModelScale;

                // Disable gameplay components on 3D preview clone
                var fc = instance.GetComponent<FighterController>();
                if (fc != null) fc.enabled = false;
                var ih = instance.GetComponent<PlayerInputHandler>();
                if (ih != null) ih.enabled = false;
                var col = instance.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                animator = instance.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.enabled = true;
                    animator.applyRootMotion = false;
                    // Reset visualRoot scale & rotation cleanly in case FighterController Awake set yaw
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
