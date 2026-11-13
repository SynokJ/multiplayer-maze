using System;

/// <summary>
/// The player's side of a connection: turns a ticket into a live connection.
/// The NGO WebSocket client implements it later; LocalMatch implements it offline.
/// </summary>
public interface IMatchClient
{
    event Action Connected;
    // Argument is a short reason, e.g. "bad token" or "room full".
    event Action<string> Disconnected;

    void Connect(RoomTicket ticket);
    void Disconnect();
}
