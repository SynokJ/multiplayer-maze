using UnityEngine;

/// <summary>
/// Placeholder sprites made in code, so walls, players and items work before real art exists.
/// Both are 1 world unit wide, so localScale equals the size in world units.
/// </summary>
public static class PlainSprites
{
    private const int _CIRCLE_SIZE = 64;

    private static Sprite _square;
    private static Sprite _circle;
    private static Sprite _rounded;

    public static Sprite Square
    {
        get
        {
            if (_square == null)
            {
                var texture = NewTexture(1);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                _square = NewSprite(texture, 1.0f);
            }

            return _square;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (_circle == null)
            {
                var texture = NewTexture(_CIRCLE_SIZE);
                float radius = _CIRCLE_SIZE * 0.5f;

                for (int y = 0; y < _CIRCLE_SIZE; ++y)
                    for (int x = 0; x < _CIRCLE_SIZE; ++x)
                    {
                        float dx = x + 0.5f - radius;
                        float dy = y + 0.5f - radius;
                        bool inside = dx * dx + dy * dy <= radius * radius;
                        texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                    }

                texture.Apply();
                _circle = NewSprite(texture, _CIRCLE_SIZE);
            }

            return _circle;
        }
    }

    // 9-slice friendly rounded rectangle for UI panels and buttons (use Image.Type.Sliced).
    public static Sprite Rounded
    {
        get
        {
            if (_rounded == null)
            {
                const int size = 64;
                const int radius = 20;

                var texture = NewTexture(size);
                for (int y = 0; y < size; ++y)
                    for (int x = 0; x < size; ++x)
                    {
                        // Distance from the nearest corner circle centre, 0 along the straight edges.
                        float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                        float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                        float dx = x + 0.5f - cx;
                        float dy = y + 0.5f - cy;
                        float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        texture.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, alpha));
                    }

                texture.Apply();

                _rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100.0f, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                _rounded.hideFlags = HideFlags.HideAndDontSave;
            }

            return _rounded;
        }
    }

    private static Texture2D NewTexture(int size)
        => new Texture2D(size, size) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };

    private static Sprite NewSprite(Texture2D texture, float pixelsPerUnit)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
