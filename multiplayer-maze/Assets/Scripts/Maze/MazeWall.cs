using UnityEngine;

/// <summary>
/// One wall with art: a thin collider and a front-facing picture drawn over it.
/// MazeBuilder calls Resize with the wall's size; the picture keeps its proportions.
/// </summary>
public class MazeWall : MonoBehaviour
{
    [SerializeField] private BoxCollider2D _collider;
    [SerializeField] private SpriteRenderer _art;
    [SerializeField] private Sprite _horizontalSprite;
    [SerializeField] private Sprite _verticalSprite;
    [Tooltip("Vertical art is fitted to the wall length, then stretched by this (x = width, y = length). Lower x = tighter.")]
    [SerializeField] private Vector2 _verticalStretch = new Vector2(0.5f, 1.5f);

    public void Resize(Vector2 size, bool horizontal)
    {
        _collider.size = size;

        Sprite sprite = horizontal ? _horizontalSprite : _verticalSprite;
        _art.sprite = sprite;
        if (sprite == null)
            return;

        // Fit the picture to the wall's length; its other side follows from its aspect ratio.
        Vector2 native = sprite.bounds.size;
        if (horizontal)
        {
            float scale = size.x / native.x;
            _art.transform.localScale = new Vector3(scale, scale, 1.0f);
        }
        else
        {
            float scale = size.y / native.y;
            _art.transform.localScale = new Vector3(scale * _verticalStretch.x, scale * _verticalStretch.y, 1.0f);
        }
    }
}
