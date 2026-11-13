/// <summary>
/// Model: everything a client needs to connect to a room. The lobby returns it;
/// the network transport (a later step) turns it into a WebSocket connection.
/// </summary>
public readonly struct RoomTicket
{
    public readonly string roomId;
    public readonly string host;
    public readonly ushort port;
    // WebSocket path, e.g. "/r/7003"; the proxy maps it to the room's server process.
    public readonly string path;
    public readonly bool secure;
    // Signed join token. Sent in the connection-approval payload, never logged.
    public readonly string token;

    public RoomTicket(string roomId, string host, ushort port, string path, bool secure, string token)
    {
        this.roomId = roomId;
        this.host = host;
        this.port = port;
        this.path = path;
        this.secure = secure;
        this.token = token;
    }
}
