using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// The room server: one process per room, started by the lobby (a headless Linux build).
/// Checks join tokens, reports who connected, spawns the replicated game state and
/// reports to the lobby every few seconds so it can remove dead rooms.
/// In the Editor it can also run as a local host for testing without the VM.
/// </summary>
public class NgoRoomServer : MonoBehaviour, IPlayerSource
{
    [SerializeField] private GameNetwork _gameNetworkPrefab;
    [SerializeField] private ushort _localHostPort = 7777;

    private const float _HEARTBEAT_SECONDS = 5.0f;
    private const string _DEV_TOKEN_PREFIX = "dev:";

    private readonly Dictionary<ulong, string> _approvedNames = new Dictionary<ulong, string>();
    private readonly Dictionary<ulong, string> _connected = new Dictionary<ulong, string>();

    private RoomLaunchOptions _options;
    private JoinTokenVerifier _verifier;
    private bool _acceptDevTokens;
    private string _localHostName = "Host";

    public event Action<ulong, string> PlayerConnected;
    public event Action<ulong> PlayerDisconnected;

    public GameNetwork Game { get; private set; }
    public RoomLaunchOptions Options => _options;
    public bool IsRunning => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private void Start()
    {
        // A Dedicated Server build (the Linux Server profile) or a process the lobby started with
        // -batchmode is a room server; a browser or the Editor never is.
        if (IsServerProcess)
            StartDedicatedServer();
    }

    private static bool IsServerProcess
    {
        get
        {
#if UNITY_SERVER
            return true;
#else
            return Application.isBatchMode;
#endif
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    // Called by GameNetwork after it has subscribed, so no connection is missed.
    public void ReplayConnectedPlayers()
    {
        foreach (KeyValuePair<ulong, string> player in _connected)
            PlayerConnected?.Invoke(player.Key, player.Value);
    }

    public void StartDedicatedServer()
    {
        Application.targetFrameRate = 30;

        _options = RoomLaunchOptions.Parse(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable);
        _verifier = new JoinTokenVerifier(Encoding.UTF8.GetBytes(_options.Secret), _options.RoomId);
        _acceptDevTokens = false;

        // Loopback only: nginx terminates TLS and forwards /r/<port> to this port.
        Configure("127.0.0.1", _options.Port);
        Application.quitting += OnQuitting;

        if (!NetworkManager.Singleton.StartServer())
        {
            Debug.LogError("StartServer failed");
            Application.Quit(1);
        }
    }

#if UNITY_EDITOR
    // Editor only: a host you can join from a WebGL build on http://localhost with the token "dev:<name>".
    public bool StartLocalHost(string hostName)
    {
        if (IsRunning || NetworkManager.Singleton.IsListening)
            return false;

        _localHostName = string.IsNullOrWhiteSpace(hostName) ? "Host" : hostName;
        _options = RoomLaunchOptions.Parse(new[] { "-port", _localHostPort.ToString(), "-roomId", "LOCAL" }, _ => null);
        _verifier = null;
        _acceptDevTokens = true;

        Configure("127.0.0.1", _localHostPort);
        return NetworkManager.Singleton.StartHost();
    }
#endif

    public void StopServer()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        _connected.Clear();
        _approvedNames.Clear();
    }

    private void Configure(string address, ushort port)
    {
        NetworkManager manager = NetworkManager.Singleton;
        var transport = (UnityTransport)manager.NetworkConfig.NetworkTransport;

        transport.UseWebSockets = true;
        transport.UseEncryption = false;
        transport.SetConnectionData(true, address, port, address);

        UnityTransport.ConnectionAddressData data = transport.ConnectionData;
        data.WebSocketPath = "/";
        transport.ConnectionData = data;

        manager.NetworkConfig.ConnectionApproval = true;
        manager.ConnectionApprovalCallback = Approve;

        Unsubscribe();
        manager.OnServerStarted += OnServerStarted;
        manager.OnClientConnectedCallback += OnClientConnected;
        manager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void Unsubscribe()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
            return;

        manager.OnServerStarted -= OnServerStarted;
        manager.OnClientConnectedCallback -= OnClientConnected;
        manager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = false;
        response.CreatePlayerObject = false;
        response.Pending = false;

        if (!ConnectionPayload.TryDecode(request.Payload, out string version, out string token))
        {
            response.Reason = "bad_request";
            return;
        }

        if (version != Application.version)
        {
            response.Reason = "version_mismatch";
            return;
        }

        if (Game == null || !Game.CanJoin)
        {
            response.Reason = "room_closed";
            return;
        }

        string name;
        if (_acceptDevTokens && token.StartsWith(_DEV_TOKEN_PREFIX, StringComparison.Ordinal))
        {
            name = token.Substring(_DEV_TOKEN_PREFIX.Length);
            if (!MatchValidation.IsValidName(name))
            {
                response.Reason = "bad_token";
                return;
            }
        }
        else if (_verifier != null)
        {
            TokenError error = _verifier.Verify(token, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out JoinClaims claims);
            if (error != TokenError.None)
            {
                response.Reason = error == TokenError.Expired ? "token_expired" : "bad_token";
                return;
            }

            name = claims.Name;
        }
        else
        {
            response.Reason = "bad_token";
            return;
        }

        _approvedNames[request.ClientNetworkId] = name;
        response.Approved = true;
    }

    private void OnServerStarted()
    {
        GameNetwork game = Instantiate(_gameNetworkPrefab);
        game.Configure(_options.LevelIndex, _options.MaxPlayers);
        game.GetComponent<NetworkObject>().Spawn();
        Game = game;

        Debug.Log("ROOM_READY " + _options.RoomId + " " + _options.Port);

        if (IsServerProcess && !string.IsNullOrEmpty(_options.LobbyUrl))
            StartCoroutine(HeartbeatLoop());
    }

    private void OnClientConnected(ulong clientId)
    {
        // The host's own client is never approved, so it has no stored name.
        string name = _approvedNames.TryGetValue(clientId, out string approved) ? approved : _localHostName;

        _connected[clientId] = name;
        PlayerConnected?.Invoke(clientId, name);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _approvedNames.Remove(clientId);

        if (_connected.Remove(clientId))
            PlayerDisconnected?.Invoke(clientId);
    }

    private void OnQuitting()
    {
        // The lobby stops rooms with SIGTERM; close cleanly if Unity routes it here.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
    }

    private IEnumerator HeartbeatLoop()
    {
        var wait = new WaitForSeconds(_HEARTBEAT_SECONDS);

        while (IsRunning)
        {
            string body = "{\"players\":" + NetworkManager.Singleton.ConnectedClientsIds.Count + "}";

            using (var request = new UnityWebRequest(_options.LobbyUrl + "/internal/rooms/" + _options.RoomId + "/heartbeat", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-internal-key", _options.LobbyKey ?? string.Empty);
                request.timeout = 4;

                yield return request.SendWebRequest();
            }

            yield return wait;
        }
    }
}
