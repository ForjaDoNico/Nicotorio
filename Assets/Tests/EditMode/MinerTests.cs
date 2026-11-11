using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    public class MinerTests
    {
        private static readonly GridPos OreTile = new GridPos(2, 2);
        private static readonly GridPos Front = new GridPos(3, 2); // OreTile facing Right

        private static World MakeWorld(int ticksPerItem = 20)
        {
            var config = new WorldConfig { Width = 10, Height = 10, OrePatchCount = 0, MinerTicksPerItem = ticksPerItem };
            var world = new World(config, new TileType[config.Width * config.Height]);
            world.SetTile(OreTile, TileType.IronOre);
            return world;
        }

        private static void Ticks(World world, int n)
        {
            for (int i = 0; i < n; i++) world.Tick();
        }

        private static Chest ChestAt(World world, GridPos pos) => (Chest)world.GetBuilding(pos);

        [Test]
        public void Miner_ProducesOneOrePerConfiguredTicks()
        {
            var world = MakeWorld(ticksPerItem: 20);
            world.TryPlace(BuildingType.Miner, OreTile, Direction.Right);
            world.TryPlace(BuildingType.Chest, Front, Direction.Up);
            var chest = ChestAt(world, Front);

            Ticks(world, 19);
            Assert.AreEqual(0, chest.Contents.Count(ItemType.IronOre), "nothing before N ticks");
            Ticks(world, 1);
            Assert.AreEqual(1, chest.Contents.Count(ItemType.IronOre), "first item exactly at N ticks");
            Ticks(world, 20 * 9);
            Assert.AreEqual(10, chest.Contents.Count(ItemType.IronOre), "1 item every N ticks");
        }

        [Test]
        public void Miner_RateFollowsConfig()
        {
            var world = MakeWorld(ticksPerItem: 5);
            world.TryPlace(BuildingType.Miner, OreTile, Direction.Right);
            world.TryPlace(BuildingType.Chest, Front, Direction.Up);

            Ticks(world, 5 * 7 + 4);
            Assert.AreEqual(7, ChestAt(world, Front).Contents.Count(ItemType.IronOre));
        }

        [Test]
        public void Miner_DeliversOnlyToTheTileInFront()
        {
            var world = MakeWorld();
            world.TryPlace(BuildingType.Miner, OreTile, Direction.Up);
            world.TryPlace(BuildingType.Chest, Front, Direction.Up); // to the right, not in front

            Ticks(world, 100);
            Assert.AreEqual(0, ChestAt(world, Front).Contents.Total);
        }

        [Test]
        public void Miner_WithNowhereToDeliver_HoldsOneItemAndStalls()
        {
            var world = MakeWorld(ticksPerItem: 20);
            world.TryPlace(BuildingType.Miner, OreTile, Direction.Right);
            var miner = (Miner)world.GetBuilding(OreTile);

            Ticks(world, 200);
            Assert.AreEqual(ItemType.IronOre, miner.Output);

            // Unblocking delivers the held item on the next tick; digging then resumes from zero.
            world.TryPlace(BuildingType.Chest, Front, Direction.Up);
            Ticks(world, 1);
            Assert.AreEqual(1, ChestAt(world, Front).Contents.Count(ItemType.IronOre), "held item, not a backlog");
            Assert.AreEqual(ItemType.None, miner.Output);
            Ticks(world, 20);
            Assert.AreEqual(2, ChestAt(world, Front).Contents.Count(ItemType.IronOre));
        }

        [Test]
        public void RemovingAMiner_GivesItsHeldItemToThePlayer()
        {
            var world = MakeWorld(ticksPerItem: 20);
            world.TryPlace(BuildingType.Miner, OreTile, Direction.Right);
            Ticks(world, 20);

            Assert.IsTrue(world.TryRemove(OreTile));
            Assert.AreEqual(1, world.PlayerInventory.Count(ItemType.IronOre));
        }
    }
}
