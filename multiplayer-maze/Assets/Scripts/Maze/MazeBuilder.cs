using UnityEngine;

/// <summary>
/// View: turns a MazeData into wall GameObjects. Does not generate anything and stores no state.
/// </summary>
public class MazeBuilder : MonoBehaviour
{
    [Tooltip("Used when the maze is not built from a level.")]
    [SerializeField] private MazeWallStyle _defaultStyle = MazeWallStyle.Default;

    private const string _WALLS_ROOT_NAME = "Walls";

    private static Sprite _whiteSprite;

    public MazeWallStyle DefaultStyle => _defaultStyle;

    public void Build(MazeData data) => Build(data, _defaultStyle);

    public void Build(MazeData data, MazeWallStyle style)
    {
        Clear();

        Transform root = GetOrCreateWallsRoot();

        // Neighbouring cells share walls, so each wall is spawned once:
        // every cell owns its north and east wall, border cells also own south and west.
        for (int y = 0; y < data.height; ++y)
            for (int x = 0; x < data.width; ++x)
            {
                if (data.HasWall(x, y, MazeData.Wall.North))
                    SpawnWall(root, data, style, x, y + 1, horizontal: true);

                if (data.HasWall(x, y, MazeData.Wall.East))
                    SpawnWall(root, data, style, x + 1, y, horizontal: false);

                if (y == 0 && data.HasWall(x, y, MazeData.Wall.South))
                    SpawnWall(root, data, style, x, y, horizontal: true);

                if (x == 0 && data.HasWall(x, y, MazeData.Wall.West))
                    SpawnWall(root, data, style, x, y, horizontal: false);
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
        ExcludeFromScene(rootObject);
        return rootObject.transform;
    }

    // (gridX, gridY) is the wall's start corner; a wall runs along +x (horizontal) or +y (vertical).
    private void SpawnWall(Transform root, MazeData data, MazeWallStyle style, int gridX, int gridY, bool horizontal)
    {
        float cell = style.cellSize;
        float thickness = style.wallThickness;

        Vector2 local = GridOrigin(data.width, data.height, cell) + new Vector2(gridX, gridY) * cell;
        local += horizontal ? new Vector2(cell * 0.5f, 0.0f) : new Vector2(0.0f, cell * 0.5f);

        Vector2 size = horizontal
            ? new Vector2(cell + thickness, thickness)
            : new Vector2(thickness, cell + thickness);

        GameObject wall = style.wallPrefab != null ? Instantiate(style.wallPrefab, root) : CreatePlainWall(root, style);

        wall.transform.localPosition = local;
        wall.transform.localScale = new Vector3(size.x, size.y, 1.0f);
        ExcludeFromScene(wall);
    }

    private GameObject CreatePlainWall(Transform root, MazeWallStyle style)
    {
        var wall = new GameObject("Wall");
        wall.transform.SetParent(root, false);

        var spriteRenderer = wall.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetWhiteSprite();
        spriteRenderer.color = style.wallColor;

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

    // Walls are rebuilt from the seed, so walls made in Edit mode must not be saved into the scene.
    private static void ExcludeFromScene(GameObject target)
    {
        if (!Application.isPlaying)
            target.hideFlags |= HideFlags.DontSaveInEditor;
    }

    private static void DestroyObject(GameObject target)
    {
        if (Application.isPlaying)
        {
            // Destroy waits until the end of the frame; turn the wall off first so
            // the old and new colliders never overlap while rebuilding.
            target.SetActive(false);
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
