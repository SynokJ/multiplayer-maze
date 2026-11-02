using UnityEngine;

/// <summary>
/// Ordered levels. The host sends a level index and a seed; every client looks the
/// level up here, so the ScriptableObject itself never goes over the network.
/// </summary>
[CreateAssetMenu(fileName = "LevelList", menuName = "Maze/Level List")]
public class LevelListSO : ScriptableObject
{
    [SerializeField] private LevelConfigSO[] _levels;

    public int Count => _levels == null ? 0 : _levels.Length;

    public bool TryGet(int index, out LevelConfigSO level)
    {
        level = index >= 0 && index < Count ? _levels[index] : null;
        return level != null;
    }
}
