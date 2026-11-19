using TMPro;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Shared runtime/editor uGUI factory for matte MKX-inspired UnDFight UI
    /// using the official institutional UnDF blue palette and dark graphite surfaces (zero neon).
    /// </summary>
    public static class MenuUiFactory
    {
        // Dark graphite / obsidian matte backgrounds
        public static readonly Color ObsidianBg = new Color(0.043f, 0.055f, 0.078f, 0.96f);       // #0B0E14
        public static readonly Color PanelDarkBg = new Color(0.071f, 0.094f, 0.133f, 0.94f);      // #121822
        public static readonly Color PanelElevatedBg = new Color(0.094f, 0.125f, 0.176f, 0.94f);  // #18202D
        public static readonly Color CharcoalMuted = new Color(0.094f, 0.125f, 0.176f, 0.88f);    // #18202D

        // Matte metallic slate borders
        public static readonly Color SlateBorderDark = new Color(0.200f, 0.255f, 0.333f, 0.95f);  // #334155
        public static readonly Color SlateBorderMid = new Color(0.278f, 0.333f, 0.412f, 0.95f);   // #475569
        public static readonly Color SlateBorderLight = new Color(0.392f, 0.455f, 0.545f, 0.95f); // #64748B

        // Official UnDF institutional blue palette (extracted from Logo_UnDF.png)
        public static readonly Color UnDFNavy = new Color(0.133f, 0.161f, 0.388f, 1.0f);          // #222963
        public static readonly Color UnDFRoyal = new Color(0.118f, 0.337f, 0.600f, 1.0f);         // #1E5699
        public static readonly Color UnDFBlue = new Color(0.239f, 0.541f, 0.780f, 1.0f);          // #3D8AC7
        public static readonly Color UnDFSky = new Color(0.545f, 0.788f, 0.933f, 1.0f);           // #8BC9EE

        // Matte crimson for P2 and Secret Final Boss Ibaneis (zero neon)
        public static readonly Color CrimsonDark = new Color(0.600f, 0.106f, 0.106f, 1.0f);       // #991B1B
        public static readonly Color CrimsonActive = new Color(0.725f, 0.110f, 0.110f, 1.0f);     // #B91C1C
        public static readonly Color CrimsonSurface = new Color(0.165f, 0.067f, 0.075f, 0.95f);   // Matte dark crimson panel

        // Matte victory green for completed Arcade Tower rungs
        public static readonly Color VictoryMutedBg = new Color(0.075f, 0.149f, 0.125f, 0.94f);
        public static readonly Color VictoryMutedBorder = new Color(0.216f, 0.435f, 0.345f, 0.95f);
        public static readonly Color VictoryMutedText = new Color(0.545f, 0.780f, 0.665f, 1.0f);

        // Clean high-contrast typography colors
        public static readonly Color TextActive = new Color(0.945f, 0.961f, 0.976f, 1.0f);        // #F1F5F9
        public static readonly Color TextInactive = new Color(0.522f, 0.580f, 0.659f, 1.0f);      // #8594A8
        public static readonly Color TextSubtitle = new Color(0.796f, 0.835f, 0.882f, 1.0f);      // #CBD5E1

        // Compatibility aliases mapped cleanly to the new matte UnDF / MKX palette
        public static readonly Color BronzeBorder = SlateBorderDark;
        public static readonly Color AccentGold = UnDFBlue;
        public static readonly Color AccentAmber = UnDFSky;
        public static readonly Color AccentCyan = UnDFSky;
        public static readonly Color AccentCrimson = CrimsonActive;

        public static RectTransform CreatePanel(string name, Transform parent, Color color, Sprite sprite, bool raycastTarget = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycastTarget;
            return rt;
        }

        /// <summary>
        /// Adds crisp 1-2px matte metallic borders around a RectTransform panel without blurred outline artifacts.
        /// </summary>
        public static void AddBorder(Transform parent, Color color, Sprite sprite, float thickness = 1.5f)
        {
            CreateEdge("Border_Top", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, color, sprite);
            CreateEdge("Border_Bottom", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), color, sprite);
            CreateEdge("Border_Left", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), color, sprite);
            CreateEdge("Border_Right", parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero, color, sprite);
        }

        private static void CreateEdge(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color,
            Sprite sprite)
        {
            RectTransform rt = CreatePanel(name, parent, color, sprite, raycastTarget: false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Color color,
            float characterSpacing = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.characterSpacing = characterSpacing;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>
        /// Creates a clean MKX-style centered list item with NO heavy box around it:
        /// soft horizontal UnDF Royal sweep (#1E5699) behind the selected label + flanking #8BC9EE diamonds.
        /// </summary>
        public static MenuItemEntry CreateMkxCleanMenuButton(
            string name,
            Transform parent,
            string label,
            Sprite whiteSprite,
            Sprite softSweepSprite,
            Sprite diamondSprite,
            float fontSize = 27f,
            float characterSpacing = 7f,
            float diamondOffsetX = 165f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var hitBg = go.AddComponent<UnityEngine.UI.Image>();
            hitBg.sprite = whiteSprite;
            hitBg.color = new Color(0f, 0f, 0f, 0f);
            hitBg.raycastTarget = true;

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = hitBg;
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            // Soft horizontal selection sweep behind text
            RectTransform sweepRt = CreatePanel(
                "SelectionSweep",
                go.transform,
                new Color(UnDFRoyal.r, UnDFRoyal.g, UnDFRoyal.b, 0f),
                softSweepSprite != null ? softSweepSprite : whiteSprite,
                raycastTarget: false);
            StretchFull(sweepRt, 0f, 2f, 0f, 2f);
            var sweepImg = sweepRt.GetComponent<UnityEngine.UI.Image>();

            // Thin center horizontal hairline under active sweep
            RectTransform topHairline = CreatePanel("SweepTopLine", sweepRt, new Color(UnDFBlue.r, UnDFBlue.g, UnDFBlue.b, 0.55f), softSweepSprite != null ? softSweepSprite : whiteSprite);
            topHairline.anchorMin = new Vector2(0.12f, 1f);
            topHairline.anchorMax = new Vector2(0.88f, 1f);
            topHairline.pivot = new Vector2(0.5f, 1f);
            topHairline.sizeDelta = new Vector2(0f, 1.5f);

            RectTransform botHairline = CreatePanel("SweepBottomLine", sweepRt, new Color(UnDFBlue.r, UnDFBlue.g, UnDFBlue.b, 0.55f), softSweepSprite != null ? softSweepSprite : whiteSprite);
            botHairline.anchorMin = new Vector2(0.12f, 0f);
            botHairline.anchorMax = new Vector2(0.88f, 0f);
            botHairline.pivot = new Vector2(0.5f, 0f);
            botHairline.sizeDelta = new Vector2(0f, 1.5f);

            // Flanking diamonds (#8BC9EE)
            RectTransform leftDiaRt = CreatePanel(
                "DiamondLeft",
                go.transform,
                new Color(UnDFSky.r, UnDFSky.g, UnDFSky.b, 0f),
                diamondSprite != null ? diamondSprite : whiteSprite,
                raycastTarget: false);
            leftDiaRt.anchorMin = new Vector2(0.5f, 0.5f);
            leftDiaRt.anchorMax = new Vector2(0.5f, 0.5f);
            leftDiaRt.pivot = new Vector2(0.5f, 0.5f);
            leftDiaRt.anchoredPosition = new Vector2(-diamondOffsetX, 0f);
            leftDiaRt.sizeDelta = new Vector2(12f, 12f);
            if (diamondSprite == null) leftDiaRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var leftDiaImg = leftDiaRt.GetComponent<UnityEngine.UI.Image>();

            RectTransform rightDiaRt = CreatePanel(
                "DiamondRight",
                go.transform,
                new Color(UnDFSky.r, UnDFSky.g, UnDFSky.b, 0f),
                diamondSprite != null ? diamondSprite : whiteSprite,
                raycastTarget: false);
            rightDiaRt.anchorMin = new Vector2(0.5f, 0.5f);
            rightDiaRt.anchorMax = new Vector2(0.5f, 0.5f);
            rightDiaRt.pivot = new Vector2(0.5f, 0.5f);
            rightDiaRt.anchoredPosition = new Vector2(diamondOffsetX, 0f);
            rightDiaRt.sizeDelta = new Vector2(12f, 12f);
            if (diamondSprite == null) rightDiaRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var rightDiaImg = rightDiaRt.GetComponent<UnityEngine.UI.Image>();

            TextMeshProUGUI tmp = CreateText(
                "Label",
                go.transform,
                label,
                fontSize,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                TextInactive,
                characterSpacing);
            StretchFull(tmp.rectTransform, 16f, 2f, 16f, 2f);

            return new MenuItemEntry
            {
                Button = btn,
                Background = hitBg,
                SweepImage = sweepImg,
                LeftDiamond = leftDiaImg,
                RightDiamond = rightDiaImg,
                Label = tmp,
                UseMkxCleanStyle = true,
                NormalBg = new Color(0f, 0f, 0f, 0f),
                SelectedBg = new Color(0f, 0f, 0f, 0f),
                SweepSelectedColor = new Color(UnDFRoyal.r, UnDFRoyal.g, UnDFRoyal.b, 0.52f),
                DiamondSelectedColor = UnDFSky,
                NormalLabelColor = TextInactive,
                SelectedLabelColor = TextActive
            };
        }

        /// <summary>
        /// Creates a structured matte graphite button with UnDF institutional blue selection bar
        /// (used in Options modal, Pause menu, Tower actions, and MatchOver panels).
        /// </summary>
        public static MenuItemEntry CreateMenuButton(
            string name,
            Transform parent,
            string label,
            Sprite whiteSprite,
            float fontSize = 26f,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
            float characterSpacing = 5f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<UnityEngine.UI.Image>();
            bg.sprite = whiteSprite;
            bg.color = PanelDarkBg;
            bg.raycastTarget = true;

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.12f, 1.18f, 1f);
            colors.pressedColor = new Color(0.86f, 0.90f, 0.96f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            AddBorder(go.transform, SlateBorderDark, whiteSprite, 1.5f);

            // Subtle matte UnDF Royal highlight band + UnDF Blue sidebar
            RectTransform glowRt = CreatePanel("AccentGlow", go.transform, new Color(UnDFRoyal.r, UnDFRoyal.g, UnDFRoyal.b, 0.28f), whiteSprite);
            glowRt.anchorMin = new Vector2(0f, 0f);
            glowRt.anchorMax = new Vector2(0f, 1f);
            glowRt.pivot = new Vector2(0f, 0.5f);
            glowRt.anchoredPosition = Vector2.zero;
            glowRt.sizeDelta = new Vector2(12f, 0f);

            RectTransform accentRt = CreatePanel("AccentBar", go.transform, UnDFBlue, whiteSprite);
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;
            accentRt.sizeDelta = new Vector2(5f, 0f);
            var accentImg = accentRt.GetComponent<UnityEngine.UI.Image>();

            TextMeshProUGUI tmp = CreateText(
                "Label",
                go.transform,
                label,
                fontSize,
                FontStyles.Bold,
                alignment,
                TextSubtitle,
                characterSpacing);
            StretchFull(tmp.rectTransform, 24f, 4f, 20f, 4f);

            return new MenuItemEntry
            {
                Button = btn,
                Background = bg,
                AccentBar = accentImg,
                AccentGlow = glowRt.GetComponent<UnityEngine.UI.Image>(),
                Label = tmp,
                UseMkxCleanStyle = false,
                NormalBg = PanelDarkBg,
                SelectedBg = new Color(0.118f, 0.215f, 0.345f, 0.96f),
                NormalAccent = SlateBorderMid,
                SelectedAccent = UnDFBlue,
                NormalLabelColor = TextInactive,
                SelectedLabelColor = TextActive
            };
        }

        /// <summary>
        /// Creates an MKX-style arched feature card using 9-sliced UI_ArchedCard.png with outer metallic frame + matte interior.
        /// </summary>
        public static UnityEngine.UI.Button CreateArchedCard(
            string name,
            Transform parent,
            Sprite archedSprite,
            Sprite fallbackWhite,
            Color borderColor,
            Color fillColor,
            Vector2 size)
        {
            Sprite useSprite = archedSprite != null ? archedSprite : fallbackWhite;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var outerImg = go.AddComponent<UnityEngine.UI.Image>();
            outerImg.sprite = useSprite;
            outerImg.type = archedSprite != null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            outerImg.color = borderColor;
            outerImg.raycastTarget = true;

            var innerGo = new GameObject("InnerFill", typeof(RectTransform));
            innerGo.transform.SetParent(go.transform, false);
            var innerRt = innerGo.GetComponent<RectTransform>();
            StretchFull(innerRt, 2.5f, 2.5f, 2.5f, 2.5f);
            var innerImg = innerGo.AddComponent<UnityEngine.UI.Image>();
            innerImg.sprite = useSprite;
            innerImg.type = archedSprite != null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            innerImg.color = fillColor;
            innerImg.raycastTarget = false;

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = outerImg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.22f, 1.28f, 1.38f, 1f);
            colors.pressedColor = new Color(0.85f, 0.90f, 0.96f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            return btn;
        }

        public static UnityEngine.UI.Slider CreateSlider(string name, Transform parent, Sprite whiteSprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var bg = CreatePanel("Background", go.transform, ObsidianBg, whiteSprite, raycastTarget: true);
            bg.anchorMin = new Vector2(0f, 0.28f);
            bg.anchorMax = new Vector2(1f, 0.72f);
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;
            AddBorder(bg, SlateBorderDark, whiteSprite, 1f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            StretchFull(fillArea.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            var fill = CreatePanel("Fill", fillArea.transform, UnDFBlue, whiteSprite);
            StretchFull(fill, 0f, 0f, 0f, 0f);
            fill.anchorMin = new Vector2(0f, 0.28f);
            fill.anchorMax = new Vector2(1f, 0.72f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            StretchFull(handleArea.GetComponent<RectTransform>(), 6f, 0f, 6f, 0f);

            var handle = CreatePanel("Handle", handleArea.transform, TextActive, whiteSprite, raycastTarget: true);
            handle.sizeDelta = new Vector2(14f, 0f);

            var slider = go.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            var nav = slider.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            slider.navigation = nav;
            return slider;
        }

        public static void StretchFull(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
