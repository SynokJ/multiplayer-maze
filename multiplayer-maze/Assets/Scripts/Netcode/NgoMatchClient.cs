using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// The player's connection through Netcode for GameObjects over WebSockets.
/// A WebGL build can only be a client, so this is the only thing a browser needs.
/// </summary>
public sealed class NgoMatchClient : IMatchClient, IDisposable
{
    private readonly NetworkManager _manager;
    private bool _attemptActive;

    public event Action Connected;
    public event Action<string> Disconnected;

    public NgoMatchClient(NetworkManager manager)
    {
        _manager = manager;
        _manager.OnClientConnectedCallback += OnClientConnected;
        _manager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public void Dispose()
    {
        _manager.OnClientConnectedCallback -= OnClientConnected;
        _manager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    public void Connect(RoomTicket ticket)
    {
        if (_manager.IsListening)
        {
            Disconnected?.Invoke("busy");
            return;
        }

        var transport = (UnityTransport)_manager.NetworkConfig.NetworkTransport;

        // The browser talks wss to nginx, which forwards plain ws to the room.
        transport.UseWebSockets = true;
        transport.UseEncryption = ticket.secure;
        if (ticket.secure)
            transport.SetClientSecrets(ticket.host);

        transport.SetConnectionData(ticket.host, ticket.port);

        // SetConnectionData replaces the connection data, so the path is set afterwards.
        UnityTransport.ConnectionAddressData data = transport.ConnectionData;
        data.WebSocketPath = string.IsNullOrEmpty(ticket.path) ? "/" : ticket.path;
        transport.ConnectionData = data;

        _manager.NetworkConfig.ConnectionApproval = true;
        _manager.NetworkConfig.ConnectionData = ConnectionPayload.Encode(Application.version, ticket.token);

        _attemptActive = true;
        if (!_manager.StartClient())
        {
            _attemptActive = false;
            Disconnected?.Invoke("start_failed");
        }
    }

    public void Disconnect()
    {
        bool wasActive = _attemptActive;
        _attemptActive = false;

        if (_manager.IsListening)
            _manager.Shutdown();

        if (wasActive)
            Disconnected?.Invoke("left");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (_attemptActive && !_manager.IsServer && clientId == _manager.LocalClientId)
            Connected?.Invoke();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!_attemptActive || _manager.IsServer || clientId != _manager.LocalClientId)
            return;

        _attemptActive = false;

        string reason = _manager.DisconnectReason;
        Disconnected?.Invoke(string.IsNullOrEmpty(reason) ? "connection_lost" : reason);
    }
}
