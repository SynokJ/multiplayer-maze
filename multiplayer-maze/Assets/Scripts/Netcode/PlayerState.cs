using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// Model (network copy): one player as every peer sees it. The server writes it from PlayerData.
/// </summary>
public struct PlayerState : INetworkSerializable, IEquatable<PlayerState>
{
    public ulong id;
    public FixedString32Bytes name;
    public int lives;
    public int items;
    public bool ready;

    public static PlayerState From(PlayerData player)
    {
        var state = new PlayerState
        {
            id = player.Id,
            lives = player.Lives,
            items = player.ItemsCollected,
            ready = player.IsReady
        };

        state.name.CopyFromTruncated(player.Name);
        return state;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref name);
        serializer.SerializeValue(ref lives);
        serializer.SerializeValue(ref items);
        serializer.SerializeValue(ref ready);
    }

    public bool Equals(PlayerState other)
        => id == other.id && name.Equals(other.name) && lives == other.lives && items == other.items && ready == other.ready;
}
