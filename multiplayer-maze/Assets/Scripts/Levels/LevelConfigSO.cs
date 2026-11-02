using UnityEngine;

/// <summary>
/// Read-only level definition. Never write runtime state into it: the asset is shared
/// and its changes persist in the Editor. Build plain C# state from it instead.
/// </summary>
[CreateAssetMenu(fileName = "Level_01", menuName = "Maze/Level Config")]
public class LevelConfigSO : ScriptableObject
{
    [Header("Maze:")]
    [Min(2)] public int width = 11;
    [Min(2)] public int height = 11;
    [Tooltip("On: the level is always the same maze. Off: the host picks a random seed.")]
    public bool useFixedSeed;
    public int fixedSeed = 12345;

    [Header("Walls:")]
    public MazeWallStyle wallStyle = MazeWallStyle.Default;

    [Header("Gameplay:")]
    [Min(0)] public int enemyCount = 3;
    [Min(0)] public int itemCount = 5;
    [Min(0.0f)] public float timeLimitSeconds = 120.0f;

    public MazeSettings CreateSettings(int randomSeed)
        => new MazeSettings(width, height, useFixedSeed ? fixedSeed : randomSeed);
}
