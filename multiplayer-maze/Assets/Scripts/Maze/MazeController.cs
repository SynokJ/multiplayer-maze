using UnityEngine;

/// <summary>
/// Wires settings, generation and building together.
/// Host: Generate(settings) and send the settings to clients. Client: Generate(receivedSettings).
/// </summary>
[RequireComponent(typeof(MazeBuilder))]
public class MazeController : MonoBehaviour
{
    [SerializeField] private MazeSettings _settings = MazeSettings.Default;

    private MazeBuilder _builder;

    public MazeSettings Settings => _settings;
    public MazeData Data { get; private set; }

    public void Generate() => Generate(_settings);

    public void Generate(MazeSettings settings)
    {
        _settings = settings;
        Data = settings.CreateData();

        GetBuilder().Build(Data);
    }

    public void Clear()
    {
        Data = null;
        GetBuilder().Clear();
    }

    public void SetSeed(int seed) => _settings.seed = seed;

    private MazeBuilder GetBuilder()
    {
        if (_builder == null)
            _builder = GetComponent<MazeBuilder>();

        return _builder;
    }
}
