using TMPro;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Shared runtime/editor uGUI factory for dark high-contrast MKX-inspired UnDFight panels,
    /// bronze borders, glowing amber selection bars, sliders, and spaced uppercase TMP typography.
    /// </summary>
    public static class MenuUiFactory
    {
        // MKX-inspired high-contrast dark obsidian + charcoal + metallic bronze + intense amber/gold palette
        public static readonly Color ObsidianBg = new Color(0.043f, 0.047f, 0.063f, 0.96f);        // #0B0C10
        public static readonly Color PanelDarkBg = new Color(0.086f, 0.098f, 0.133f, 0.92f);      // #161922
        public static readonly Color CharcoalMuted = new Color(0.067f, 0.075f, 0.102f, 0.88f);
        public static readonly Color BronzeBorder = new Color(0.290f, 0.247f, 0.208f, 0.95f);     // #4A3F35
        public static readonly Color AccentGold = new Color(1.000f, 0.722f, 0.000f, 1.0f);        // #FFB800
        public static readonly Color AccentAmber = new Color(0.961f, 0.651f, 0.137f, 1.0f);       // #F5A623
        public static readonly Color AccentCyan = new Color(0.55f, 0.72f, 0.88f, 1.0f);           // Cool steel rim accent
        public static readonly Color AccentCrimson = new Color(0.82f, 0.22f, 0.18f, 1.0f);        // Warm crimson accent

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
        /// Adds crisp 1-2px metallic borders around a RectTransform panel without blurred outline artifacts.
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
            colors.highlightedColor = new Color(1.12f, 1.08f, 1.02f, 1f);
            colors.pressedColor = new Color(0.88f, 0.84f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            AddBorder(go.transform, BronzeBorder, whiteSprite, 1.5f);

            // Outer amber glow strip + bright gold core sidebar
            RectTransform glowRt = CreatePanel("AccentGlow", go.transform, new Color(AccentAmber.r, AccentAmber.g, AccentAmber.b, 0.25f), whiteSprite);
            glowRt.anchorMin = new Vector2(0f, 0f);
            glowRt.anchorMax = new Vector2(0f, 1f);
            glowRt.pivot = new Vector2(0f, 0.5f);
            glowRt.anchoredPosition = Vector2.zero;
            glowRt.sizeDelta = new Vector2(14f, 0f);

            RectTransform accentRt = CreatePanel("AccentBar", go.transform, AccentGold, whiteSprite);
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;
            accentRt.sizeDelta = new Vector2(6f, 0f);
            var accentImg = accentRt.GetComponent<UnityEngine.UI.Image>();

            TextMeshProUGUI tmp = CreateText(
                "Label",
                go.transform,
                label,
                fontSize,
                FontStyles.Bold,
                alignment,
                new Color(0.88f, 0.87f, 0.85f, 1f),
                characterSpacing);
            StretchFull(tmp.rectTransform, 24f, 4f, 20f, 4f);

            return new MenuItemEntry
            {
                Button = btn,
                Background = bg,
                AccentBar = accentImg,
                AccentGlow = glowRt.GetComponent<UnityEngine.UI.Image>(),
                Label = tmp
            };
        }

        public static UnityEngine.UI.Slider CreateSlider(string name, Transform parent, Sprite whiteSprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var bg = CreatePanel("Background", go.transform, new Color(0.05f, 0.06f, 0.08f, 1f), whiteSprite, raycastTarget: true);
            bg.anchorMin = new Vector2(0f, 0.28f);
            bg.anchorMax = new Vector2(1f, 0.72f);
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;
            AddBorder(bg, BronzeBorder, whiteSprite, 1f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            StretchFull(fillArea.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            var fill = CreatePanel("Fill", fillArea.transform, AccentGold, whiteSprite);
            StretchFull(fill, 0f, 0f, 0f, 0f);
            fill.anchorMin = new Vector2(0f, 0.28f);
            fill.anchorMax = new Vector2(1f, 0.72f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            StretchFull(handleArea.GetComponent<RectTransform>(), 6f, 0f, 6f, 0f);

            var handle = CreatePanel("Handle", handleArea.transform, new Color(1.0f, 0.92f, 0.72f, 1f), whiteSprite, raycastTarget: true);
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
