using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// View: a plain on-screen panel (IMGUI works in WebGL) to create or join a room and play a round.
/// It only shows state and asks the controllers; it never changes game data itself.
/// Replace it with the real menu later; MatchFlow and GameNetwork stay the same.
/// </summary>
public class NgoMatchDebugUI : MonoBehaviour
{
    [SerializeField] private NgoRoomServer _roomServer;
    [Tooltip("Lobby base URL, e.g. https://maze.duckdns.org. Leave empty in a WebGL build: the page's own origin is used.")]
    [SerializeField] private string _lobbyUrl = string.Empty;
    [SerializeField] private string _playerName = "Player";
    [SerializeField] private string _roomCode = string.Empty;
    [SerializeField, Range(1, GameRules.MAX_PLAYERS_LIMIT)] private int _newRoomPlayers = 4;

    private NgoMatchClient _client;
    private MatchFlow _flow;
    private GameNetwork _game;
    private IReadOnlyList<RoomInfo> _rooms = Array.Empty<RoomInfo>();
    private string _status = string.Empty;
    private bool _busy;

    private void Start()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            Debug.LogError("NgoMatchDebugUI: there is no NetworkManager in the scene.");
            enabled = false;
            return;
        }

        _client = new NgoMatchClient(manager);
        _client.Connected += () => _status = "Connected";
        _client.Disconnected += reason => _status = "Disconnected: " + reason;
    }

    private void OnDestroy()
    {
        _flow?.Dispose();
        _client?.Dispose();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 380, Screen.height - 20), GUI.skin.box);
        GUI.enabled = !_busy;

        if (NetworkManager.Singleton.IsListening)
            DrawRoom();
        else
            DrawMenu();

        GUI.enabled = true;
        if (!string.IsNullOrEmpty(_status))
            GUILayout.Label(_status);

        GUILayout.EndArea();
    }

    private void DrawMenu()
    {
        _playerName = Field("Name", _playerName);
        _lobbyUrl = Field("Lobby URL", _lobbyUrl);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Create room"))
            Run(() => NewFlow().CreateRoomAsync(_playerName, null, _newRoomPlayers));
        GUILayout.Label("players", GUILayout.Width(50));
        _newRoomPlayers = Mathf.RoundToInt(GUILayout.HorizontalSlider(_newRoomPlayers, 1, GameRules.MAX_PLAYERS_LIMIT, GUILayout.Width(80)));
        GUILayout.Label(_newRoomPlayers.ToString(), GUILayout.Width(24));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        _roomCode = GUILayout.TextField(_roomCode, MatchValidation.ROOM_ID_LENGTH, GUILayout.Width(100));
        if (GUILayout.Button("Join by code"))
            Run(() => NewFlow().JoinRoomAsync(_roomCode, _playerName));
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Refresh room list"))
            Run(RefreshRooms);

        foreach (RoomInfo room in _rooms)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{room.Id}  {room.Name}  {room.Players}/{room.MaxPlayers}");
            if (GUILayout.Button("Join", GUILayout.Width(50)))
            {
                string id = room.Id;
                Run(() => NewFlow().JoinRoomAsync(id, _playerName));
            }
            GUILayout.EndHorizontal();
        }

#if UNITY_EDITOR
        GUILayout.Space(8);
        GUILayout.Label("Editor testing without the VM");
        if (GUILayout.Button("Start local host (ws://127.0.0.1:7777)"))
            _status = _roomServer != null && _roomServer.StartLocalHost(_playerName) ? "Local host started" : "Could not start the host";
        if (GUILayout.Button("Join local host"))
        {
            _status = "Connecting...";
            _client.Connect(new RoomTicket("LOCAL", "127.0.0.1", 7777, "/", false, "dev:" + _playerName));
        }
#endif
    }

    private void DrawRoom()
    {
        if (_game == null)
            _game = FindAnyObjectByType<GameNetwork>();

        if (_game == null || !_game.IsSpawned)
        {
            GUILayout.Label("Waiting for the room state...");
            DrawLeave();
            return;
        }

        ulong me = NetworkManager.Singleton.LocalClientId;
        GamePhase phase = _game.Phase.Value;

        GUILayout.Label($"Round {_game.Round.Value + 1}   {phase}" + (phase == GamePhase.Won || phase == GamePhase.Lost ? $" ({_game.Reason.Value})" : string.Empty));

        if (phase == GamePhase.Countdown)
            GUILayout.Label("Starts in " + _game.Countdown.Value);
        else if (phase == GamePhase.Playing)
            GUILayout.Label($"Time left {_game.TimeLeft.Value}s   items {_game.Items.Value}/{_game.ItemsRequired.Value}");

        bool myReady = false;
        for (int i = 0; i < _game.Players.Count; ++i)
        {
            PlayerState player = _game.Players[i];
            string role = player.id == _game.RoomOwnerId ? " (owner)" : string.Empty;
            GUILayout.Label($"{player.name}{role}   lives {player.lives}   items {player.items}" + (player.ready ? "   READY" : string.Empty));

            if (player.id == me)
                myReady = player.ready;
        }

        if (phase == GamePhase.Lobby || phase == GamePhase.Countdown)
        {
            if (GUILayout.Button(myReady ? "Not ready" : "Ready"))
                _game.SetReadyRpc(!myReady);
        }
        else if ((phase == GamePhase.Won || phase == GamePhase.Lost) && me == _game.RoomOwnerId)
        {
            if (GUILayout.Button("Play again"))
                _game.RequestRestartRpc();
        }

        DrawLeave();
    }

    private void DrawLeave()
    {
        if (!GUILayout.Button("Leave"))
            return;

        _game = null;

        if (NetworkManager.Singleton.IsServer)
            _roomServer.StopServer();
        else if (_flow != null && _flow.State != MatchState.Idle)
            _flow.Leave();
        else
            _client.Disconnect();

        _status = string.Empty;
    }

    private MatchFlow NewFlow()
    {
        _flow?.Dispose();
        _flow = new MatchFlow(new LobbyHttpMatchmaker(_lobbyUrl), _client);
        return _flow;
    }

    private async Task RefreshRooms()
    {
        RoomListResult result = await new LobbyHttpMatchmaker(_lobbyUrl).ListRoomsAsync();

        _rooms = result.Rooms;
        _status = result.Ok ? _rooms.Count + " room(s)" : "Error: " + result.Error;
    }

    private async void Run(Func<Task> action)
    {
        _busy = true;
        _status = "Working...";

        try
        {
            await action();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            _status = "Error: " + exception.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    // Overload for MatchFlow requests, which return a success flag.
    private void Run(Func<Task<bool>> action)
    {
        Run(async () =>
        {
            bool ok = await action();
            _status = ok ? "Connecting..." : "Error: " + (_flow != null ? _flow.LastError : "unknown");
        });
    }

    private static string Field(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(70));
        string text = GUILayout.TextField(value ?? string.Empty);
        GUILayout.EndHorizontal();
        return text;
    }
}
