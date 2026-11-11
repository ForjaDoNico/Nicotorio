using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    /// <summary>Uses the real default cost table from WorldConfig.</summary>
    public class BuildCostTests
    {
        private static readonly GridPos OreTile = new GridPos(4, 4);
        private static readonly GridPos GrassTile = new GridPos(8, 8);

        private static World MakeWorld(params ItemAmount[] start)
        {
            var config = new WorldConfig { Width = 12, Height = 12, OrePatchCount = 0, StartingInventory = start };
            var world = new World(config, new TileType[config.Width * config.Height]);
            world.SetTile(OreTile, TileType.IronOre);
            return world;
        }

        private static int Plates(World w) => w.PlayerInventory.Count(ItemType.IronPlate);
        private static int Ore(World w) => w.PlayerInventory.Count(ItemType.IronOre);

        [Test]
        public void DefaultCostTable_IsTheDocumentedOne()
        {
            var world = MakeWorld(new ItemAmount(ItemType.IronOre, 100), new ItemAmount(ItemType.IronPlate, 100));
            world.TryPlace(BuildingType.Furnace, new GridPos(1, 1), Direction.Up);
            Assert.AreEqual(95, Ore(world), "furnace = 5 ore");
            world.TryPlace(BuildingType.Miner, OreTile, Direction.Up);
            Assert.AreEqual(97, Plates(world), "miner = 3 plates");
            world.TryPlace(BuildingType.Belt, new GridPos(2, 2), Direction.Up);
            Assert.AreEqual(96, Plates(world), "belt = 1 plate");
            world.TryPlace(BuildingType.Chest, new GridPos(3, 3), Direction.Up);
            Assert.AreEqual(94, Plates(world), "chest = 2 plates");
        }

        [Test]
        public void StartingInventory_IsGivenToThePlayer()
        {
            var world = MakeWorld(new ItemAmount(ItemType.IronPlate, 7));
            Assert.AreEqual(7, Plates(world));
            Assert.AreEqual(0, new World(new WorldConfig()).PlayerInventory.Total, "default start is empty");
        }

        [Test]
        public void Building_DeductsTheCost()
        {
            var world = MakeWorld(new ItemAmount(ItemType.IronPlate, 10));
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Miner, OreTile, Direction.Up));
            Assert.AreEqual(7, Plates(world));
        }

        [Test]
        public void CanPlace_DoesNotSpendAnything()
        {
            var world = MakeWorld(new ItemAmount(ItemType.IronPlate, 3));
            Assert.AreEqual(PlaceResult.Ok, world.CanPlace(BuildingType.Miner, OreTile));
            Assert.AreEqual(3, Plates(world));
        }

        [Test]
        public void WithoutEnoughItems_IsRefused_AndNothingChanges()
        {
            var world = MakeWorld(new ItemAmount(ItemType.IronPlate, 2), new ItemAmount(ItemType.IronOre, 4));
            int version = world.BuildingsVersion;

            Assert.AreEqual(PlaceResult.NotEnoughItems, world.TryPlace(BuildingType.Miner, OreTile, Direction.Up));
            Assert.AreEqual(PlaceResult.NotEnoughItems, world.TryPlace(BuildingType.Furnace, GrassTile, Direction.Up));
            Assert.AreEqual(PlaceResult.NotEnoughItems, world.CanPlace(BuildingType.Miner, OreTile));

            Assert.AreEqual(2, Plates(world));
            Assert.AreEqual(4, Ore(world));
            Assert.AreEqual(0, world.Buildings.Count);
            Assert.AreEqual(version, world.BuildingsVersion);
            Assert.IsNull(world.GetBuilding(OreTile));
        }

        [Test]
        public void LocationProblems_AreReportedBeforeMissingItems()
        {
            var world = MakeWorld();
            Assert.AreEqual(PlaceResult.RequiresOre, world.CanPlace(BuildingType.Miner, GrassTile));
            Assert.AreEqual(PlaceResult.OutOfBounds, world.CanPlace(BuildingType.Belt, new GridPos(-1, 0)));
        }

        [Test]
        public void Removing_RefundsTheCost_PlusWhatItHeld()
        {
            var world = MakeWorld(new ItemAmount(ItemType.IronOre, 8), new ItemAmount(ItemType.IronPlate, 2));
            world.TryPlace(BuildingType.Furnace, GrassTile, Direction.Up);  // -5 ore
            world.InteractWithFurnace(GrassTile);                            // -3 ore into the input
            Assert.AreEqual(0, Ore(world));
            world.TryPlace(BuildingType.Chest, new GridPos(1, 1), Direction.Up); // -2 plates
            Assert.AreEqual(0, Plates(world));

            Assert.IsTrue(world.TryRemove(GrassTile));
            Assert.AreEqual(5 + 3, Ore(world), "cost refunded + input returned");
            Assert.IsTrue(world.TryRemove(new GridPos(1, 1)));
            Assert.AreEqual(2, Plates(world));
        }

        [Test]
        public void FullLoop_FromEmptyInventory_ToABuiltMiner()
        {
            // Default config: empty start, real costs. Player stands next to the ore and the furnace spot.
            var world = MakeWorld();
            var player = new GridPos(5, 4);
            var furnaceTile = new GridPos(6, 4);
            Assert.AreEqual(0, world.PlayerInventory.Total);
            Assert.AreEqual(PlaceResult.NotEnoughItems, world.CanPlace(BuildingType.Furnace, furnaceTile));

            // 1. Mine 5 ore by hand and build a furnace with it.
            HandMine(world, player, 5);
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Furnace, furnaceTile, Direction.Up));
            Assert.AreEqual(0, Ore(world));

            // 2. Mine 3 more ore and load it into the furnace by hand.
            HandMine(world, player, 3);
            Assert.AreEqual(3, world.InteractWithFurnace(furnaceTile));

            // 3. Wait for 3 plates and take them.
            for (int i = 0; i < 3 * world.Config.FurnaceTicksPerItem; i++) world.Tick();
            Assert.AreEqual(3, world.InteractWithFurnace(furnaceTile));
            Assert.AreEqual(3, Plates(world));

            // 4. Build a miner with them.
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Miner, OreTile, Direction.Right));
            Assert.AreEqual(0, Plates(world));
            Assert.AreEqual(BuildingType.Miner, world.GetBuilding(OreTile).Type);
        }

        /// <summary>Holds mine on the ore tile until count more items arrive, like the player would.</summary>
        private static void HandMine(World world, GridPos player, int count)
        {
            int target = Ore(world) + count;
            for (int guard = 0; Ore(world) < target; guard++)
            {
                Assert.Less(guard, 10000, "hand mining never produced");
                world.MineStep(OreTile, player);
                world.Tick();
            }
        }
    }
}
