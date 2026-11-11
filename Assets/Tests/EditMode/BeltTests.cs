using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    public class BeltTests
    {
        // speed 100 and spacing 250 keep the arithmetic readable: 10 ticks per tile, 5 items per belt.
        private const int Speed = 100;
        private const int Spacing = 250;
        private const int Capacity = Belt.Length / Spacing + 1;

        private static World MakeWorld()
        {
            var config = new WorldConfig
            {
                Width = 16, Height = 8, OrePatchCount = 0,
                BeltSpeed = Speed, BeltItemSpacing = Spacing, MinerTicksPerItem = 20,
                BuildCosts = TestWorlds.NoCosts,
            };
            return new World(config, new TileType[config.Width * config.Height]);
        }

        private static GridPos At(int x) => new GridPos(x, 2);

        private static void Ticks(World world, int n)
        {
            for (int i = 0; i < n; i++) world.Tick();
        }

        private static Belt BeltAt(World w, GridPos p) => (Belt)w.GetBuilding(p);
        private static Chest ChestAt(World w, GridPos p) => (Chest)w.GetBuilding(p);

        [Test]
        public void Belt_MovesItemTowardItsFacing()
        {
            var world = MakeWorld();
            world.TryPlace(BuildingType.Belt, At(1), Direction.Right);
            Assert.IsTrue(world.TryInsert(At(1), ItemType.IronOre));

            Ticks(world, 3);
            Assert.AreEqual(1, BeltAt(world, At(1)).Items.Count);
            Assert.AreEqual(3 * Speed, BeltAt(world, At(1)).Items[0].Position);
        }

        [Test]
        public void Belt_DeliversItemIntoChestInFront()
        {
            var world = MakeWorld();
            world.TryPlace(BuildingType.Belt, At(1), Direction.Right);
            world.TryPlace(BuildingType.Chest, At(2), Direction.Up);
            world.TryInsert(At(1), ItemType.IronOre);

            Ticks(world, Belt.Length / Speed - 1);
            Assert.AreEqual(0, ChestAt(world, At(2)).Contents.Total, "still on the belt before reaching the edge");
            Ticks(world, 1);
            Assert.AreEqual(1, ChestAt(world, At(2)).Contents.Count(ItemType.IronOre));
            Assert.AreEqual(0, BeltAt(world, At(1)).Items.Count);
        }

        [Test]
        public void Belt_HandsItemsToTheNextBelt_AlongALine()
        {
            var world = MakeWorld();
            for (int x = 1; x <= 4; x++) world.TryPlace(BuildingType.Belt, At(x), Direction.Right);
            world.TryPlace(BuildingType.Chest, At(5), Direction.Up);
            world.TryInsert(At(1), ItemType.IronOre);

            Ticks(world, 4 * Belt.Length / Speed + 4); // 4 tiles + at most one tick of handoff lag per belt
            Assert.AreEqual(1, ChestAt(world, At(5)).Contents.Count(ItemType.IronOre));
            for (int x = 1; x <= 4; x++) Assert.AreEqual(0, BeltAt(world, At(x)).Items.Count);
        }

        [Test]
        public void Belt_KeepsSpacingAndRefusesWhenEntryIsTaken()
        {
            var world = MakeWorld();
            world.TryPlace(BuildingType.Belt, At(1), Direction.Right);

            Assert.IsTrue(world.TryInsert(At(1), ItemType.IronOre));
            Assert.IsFalse(world.TryInsert(At(1), ItemType.IronOre), "entry edge is occupied");
            Ticks(world, 2); // 200 < spacing
            Assert.IsFalse(world.TryInsert(At(1), ItemType.IronOre));
            Ticks(world, 1); // 300 >= spacing
            Assert.IsTrue(world.TryInsert(At(1), ItemType.IronOre));
        }

        [Test]
        public void FullBelt_BlocksTheBeltBehindIt()
        {
            // A -> B, and B points at bare grass, so B can never empty.
            var world = MakeWorld();
            world.TryPlace(BuildingType.Belt, At(1), Direction.Right); // A
            world.TryPlace(BuildingType.Belt, At(2), Direction.Right); // B
            Belt a = BeltAt(world, At(1)), b = BeltAt(world, At(2));

            for (int i = 0; i < 400; i++)
            {
                world.TryInsert(At(1), ItemType.IronOre); // feed A as fast as it accepts
                world.Tick();
            }

            Assert.AreEqual(Capacity, b.Items.Count, "B is full");
            Assert.AreEqual(Belt.Length, b.Items[0].Position, "B's front item waits at the edge");
            Assert.AreEqual(Capacity, a.Items.Count, "A backed up behind B");
            Assert.AreEqual(Belt.Length, a.Items[0].Position, "A's front item is stuck at the handoff");
            for (int i = 1; i < a.Items.Count; i++)
                Assert.GreaterOrEqual(a.Items[i - 1].Position - a.Items[i].Position, Spacing);
            Assert.IsFalse(world.TryInsert(At(1), ItemType.IronOre), "A refuses more");

            // Give B somewhere to go: both belts drain into the chest, nothing lost.
            world.TryPlace(BuildingType.Chest, At(3), Direction.Up);
            Ticks(world, 200);
            Assert.AreEqual(0, a.Items.Count);
            Assert.AreEqual(0, b.Items.Count);
            Assert.AreEqual(2 * Capacity, ChestAt(world, At(3)).Contents.Count(ItemType.IronOre));
        }

        [Test]
        public void Miner_FeedsBelt_IntoChest_EndToEnd()
        {
            var world = MakeWorld();
            world.SetTile(At(0), TileType.IronOre);
            world.TryPlace(BuildingType.Miner, At(0), Direction.Right);
            world.TryPlace(BuildingType.Belt, At(1), Direction.Right);
            world.TryPlace(BuildingType.Belt, At(2), Direction.Right);
            world.TryPlace(BuildingType.Chest, At(3), Direction.Up);

            // Item k is dug at tick 20k and needs ~20 ticks to cross two belts. At tick 230
            // items 1..10 have arrived and item 11 (dug at 220) is still riding.
            Ticks(world, 230);
            Assert.AreEqual(10, ChestAt(world, At(3)).Contents.Count(ItemType.IronOre));
            int riding = BeltAt(world, At(1)).Items.Count + BeltAt(world, At(2)).Items.Count;
            Assert.AreEqual(1, riding, "nothing lost or duplicated in transit");
        }

        [Test]
        public void RemovingABelt_GivesItsItemsToThePlayer()
        {
            var world = MakeWorld();
            world.TryPlace(BuildingType.Belt, At(1), Direction.Right);
            world.TryInsert(At(1), ItemType.IronOre);
            Ticks(world, 3);
            world.TryInsert(At(1), ItemType.IronOre);

            Assert.IsTrue(world.TryRemove(At(1)));
            Assert.AreEqual(2, world.PlayerInventory.Count(ItemType.IronOre));
        }
    }
}
