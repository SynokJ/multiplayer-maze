using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A collectable item. Only the server notices the touch; GameNetwork decides what it counts for.
/// </summary>
public class ItemPickup : NetworkBehaviour
{
    private GameNetwork _game;

    // Server only, before Spawn.
    public void Initialize(GameNetwork game) => _game = game;

    private void Awake()
    {
        var spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite == null)
            spriteRenderer.sprite = PlainSprites.Circle;
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
