using System;
using UnityEngine;

/// <summary>
/// Everything needed to rebuild the exact same maze. This is the only thing
/// the host has to send to clients.
/// </summary>
[Serializable]
public struct MazeSettings
{
    [Min(2)] public int width;
    [Min(2)] public int height;
    public int seed;

    public MazeSettings(int width, int height, int seed)
    {
        this.width = width;
        this.height = height;
        this.seed = seed;
    }

    public static MazeSettings Default => new MazeSettings(11, 11, 12345);

    public MazeData CreateData() => MazeData.Generate(width, height, seed);
}
