using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// View: an on-screen stick that appears where the finger (or mouse) goes down and reports how
/// far it was dragged. Touch and mouse both arrive as Pointer.current, so one path covers phones,
/// tablets and desktop browsers. It builds its own UI, so it needs no prefab or scene wiring.
/// </summary>
public class FloatingJoystick : MonoBehaviour
{
    private const float _DEAD_ZONE = 0.15f;
    private const float _MIN_RADIUS_PIXELS = 60.0f;
    private const float _RADIUS_SCREEN_FRACTION = 0.12f;

    [SerializeField] private Color _color = new Color(1.0f, 1.0f, 1.0f, 0.35f);

    private RectTransform _outer;
    private RectTransform _knob;
    private Vector2 _origin;
    private float _radius;
    private bool _held;
    private bool _listening;

    // Drag from the touch-down point, length 0..1 (the dead zone is already cut away).
    public Vector2 Value { get; private set; }

    private void Awake()
    {
        var canvasObject = new GameObject("JoystickCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        _outer = CreateCircle(canvasObject.transform, "Outer", _color);
        _knob = CreateCircle(canvasObject.transform, "Knob", new Color(_color.r, _color.g, _color.b, _color.a * 1.6f));
        SetVisible(false);
    }

    // The local player's avatar turns it on while it exists, so it never shows in menus.
    public void SetListening(bool listening)
    {
        _listening = listening;
        if (!listening)
            Release();
    }

    private void OnDisable() => Release();

    private void Update()
    {
        if (!_listening)
            return;

        Pointer pointer = Pointer.current;
        if (pointer == null)
            return;

        Vector2 position = pointer.position.ReadValue();

        // A tap on a button must not start the stick.
        if (pointer.press.wasPressedThisFrame)
        {
            if (!IsOverUi())
                Press(position);
        }
        else if (_held && !pointer.press.isPressed)
            Release();

        if (_held)
            Drag(position);
    }

    private static bool IsOverUi()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private void Press(Vector2 position)
    {
        _held = true;
        _origin = position;
        _radius = Mathf.Max(_MIN_RADIUS_PIXELS, _RADIUS_SCREEN_FRACTION * Mathf.Min(Screen.width, Screen.height));

        _outer.sizeDelta = Vector2.one * (_radius * 2.0f);
        _knob.sizeDelta = Vector2.one * _radius;
        _outer.anchoredPosition = _origin;
        SetVisible(true);
    }

    private void Drag(Vector2 position)
    {
        Vector2 drag = Vector2.ClampMagnitude((position - _origin) / _radius, 1.0f);

        _knob.anchoredPosition = _origin + drag * _radius;
        Value = drag.magnitude < _DEAD_ZONE ? Vector2.zero : drag;
    }

    private void Release()
    {
        _held = false;
        Value = Vector2.zero;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (_outer != null) _outer.gameObject.SetActive(visible);
        if (_knob != null) _knob.gameObject.SetActive(visible);
    }

    private static RectTransform CreateCircle(Transform parent, string name, Color color)
    {
        var item = new GameObject(name);
        item.transform.SetParent(parent, false);

        var image = item.AddComponent<Image>();
        image.sprite = PlainSprites.Circle;
        image.color = color;
        image.raycastTarget = false;

        // Bottom-left anchor, so anchoredPosition is the screen position in pixels.
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }
}
