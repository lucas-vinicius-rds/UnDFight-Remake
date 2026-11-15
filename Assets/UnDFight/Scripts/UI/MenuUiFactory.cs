using TMPro;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Shared runtime/editor uGUI factory for consistent UnDFight styled panels, buttons, sliders, and TMP labels.
    /// </summary>
    public static class MenuUiFactory
    {
        public static readonly Color PanelDarkBg = new Color(0.06f, 0.08f, 0.13f, 0.94f);
        public static readonly Color AccentGold = new Color(1.0f, 0.76f, 0.16f, 1.0f);
        public static readonly Color AccentCyan = new Color(0.22f, 0.80f, 1.0f, 1.0f);
        public static readonly Color AccentCrimson = new Color(0.95f, 0.28f, 0.28f, 1.0f);

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

        public static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static MenuItemEntry CreateMenuButton(
            string name,
            Transform parent,
            string label,
            Sprite whiteSprite,
            float fontSize = 26f,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<UnityEngine.UI.Image>();
            bg.sprite = whiteSprite;
            bg.color = new Color(0.11f, 0.14f, 0.20f, 0.94f);
            bg.raycastTarget = true;

            var btn = go.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = nav;

            RectTransform accentRt = CreatePanel("AccentBar", go.transform, AccentGold, whiteSprite);
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;
            accentRt.sizeDelta = new Vector2(7f, 0f);
            var accentImg = accentRt.GetComponent<UnityEngine.UI.Image>();

            TextMeshProUGUI tmp = CreateText("Label", go.transform, label, fontSize, FontStyles.Bold, alignment, Color.white);
            StretchFull(tmp.rectTransform, 22f, 4f, 18f, 4f);

            return new MenuItemEntry
            {
                Button = btn,
                Background = bg,
                AccentBar = accentImg,
                Label = tmp
            };
        }

        public static UnityEngine.UI.Slider CreateSlider(string name, Transform parent, Sprite whiteSprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var bg = CreatePanel("Background", go.transform, new Color(0.18f, 0.22f, 0.29f, 1f), whiteSprite, raycastTarget: true);
            bg.anchorMin = new Vector2(0f, 0.28f);
            bg.anchorMax = new Vector2(1f, 0.72f);
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            StretchFull(fillAreaRt, 0f, 0f, 0f, 0f);

            var fill = CreatePanel("Fill", fillArea.transform, AccentGold, whiteSprite);
            StretchFull(fill, 0f, 0f, 0f, 0f);
            fill.anchorMin = new Vector2(0f, 0.28f);
            fill.anchorMax = new Vector2(1f, 0.72f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            StretchFull(handleArea.GetComponent<RectTransform>(), 6f, 0f, 6f, 0f);

            var handle = CreatePanel("Handle", handleArea.transform, Color.white, whiteSprite, raycastTarget: true);
            handle.sizeDelta = new Vector2(16f, 0f);

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
