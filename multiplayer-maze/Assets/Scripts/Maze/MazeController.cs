using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Controller: wires settings, generation and building together.
/// Every peer (server and clients) calls Generate(level, seed) with the same replicated values.
/// The serialized fields are inspector defaults only; Generate never overwrites them.
/// </summary>
[RequireComponent(typeof(MazeBuilder))]
public class MazeController : MonoBehaviour
{
    [Tooltip("Optional. When set, size and wall style come from the level; only the seed below is used.")]
    [SerializeField] private LevelConfigSO _level;
    [FormerlySerializedAs("_settings")]
    [SerializeField] private MazeSettings _defaultSettings = MazeSettings.Default;

    private MazeBuilder _builder;

    // Raised after the walls are built, so other Views (minimap, spawners) can react.
    public event Action<MazeData> MazeGenerated;

    public MazeData Data { get; private set; }
    public MazeSettings CurrentSettings { get; private set; }
    public MazeWallStyle CurrentStyle { get; private set; }

    // What the inspector values would generate right now (used by the preview gizmo).
    public MazeSettings PreviewSettings
        => (_level != null ? _level.CreateSettings(_defaultSettings.seed) : _defaultSettings).Clamped();

    public MazeWallStyle PreviewWallStyle => _level != null ? _level.WallStyle : GetBuilder().DefaultStyle;

    // Used by the Editor buttons; at runtime GameNetwork builds the maze from the replicated seed.
    public void Generate()
    {
        if (_level != null)
            Generate(_level, _defaultSettings.seed);
        else
            Generate(_defaultSettings, GetBuilder().DefaultStyle);
    }

    public void Generate(LevelConfigSO level, int seed)
        => Generate(level.CreateSettings(seed), level.WallStyle);

    public void Generate(MazeSettings settings, MazeWallStyle style)
    {
        CurrentSettings = settings.Clamped();
        CurrentStyle = style;
        Data = CurrentSettings.CreateData();

        GetBuilder().Build(Data, style);
        MazeGenerated?.Invoke(Data);
    }

    // World position of a cell's center. Only valid while a maze is built.
    public Vector2 CellToWorld(int x, int y)
        => GetBuilder().CellToWorld(CurrentSettings.width, CurrentSettings.height, x, y, CurrentStyle.cellSize);

    public void Clear()
    {
        Data = null;
        GetBuilder().Clear();
    }

    // Editor helper: changes the inspector default seed.
    public void SetDefaultSeed(int seed) => _defaultSettings.seed = seed;

    private MazeBuilder GetBuilder()
    {
        if (_builder == null)
            _builder = GetComponent<MazeBuilder>();

        return _builder;
    }
}
