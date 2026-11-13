using NUnit.Framework;

public class MazePlacementTests
{
    [TestCase(11, 11, 1, 5)]
    [TestCase(30, 30, 999, 8)]
    [TestCase(2, 2, 5, 10)]
    public void ItemsAreDistinctAndAvoidSpawnAndHatch(int width, int height, int seed, int count)
    {
        MazeData maze = MazeData.Generate(width, height, seed);
        MazePlacement placement = MazePlacement.Create(maze, count);

        Assert.AreNotEqual(placement.Spawn, placement.Hatch);
        Assert.AreEqual(System.Math.Min(count, width * height - 2), placement.Items.Count);

        var seen = new System.Collections.Generic.HashSet<(int x, int y)>();
        foreach ((int x, int y) item in placement.Items)
        {
            Assert.IsTrue(maze.InBounds(item.x, item.y));
            Assert.AreNotEqual(placement.Spawn, item);
            Assert.AreNotEqual(placement.Hatch, item);
            Assert.IsTrue(seen.Add(item), "duplicate item cell");
        }
    }

    [Test]
    public void SameMaze_GivesSamePlacement()
    {
        MazePlacement a = MazePlacement.Create(MazeData.Generate(15, 15, 42), 6);
        MazePlacement b = MazePlacement.Create(MazeData.Generate(15, 15, 42), 6);

        Assert.AreEqual(a.Spawn, b.Spawn);
        Assert.AreEqual(a.Hatch, b.Hatch);
        CollectionAssert.AreEqual(a.Items, b.Items);
    }

    [Test]
    public void Hatch_IsFarthestFromSpawn()
    {
        MazeData maze = MazeData.Generate(11, 11, 7);
        MazePlacement placement = MazePlacement.Create(maze, 0);

        var farthest = maze.FindFarthestCell(placement.Spawn.x, placement.Spawn.y);
        Assert.AreEqual((farthest.x, farthest.y), placement.Hatch);
        Assert.AreEqual(0, placement.Items.Count);
    }
}
