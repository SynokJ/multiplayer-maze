using System;
using UnityEngine;

[Serializable]
public struct MazeWallStyle
{
    [Min(0.1f)] public float cellSize;
    [Min(0.01f)] public float wallThickness;
    [Tooltip("Optional. A prefab with MazeWall is resized by it; any other prefab is scaled to the wall size. Empty = plain sprite wall with a BoxCollider2D.")]
    public GameObject wallPrefab;
    public Color wallColor;
    [Tooltip("Optional floor art: the four slices of one 2x2 floor texture. They are tiled over the whole maze. Empty = no floor.")]
    public Sprite[] floorSprites;
    [Tooltip("World size of one floor tile (a 2x2 pattern is twice this). 0 = one tile per cell.")]
    [Min(0.0f)] public float floorTileSize;

    public MazeWallStyle(float cellSize, float wallThickness, GameObject wallPrefab, Color wallColor, Sprite[] floorSprites = null, float floorTileSize = 0.0f)
    {
        this.cellSize = cellSize;
        this.wallThickness = wallThickness;
        this.wallPrefab = wallPrefab;
        this.wallColor = wallColor;
        this.floorSprites = floorSprites;
        this.floorTileSize = floorTileSize;
    }

    public static MazeWallStyle Default => new MazeWallStyle(1.0f, 0.1f, null, Color.white);
}
