using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The exit. Only the server notices a player standing on it; the session decides if it is a win.
/// Stay (not Enter) is used so a player who waits here for the last item still wins.
/// </summary>
public class HatchTrigger : NetworkBehaviour
{
    private GameNetwork _game;

    // Server only, before Spawn.
    public void Initialize(GameNetwork game) => _game = game;

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsServer || !IsSpawned || _game == null)
            return;

        var avatar = other.GetComponentInParent<PlayerAvatar>();
        if (avatar != null)
            _game.TouchHatch(avatar.PlayerId);
    }
}
