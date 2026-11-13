using System.Collections.Generic;

/// <summary>
/// Model: everything that describes the current round. Plain data, no behaviour.
/// The server owns it; clients receive a copy through a network adapter (later).
/// </summary>
public sealed class GameData
{
    private readonly List<PlayerData> _players = new List<PlayerData>();

    public GameRules Rules { get; }

    public GamePhase Phase { get; internal set; } = GamePhase.Lobby;
    public FinishReason FinishReason { get; internal set; } = FinishReason.None;

    public int Round { get; internal set; }
    public int Seed { get; internal set; }

    public float TimeRemaining { get; internal set; }
    public float CountdownRemaining { get; internal set; }

    public int ItemsCollected { get; internal set; }
    public int ItemsRequired => Rules.itemsRequired;
    public bool HasEnoughItems => ItemsCollected >= Rules.itemsRequired;

    public IReadOnlyList<PlayerData> Players => _players;

    internal GameData(GameRules rules, int seed)
    {
        Rules = rules;
        Seed = seed;
        TimeRemaining = rules.timeLimitSeconds;
    }

    public PlayerData GetPlayer(ulong id)
    {
        for (int i = 0; i < _players.Count; ++i)
            if (_players[i].Id == id)
                return _players[i];

        return null;
    }

    internal void AddPlayer(PlayerData player) => _players.Add(player);
    internal bool RemovePlayer(PlayerData player) => _players.Remove(player);
}
