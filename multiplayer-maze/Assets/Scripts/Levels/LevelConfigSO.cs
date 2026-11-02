using UnityEngine;

/// <summary>
/// Model: read-only level definition. Never write runtime state into it: the asset is shared
/// and its changes persist in the Editor. Build plain C# state from it instead.
/// </summary>
[CreateAssetMenu(fileName = "Level_01", menuName = "Maze/Level Config")]
public class LevelConfigSO : ScriptableObject
{
    [Header("Maze:")]
    [SerializeField, Range(MazeSettings.MIN_SIZE, MazeSettings.MAX_SIZE)] private int _width = 11;
    [SerializeField, Range(MazeSettings.MIN_SIZE, MazeSettings.MAX_SIZE)] private int _height = 11;
    [Tooltip("On: the level is always the same maze. Off: the host picks a random seed.")]
    [SerializeField] private bool _useFixedSeed;
    [SerializeField] private int _fixedSeed = 12345;

    [Header("Walls:")]
    [SerializeField] private MazeWallStyle _wallStyle = MazeWallStyle.Default;

    public int Width => _width;
    public int Height => _height;
    public bool UseFixedSeed => _useFixedSeed;
    public int FixedSeed => _fixedSeed;
    public MazeWallStyle WallStyle => _wallStyle;

    public MazeSettings CreateSettings(int randomSeed)
        => new MazeSettings(_width, _height, _useFixedSeed ? _fixedSeed : randomSeed);
}
