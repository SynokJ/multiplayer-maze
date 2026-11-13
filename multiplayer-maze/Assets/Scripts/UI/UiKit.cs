using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Small helpers that build uGUI objects in code, so the menu needs no prefabs or imported fonts.
/// Sizes are in canvas units (the canvas scales to the screen).
/// </summary>
public static class UiKit
{
    public static readonly Color Dim = new Color(0.05f, 0.06f, 0.10f, 0.85f);
    public static readonly Color Card = new Color(0.13f, 0.15f, 0.22f, 0.97f);
    public static readonly Color Field = new Color(0.07f, 0.08f, 0.13f, 1.0f);
    public static readonly Color Primary = new Color(0.22f, 0.52f, 0.95f, 1.0f);
    public static readonly Color Secondary = new Color(0.27f, 0.30f, 0.42f, 1.0f);
    public static readonly Color Danger = new Color(0.80f, 0.30f, 0.30f, 1.0f);
    public static readonly Color Good = new Color(0.30f, 0.78f, 0.50f, 1.0f);
    public static readonly Color TextMain = Color.white;
    public static readonly Color TextMuted = new Color(0.66f, 0.71f, 0.82f, 1.0f);

    public const int BODY = 38;
    public const int BUTTON_HEIGHT = 112;

    private static Font _font;

    // 9-slice sprite for panels, buttons and fields. The menu sets it once; until then
    // (or when none is assigned) a generated rounded sprite is used.
    public static Sprite PanelSprite { get; set; }

    private static Font Font
    {
        get
        {
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
    }

    public static RectTransform Rect(Transform parent, string name)
    {
        var item = new GameObject(name, typeof(RectTransform));
        item.transform.SetParent(parent, false);
        return (RectTransform)item.transform;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static RectTransform Panel(Transform parent, string name, Color color)
    {
        RectTransform rect = Rect(parent, name);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = PanelSprite != null ? PanelSprite : PlainSprites.Rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        return rect;
    }

    // A picture that keeps its proportions and does not catch taps.
    public static Image Icon(Transform parent, float width, float height)
    {
        RectTransform rect = Rect(parent, "Icon");
        var image = rect.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        Size(rect, width, height);
        return image;
    }

    public static VerticalLayoutGroup Column(RectTransform rect, float spacing, int padding)
    {
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static RectTransform Row(Transform parent, string name, float height, float spacing = 16.0f)
    {
        RectTransform rect = Rect(parent, name);
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleCenter;

        Size(rect, height: height);
        return rect;
    }

    public static LayoutElement Size(Component target, float width = -1.0f, float height = -1.0f, float flexWidth = -1.0f, float flexHeight = -1.0f)
    {
        var element = target.GetComponent<LayoutElement>();
        if (element == null)
            element = target.gameObject.AddComponent<LayoutElement>();

        if (width >= 0.0f) element.preferredWidth = width;
        if (height >= 0.0f) element.preferredHeight = height;
        if (flexWidth >= 0.0f) element.flexibleWidth = flexWidth;
        if (flexHeight >= 0.0f) element.flexibleHeight = flexHeight;
        return element;
    }

    public static Text Label(Transform parent, string text, int size = BODY, Color? color = null, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
    {
        RectTransform rect = Rect(parent, "Label");
        var label = rect.gameObject.AddComponent<Text>();
        label.font = Font;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = anchor;
        label.color = color ?? TextMain;
        label.text = text;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    public static Button Button(Transform parent, string text, Color color, float height, UnityAction onClick, int fontSize = BODY)
    {
        RectTransform rect = Panel(parent, "Button", color);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();

        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1.0f);
        button.colors = colors;

        Text label = Label(rect, text, fontSize, TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(label.rectTransform);

        Size(rect, height: height);
        button.onClick.AddListener(onClick);
        return button;
    }

    public static void SetButtonText(Button button, string text)
        => button.GetComponentInChildren<Text>().text = text;

    public static InputField Input(Transform parent, string placeholder, int characterLimit, float height = 96.0f)
    {
        RectTransform rect = Panel(parent, "Input", Field);
        var field = rect.gameObject.AddComponent<InputField>();
        field.targetGraphic = rect.GetComponent<Image>();

        Text text = Label(rect, string.Empty, BODY, TextMain);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(24.0f, 6.0f);
        text.rectTransform.offsetMax = new Vector2(-24.0f, -6.0f);
        text.supportRichText = false;

        Text hint = Label(rect, placeholder, BODY, TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
        Stretch(hint.rectTransform);
        hint.rectTransform.offsetMin = new Vector2(24.0f, 6.0f);
        hint.rectTransform.offsetMax = new Vector2(-24.0f, -6.0f);

        field.textComponent = text;
        field.placeholder = hint;
        field.characterLimit = characterLimit;
        field.lineType = InputField.LineType.SingleLine;

        Size(rect, height: height);
        return field;
    }

    public static ScrollRect Scroll(Transform parent, out RectTransform content)
    {
        RectTransform root = Panel(parent, "Scroll", Field);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40.0f;

        RectTransform viewport = Rect(root, "Viewport");
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = Rect(viewport, "Content");
        content.anchorMin = new Vector2(0.0f, 1.0f);
        content.anchorMax = new Vector2(1.0f, 1.0f);
        content.pivot = new Vector2(0.5f, 1.0f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        Column(content, 10.0f, 16);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        return scroll;
    }
}
