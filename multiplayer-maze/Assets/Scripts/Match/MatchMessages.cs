/// <summary>
/// Pure: turns the short error and disconnect codes into text a player can read.
/// </summary>
public static class MatchMessages
{
    public static string Describe(string code)
    {
        switch (code)
        {
            case MatchErrors.NoSuchRoom: return "That room does not exist.";
            case MatchErrors.RoomFull: return "The room is full.";
            case MatchErrors.RoomFailedToStart: return "The room could not start. Try again.";
            case MatchErrors.NoCapacity: return "All servers are busy. Try again in a minute.";
            case MatchErrors.InvalidRequest: return "Check the name or the room code.";
            case MatchErrors.RateLimited: return "Too many requests. Wait a moment.";
            case MatchErrors.Network: return "Cannot reach the server.";
            case "connection_lost": return "The connection was lost.";
            case "version_mismatch": return "The game was updated. Reload the page.";
            case "token_expired": return "The invite expired. Try again.";
            case "bad_token":
            case "bad_request": return "The server refused the connection.";
            case "room_closed": return "The round has already started.";
            case "start_failed": return "Could not start the connection.";
            case "busy": return "Still disconnecting. Try again.";
            case "left": return string.Empty;
            default: return string.IsNullOrEmpty(code) ? string.Empty : code;
        }
    }
}
