using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    public class WorldGenerationTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalMap()
        {
            var a = new World(new WorldConfig { Seed = 42 });
            var b = new World(new WorldConfig { Seed = 42 });
            for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
                Assert.AreEqual(a.GetTile(new GridPos(x, y)), b.GetTile(new GridPos(x, y)), $"tile {x},{y}");
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentMaps()
        {
            var a = new World(new WorldConfig { Seed = 1 });
            var b = new World(new WorldConfig { Seed = 2 });
            int differences = 0;
            for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
                if (a.GetTile(new GridPos(x, y)) != b.GetTile(new GridPos(x, y))) differences++;
            Assert.Greater(differences, 0);
        }

        [Test]
        public void DefaultMap_Is64x64_WithOreNearSpawn()
        {
            var world = new World(new WorldConfig());
            Assert.AreEqual(64, world.Width);
            Assert.AreEqual(64, world.Height);

            var spawn = world.Config.PlayerSpawn;
            int oreNearSpawn = 0;
            for (int dy = -8; dy <= 8; dy++)
            for (int dx = -8; dx <= 8; dx++)
                if (world.GetTile(new GridPos(spawn.X + dx, spawn.Y + dy)) == TileType.IronOre) oreNearSpawn++;
            Assert.Greater(oreNearSpawn, 0);
        }

        [Test]
        public void DefaultMap_HasCoalNearSpawn()
        {
            var world = new World(new WorldConfig());
            var spawn = world.Config.PlayerSpawn;
            int coalNearSpawn = 0;
            for (int dy = -10; dy <= 10; dy++)
            for (int dx = -10; dx <= 10; dx++)
                if (world.GetTile(new GridPos(spawn.X + dx, spawn.Y + dy)) == TileType.CoalOre) coalNearSpawn++;
            Assert.Greater(coalNearSpawn, 0);
        }

        [Test]
        public void AddingCoal_DoesNotMoveTheIron()
        {
            var withCoal = new World(new WorldConfig());
            var ironOnly = new World(new WorldConfig { CoalPatchCount = 0 });
            for (int y = 0; y < withCoal.Height; y++)
            for (int x = 0; x < withCoal.Width; x++)
            {
                var p = new GridPos(x, y);
                bool iron = ironOnly.GetTile(p) == TileType.IronOre;
                Assert.AreEqual(iron, withCoal.GetTile(p) == TileType.IronOre, $"tile {p}");
            }
        }

        [Test]
        public void Tick_AdvancesTickCount()
        {
            var world = World.CreateEmpty(4, 4);
            world.Tick();
            world.Tick();
            Assert.AreEqual(2, world.TickCount);
        }
    }
}
