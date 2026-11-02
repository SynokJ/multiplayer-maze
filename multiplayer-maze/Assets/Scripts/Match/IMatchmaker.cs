using System.Threading.Tasks;

/// <summary>
/// Finds or creates a room and hands back a ticket. Implementations: LobbyHttpMatchmaker
/// (your Node lobby) and OfflineMatchmaker (single player and tests).
/// </summary>
public interface IMatchmaker
{
    Task<MatchResult> CreateRoomAsync(string playerName, string roomName, int maxPlayers);
    Task<MatchResult> JoinRoomAsync(string roomId, string playerName);
    Task<RoomListResult> ListRoomsAsync();
}
