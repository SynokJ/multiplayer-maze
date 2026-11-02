using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// In-memory matchmaker for single player, the Editor and tests. No network.
/// The ticket's token is the player name, which LocalMatch reads back.
/// </summary>
public sealed class OfflineMatchmaker : IMatchmaker
{
    private readonly Dictionary<string, RoomInfo> _rooms = new Dictionary<string, RoomInfo>();
    private int _nextRoom = 1;

    public Task<MatchResult> CreateRoomAsync(string playerName, string roomName, int maxPlayers)
    {
        if (!MatchValidation.IsValidName(playerName))
            return Task.FromResult(MatchResult.Failure(MatchErrors.InvalidRequest));

        string id = (_nextRoom++).ToString("D5");
        string name = string.IsNullOrEmpty(roomName) ? playerName + " room" : roomName;
        var room = new RoomInfo(id, name, 1, maxPlayers > 0 ? maxPlayers : 8, "ready");

        _rooms[id] = room;
        return Task.FromResult(MatchResult.Success(room, MakeTicket(id, playerName)));
    }

    public Task<MatchResult> JoinRoomAsync(string roomId, string playerName)
    {
        string id = MatchValidation.NormalizeRoomId(roomId);
        if (id == null || !MatchValidation.IsValidName(playerName))
            return Task.FromResult(MatchResult.Failure(MatchErrors.InvalidRequest));

        if (!_rooms.TryGetValue(id, out RoomInfo room))
            return Task.FromResult(MatchResult.Failure(MatchErrors.NoSuchRoom));

        if (room.Players >= room.MaxPlayers)
            return Task.FromResult(MatchResult.Failure(MatchErrors.RoomFull));

        var joined = new RoomInfo(room.Id, room.Name, room.Players + 1, room.MaxPlayers, room.State);
        _rooms[id] = joined;
        return Task.FromResult(MatchResult.Success(joined, MakeTicket(id, playerName)));
    }

    public Task<RoomListResult> ListRoomsAsync()
        => Task.FromResult(RoomListResult.Success(new List<RoomInfo>(_rooms.Values)));

    private static RoomTicket MakeTicket(string roomId, string playerName)
        => new RoomTicket(roomId, "localhost", 0, "/", false, playerName);
}
