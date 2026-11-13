using UnityEngine;

/// <summary>
/// View: turns a MazeData into wall GameObjects. Does not generate anything and stores no state.
/// </summary>
public class MazeBuilder : MonoBehaviour
{
    [Tooltip("Used when the maze is not built from a level.")]
    [SerializeField] private MazeWallStyle _defaultStyle = MazeWallStyle.Default;

    private const string _WALLS_ROOT_NAME = "Walls";
    private const string _FLOOR_ROOT_NAME = "Floor";
    private const int _FLOOR_SORTING_ORDER = -100;

    private Sprite _floorPattern;

    public MazeWallStyle DefaultStyle => _defaultStyle;

    public void Build(MazeData data) => Build(data, _defaultStyle);

    public void Build(MazeData data, MazeWallStyle style)
    {
        Clear();

        BuildFloor(data, style);

        Transform root = GetOrCreateRoot(_WALLS_ROOT_NAME);

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
        ClearRoot(_WALLS_ROOT_NAME);
        ClearRoot(_FLOOR_ROOT_NAME);
        DestroyFloorPattern();
    }

    public Vector2 CellToWorld(int mazeWidth, int mazeHeight, int x, int y, float cellSize)
        => (Vector2)transform.position + GridOrigin(mazeWidth, mazeHeight, cellSize) + new Vector2(x + 0.5f, y + 0.5f) * cellSize;

    // Bottom-left corner of the maze in local space; the maze is centered on this object.
    public Vector2 GridOrigin(int mazeWidth, int mazeHeight, float cellSize)
        => new Vector2(-mazeWidth, -mazeHeight) * (cellSize * 0.5f);

    private void ClearRoot(string rootName)
    {
        Transform root = transform.Find(rootName);
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; --i)
            DestroyObject(root.GetChild(i).gameObject);
    }

    private Transform GetOrCreateRoot(string rootName)
    {
        Transform root = transform.Find(rootName);
        if (root != null)
            return root;

        var rootObject = new GameObject(rootName);
        rootObject.transform.SetParent(transform, false);
        ExcludeFromScene(rootObject);
        return rootObject.transform;
    }

    // The floor slices are joined back into one pattern sprite and tiled over the whole maze:
    // one renderer however big the maze is.
    private void BuildFloor(MazeData data, MazeWallStyle style)
    {
        Sprite pattern = CreateFloorPattern(style.floorSprites, out int tilesPerSide);
        if (pattern == null)
            return;

        _floorPattern = pattern;

        float tile = style.floorTileSize > 0.0f ? style.floorTileSize : style.cellSize;
        float scale = tile * tilesPerSide / pattern.bounds.size.x;

        var floor = new GameObject("Floor");
        floor.transform.SetParent(GetOrCreateRoot(_FLOOR_ROOT_NAME), false);
        floor.transform.localScale = new Vector3(scale, scale, 1.0f);

        var spriteRenderer = floor.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = pattern;
        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        spriteRenderer.tileMode = SpriteTileMode.Continuous;
        spriteRenderer.size = new Vector2(data.width, data.height) * (style.cellSize / scale);
        spriteRenderer.sortingOrder = _FLOOR_SORTING_ORDER;
        ExcludeFromScene(floor);
    }

    private static Sprite CreateFloorPattern(Sprite[] slices, out int tilesPerSide)
    {
        tilesPerSide = 1;
        if (slices == null || slices.Length == 0 || slices[0] == null)
            return null;

        Rect area = slices[0].rect;
        foreach (Sprite slice in slices)
            if (slice != null)
                area = Rect.MinMaxRect(Mathf.Min(area.xMin, slice.rect.xMin), Mathf.Min(area.yMin, slice.rect.yMin),
                    Mathf.Max(area.xMax, slice.rect.xMax), Mathf.Max(area.yMax, slice.rect.yMax));

        tilesPerSide = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(slices.Length)));

        // Tiled drawing needs a full-rect sprite.
        Sprite pattern = Sprite.Create(slices[0].texture, area, new Vector2(0.5f, 0.5f), slices[0].pixelsPerUnit, 0, SpriteMeshType.FullRect);
        pattern.hideFlags = HideFlags.HideAndDontSave;
        return pattern;
    }

    private void DestroyFloorPattern()
    {
        if (_floorPattern == null)
            return;

        if (Application.isPlaying)
            Destroy(_floorPattern);
        else
            DestroyImmediate(_floorPattern);

        _floorPattern = null;
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

        // A MazeWall sizes its own collider and art; any other prefab is simply scaled.
        if (wall.TryGetComponent(out MazeWall mazeWall))
            mazeWall.Resize(size, horizontal);
        else
            wall.transform.localScale = new Vector3(size.x, size.y, 1.0f);

        ExcludeFromScene(wall);
    }

    private GameObject CreatePlainWall(Transform root, MazeWallStyle style)
    {
        var wall = new GameObject("Wall");
        wall.transform.SetParent(root, false);

        var spriteRenderer = wall.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = PlainSprites.Square;
        spriteRenderer.color = style.wallColor;

        wall.AddComponent<BoxCollider2D>();
        return wall;
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
