using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    public class FurnaceTests
    {
        private static readonly GridPos FurnaceTile = new GridPos(5, 5);
        private static readonly GridPos Front = new GridPos(6, 5); // furnace faces Right

        private static WorldConfig Config(int ticks = 40, int inCap = 10, int outCap = 10) => new WorldConfig
        {
            Width = 16, Height = 10, OrePatchCount = 0,
            FurnaceTicksPerItem = ticks, FurnaceInputCapacity = inCap, FurnaceOutputCapacity = outCap,
            MinerTicksPerItem = 20, BeltSpeed = 100, BeltItemSpacing = 250,
                FreeBuilding = true,
        };

        private static World MakeWorld(WorldConfig config = null)
        {
            config = config ?? Config();
            var world = new World(config, new TileType[config.Width * config.Height]);
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Furnace, FurnaceTile, Direction.Right));
            // These tests are about smelting, so the furnace starts with a full fuel slot
            // (10 coal = 40 plates, more than any test here smelts). Fuel has its own tests.
            AddCoal(world, FurnaceTile, 10);
            return world;
        }

        private static void AddCoal(World world, GridPos furnace, int coal)
        {
            for (int i = 0; i < coal; i++) Assert.IsTrue(world.TryInsert(furnace, ItemType.Coal));
        }

        private static Furnace TheFurnace(World w) => (Furnace)w.GetBuilding(FurnaceTile);

        private static void Ticks(World world, int n)
        {
            for (int i = 0; i < n; i++) world.Tick();
        }

        private static void Feed(World world, int ore)
        {
            for (int i = 0; i < ore; i++) Assert.IsTrue(world.TryInsert(FurnaceTile, ItemType.IronOre));
        }

        [Test]
        public void Furnace_SmeltsOnePlatePerConfiguredTicks()
        {
            var world = MakeWorld(Config(ticks: 40));
            world.TryPlace(BuildingType.Chest, Front, Direction.Up);
            var chest = (Chest)world.GetBuilding(Front);
            Feed(world, 10);

            Ticks(world, 39);
            Assert.AreEqual(0, chest.Contents.Total, "nothing before N ticks");
            Ticks(world, 1);
            Assert.AreEqual(1, chest.Contents.Count(ItemType.IronPlate), "first plate exactly at N ticks");
            Ticks(world, 40 * 9);
            Assert.AreEqual(10, chest.Contents.Count(ItemType.IronPlate));
            Assert.AreEqual(0, chest.Contents.Count(ItemType.IronOre));
            Assert.IsTrue(TheFurnace(world).Input.IsEmpty);
        }

        [Test]
        public void Furnace_WithoutOre_DoesNothing()
        {
            var world = MakeWorld();
            Ticks(world, 500);
            Assert.AreEqual(0, TheFurnace(world).Progress);
            Assert.IsTrue(TheFurnace(world).Output.IsEmpty);
        }

        [Test]
        public void Furnace_StopsWhenOutputIsFull()
        {
            var world = MakeWorld(Config(ticks: 10, inCap: 10, outCap: 3));
            Feed(world, 10); // nothing in front, so plates pile up in the output

            Ticks(world, 1000);
            var furnace = TheFurnace(world);
            Assert.AreEqual(3, furnace.Output.Count);
            Assert.AreEqual(ItemType.IronPlate, furnace.Output.Item);
            Assert.AreEqual(7, furnace.Input.Count, "no ore consumed once the output is full");
            Assert.AreEqual(0, furnace.Progress, "stalled, not secretly smelting");

            // Making room lets it continue.
            world.TryPlace(BuildingType.Chest, Front, Direction.Up);
            Ticks(world, 10 * 7 + 10);
            Assert.AreEqual(10, ((Chest)world.GetBuilding(Front)).Contents.Count(ItemType.IronPlate));
        }

        [Test]
        public void Furnace_RefusesItemsThatAreNotOre()
        {
            var world = MakeWorld();
            Assert.IsFalse(world.TryInsert(FurnaceTile, ItemType.IronPlate));
            Assert.IsTrue(TheFurnace(world).Input.IsEmpty);
        }

        [Test]
        public void Furnace_RefusesOreWhenInputIsFull()
        {
            var world = MakeWorld(Config(inCap: 4));
            Feed(world, 4);
            Assert.IsFalse(world.TryInsert(FurnaceTile, ItemType.IronOre));
            Assert.AreEqual(4, TheFurnace(world).Input.Count);
        }

        [Test]
        public void MinerToFurnaceToBeltToChest_DeliversPlates()
        {
            // ore(2,5) miner -> furnace(3,5) -> belt(4,5) -> chest(5,5)... uses its own layout.
            var config = Config(ticks: 40);
            var world = new World(config, new TileType[config.Width * config.Height]);
            var ore = new GridPos(1, 2);
            world.SetTile(ore, TileType.IronOre);
            world.TryPlace(BuildingType.Miner, ore, Direction.Right);
            world.TryPlace(BuildingType.Furnace, new GridPos(2, 2), Direction.Right);
            AddCoal(world, new GridPos(2, 2), 3); // 12 plates of burn time
            world.TryPlace(BuildingType.Belt, new GridPos(3, 2), Direction.Right);
            world.TryPlace(BuildingType.Belt, new GridPos(4, 2), Direction.Right);
            world.TryPlace(BuildingType.Chest, new GridPos(5, 2), Direction.Up);

            // The furnace (2 s/plate) is slower than the miner (1 s/ore), so it limits the line:
            // plate k leaves the furnace at about tick 20 + 40k and needs ~20 ticks on the belts.
            Ticks(world, 20 + 40 * 10 + 30);
            var chest = (Chest)world.GetBuilding(new GridPos(5, 2));
            Assert.AreEqual(10, chest.Contents.Count(ItemType.IronPlate));
            Assert.AreEqual(0, chest.Contents.Count(ItemType.IronOre), "ore never skips the furnace");
        }

        [Test]
        public void RemovingAFurnace_GivesInputAndOutputToThePlayer()
        {
            var world = MakeWorld(Config(ticks: 10));
            Feed(world, 6);
            Ticks(world, 20); // 2 plates done, 4 ore left

            Assert.IsTrue(world.TryRemove(FurnaceTile));
            Assert.AreEqual(4, world.PlayerInventory.Count(ItemType.IronOre));
            Assert.AreEqual(2, world.PlayerInventory.Count(ItemType.IronPlate));
        }

        [Test]
        public void Interact_LoadsPlayerOreAndTakesPlates()
        {
            var world = MakeWorld(Config(ticks: 10, inCap: 10));
            world.PlayerInventory.Add(ItemType.IronOre, 25);
            world.PlayerInventory.Add(ItemType.IronPlate, 1);

            Assert.AreEqual(10, world.InteractWithFurnace(FurnaceTile), "loads up to the input capacity");
            Assert.AreEqual(15, world.PlayerInventory.Count(ItemType.IronOre));
            Assert.AreEqual(10, TheFurnace(world).Input.Count);

            Ticks(world, 30); // 3 plates
            Assert.AreEqual(3 + 3, world.InteractWithFurnace(FurnaceTile), "3 plates out, 3 ore topped up");
            Assert.AreEqual(4, world.PlayerInventory.Count(ItemType.IronPlate));
            Assert.AreEqual(12, world.PlayerInventory.Count(ItemType.IronOre));
            Assert.AreEqual(10, TheFurnace(world).Input.Count);
            Assert.IsTrue(TheFurnace(world).Output.IsEmpty);

            Assert.AreEqual(0, world.InteractWithFurnace(new GridPos(0, 0)), "no furnace there");
        }

        [Test]
        public void Interact_DoesNotPutPlatesIntoTheFurnace()
        {
            var world = MakeWorld();
            world.PlayerInventory.Add(ItemType.IronPlate, 5);
            Assert.AreEqual(0, world.InteractWithFurnace(FurnaceTile));
            Assert.AreEqual(5, world.PlayerInventory.Count(ItemType.IronPlate));
        }
    }
}
