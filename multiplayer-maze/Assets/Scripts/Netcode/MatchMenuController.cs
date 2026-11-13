using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Controller: connects the menu View to matchmaking (MatchFlow) and to the replicated game state.
/// It turns taps into requests and the game state into a RoomSnapshot; it holds no rules itself.
/// </summary>
public class MatchMenuController : MonoBehaviour
{
    private const string _NAME_KEY = "player_name";
    private const float _ROOM_LIST_REFRESH_SECONDS = 10.0f;

    [SerializeField] private MatchMenuView _view;
    [SerializeField] private NgoRoomServer _roomServer;
    [Tooltip("Lobby base URL, e.g. https://maze.duckdns.org. Leave empty in a WebGL build: the page's own origin is used.")]
    [SerializeField] private string _lobbyUrl = string.Empty;

    private NgoMatchClient _client;
    private MatchFlow _flow;
    private GameNetwork _game;
    private readonly RoomSnapshot _snapshot = new RoomSnapshot();

    private bool _busy;
    private bool _roomShown;
    private float _nextRefresh;

    private void Start()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || _view == null)
        {
            Debug.LogError("MatchMenuController: needs a NetworkManager and a MatchMenuView in the scene.");
            enabled = false;
            return;
        }

        _client = new NgoMatchClient(manager);
        _client.Disconnected += OnClientDisconnected;

        _view.PlayerName = LoadName();
        _view.CreateRequested += OnCreate;
        _view.JoinCodeRequested += OnJoin;
        _view.JoinRoomRequested += OnJoin;
        _view.RefreshRequested += OnRefresh;
        _view.LeaveRequested += OnLeave;
        _view.ReadyRequested += OnReady;
        _view.RestartRequested += OnRestart;
        _view.LocalHostRequested += OnLocalHost;
        _view.LocalJoinRequested += OnLocalJoin;

        OnRefresh();
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.CreateRequested -= OnCreate;
            _view.JoinCodeRequested -= OnJoin;
            _view.JoinRoomRequested -= OnJoin;
            _view.RefreshRequested -= OnRefresh;
            _view.LeaveRequested -= OnLeave;
            _view.ReadyRequested -= OnReady;
            _view.RestartRequested -= OnRestart;
            _view.LocalHostRequested -= OnLocalHost;
            _view.LocalJoinRequested -= OnLocalJoin;
        }

        if (_client != null)
            _client.Disconnected -= OnClientDisconnected;

        _flow?.Dispose();
        _client?.Dispose();
    }

    private void Update()
    {
        bool inRoom = NetworkManager.Singleton.IsListening;

        if (inRoom != _roomShown)
        {
            _roomShown = inRoom;
            if (inRoom)
                _view.ShowRoom();
            else
                ShowMenu();
        }

        if (inRoom)
            RenderRoom();
        else if (!_busy && Time.unscaledTime >= _nextRefresh)
            OnRefresh();
    }

    private void ShowMenu()
    {
        _game = null;
        _view.ShowMenu();
        _nextRefresh = 0.0f;
    }

    private void RenderRoom()
    {
        if (_game == null)
            _game = FindAnyObjectByType<GameNetwork>();

        if (_game == null || !_game.IsSpawned)
        {
            _view.RenderConnecting();
            return;
        }

        ulong me = NetworkManager.Singleton.LocalClientId;

        _snapshot.Phase = _game.Phase.Value;
        _snapshot.Reason = _game.Reason.Value;
        _snapshot.Round = _game.Round.Value;
        _snapshot.Countdown = _game.Countdown.Value;
        _snapshot.TimeLeft = _game.TimeLeft.Value;
        _snapshot.Items = _game.Items.Value;
        _snapshot.ItemsRequired = _game.ItemsRequired.Value;
        _snapshot.IsOwner = me == _game.RoomOwnerId;
        _snapshot.MyReady = false;
        _snapshot.Players.Clear();

        for (int i = 0; i < _game.Players.Count; ++i)
        {
            PlayerState player = _game.Players[i];
            bool isMe = player.id == me;

            if (isMe)
                _snapshot.MyReady = player.ready;

            _snapshot.Players.Add(new RoomSnapshot.PlayerLine
            {
                Id = player.id,
                Name = player.name.ToString(),
                IsOwner = player.id == _game.RoomOwnerId,
                IsMe = isMe,
                Ready = player.ready,
                Items = player.items
            });
        }

        _view.Render(_snapshot);
    }

    // ---- requests from the View ----

    private void OnCreate(string playerName, int maxPlayers)
    {
        SaveName(playerName);
        Run(() => NewFlow().CreateRoomAsync(playerName, null, maxPlayers));
    }

    private void OnJoin(string roomCode, string playerName)
    {
        SaveName(playerName);
        Run(() => NewFlow().JoinRoomAsync(roomCode, playerName));
    }

    private void OnRefresh()
    {
        _nextRefresh = Time.unscaledTime + _ROOM_LIST_REFRESH_SECONDS;
        Run(RefreshRooms);
    }

    private void OnLeave()
    {
        _game = null;

        if (NetworkManager.Singleton.IsServer)
            _roomServer.StopServer();
        else if (_flow != null && _flow.State != MatchState.Idle)
            _flow.Leave();
        else
            _client.Disconnect();

        _view.SetStatus(string.Empty);
    }

    private void OnReady(bool ready)
    {
        if (_game != null && _game.IsSpawned)
            _game.SetReadyRpc(ready);
    }

    private void OnRestart()
    {
        if (_game != null && _game.IsSpawned)
            _game.RequestRestartRpc();
    }

    private void OnLocalHost()
    {
#if UNITY_EDITOR
        if (!_roomServer.StartLocalHost(_view.PlayerName))
            _view.SetStatus("Could not start the host.", true);
#endif
    }

    private void OnLocalJoin()
    {
#if UNITY_EDITOR
        _view.SetStatus("Connecting...");
        _client.Connect(new RoomTicket("LOCAL", "127.0.0.1", 7777, "/", false, "dev:" + _view.PlayerName));
#endif
    }

    private void OnClientDisconnected(string reason)
    {
        string text = MatchMessages.Describe(reason);
        _view.SetStatus(text, !string.IsNullOrEmpty(text));
    }

    // ---- helpers ----

    private MatchFlow NewFlow()
    {
        _flow?.Dispose();
        _flow = new MatchFlow(new LobbyHttpMatchmaker(_lobbyUrl), _client);
        return _flow;
    }

    private async Task RefreshRooms()
    {
        RoomListResult result = await new LobbyHttpMatchmaker(_lobbyUrl).ListRoomsAsync();

        if (result.Ok)
        {
            _view.SetRooms(result.Rooms);
            _view.SetStatus(string.Empty);
        }
        else
        {
            _view.SetStatus(MatchMessages.Describe(result.Error), true);
        }
    }

    private async void Run(Func<Task> action)
    {
        if (_busy)
            return;

        _busy = true;
        _view.SetBusy(true);

        try
        {
            await action();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            _view.SetStatus(MatchMessages.Describe(MatchErrors.Network), true);
        }
        finally
        {
            _busy = false;
            _view.SetBusy(false);
        }
    }

    // Overload for MatchFlow requests, which return a success flag.
    private void Run(Func<Task<bool>> action)
    {
        Run(async () =>
        {
            _view.SetStatus("Connecting...");
            bool ok = await action();

            if (!ok)
                _view.SetStatus(MatchMessages.Describe(_flow != null ? _flow.LastError : MatchErrors.InvalidRequest), true);
        });
    }

    private static string LoadName()
    {
        string saved = PlayerPrefs.GetString(_NAME_KEY, string.Empty);
        return MatchValidation.IsValidName(saved) ? saved : "Player" + UnityEngine.Random.Range(100, 1000);
    }

    private static void SaveName(string playerName)
    {
        if (!MatchValidation.IsValidName(playerName))
            return;

        PlayerPrefs.SetString(_NAME_KEY, playerName);
        PlayerPrefs.Save();
    }
}
