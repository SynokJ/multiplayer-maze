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

    private static Texture2D NewTexture(int size)
        => new Texture2D(size, size) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };

    private static Sprite NewSprite(Texture2D texture, float pixelsPerUnit)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
