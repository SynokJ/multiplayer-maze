using System.Collections.Generic;

/// <summary>
/// Pure: where the players start, where the hatch is and where the items lie.
/// Fully determined by the maze (and so by its seed).
/// </summary>
public sealed class MazePlacement
{
    private const int _ITEM_SALT = 0x1F3D5B79;

    public (int x, int y) Spawn { get; }
    public (int x, int y) Hatch { get; }
    public IReadOnlyList<(int x, int y)> Items { get; }

    private MazePlacement((int x, int y) spawn, (int x, int y) hatch, List<(int x, int y)> items)
    {
        Spawn = spawn;
        Hatch = hatch;
        Items = items;
    }

    // Players start in the middle, the hatch is the farthest cell from there.
    // If the maze has fewer free cells than itemCount, fewer items are placed.
    public static MazePlacement Create(MazeData maze, int itemCount)
    {
        var spawn = (x: maze.width / 2, y: maze.height / 2);
        var farthest = maze.FindFarthestCell(spawn.x, spawn.y);
        var hatch = (x: farthest.x, y: farthest.y);

        var free = new List<(int x, int y)>();
        for (int y = 0; y < maze.height; ++y)
            for (int x = 0; x < maze.width; ++x)
                if ((x, y) != spawn && (x, y) != hatch)
                    free.Add((x, y));

        var rng = new SeededRandom(maze.seed ^ _ITEM_SALT);
        for (int i = free.Count - 1; i > 0; --i)
        {
            int j = rng.Next(i + 1);
            (free[i], free[j]) = (free[j], free[i]);
        }

        int count = System.Math.Max(0, System.Math.Min(itemCount, free.Count));
        return new MazePlacement(spawn, hatch, free.GetRange(0, count));
    }
}
