using System;
using System.Threading.Tasks;

public enum MatchState : byte
{
    Idle = 0,
    Requesting = 1,
    Connecting = 2,
    Connected = 3,
    Failed = 4
}

/// <summary>
/// Controller logic (plain C#): create or join a room, then connect with the ticket.
/// The menu View calls it and listens to StateChanged.
/// </summary>
public sealed class MatchFlow : IDisposable
{
    private readonly IMatchmaker _matchmaker;
    private readonly IMatchClient _client;

    public MatchState State { get; private set; } = MatchState.Idle;
    public string LastError { get; private set; }
    public RoomInfo Room { get; private set; }

    public event Action<MatchState> StateChanged;

    public MatchFlow(IMatchmaker matchmaker, IMatchClient client)
    {
        _matchmaker = matchmaker;
        _client = client;

        _client.Connected += OnClientConnected;
        _client.Disconnected += OnClientDisconnected;
    }

    public void Dispose()
    {
        _client.Connected -= OnClientConnected;
        _client.Disconnected -= OnClientDisconnected;
    }

    public Task<bool> CreateRoomAsync(string playerName, string roomName, int maxPlayers)
    {
        if (!MatchValidation.IsValidName(playerName) || (!string.IsNullOrEmpty(roomName) && !MatchValidation.IsValidName(roomName)))
            return Task.FromResult(FailImmediately(MatchErrors.InvalidRequest));

        return RequestAsync(() => _matchmaker.CreateRoomAsync(playerName, roomName, maxPlayers));
    }

    public Task<bool> JoinRoomAsync(string roomId, string playerName)
    {
        string code = MatchValidation.NormalizeRoomId(roomId);
        if (code == null || !MatchValidation.IsValidName(playerName))
            return Task.FromResult(FailImmediately(MatchErrors.InvalidRequest));

        return RequestAsync(() => _matchmaker.JoinRoomAsync(code, playerName));
    }

    public void Leave()
    {
        if (State == MatchState.Idle)
            return;

        // Go idle first, so the client's Disconnected event is not mistaken for a failure.
        Room = null;
        LastError = null;
        SetState(MatchState.Idle);
        _client.Disconnect();
    }

    private async Task<bool> RequestAsync(Func<Task<MatchResult>> call)
    {
        if (State == MatchState.Requesting || State == MatchState.Connecting || State == MatchState.Connected)
            return false;

        LastError = null;
        SetState(MatchState.Requesting);

        MatchResult result;
        try
        {
            result = await call();
        }
        catch (Exception)
        {
            result = MatchResult.Failure(MatchErrors.Network);
        }

        if (!result.Ok)
        {
            Fail(result.Error);
            return false;
        }

        Room = result.Room;
        SetState(MatchState.Connecting);
        _client.Connect(result.Ticket);
        return true;
    }

    private bool FailImmediately(string error)
    {
        if (State == MatchState.Requesting || State == MatchState.Connecting || State == MatchState.Connected)
            return false;

        Fail(error);
        return false;
    }

    private void OnClientConnected()
    {
        if (State == MatchState.Connecting)
            SetState(MatchState.Connected);
    }

    private void OnClientDisconnected(string reason)
    {
        // A deliberate Leave() already moved to Idle.
        if (State == MatchState.Connecting || State == MatchState.Connected)
            Fail(string.IsNullOrEmpty(reason) ? MatchErrors.Network : reason);
    }

    private void Fail(string error)
    {
        LastError = error;
        Room = null;
        SetState(MatchState.Failed);
    }

    private void SetState(MatchState next)
    {
        if (State == next)
            return;

        State = next;
        StateChanged?.Invoke(next);
    }
}
