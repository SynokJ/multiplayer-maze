using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spawned by the room server. On the server it owns the GameSession and copies its data into
/// NetworkVariables; on every peer it builds the maze from the replicated level and seed.
/// Clients never change the game directly: they ask through the Rpc methods.
/// </summary>
public class GameNetwork : NetworkBehaviour
{
    // Level and seed are declared before Phase, so they are applied first on a client.
    public readonly NetworkVariable<int> LevelIndex = new NetworkVariable<int>(0);
    public readonly NetworkVariable<int> Seed = new NetworkVariable<int>(0);
    public readonly NetworkVariable<int> Round = new NetworkVariable<int>(0);
    public readonly NetworkVariable<FinishReason> Reason = new NetworkVariable<FinishReason>(FinishReason.None);
    public readonly NetworkVariable<GamePhase> Phase = new NetworkVariable<GamePhase>(GamePhase.Lobby);

    // Whole seconds only, so a value is sent once per second and not every frame.
    public readonly NetworkVariable<int> TimeLeft = new NetworkVariable<int>(0);
    public readonly NetworkVariable<int> Countdown = new NetworkVariable<int>(0);

    public readonly NetworkVariable<int> Items = new NetworkVariable<int>(0);
    public readonly NetworkVariable<int> ItemsRequired = new NetworkVariable<int>(0);

    public NetworkList<PlayerState> Players;

    [SerializeField] private LevelListSO _levels;

    private GameSession _session;
    private SessionPlayerBridge _bridge;
    private MazeController _maze;

    private int _configuredLevel;
    private int _configuredMaxPlayers;

    // Server only: gameplay code (items, hatch, enemies) will call into this.
    public GameSession Session => _session;

    public bool CanJoin => _session != null && _session.CanJoin;

    public ulong RoomOwnerId => Players.Count > 0 ? Players[0].id : ulong.MaxValue;

    private void Awake()
    {
        // NetworkList must exist before the object spawns.
        Players = new NetworkList<PlayerState>();
    }

    // Called by the room server before Spawn.
    public void Configure(int levelIndex, int maxPlayers)
    {
        _configuredLevel = levelIndex;
        _configuredMaxPlayers = maxPlayers;
    }

    public override void OnNetworkSpawn()
    {
        _maze = FindFirstObjectByType<MazeController>();
        Phase.OnValueChanged += OnPhaseChanged;

        if (IsServer)
            StartSession();
        else if (Phase.Value != GamePhase.Lobby)
            BuildMaze();
    }

    public override void OnNetworkDespawn()
    {
        Phase.OnValueChanged -= OnPhaseChanged;

        if (_session != null)
        {
            _session.PhaseChanged -= OnSessionPhaseChanged;
            _session.ItemsChanged -= OnSessionItemsChanged;
            _session.PlayerJoined -= OnSessionPlayerChanged;
            _session.PlayerChanged -= OnSessionPlayerChanged;
            _session.PlayerLeft -= OnSessionPlayerLeft;
        }

        _bridge?.Dispose();
        _bridge = null;
        _session = null;

        if (_maze != null)
            _maze.Clear();
    }

    private void Update()
    {
        if (!IsServer || _session == null)
            return;

        _session.Tick(Time.deltaTime);
        PushTimers();
    }

    // Clients ask; the server decides. Rpc methods must end in "Rpc".
    [Rpc(SendTo.Server)]
    public void SetReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        _session?.SetReady(rpcParams.Receive.SenderClientId, ready);
    }

    [Rpc(SendTo.Server)]
    public void RequestRestartRpc(RpcParams rpcParams = default)
    {
        if (_session == null || rpcParams.Receive.SenderClientId != RoomOwnerId)
            return;

        _session.Restart(NewSeed());
    }

    private void StartSession()
    {
        LevelConfigSO level = null;
        if (_levels != null)
            _levels.TryGet(_configuredLevel, out level);

        GameRules rules = level != null ? level.Rules : GameRules.Default;
        if (_configuredMaxPlayers > 0)
            rules.maxPlayers = _configuredMaxPlayers;

        _session = new GameSession(rules, NewSeed());
        _session.PhaseChanged += OnSessionPhaseChanged;
        _session.ItemsChanged += OnSessionItemsChanged;
        _session.PlayerJoined += OnSessionPlayerChanged;
        _session.PlayerChanged += OnSessionPlayerChanged;
        _session.PlayerLeft += OnSessionPlayerLeft;

        LevelIndex.Value = _configuredLevel;
        PushRound();
        PushTimers();
        Phase.Value = _session.Data.Phase;

        // Connections that arrived before this object was ready are replayed into the session.
        var roomServer = FindFirstObjectByType<NgoRoomServer>();
        if (roomServer != null)
        {
            _bridge = new SessionPlayerBridge(_session, roomServer);
            _bridge.PlayerRejected += id => NetworkManager.DisconnectClient(id, "room_closed");
            roomServer.ReplayConnectedPlayers();
        }
    }

    private void OnSessionPhaseChanged(GamePhase from, GamePhase to)
    {
        // Seed, round and result are written before the phase, so clients see them first.
        PushRound();
        PushTimers();
        Phase.Value = to;
    }

    private void OnSessionItemsChanged() => Items.Value = _session.Data.ItemsCollected;

    private void OnSessionPlayerChanged(PlayerData player)
    {
        PlayerState state = PlayerState.From(player);

        for (int i = 0; i < Players.Count; ++i)
            if (Players[i].id == state.id)
            {
                Players[i] = state;
                return;
            }

        Players.Add(state);
    }

    private void OnSessionPlayerLeft(ulong id)
    {
        for (int i = 0; i < Players.Count; ++i)
            if (Players[i].id == id)
            {
                Players.RemoveAt(i);
                return;
            }
    }

    private void PushRound()
    {
        GameData data = _session.Data;

        Seed.Value = data.Seed;
        Round.Value = data.Round;
        ItemsRequired.Value = data.ItemsRequired;
        Items.Value = data.ItemsCollected;
        Reason.Value = data.FinishReason;
    }

    private void PushTimers()
    {
        GameData data = _session.Data;

        int timeLeft = Mathf.CeilToInt(data.TimeRemaining);
        if (TimeLeft.Value != timeLeft)
            TimeLeft.Value = timeLeft;

        int countdown = Mathf.CeilToInt(data.CountdownRemaining);
        if (Countdown.Value != countdown)
            Countdown.Value = countdown;
    }

    private void OnPhaseChanged(GamePhase previous, GamePhase next)
    {
        if (next == GamePhase.Countdown)
            BuildMaze();
        else if (next == GamePhase.Lobby && _maze != null)
            _maze.Clear();
    }

    private void BuildMaze()
    {
        if (_maze == null)
        {
            Debug.LogError("GameNetwork: no MazeController in the scene.");
            return;
        }

        LevelConfigSO level = null;
        if (_levels == null || !_levels.TryGet(LevelIndex.Value, out level))
        {
            Debug.LogError("GameNetwork: level " + LevelIndex.Value + " is missing from the level list.");
            return;
        }

        _maze.Generate(level, Seed.Value);
    }

    private static int NewSeed() => UnityEngine.Random.Range(int.MinValue, int.MaxValue);
}
