using System;
using UnityEngine;

/// <summary>
/// Everything needed to rebuild the exact same maze. This is the only thing
/// the host has to send to clients.
/// </summary>
[Serializable]
public struct MazeSettings
{
    public const int MIN_SIZE = 2;
    public const int MAX_SIZE = 64;

    [Range(MIN_SIZE, MAX_SIZE)] public int width;
    [Range(MIN_SIZE, MAX_SIZE)] public int height;
    public int seed;

    public MazeSettings(int width, int height, int seed)
    {
        this.width = width;
        this.height = height;
        this.seed = seed;
    }

    public static MazeSettings Default => new MazeSettings(11, 11, 12345);

    // Settings received from the network are untrusted: always clamp before generating.
    public MazeSettings Clamped() => new MazeSettings(
        Mathf.Clamp(width, MIN_SIZE, MAX_SIZE),
        Mathf.Clamp(height, MIN_SIZE, MAX_SIZE),
        seed);

    public MazeData CreateData()
    {
        MazeSettings safe = Clamped();
        return MazeData.Generate(safe.width, safe.height, safe.seed);
    }
}
