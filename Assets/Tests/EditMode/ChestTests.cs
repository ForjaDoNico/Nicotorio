using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    public class ChestTests
    {
        private static readonly GridPos ChestTile = new GridPos(5, 5);

        private static World MakeWorld()
        {
            var config = new WorldConfig
            {
                Width = 12, Height = 12, OrePatchCount = 0,
                MinerTicksPerItem = 20, BeltSpeed = 100, BeltItemSpacing = 250,
            };
            var world = new World(config, new TileType[config.Width * config.Height]);
            world.TryPlace(BuildingType.Chest, ChestTile, Direction.Up);
            return world;
        }

        private static Chest TheChest(World w) => (Chest)w.GetBuilding(ChestTile);

        private static void Ticks(World world, int n)
        {
            for (int i = 0; i < n; i++) world.Tick();
        }

        [Test]
        public void Chest_AccumulatesEverythingItReceives()
        {
            var world = MakeWorld();
            for (int i = 0; i < 1000; i++)
                Assert.IsTrue(world.TryInsert(ChestTile, ItemType.IronOre), "a chest never refuses");

            Assert.AreEqual(1000, TheChest(world).Contents.Count(ItemType.IronOre));
        }

        [Test]
        public void Chest_AccumulatesFromSeveralInputsAtOnce()
        {
            // Miner directly below, and a miner -> belt from the left.
            var world = MakeWorld();
            var below = new GridPos(5, 4);
            var farLeft = new GridPos(3, 5);
            world.SetTile(below, TileType.IronOre);
            world.SetTile(farLeft, TileType.IronOre);
            world.TryPlace(BuildingType.Miner, below, Direction.Up);
            world.TryPlace(BuildingType.Miner, farLeft, Direction.Right);
            world.TryPlace(BuildingType.Belt, new GridPos(4, 5), Direction.Right);

            Ticks(world, 20 * 10);
            // Direct miner: 10 items. Belted miner: item 10 dug at tick 200 is still on the belt.
            Assert.AreEqual(19, TheChest(world).Contents.Count(ItemType.IronOre));
            Ticks(world, 20);
            Assert.AreEqual(21, TheChest(world).Contents.Count(ItemType.IronOre));
        }

        [Test]
        public void TakeAll_MovesChestContentsToThePlayer()
        {
            var world = MakeWorld();
            for (int i = 0; i < 7; i++) world.TryInsert(ChestTile, ItemType.IronOre);
            world.PlayerInventory.Add(ItemType.IronOre, 3);

            Assert.AreEqual(7, world.TakeAllFromChest(ChestTile));
            Assert.AreEqual(0, TheChest(world).Contents.Total);
            Assert.AreEqual(10, world.PlayerInventory.Count(ItemType.IronOre));

            Assert.AreEqual(0, world.TakeAllFromChest(ChestTile), "empty chest gives nothing");
            Assert.AreEqual(0, world.TakeAllFromChest(new GridPos(1, 1)), "no chest there");
        }

        [Test]
        public void Chest_KeepsAccumulatingAfterBeingEmptied()
        {
            var world = MakeWorld();
            var ore = new GridPos(5, 4);
            world.SetTile(ore, TileType.IronOre);
            world.TryPlace(BuildingType.Miner, ore, Direction.Up);

            Ticks(world, 60);
            Assert.AreEqual(3, world.TakeAllFromChest(ChestTile));
            Ticks(world, 40);
            Assert.AreEqual(2, TheChest(world).Contents.Count(ItemType.IronOre));
            Assert.AreEqual(3, world.PlayerInventory.Count(ItemType.IronOre));
        }
    }
}
