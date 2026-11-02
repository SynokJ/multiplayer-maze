using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Controller: wires settings, generation and building together.
/// Host: pick a level and a seed, Generate(level, seed), send both to clients.
/// Client: Generate(level, receivedSeed).
/// The serialized fields are inspector defaults only; Generate never overwrites them.
/// </summary>
[RequireComponent(typeof(MazeBuilder))]
public class MazeController : MonoBehaviour
{
    [Tooltip("Optional. When set, size and wall style come from the level; only the seed below is used.")]
    [SerializeField] private LevelConfigSO _level;
    [FormerlySerializedAs("_settings")]
    [SerializeField] private MazeSettings _defaultSettings = MazeSettings.Default;
    [Tooltip("Single-player testing. With networking, the host calls Generate instead.")]
    [SerializeField] private bool _generateOnStart = true;

    private MazeBuilder _builder;

    // Raised after the walls are built, so other Views (minimap, spawners) can react.
    public event Action<MazeData> MazeGenerated;

    public MazeData Data { get; private set; }
    public MazeSettings CurrentSettings { get; private set; }

    // What the inspector values would generate right now (used by the preview gizmo).
    public MazeSettings PreviewSettings
        => (_level != null ? _level.CreateSettings(_defaultSettings.seed) : _defaultSettings).Clamped();

    public MazeWallStyle PreviewWallStyle => _level != null ? _level.WallStyle : GetBuilder().DefaultStyle;

    private void Start()
    {
        if (_generateOnStart)
            Generate();
    }

    public void Generate()
    {
        if (_level != null)
            Generate(_level, _defaultSettings.seed);
        else
            Generate(_defaultSettings, GetBuilder().DefaultStyle);
    }

    public void Generate(LevelConfigSO level, int seed)
        => Generate(level.CreateSettings(seed), level.WallStyle);

    public void Generate(MazeSettings settings) => Generate(settings, GetBuilder().DefaultStyle);

    public void Generate(MazeSettings settings, MazeWallStyle style)
    {
        CurrentSettings = settings.Clamped();
        Data = CurrentSettings.CreateData();

        GetBuilder().Build(Data, style);
        MazeGenerated?.Invoke(Data);
    }

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
