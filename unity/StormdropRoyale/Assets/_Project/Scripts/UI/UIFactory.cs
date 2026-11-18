using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>Builds uGUI elements from code so the whole UI works without prefabs. Swap in art later.</summary>
    public static class UIFactory
    {
        public static readonly Color Bg = new Color(0.06f, 0.07f, 0.1f, 0.96f);
        public static readonly Color Panel = new Color(0.12f, 0.14f, 0.19f, 0.95f);
        public static readonly Color PanelLight = new Color(0.18f, 0.21f, 0.28f, 0.95f);
        public static readonly Color Accent = new Color(1f, 0.55f, 0.1f);
        public static readonly Color Accent2 = new Color(0.2f, 0.75f, 1f);
        public static readonly Color TextMain = new Color(0.95f, 0.96f, 0.98f);
        public static readonly Color TextDim = new Color(0.65f, 0.7f, 0.78f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
        public static readonly Color Diamond = new Color(0.55f, 0.85f, 1f);
        public static readonly Color Danger = new Color(0.9f, 0.25f, 0.25f);
        public static readonly Color Good = new Color(0.3f, 0.85f, 0.45f);

        static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Color RarityColor(string rarity) => rarity switch
        {
            "rare" => new Color(0.3f, 0.6f, 1f),
            "epic" => new Color(0.7f, 0.4f, 1f),
            "legendary" => new Color(1f, 0.7f, 0.2f),
            "mythic" => new Color(1f, 0.3f, 0.4f),
            _ => new Color(0.7f, 0.72f, 0.76f),
        };

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(es);
        }

        public static RectTransform Rect(Transform parent, string name = "Rect")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Place with anchors (0–1) and optional pixel offsets.</summary>
        public static RectTransform Place(this RectTransform rt, float minX, float minY, float maxX, float maxY, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Fixed-size element anchored at a point (0–1) with a pixel position.</summary>
        public static RectTransform At(this RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static Image Image(Transform parent, Color color, string name = "Image")
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Text(Transform parent, string text, int size = 30, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color ?? TextMain;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string label, Action onClick, Color? color = null, int fontSize = 30)
        {
            var img = Image(parent, color ?? PanelLight, "Button");
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var t = Text(img.transform, label, fontSize, TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Place(0, 0, 1, 1, 8, 4, 8, 4);
            return b;
        }

        public static void SetLabel(this Button b, string label) => b.GetComponentInChildren<Text>().text = label;

        public static InputField Input(Transform parent, string placeholder, int size = 28)
        {
            var img = Image(parent, new Color(0, 0, 0, 0.5f), "Input");
            var field = img.gameObject.AddComponent<InputField>();
            var text = Text(img.transform, "", size);
            text.rectTransform.Place(0, 0, 1, 1, 14, 4, 14, 4);
            text.supportRichText = false;
            var ph = Text(img.transform, placeholder, size, TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            ph.rectTransform.Place(0, 0, 1, 1, 14, 4, 14, 4);
            field.textComponent = text;
            field.placeholder = ph;
            return field;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChange)
        {
            var root = Rect(parent, "Slider");
            var bg = Image(root, new Color(0, 0, 0, 0.5f), "Bg");
            bg.rectTransform.Place(0, 0.35f, 1, 0.65f);
            var fillArea = Rect(root, "FillArea").Place(0, 0.35f, 1, 0.65f);
            var fill = Image(fillArea, Accent, "Fill");
            fill.rectTransform.Place(0, 0, 1, 1);
            var handleArea = Rect(root, "HandleArea").Place(0, 0, 1, 1, 15, 0, 15, 0);
            var handle = Image(handleArea, TextMain, "Handle");
            handle.rectTransform.sizeDelta = new Vector2(30, 0);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        /// <summary>Vertical scroll list; returns the content transform to add rows to.</summary>
        public static RectTransform ScrollList(Transform parent, out ScrollRect scroll, float spacing = 10)
        {
            var view = Image(parent, new Color(0, 0, 0, 0.15f), "Scroll");
            view.gameObject.AddComponent<RectMask2D>();
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var content = Rect(view.transform, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = view.rectTransform;
            return content;
        }

        /// <summary>A row of fixed height for use inside a ScrollList / VerticalLayoutGroup.</summary>
        public static RectTransform Row(Transform list, float height, Color? color = null)
        {
            var img = Image(list, color ?? Panel, "Row");
            img.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return img.rectTransform;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing = 10, int pad = 0)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.childForceExpandWidth = true;
            h.childControlWidth = true;
            h.childControlHeight = true;
            return h;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static string Money(long v) => v >= 1_000_000 ? (v / 1_000_000f).ToString("0.#") + "M" : v >= 10_000 ? (v / 1000f).ToString("0.#") + "K" : v.ToString("N0");
    }
}
