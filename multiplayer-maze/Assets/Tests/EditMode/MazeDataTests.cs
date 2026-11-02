using System.Collections.Generic;
using NUnit.Framework;

public class MazeDataTests
{
    [TestCase(11, 11, 1)]
    [TestCase(11, 11, 999)]
    [TestCase(2, 2, 5)]
    [TestCase(7, 13, -42)]
    [TestCase(64, 64, int.MinValue)]
    public void SameSeed_BuildsSameMaze(int width, int height, int seed)
    {
        MazeData a = MazeData.Generate(width, height, seed);
        MazeData b = MazeData.Generate(width, height, seed);

        for (int y = 0; y < height; ++y)
            for (int x = 0; x < width; ++x)
                Assert.AreEqual(a.GetWalls(x, y), b.GetWalls(x, y), $"cell [{x}, {y}]");
    }

    [Test]
    public void DifferentSeeds_BuildDifferentMazes()
    {
        MazeData a = MazeData.Generate(11, 11, 1);
        MazeData b = MazeData.Generate(11, 11, 2);

        bool anyDifference = false;
        for (int y = 0; y < 11 && !anyDifference; ++y)
            for (int x = 0; x < 11; ++x)
                if (a.GetWalls(x, y) != b.GetWalls(x, y))
                {
                    anyDifference = true;
                    break;
                }

        Assert.IsTrue(anyDifference);
    }

    [TestCase(11, 11, 1)]
    [TestCase(2, 2, 5)]
    [TestCase(7, 13, -42)]
    [TestCase(64, 64, 7)]
    public void Maze_IsPerfect_EveryCellReachableWithoutLoops(int width, int height, int seed)
    {
        MazeData maze = MazeData.Generate(width, height, seed);

        int passages = 0;
        for (int y = 0; y < height; ++y)
            for (int x = 0; x < width; ++x)
            {
                if (x + 1 < width && !maze.HasWall(x, y, MazeData.Wall.East))
                    ++passages;
                if (y + 1 < height && !maze.HasWall(x, y, MazeData.Wall.North))
                    ++passages;
            }

        // A connected graph with cells - 1 edges is a tree: reachable and loop free.
        Assert.AreEqual(width * height - 1, passages);
        Assert.AreEqual(width * height, CountReachable(maze, 0, 0));
    }

    [Test]
    public void Walls_AreSymmetricBetweenNeighbours_AndBorderIsClosed()
    {
        MazeData maze = MazeData.Generate(9, 9, 3);

        for (int y = 0; y < 9; ++y)
            for (int x = 0; x < 9; ++x)
            {
                if (x + 1 < 9)
                    Assert.AreEqual(maze.HasWall(x, y, MazeData.Wall.East), maze.HasWall(x + 1, y, MazeData.Wall.West));
                if (y + 1 < 9)
                    Assert.AreEqual(maze.HasWall(x, y, MazeData.Wall.North), maze.HasWall(x, y + 1, MazeData.Wall.South));

                if (x == 0) Assert.IsTrue(maze.HasWall(x, y, MazeData.Wall.West));
                if (x == 8) Assert.IsTrue(maze.HasWall(x, y, MazeData.Wall.East));
                if (y == 0) Assert.IsTrue(maze.HasWall(x, y, MazeData.Wall.South));
                if (y == 8) Assert.IsTrue(maze.HasWall(x, y, MazeData.Wall.North));
            }
    }

    [Test]
    public void FindFarthestCell_ReturnsReachableCellWithLongestPath()
    {
        MazeData maze = MazeData.Generate(11, 11, 1);

        var farthest = maze.FindFarthestCell(5, 5);

        Assert.IsTrue(maze.InBounds(farthest.x, farthest.y));
        Assert.Greater(farthest.distance, 0);

        // In a maze without loops the farthest cell is always a dead end: exactly one opening.
        int openings = 0;
        foreach (MazeData.Wall side in new[] { MazeData.Wall.North, MazeData.Wall.East, MazeData.Wall.South, MazeData.Wall.West })
            if (!maze.HasWall(farthest.x, farthest.y, side))
                ++openings;

        Assert.AreEqual(1, openings);
    }

    [Test]
    public void Settings_AreClampedToSafeSize()
    {
        MazeSettings clamped = new MazeSettings(100000, -5, 7).Clamped();

        Assert.AreEqual(MazeSettings.MAX_SIZE, clamped.width);
        Assert.AreEqual(MazeSettings.MIN_SIZE, clamped.height);
        Assert.AreEqual(7, clamped.seed);
    }

    private static int CountReachable(MazeData maze, int startX, int startY)
    {
        var visited = new HashSet<(int, int)> { (startX, startY) };
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            TryVisit(maze, visited, queue, x, y, MazeData.Wall.North, x, y + 1);
            TryVisit(maze, visited, queue, x, y, MazeData.Wall.East, x + 1, y);
            TryVisit(maze, visited, queue, x, y, MazeData.Wall.South, x, y - 1);
            TryVisit(maze, visited, queue, x, y, MazeData.Wall.West, x - 1, y);
        }

        return visited.Count;
    }

    private static void TryVisit(MazeData maze, HashSet<(int, int)> visited, Queue<(int x, int y)> queue,
        int x, int y, MazeData.Wall side, int nx, int ny)
    {
        if (maze.HasWall(x, y, side) || !maze.InBounds(nx, ny) || !visited.Add((nx, ny)))
            return;

        queue.Enqueue((nx, ny));
    }
}
