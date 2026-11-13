using System;

/// <summary>
/// Connects the room's connection events to the game: a connected player joins the
/// GameSession, a disconnected one leaves it. Runs on the server (authority) side.
/// </summary>
public sealed class SessionPlayerBridge : IDisposable
{
    private readonly GameSession _session;
    private readonly IPlayerSource _source;

    // The session refused a player that the transport had already accepted
    // (e.g. the round already started). The transport should disconnect them.
    public event Action<ulong> PlayerRejected;

    public SessionPlayerBridge(GameSession session, IPlayerSource source)
    {
        _session = session;
        _source = source;

        _source.PlayerConnected += OnPlayerConnected;
        _source.PlayerDisconnected += OnPlayerDisconnected;
    }

    public void Dispose()
    {
        _source.PlayerConnected -= OnPlayerConnected;
        _source.PlayerDisconnected -= OnPlayerDisconnected;
    }

    private void OnPlayerConnected(ulong id, string name)
    {
        if (!_session.AddPlayer(id, name))
            PlayerRejected?.Invoke(id);
    }

    private void OnPlayerDisconnected(ulong id) => _session.RemovePlayer(id);
}
