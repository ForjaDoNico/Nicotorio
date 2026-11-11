using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    public class PlacementTests
    {
        private static readonly GridPos OreTile = new GridPos(2, 2);
        private static readonly GridPos GrassTile = new GridPos(5, 5);

        private static World MakeWorld()
        {
            var world = TestWorlds.Empty(10, 10);
            world.SetTile(OreTile, TileType.IronOre);
            return world;
        }

        [Test]
        public void CannotBuildOnOccupiedTile()
        {
            var world = MakeWorld();
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Belt, GrassTile, Direction.Right));

            Assert.AreEqual(PlaceResult.Occupied, world.TryPlace(BuildingType.Chest, GrassTile, Direction.Up));
            Assert.AreEqual(PlaceResult.Occupied, world.TryPlace(BuildingType.Belt, GrassTile, Direction.Up));
            Assert.AreEqual(1, world.Buildings.Count);
            Assert.AreEqual(BuildingType.Belt, world.GetBuilding(GrassTile).Type);
            Assert.AreEqual(Direction.Right, world.GetBuilding(GrassTile).Facing);
        }

        [Test]
        public void MinerOnlyOnOre()
        {
            var world = MakeWorld();
            Assert.AreEqual(PlaceResult.RequiresOre, world.TryPlace(BuildingType.Miner, GrassTile, Direction.Up));
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Miner, OreTile, Direction.Up));
        }

        [Test]
        public void BeltAndChestCanGoOnOre()
        {
            var world = MakeWorld();
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Belt, OreTile, Direction.Up));
        }

        [Test]
        public void CannotBuildOutOfBounds()
        {
            var world = MakeWorld();
            Assert.AreEqual(PlaceResult.OutOfBounds, world.TryPlace(BuildingType.Chest, new GridPos(-1, 0), Direction.Up));
            Assert.AreEqual(PlaceResult.OutOfBounds, world.TryPlace(BuildingType.Chest, new GridPos(10, 3), Direction.Up));
        }

        [Test]
        public void Remove_FreesTheTile()
        {
            var world = MakeWorld();
            world.TryPlace(BuildingType.Belt, GrassTile, Direction.Up);

            Assert.IsTrue(world.TryRemove(GrassTile));
            Assert.IsNull(world.GetBuilding(GrassTile));
            Assert.AreEqual(0, world.Buildings.Count);
            Assert.IsFalse(world.TryRemove(GrassTile));
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.Chest, GrassTile, Direction.Up));
        }

        [Test]
        public void Buildings_AreIteratedInPlacementOrder()
        {
            var world = MakeWorld();
            world.TryPlace(BuildingType.Chest, new GridPos(9, 9), Direction.Up);
            world.TryPlace(BuildingType.Chest, new GridPos(0, 0), Direction.Up);
            world.TryPlace(BuildingType.Chest, new GridPos(5, 0), Direction.Up);
            world.TryRemove(new GridPos(0, 0));
            world.TryPlace(BuildingType.Chest, new GridPos(0, 0), Direction.Up);

            var ids = new int[world.Buildings.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = world.Buildings[i].Id;
            CollectionAssert.IsOrdered(ids);
        }

        [Test]
        public void CannotHandMineUnderABuilding()
        {
            var world = MakeWorld();
            world.TryPlace(BuildingType.Miner, OreTile, Direction.Up);
            Assert.AreEqual(MineResult.NotMineable, world.MineStep(OreTile, OreTile));
        }
    }
}
