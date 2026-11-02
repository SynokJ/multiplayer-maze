using System;

/// <summary>
/// Offline stand-in for the network: the single local player is "connected" at once.
/// Implements both sides (client and room server) because they are the same process.
/// </summary>
public sealed class LocalMatch : IMatchClient, IPlayerSource
{
    public const ulong LocalPlayerId = 1;

    private bool _isConnected;

    public event Action Connected;
    public event Action<string> Disconnected;
    public event Action<ulong, string> PlayerConnected;
    public event Action<ulong> PlayerDisconnected;

    public void Connect(RoomTicket ticket)
    {
        if (_isConnected)
            return;

        _isConnected = true;
        Connected?.Invoke();
        PlayerConnected?.Invoke(LocalPlayerId, ticket.token);
    }

    public void Disconnect()
    {
        if (!_isConnected)
            return;

        _isConnected = false;
        PlayerDisconnected?.Invoke(LocalPlayerId);
        Disconnected?.Invoke("left");
    }
}
