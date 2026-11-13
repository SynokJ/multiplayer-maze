using System;

/// <summary>
/// Controller logic (plain C#, no Unity types): the round's rules and flow.
/// Lobby -> Countdown -> Playing -> Won/Lost -> (Restart) -> Lobby.
/// Authority only: on a networked game the server runs it; clients just show GameData.
/// Time comes in through Tick, so nothing here depends on Time.timeScale.
/// </summary>
public sealed class GameSession
{
    public GameData Data { get; }

    public event Action<GamePhase, GamePhase> PhaseChanged;
    public event Action ItemsChanged;
    public event Action<PlayerData> PlayerJoined;
    public event Action<ulong> PlayerLeft;
    public event Action<PlayerData> PlayerChanged;
    // A player reached the hatch before the team had enough items.
    public event Action<ulong> HatchBlocked;

    public GameSession(GameRules rules, int seed)
    {
        Data = new GameData(rules.Clamped(), seed);
    }

    // Use this in connection approval, before the player is added.
    public bool CanJoin => Data.Phase == GamePhase.Lobby && Data.Players.Count < Data.Rules.maxPlayers;

    public bool AddPlayer(ulong id, string name)
    {
        if (!CanJoin || Data.GetPlayer(id) != null)
            return false;

        string displayName = string.IsNullOrWhiteSpace(name) ? $"Player {id}" : name.Trim();
        var player = new PlayerData(id, displayName, Data.Rules.startingLives);

        Data.AddPlayer(player);
        PlayerJoined?.Invoke(player);
        return true;
    }

    public bool RemovePlayer(ulong id)
    {
        PlayerData player = Data.GetPlayer(id);
        if (player == null)
            return false;

        Data.RemovePlayer(player);
        PlayerLeft?.Invoke(id);

        switch (Data.Phase)
        {
            case GamePhase.Lobby:
                TryBeginCountdown();
                break;
            case GamePhase.Countdown:
                if (!CanStart())
                    SetPhase(GamePhase.Lobby);
                break;
            case GamePhase.Playing:
                if (!AnyPlayerAlive())
                    Finish(GamePhase.Lost, FinishReason.AllPlayersDead);
                break;
        }

        return true;
    }

    public bool SetReady(ulong id, bool ready)
    {
        PlayerData player = Data.GetPlayer(id);
        if (player == null || (Data.Phase != GamePhase.Lobby && Data.Phase != GamePhase.Countdown))
            return false;

        if (player.IsReady == ready)
            return true;

        player.IsReady = ready;
        PlayerChanged?.Invoke(player);

        if (Data.Phase == GamePhase.Countdown && !CanStart())
            SetPhase(GamePhase.Lobby);
        else
            TryBeginCountdown();

        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!(deltaTime > 0.0f))
            return;

        switch (Data.Phase)
        {
            case GamePhase.Countdown:
                Data.CountdownRemaining -= deltaTime;
                if (Data.CountdownRemaining <= 0.0f)
                    BeginRound();
                break;

            case GamePhase.Playing:
                Data.TimeRemaining -= deltaTime;
                if (Data.TimeRemaining <= 0.0f)
                {
                    Data.TimeRemaining = 0.0f;
                    Finish(GamePhase.Lost, FinishReason.TimeUp);
                }
                break;
        }
    }

    public bool CollectItem(ulong playerId)
    {
        PlayerData player = GetAlivePlayerWhilePlaying(playerId);
        if (player == null)
            return false;

        player.ItemsCollected++;
        Data.ItemsCollected++;

        ItemsChanged?.Invoke();
        PlayerChanged?.Invoke(player);
        return true;
    }

    public bool HitPlayer(ulong playerId)
    {
        PlayerData player = GetAlivePlayerWhilePlaying(playerId);
        if (player == null)
            return false;

        player.Lives--;
        PlayerChanged?.Invoke(player);

        if (!AnyPlayerAlive())
            Finish(GamePhase.Lost, FinishReason.AllPlayersDead);

        return true;
    }

    // Returns true when this ended the round with a win.
    public bool ReachHatch(ulong playerId)
    {
        if (GetAlivePlayerWhilePlaying(playerId) == null)
            return false;

        if (!Data.HasEnoughItems)
        {
            HatchBlocked?.Invoke(playerId);
            return false;
        }

        Finish(GamePhase.Won, FinishReason.HatchReached);
        return true;
    }

    // Back to the lobby with the same players and a new maze seed.
    // Who may ask for this is decided by the caller (e.g. the room host).
    public bool Restart(int newSeed)
    {
        if (Data.Phase != GamePhase.Won && Data.Phase != GamePhase.Lost)
            return false;

        Data.Round++;
        Data.Seed = newSeed;
        Data.FinishReason = FinishReason.None;
        Data.ItemsCollected = 0;
        Data.TimeRemaining = Data.Rules.timeLimitSeconds;
        Data.CountdownRemaining = 0.0f;

        foreach (PlayerData player in Data.Players)
        {
            player.IsReady = false;
            player.Lives = Data.Rules.startingLives;
            player.ItemsCollected = 0;
            PlayerChanged?.Invoke(player);
        }

        ItemsChanged?.Invoke();
        SetPhase(GamePhase.Lobby);
        return true;
    }

    private bool CanStart()
    {
        if (Data.Players.Count == 0 || Data.Players.Count < Data.Rules.minPlayers)
            return false;

        foreach (PlayerData player in Data.Players)
            if (!player.IsReady)
                return false;

        return true;
    }

    private void TryBeginCountdown()
    {
        if (Data.Phase != GamePhase.Lobby || !CanStart())
            return;

        Data.CountdownRemaining = Data.Rules.countdownSeconds;
        SetPhase(GamePhase.Countdown);

        if (Data.CountdownRemaining <= 0.0f)
            BeginRound();
    }

    private void BeginRound()
    {
        Data.CountdownRemaining = 0.0f;
        Data.TimeRemaining = Data.Rules.timeLimitSeconds;
        Data.ItemsCollected = 0;

        foreach (PlayerData player in Data.Players)
        {
            player.Lives = Data.Rules.startingLives;
            player.ItemsCollected = 0;
        }

        ItemsChanged?.Invoke();
        SetPhase(GamePhase.Playing);
    }

    private void Finish(GamePhase result, FinishReason reason)
    {
        Data.FinishReason = reason;
        SetPhase(result);
    }

    private void SetPhase(GamePhase next)
    {
        GamePhase previous = Data.Phase;
        if (previous == next)
            return;

        Data.Phase = next;
        PhaseChanged?.Invoke(previous, next);
    }

    private PlayerData GetAlivePlayerWhilePlaying(ulong playerId)
    {
        if (Data.Phase != GamePhase.Playing)
            return null;

        PlayerData player = Data.GetPlayer(playerId);
        return player != null && player.IsAlive ? player : null;
    }

    private bool AnyPlayerAlive()
    {
        foreach (PlayerData player in Data.Players)
            if (player.IsAlive)
                return true;

        return false;
    }
}
