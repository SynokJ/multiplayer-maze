using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server only: puts the avatars, the hatch and the items into a freshly built maze and
/// removes them again. Everything else is replicated by NGO.
/// </summary>
public class RoundSpawner : MonoBehaviour
{
    [SerializeField] private PlayerAvatar _avatarPrefab;
    [SerializeField] private ItemPickup _itemPrefab;
    [SerializeField] private HatchTrigger _hatchPrefab;

    private readonly Dictionary<ulong, PlayerAvatar> _avatars = new Dictionary<ulong, PlayerAvatar>();
    private readonly List<NetworkObject> _others = new List<NetworkObject>();

    private bool _movementEnabled;

    public void SpawnRound(GameNetwork game, MazeController maze, IReadOnlyList<PlayerData> players, int itemCount)
    {
        Clear();

        MazePlacement placement = MazePlacement.Create(maze.Data, itemCount);

        Vector2 spawnCenter = maze.CellToWorld(placement.Spawn.x, placement.Spawn.y);
        float ring = maze.CurrentStyle.cellSize * 0.25f;

        for (int i = 0; i < players.Count; ++i)
        {
            // Players start spread on a small circle, so they do not stack on one point.
            float angle = players.Count > 1 ? i * Mathf.PI * 2.0f / players.Count : 0.0f;
            Vector2 offset = players.Count > 1 ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring : Vector2.zero;

            PlayerAvatar avatar = Instantiate(_avatarPrefab, spawnCenter + offset, Quaternion.identity);
            avatar.SetMovementEnabled(_movementEnabled);
            avatar.NetworkObject.SpawnWithOwnership(players[i].Id);
            _avatars[players[i].Id] = avatar;
        }

        HatchTrigger hatch = Instantiate(_hatchPrefab, maze.CellToWorld(placement.Hatch.x, placement.Hatch.y), Quaternion.identity);
        hatch.Initialize(game);
        hatch.NetworkObject.Spawn();
        _others.Add(hatch.NetworkObject);

        foreach ((int x, int y) cell in placement.Items)
        {
            ItemPickup item = Instantiate(_itemPrefab, maze.CellToWorld(cell.x, cell.y), Quaternion.identity);
            item.Initialize(game);
            item.NetworkObject.Spawn();
            _others.Add(item.NetworkObject);
        }
    }

    public void SetMovementEnabled(bool enabled)
    {
        _movementEnabled = enabled;

        foreach (PlayerAvatar avatar in _avatars.Values)
            if (avatar != null)
                avatar.SetMovementEnabled(enabled);
    }

    // A player who left takes their avatar with them.
    public void Release(ulong playerId)
    {
        if (_avatars.TryGetValue(playerId, out PlayerAvatar avatar))
        {
            _avatars.Remove(playerId);
            Despawn(avatar != null ? avatar.NetworkObject : null);
        }
    }

    public void Clear()
    {
        foreach (PlayerAvatar avatar in _avatars.Values)
            Despawn(avatar != null ? avatar.NetworkObject : null);
        _avatars.Clear();

        foreach (NetworkObject other in _others)
            Despawn(other);
        _others.Clear();
    }

    private static void Despawn(NetworkObject target)
    {
        if (target != null && target.IsSpawned)
            target.Despawn();
    }
}
