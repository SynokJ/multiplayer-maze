/// <summary>
/// Model: one player's state in a round. Only GameSession changes it.
/// </summary>
public sealed class PlayerData
{
    public ulong Id { get; }
    public string Name { get; }

    public int Lives { get; internal set; }
    public int ItemsCollected { get; internal set; }
    public bool IsReady { get; internal set; }

    public bool IsAlive => Lives > 0;

    internal PlayerData(ulong id, string name, int lives)
    {
        Id = id;
        Name = name;
        Lives = lives;
    }
}
