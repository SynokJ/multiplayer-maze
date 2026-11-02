using UnityEngine;

/// <summary>
/// Turns a MazeData into wall GameObjects. Does not generate anything itself.
/// </summary>
public class MazeBuilder : MonoBehaviour
{
    [Header("Walls:")]
    [SerializeField, Min(0.1f)] private float _cellSize = 1.0f;
    [SerializeField, Min(0.01f)] private float _wallThickness = 0.1f;
    [Tooltip("Optional. Scaled to the wall size. Empty = plain sprite wall with a BoxCollider2D.")]
    [SerializeField] private GameObject _wallPrefab;
    [SerializeField] private Color _wallColor = Color.white;

    private const string _WALLS_ROOT_NAME = "Walls";

    private static Sprite _whiteSprite;

    public float CellSize => _cellSize;

    public void Build(MazeData data)
    {
        Clear();

        Transform root = GetOrCreateWallsRoot();

        // Neighbouring cells share walls, so each wall is spawned once:
        // every cell owns its north and east wall, border cells also own south and west.
        for (int y = 0; y < data.height; ++y)
            for (int x = 0; x < data.width; ++x)
            {
                if (data.HasWall(x, y, MazeData.Wall.North))
                    SpawnWall(root, data, x, y + 1, horizontal: true);

                if (data.HasWall(x, y, MazeData.Wall.East))
                    SpawnWall(root, data, x + 1, y, horizontal: false);

                if (y == 0 && data.HasWall(x, y, MazeData.Wall.South))
                    SpawnWall(root, data, x, y, horizontal: true);

                if (x == 0 && data.HasWall(x, y, MazeData.Wall.West))
                    SpawnWall(root, data, x, y, horizontal: false);
            }
    }

    public void Clear()
    {
        Transform root = transform.Find(_WALLS_ROOT_NAME);
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; --i)
            DestroyObject(root.GetChild(i).gameObject);
    }

    public Vector2 CellToWorld(MazeData data, int x, int y)
        => CellToWorld(data.width, data.height, x, y);

    public Vector2 CellToWorld(int mazeWidth, int mazeHeight, int x, int y)
        => (Vector2)transform.position + GridOrigin(mazeWidth, mazeHeight) + new Vector2(x + 0.5f, y + 0.5f) * _cellSize;

    // Bottom-left corner of the maze in local space; the maze is centered on this object.
    public Vector2 GridOrigin(int mazeWidth, int mazeHeight)
        => new Vector2(-mazeWidth, -mazeHeight) * (_cellSize * 0.5f);

    private Transform GetOrCreateWallsRoot()
    {
        Transform root = transform.Find(_WALLS_ROOT_NAME);
        if (root != null)
            return root;

        var rootObject = new GameObject(_WALLS_ROOT_NAME);
        rootObject.transform.SetParent(transform, false);
        return rootObject.transform;
    }

    // (gridX, gridY) is the wall's start corner; a wall runs along +x (horizontal) or +y (vertical).
    private void SpawnWall(Transform root, MazeData data, int gridX, int gridY, bool horizontal)
    {
        Vector2 local = GridOrigin(data.width, data.height) + new Vector2(gridX, gridY) * _cellSize;
        local += horizontal ? new Vector2(_cellSize * 0.5f, 0.0f) : new Vector2(0.0f, _cellSize * 0.5f);

        Vector2 size = horizontal
            ? new Vector2(_cellSize + _wallThickness, _wallThickness)
            : new Vector2(_wallThickness, _cellSize + _wallThickness);

        GameObject wall = _wallPrefab != null ? Instantiate(_wallPrefab, root) : CreatePlainWall(root);

        wall.transform.localPosition = local;
        wall.transform.localScale = new Vector3(size.x, size.y, 1.0f);
    }

    private GameObject CreatePlainWall(Transform root)
    {
        var wall = new GameObject("Wall");
        wall.transform.SetParent(root, false);

        var spriteRenderer = wall.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetWhiteSprite();
        spriteRenderer.color = _wallColor;

        wall.AddComponent<BoxCollider2D>();
        return wall;
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;

        var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        // 1 pixel per unit: localScale then equals the wall size in world units.
        _whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1.0f);
        _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return _whiteSprite;
    }

    private static void DestroyObject(GameObject target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
