using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    public class CoalTests
    {
        private static readonly GridPos FurnaceTile = new GridPos(5, 5);
        private static readonly GridPos Front = new GridPos(6, 5); // furnace faces Right

        private static World MakeWorld()
        {
            // Default furnace numbers: 40 ticks per plate, 160 ticks per coal.
            var config = new WorldConfig
            {
                Width = 16, Height = 10, OrePatchCount = 0, CoalPatchCount = 0,
                BuildCosts = TestWorlds.NoCosts, BeltSpeed = 100, BeltItemSpacing = 250,
            };
            var world = new World(config, new TileType[config.Width * config.Height]);
            world.TryPlace(BuildingType.Furnace, FurnaceTile, Direction.Right);
            return world;
        }

        private static Furnace TheFurnace(World w) => (Furnace)w.GetBuilding(FurnaceTile);

        private static void Insert(World world, ItemType item, int n)
        {
            for (int i = 0; i < n; i++) Assert.IsTrue(world.TryInsert(FurnaceTile, item), $"{item} #{i}");
        }

        private static void Ticks(World world, int n)
        {
            for (int i = 0; i < n; i++) world.Tick();
        }

        [Test]
        public void Furnace_WithoutFuel_DoesNotSmelt()
        {
            var world = MakeWorld();
            Insert(world, ItemType.IronOre, 5);
            Ticks(world, 1000);

            var furnace = TheFurnace(world);
            Assert.IsTrue(furnace.Output.IsEmpty);
            Assert.AreEqual(5, furnace.Input.Count);
            Assert.AreEqual(0, furnace.Progress);
        }

        [Test]
        public void OneCoal_SmeltsExactlyFourPlates()
        {
            var world = MakeWorld();
            world.TryPlace(BuildingType.Chest, Front, Direction.Up);
            Insert(world, ItemType.IronOre, 10);
            Insert(world, ItemType.Coal, 1);

            Ticks(world, 2000);
            Assert.AreEqual(4, ((Chest)world.GetBuilding(Front)).Contents.Count(ItemType.IronPlate));
            Assert.AreEqual(6, TheFurnace(world).Input.Count);
            Assert.IsTrue(TheFurnace(world).Fuel.IsEmpty);
            Assert.AreEqual(0, TheFurnace(world).BurnTicksLeft);
        }

        [Test]
        public void Fuel_BurnsOnlyWhileSmelting()
        {
            var world = MakeWorld();
            world.TryPlace(BuildingType.Chest, Front, Direction.Up);
            Insert(world, ItemType.Coal, 1);

            Ticks(world, 500);
            Assert.AreEqual(1, TheFurnace(world).Fuel.Count, "nothing to smelt: coal is not lit");
            Assert.AreEqual(0, TheFurnace(world).BurnTicksLeft);

            Insert(world, ItemType.IronOre, 1);
            Ticks(world, 40);
            Assert.AreEqual(1, ((Chest)world.GetBuilding(Front)).Contents.Count(ItemType.IronPlate));
            Assert.IsTrue(TheFurnace(world).Fuel.IsEmpty, "the coal was lit");
            Assert.AreEqual(160 - 40, TheFurnace(world).BurnTicksLeft);

            Ticks(world, 500);
            Assert.AreEqual(120, TheFurnace(world).BurnTicksLeft, "idle furnace keeps its burn time");
        }

        [Test]
        public void Coal_GoesToTheFuelSlot_AndIsRefusedWhenFull()
        {
            var world = MakeWorld();
            Insert(world, ItemType.Coal, 10);
            Assert.IsFalse(world.TryInsert(FurnaceTile, ItemType.Coal));
            Assert.AreEqual(10, TheFurnace(world).Fuel.Count);
            Assert.IsTrue(TheFurnace(world).Input.IsEmpty, "coal is not smelted");
            Assert.IsTrue(world.TryInsert(FurnaceTile, ItemType.IronOre), "ore still has room");
        }

        [Test]
        public void SeparateOreAndCoalLines_KeepAFurnaceRunning()
        {
            // iron miner (1,2) -> belt (2,2) -> belt (3,2) -> furnace (4,2) -> chest (5,2)
            // coal miner (4,4) -> belt (4,3) going down into the furnace from below.
            var world = MakeWorld();
            world.SetTile(new GridPos(1, 2), TileType.IronOre);
            world.SetTile(new GridPos(4, 4), TileType.CoalOre);
            world.TryPlace(BuildingType.Miner, new GridPos(1, 2), Direction.Right);
            world.TryPlace(BuildingType.Belt, new GridPos(2, 2), Direction.Right);
            world.TryPlace(BuildingType.Belt, new GridPos(3, 2), Direction.Right);
            world.TryPlace(BuildingType.Miner, new GridPos(4, 4), Direction.Down);
            world.TryPlace(BuildingType.Belt, new GridPos(4, 3), Direction.Down);
            world.TryPlace(BuildingType.Furnace, new GridPos(4, 2), Direction.Right);
            world.TryPlace(BuildingType.Chest, new GridPos(5, 2), Direction.Up);

            // One minute. A plate every 2 s once ore and coal arrive (~1-2 s): about 29.
            // The coal line backs up once the fuel slot is full, which is fine: it has its own belt.
            Ticks(world, 20 * 60);
            var chest = (Chest)world.GetBuilding(new GridPos(5, 2));
            Assert.GreaterOrEqual(chest.Contents.Count(ItemType.IronPlate), 27);
            Assert.AreEqual(0, chest.Contents.Count(ItemType.Coal), "coal is burned, never passed through");
        }

        [Test]
        public void HandAndMiner_ProduceCoalFromCoalTiles()
        {
            var world = MakeWorld();
            var coalTile = new GridPos(1, 1);
            world.SetTile(coalTile, TileType.CoalOre);
            for (int i = 0; i < world.Config.HandMineTicksPerItem; i++)
            {
                world.MineStep(coalTile, new GridPos(2, 2));
                world.Tick();
            }
            Assert.AreEqual(1, world.PlayerInventory.Count(ItemType.Coal));

            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Miner, coalTile, Direction.Right));
            world.TryPlace(BuildingType.Chest, new GridPos(2, 1), Direction.Up);
            Ticks(world, world.Config.MinerTicksPerItem * 3);
            Assert.AreEqual(3, ((Chest)world.GetBuilding(new GridPos(2, 1))).Contents.Count(ItemType.Coal));
        }

        [Test]
        public void LoadAndTake_MoveOnlyTheRightItems()
        {
            var world = MakeWorld();
            world.PlayerInventory.Add(ItemType.IronOre, 4);
            world.PlayerInventory.Add(ItemType.Coal, 15);
            world.PlayerInventory.Add(ItemType.IronPlate, 2);

            Assert.AreEqual(10, world.LoadFurnaceFuel(FurnaceTile), "coal up to the fuel capacity");
            Assert.AreEqual(5, world.PlayerInventory.Count(ItemType.Coal));
            Assert.AreEqual(4, world.PlayerInventory.Count(ItemType.IronOre), "fuel load leaves ore alone");

            Assert.AreEqual(4, world.LoadFurnaceInput(FurnaceTile));
            Assert.AreEqual(5, world.PlayerInventory.Count(ItemType.Coal), "input load leaves coal alone");
            Assert.AreEqual(2, world.PlayerInventory.Count(ItemType.IronPlate), "plates are never loaded");

            Assert.AreEqual(0, world.TakeFurnaceOutput(FurnaceTile), "nothing smelted yet");
            Ticks(world, 40 * 3);
            Assert.AreEqual(3, world.TakeFurnaceOutput(FurnaceTile));
            Assert.AreEqual(5, world.PlayerInventory.Count(ItemType.IronPlate));
            Assert.AreEqual(0, world.LoadFurnaceFuel(new GridPos(0, 0)), "no furnace there");
        }

        [Test]
        public void RemovingAFurnace_ReturnsItsFuel()
        {
            var world = MakeWorld();
            Insert(world, ItemType.Coal, 6);
            Assert.IsTrue(world.TryRemove(FurnaceTile));
            Assert.AreEqual(6, world.PlayerInventory.Count(ItemType.Coal));
        }
    }
}
