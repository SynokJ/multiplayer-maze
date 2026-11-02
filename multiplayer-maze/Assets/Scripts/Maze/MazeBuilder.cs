using UnityEngine;

/// <summary>
/// Turns a MazeData into wall GameObjects. Does not generate anything itself.
/// </summary>
public class MazeBuilder : MonoBehaviour
{
    [SerializeField] private MazeWallStyle _style = MazeWallStyle.Default;

    private const string _WALLS_ROOT_NAME = "Walls";

    private static Sprite _whiteSprite;

    public MazeWallStyle Style => _style;

    public void SetStyle(MazeWallStyle style) => _style = style;

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
        => CellToWorld(data.width, data.height, x, y, _style.cellSize);

    public Vector2 CellToWorld(int mazeWidth, int mazeHeight, int x, int y, float cellSize)
        => (Vector2)transform.position + GridOrigin(mazeWidth, mazeHeight, cellSize) + new Vector2(x + 0.5f, y + 0.5f) * cellSize;

    // Bottom-left corner of the maze in local space; the maze is centered on this object.
    public Vector2 GridOrigin(int mazeWidth, int mazeHeight, float cellSize)
        => new Vector2(-mazeWidth, -mazeHeight) * (cellSize * 0.5f);

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
        float cell = _style.cellSize;
        float thickness = _style.wallThickness;

        Vector2 local = GridOrigin(data.width, data.height, cell) + new Vector2(gridX, gridY) * cell;
        local += horizontal ? new Vector2(cell * 0.5f, 0.0f) : new Vector2(0.0f, cell * 0.5f);

        Vector2 size = horizontal
            ? new Vector2(cell + thickness, thickness)
            : new Vector2(thickness, cell + thickness);

        GameObject wall = _style.wallPrefab != null ? Instantiate(_style.wallPrefab, root) : CreatePlainWall(root);

        wall.transform.localPosition = local;
        wall.transform.localScale = new Vector3(size.x, size.y, 1.0f);
    }

    private GameObject CreatePlainWall(Transform root)
    {
        var wall = new GameObject("Wall");
        wall.transform.SetParent(root, false);

        var spriteRenderer = wall.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetWhiteSprite();
        spriteRenderer.color = _style.wallColor;

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
