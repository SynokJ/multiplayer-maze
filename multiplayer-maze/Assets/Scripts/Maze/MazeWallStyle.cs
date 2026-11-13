using System;
using UnityEngine;

[Serializable]
public struct MazeWallStyle
{
    [Min(0.1f)] public float cellSize;
    [Min(0.01f)] public float wallThickness;
    [Tooltip("Optional. Scaled to the wall size. Empty = plain sprite wall with a BoxCollider2D.")]
    public GameObject wallPrefab;
    public Color wallColor;

    public MazeWallStyle(float cellSize, float wallThickness, GameObject wallPrefab, Color wallColor)
    {
        this.cellSize = cellSize;
        this.wallThickness = wallThickness;
        this.wallPrefab = wallPrefab;
        this.wallColor = wallColor;
    }

    public static MazeWallStyle Default => new MazeWallStyle(1.0f, 0.1f, null, Color.white);
}
