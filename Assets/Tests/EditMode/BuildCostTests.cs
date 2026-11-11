using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    /// <summary>
    /// Uses the real default config: the crafting table costs 5 wood, every other building is
    /// placed from its own crafted item, and removal gives the cost back.
    /// </summary>
    public class BuildCostTests
    {
        private static readonly GridPos OreTile = new GridPos(4, 4);
        private static readonly GridPos GrassTile = new GridPos(8, 8);

        private static World MakeWorld(params ItemAmount[] start)
        {
            var config = new WorldConfig { Width = 16, Height = 16, OrePatchCount = 0, StartingInventory = start };
            var world = new World(config, new TileType[config.Width * config.Height]);
            world.SetTile(OreTile, TileType.IronOre);
            return world;
        }

        private static int Count(World w, ItemType item) => w.PlayerInventory.Count(item);

        [Test]
        public void DefaultCosts_TableTakesWood_OthersTakeTheirOwnItem()
        {
            var world = MakeWorld(new ItemAmount(ItemType.Wood, 5),
                new ItemAmount(ItemType.Miner, 1), new ItemAmount(ItemType.Belt, 1),
                new ItemAmount(ItemType.Chest, 1), new ItemAmount(ItemType.Furnace, 1));

            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.CraftingTable, new GridPos(1, 1), Direction.Up));
            Assert.AreEqual(0, Count(world, ItemType.Wood), "table = 5 wood");
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Miner, OreTile, Direction.Up));
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Belt, new GridPos(2, 2), Direction.Up));
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Chest, new GridPos(3, 3), Direction.Up));
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Furnace, new GridPos(5, 5), Direction.Up));
            Assert.AreEqual(0, world.PlayerInventory.Total, "each building took exactly its one item");
        }

        [Test]
        public void StartingInventory_IsGivenToThePlayer()
        {
            var world = MakeWorld(new ItemAmount(ItemType.IronPlate, 7));
            Assert.AreEqual(7, Count(world, ItemType.IronPlate));
            Assert.AreEqual(0, new World(new WorldConfig()).PlayerInventory.Total, "default start is empty");
        }

        [Test]
        public void Placing_SpendsOneBuildingItem()
        {
            var world = MakeWorld(new ItemAmount(ItemType.Miner, 2));
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Miner, OreTile, Direction.Up));
            Assert.AreEqual(1, Count(world, ItemType.Miner));
        }

        [Test]
        public void CanPlace_DoesNotSpendAnything()
        {
            var world = MakeWorld(new ItemAmount(ItemType.Miner, 1));
            Assert.AreEqual(PlaceResult.Ok, world.CanPlace(BuildingType.Miner, OreTile));
            Assert.AreEqual(1, Count(world, ItemType.Miner));
        }

        [Test]
        public void WithoutTheItem_IsRefused_AndNothingChanges()
        {
            // Plenty of raw materials, but no crafted building items and too little wood for a table.
            var world = MakeWorld(new ItemAmount(ItemType.IronPlate, 50), new ItemAmount(ItemType.IronOre, 50),
                new ItemAmount(ItemType.Wood, 4));
            int version = world.BuildingsVersion;

            Assert.AreEqual(PlaceResult.NotEnoughItems, world.TryPlace(BuildingType.Miner, OreTile, Direction.Up));
            Assert.AreEqual(PlaceResult.NotEnoughItems, world.TryPlace(BuildingType.Furnace, GrassTile, Direction.Up));
            Assert.AreEqual(PlaceResult.NotEnoughItems, world.TryPlace(BuildingType.CraftingTable, GrassTile, Direction.Up));
            Assert.AreEqual(PlaceResult.NotEnoughItems, world.CanPlace(BuildingType.Belt, GrassTile));

            Assert.AreEqual(50, Count(world, ItemType.IronPlate));
            Assert.AreEqual(50, Count(world, ItemType.IronOre));
            Assert.AreEqual(4, Count(world, ItemType.Wood));
            Assert.AreEqual(0, world.Buildings.Count);
            Assert.AreEqual(version, world.BuildingsVersion);
        }

        [Test]
        public void LocationProblems_AreReportedBeforeMissingItems()
        {
            var world = MakeWorld();
            Assert.AreEqual(PlaceResult.RequiresOre, world.CanPlace(BuildingType.Miner, GrassTile));
            Assert.AreEqual(PlaceResult.OutOfBounds, world.CanPlace(BuildingType.Belt, new GridPos(-1, 0)));
        }

        [Test]
        public void Removing_GivesTheItemBack_PlusWhatItHeld()
        {
            var world = MakeWorld(new ItemAmount(ItemType.Furnace, 1), new ItemAmount(ItemType.IronOre, 3),
                new ItemAmount(ItemType.Wood, 5));
            world.TryPlace(BuildingType.Furnace, GrassTile, Direction.Up);
            world.LoadFurnaceInput(GrassTile);
            world.TryPlace(BuildingType.CraftingTable, new GridPos(1, 1), Direction.Up);
            Assert.AreEqual(0, world.PlayerInventory.Total);

            Assert.IsTrue(world.TryRemove(GrassTile));
            Assert.AreEqual(1, Count(world, ItemType.Furnace), "the furnace item comes back");
            Assert.AreEqual(3, Count(world, ItemType.IronOre), "and so does its input");
            Assert.IsTrue(world.TryRemove(new GridPos(1, 1)));
            Assert.AreEqual(5, Count(world, ItemType.Wood), "the table refunds its wood");
        }

        [Test]
        public void FreeBuilding_CostsNothing_AndRefundsNothing()
        {
            var config = new WorldConfig { Width = 8, Height = 8, OrePatchCount = 0, FreeBuilding = true };
            var world = new World(config, new TileType[64]);
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Chest, new GridPos(1, 1), Direction.Up));
            Assert.IsTrue(world.TryRemove(new GridPos(1, 1)));
            Assert.AreEqual(0, world.PlayerInventory.Total);
        }

        [Test]
        public void FullLoop_FromEmptyInventory_ToAPlacedMiner_UsingOnlyWoodAsFuel()
        {
            // Default config, empty start. Trees, ore and the build spots are all within reach.
            var world = MakeWorld();
            var player = new GridPos(5, 5);
            var trees = new[] { new GridPos(6, 4), new GridPos(6, 6), new GridPos(4, 6), new GridPos(3, 5), new GridPos(7, 5) };
            foreach (GridPos t in trees) world.SetTile(t, TileType.Tree);
            var table = new GridPos(5, 7);
            var furnace = new GridPos(7, 7);
            var recipes = world.Config;
            Assert.AreEqual(0, world.PlayerInventory.Total);

            // 1. Chop 5 wood, build the crafting table.
            Chop(world, player, trees, 5);
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.CraftingTable, table, Direction.Up));

            // 2. 5 ore + 2 wood -> a furnace, and place it.
            HandMine(world, player, 5);
            Chop(world, player, trees, 2);
            Assert.AreEqual(1, world.TryCraft(table, player, 3, 1));
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Furnace, furnace, Direction.Up));

            // 3. Smelt 8 plates with wood only: 4 wood x 2 plates each.
            HandMine(world, player, 8);
            Assert.AreEqual(8, world.LoadFurnaceInput(furnace));
            Chop(world, player, trees, 4);
            Assert.AreEqual(4, world.LoadFurnaceFuel(furnace));
            Assert.AreEqual(0, Count(world, ItemType.Coal));
            for (int i = 0; i < 8 * recipes.FurnaceTicksPerItem; i++) world.Tick();
            Assert.AreEqual(8, world.TakeFurnaceOutput(furnace));

            // 4. 3 gears (6 plates), then the miner: 3 gears + 2 plates + 2 wood.
            Assert.AreEqual(3, world.TryCraft(table, player, 1, 3));
            Chop(world, player, trees, 2);
            Assert.AreEqual(1, world.TryCraft(table, player, 5, 1));
            Assert.AreEqual(1, Count(world, ItemType.Miner));

            // 5. Place it.
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Miner, OreTile, Direction.Right));
            Assert.AreEqual(BuildingType.Miner, world.GetBuilding(OreTile).Type);
            Assert.AreEqual(0, Count(world, ItemType.Miner));
        }

        private static void HandMine(World world, GridPos player, int count) =>
            Gather(world, player, new[] { OreTile }, ItemType.IronOre, count);

        private static void Chop(World world, GridPos player, GridPos[] trees, int count) =>
            Gather(world, player, trees, ItemType.Wood, count);

        /// <summary>Holds mine on the first tile that still yields item until count more arrive.</summary>
        private static void Gather(World world, GridPos player, GridPos[] tiles, ItemType item, int count)
        {
            int target = Count(world, item) + count;
            for (int guard = 0; Count(world, item) < target; guard++)
            {
                Assert.Less(guard, 20000, $"never got enough {item}");
                GridPos tile = tiles[0];
                foreach (GridPos t in tiles)
                    if (TileYield.ForHand(world.GetTile(t)) == item) { tile = t; break; }
                world.MineStep(tile, player);
                world.Tick();
            }
        }
    }
}
