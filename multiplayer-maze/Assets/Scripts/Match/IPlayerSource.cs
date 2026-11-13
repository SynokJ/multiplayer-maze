using System;

/// <summary>
/// The room server's side: reports which players connected and left.
/// The NGO server implements it later (from connection approval and disconnect callbacks).
/// </summary>
public interface IPlayerSource
{
    // (client id, display name taken from the verified join token)
    event Action<ulong, string> PlayerConnected;
    event Action<ulong> PlayerDisconnected;
}
