using UnityEngine;

/// <summary>
/// Wires settings, generation and building together.
/// Host: pick a level and a seed, Generate(level, seed), send both to clients.
/// Client: Generate(level, receivedSeed).
/// </summary>
[RequireComponent(typeof(MazeBuilder))]
public class MazeController : MonoBehaviour
{
    [Tooltip("Optional. When set, size and wall style come from the level; only the seed below is used.")]
    [SerializeField] private LevelConfigSO _level;
    [SerializeField] private MazeSettings _settings = MazeSettings.Default;

    private MazeBuilder _builder;

    public MazeData Data { get; private set; }

    // What Generate() will use right now.
    public MazeSettings Settings => _level != null ? _level.CreateSettings(_settings.seed) : _settings;
    public MazeWallStyle WallStyle => _level != null ? _level.wallStyle : GetBuilder().Style;

    public void Generate()
    {
        if (_level != null)
            Generate(_level, _settings.seed);
        else
            Generate(_settings);
    }

    public void Generate(LevelConfigSO level, int seed)
    {
        _level = level;
        _settings = level.CreateSettings(seed);

        GetBuilder().SetStyle(level.wallStyle);
        BuildFrom(_settings);
    }

    public void Generate(MazeSettings settings)
    {
        _level = null;
        _settings = settings;

        BuildFrom(settings);
    }

    public void Clear()
    {
        Data = null;
        GetBuilder().Clear();
    }

    public void SetSeed(int seed) => _settings.seed = seed;

    private void BuildFrom(MazeSettings settings)
    {
        Data = settings.CreateData();
        GetBuilder().Build(Data);
    }

    private MazeBuilder GetBuilder()
    {
        if (_builder == null)
            _builder = GetComponent<MazeBuilder>();

        return _builder;
    }
}
