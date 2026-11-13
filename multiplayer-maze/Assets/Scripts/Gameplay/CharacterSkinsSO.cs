using UnityEngine;

/// <summary>
/// Model: read-only list of character art. Player n gets skin n (wrapping), so every peer
/// picks the same skin from a player id without any sync. Shared by the avatar and the room UI.
/// </summary>
[CreateAssetMenu(fileName = "CharacterSkins", menuName = "Maze/Character Skins")]
public class CharacterSkinsSO : ScriptableObject
{
    [Tooltip("Character art prefabs (a SpriteRenderer and an Animator).")]
    [SerializeField] private GameObject[] _skins;

    public GameObject Prefab(ulong playerId)
        => _skins == null || _skins.Length == 0 ? null : _skins[(int)(playerId % (ulong)_skins.Length)];

    // The skin's first sprite, for lists and icons.
    public Sprite Portrait(ulong playerId)
    {
        GameObject prefab = Prefab(playerId);
        if (prefab == null)
            return null;

        var spriteRenderer = prefab.GetComponentInChildren<SpriteRenderer>();
        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }
}
