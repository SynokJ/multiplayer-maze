/// <summary>
/// Model: a room as shown in a room list.
/// </summary>
public sealed class RoomInfo
{
    public string Id { get; }
    public string Name { get; }
    public int Players { get; }
    public int MaxPlayers { get; }
    public string State { get; }

    public RoomInfo(string id, string name, int players, int maxPlayers, string state)
    {
        Id = id;
        Name = name;
        Players = players;
        MaxPlayers = maxPlayers;
        State = state;
    }
}
