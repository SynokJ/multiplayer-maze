using System;
using System.Collections.Generic;

/// <summary>
/// Pure maze layout: no Unity types, fully determined by (width, height, seed).
/// Every client that uses the same three values builds the identical maze.
/// </summary>
public sealed class MazeData
{
    [Flags]
    public enum Wall : byte
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8,
        All = North | East | South | West
    }

    public readonly int width;
    public readonly int height;
    public readonly int seed;

    private readonly Wall[] _walls;

    // North is +y, East is +x.
    private static readonly int[] _DX = { 0, 1, 0, -1 };
    private static readonly int[] _DY = { 1, 0, -1, 0 };
    private static readonly Wall[] _DIRS = { Wall.North, Wall.East, Wall.South, Wall.West };
    private static readonly Wall[] _OPPOSITE = { Wall.South, Wall.West, Wall.North, Wall.East };

    private MazeData(int width, int height, int seed)
    {
        this.width = width;
        this.height = height;
        this.seed = seed;

        _walls = new Wall[width * height];
        for (int i = 0; i < _walls.Length; ++i)
            _walls[i] = Wall.All;
    }

    public static MazeData Generate(int width, int height, int seed)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Maze needs at least 1x1 cells.");

        var maze = new MazeData(width, height, seed);
        maze.Carve(new SeededRandom(seed));
        return maze;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    public bool HasWall(int x, int y, Wall wall) => (_walls[Index(x, y)] & wall) != 0;

    public Wall GetWalls(int x, int y) => _walls[Index(x, y)];

    /// <summary>
    /// BFS over open passages from the start cell. Returns the reachable cell
    /// with the longest path (useful for hatch or enemy spawn placement).
    /// </summary>
    public (int x, int y, int distance) FindFarthestCell(int startX, int startY)
    {
        var distance = new int[width * height];
        Array.Fill(distance, -1);

        var queue = new Queue<int>();
        int start = Index(startX, startY);
        distance[start] = 0;
        queue.Enqueue(start);

        int farthest = start;

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            int cx = current % width;
            int cy = current / width;

            if (distance[current] > distance[farthest])
                farthest = current;

            for (int d = 0; d < 4; ++d)
            {
                if (HasWall(cx, cy, _DIRS[d]))
                    continue;

                int next = Index(cx + _DX[d], cy + _DY[d]);
                if (distance[next] != -1)
                    continue;

                distance[next] = distance[current] + 1;
                queue.Enqueue(next);
            }
        }

        return (farthest % width, farthest / width, distance[farthest]);
    }

    // Iterative recursive-backtracker (DFS): no recursion, so size is not limited by stack depth.
    private void Carve(SeededRandom rng)
    {
        var visited = new bool[width * height];
        var stack = new Stack<int>();
        var order = new int[] { 0, 1, 2, 3 };

        int start = Index(width / 2, height / 2);
        visited[start] = true;
        stack.Push(start);

        while (stack.Count > 0)
        {
            int current = stack.Peek();
            int cx = current % width;
            int cy = current / width;

            Shuffle(order, rng);

            bool moved = false;
            for (int i = 0; i < order.Length; ++i)
            {
                int d = order[i];
                int nx = cx + _DX[d];
                int ny = cy + _DY[d];

                if (!InBounds(nx, ny) || visited[Index(nx, ny)])
                    continue;

                int next = Index(nx, ny);
                _walls[current] &= ~_DIRS[d];
                _walls[next] &= ~_OPPOSITE[d];

                visited[next] = true;
                stack.Push(next);
                moved = true;
                break;
            }

            if (!moved)
                stack.Pop();
        }
    }

    private static void Shuffle(int[] array, SeededRandom rng)
    {
        for (int i = array.Length - 1; i > 0; --i)
        {
            int j = rng.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    private int Index(int x, int y) => y * width + x;
}
