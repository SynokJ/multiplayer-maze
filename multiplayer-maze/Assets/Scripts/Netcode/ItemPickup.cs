using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A collectable item. Only the server notices the touch; GameNetwork decides what it counts for.
/// </summary>
public class ItemPickup : NetworkBehaviour
{
    [Tooltip("Item art. Each item shows one of them, picked by its network id.")]
    [SerializeField] private Sprite[] _sprites;

    private GameNetwork _game;

    // Server only, before Spawn.
    public void Initialize(GameNetwork game) => _game = game;

    // The network id is the same on every peer, so everyone sees the same art without any sync.
    public override void OnNetworkSpawn()
    {
        var spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && _sprites != null && _sprites.Length > 0)
            spriteRenderer.sprite = _sprites[(int)(NetworkObjectId % (ulong)_sprites.Length)];
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer || !IsSpawned || _game == null)
            return;

        var avatar = other.GetComponentInParent<PlayerAvatar>();
        if (avatar != null)
            _game.TouchItem(this, avatar.PlayerId);
    }
}
