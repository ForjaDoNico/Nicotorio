using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    public class WoodTests
    {
        private static readonly GridPos Player = new GridPos(5, 5);
        private static readonly GridPos TreeTile = new GridPos(6, 5);

        private static World MakeWorld()
        {
            var world = TestWorlds.Empty(16, 16);
            world.SetTile(TreeTile, TileType.Tree);
            return world;
        }

        /// <summary>Holds mine on target for n ticks, the way the presentation does.</summary>
        private static void Hold(World world, GridPos target, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                world.MineStep(target, Player);
                world.Tick();
            }
        }

        private static int Wood(World w) => w.PlayerInventory.Count(ItemType.Wood);

        [Test]
        public void AddingTrees_DoesNotMoveIronOrCoal()
        {
            var withTrees = new World(new WorldConfig());
            var withoutTrees = new World(new WorldConfig { TreePatchCount = 0 });
            int trees = 0;
            for (int y = 0; y < withTrees.Height; y++)
            for (int x = 0; x < withTrees.Width; x++)
            {
                var p = new GridPos(x, y);
                TileType before = withoutTrees.GetTile(p);
                TileType after = withTrees.GetTile(p);
                if (before != TileType.Grass) Assert.AreEqual(before, after, $"resource moved at {p}");
                else Assert.That(after == TileType.Grass || after == TileType.Tree, $"tile {p}");
                if (after == TileType.Tree) trees++;
            }
            Assert.Greater(trees, 0);
        }

        [Test]
        public void DefaultMap_HasTreesNearSpawn()
        {
            var world = new World(new WorldConfig());
            var spawn = world.Config.PlayerSpawn;
            int near = 0;
            for (int dy = -10; dy <= 10; dy++)
            for (int dx = -10; dx <= 10; dx++)
            {
                var p = new GridPos(spawn.X + dx, spawn.Y + dy);
                if (world.GetTile(p) == TileType.Tree)
                {
                    near++;
                    Assert.AreEqual(world.Config.WoodPerTree, world.WoodLeft(p), "generated trees start full");
                }
            }
            Assert.Greater(near, 0);
        }

        [Test]
        public void Chopping_GivesWoodAtTheChopRate()
        {
            var world = MakeWorld(); // HandChopTicksPerItem = 15
            Hold(world, TreeTile, 14);
            Assert.AreEqual(0, Wood(world));
            Hold(world, TreeTile, 1);
            Assert.AreEqual(1, Wood(world));
            Hold(world, TreeTile, 15 * 2);
            Assert.AreEqual(3, Wood(world));
            Assert.AreEqual(1, world.WoodLeft(TreeTile));
        }

        [Test]
        public void Tree_TurnsIntoGrass_AfterWoodPerTree()
        {
            var world = MakeWorld();
            int version = world.TilesVersion;

            Hold(world, TreeTile, 15 * world.Config.WoodPerTree);
            Assert.AreEqual(world.Config.WoodPerTree, Wood(world));
            Assert.AreEqual(TileType.Grass, world.GetTile(TreeTile));
            Assert.AreEqual(0, world.WoodLeft(TreeTile));
            Assert.Greater(world.TilesVersion, version, "views are told the tile changed");

            Hold(world, TreeTile, 100);
            Assert.AreEqual(world.Config.WoodPerTree, Wood(world), "grass gives nothing");
        }

        [Test]
        public void MinersAndBuildings_AreRefusedOnTrees()
        {
            var world = MakeWorld();
            Assert.AreEqual(PlaceResult.Blocked, world.CanPlace(BuildingType.Miner, TreeTile));
            Assert.AreEqual(PlaceResult.Blocked, world.TryPlace(BuildingType.Belt, TreeTile, Direction.Up));
            Assert.AreEqual(PlaceResult.Blocked, world.TryPlace(BuildingType.Chest, TreeTile, Direction.Up));
            Assert.AreEqual(PlaceResult.Blocked, world.TryPlace(BuildingType.Furnace, TreeTile, Direction.Up));
            Assert.AreEqual(0, world.Buildings.Count);
        }

        [Test]
        public void Wood_IsHandOnly_MinersExtractOre()
        {
            Assert.AreEqual(ItemType.Wood, TileYield.ForHand(TileType.Tree));
            Assert.AreEqual(ItemType.None, TileYield.ForMiner(TileType.Tree));
            Assert.AreEqual(ItemType.IronOre, TileYield.ForMiner(TileType.IronOre));
            Assert.AreEqual(ItemType.Coal, TileYield.ForMiner(TileType.CoalOre));
        }

        [Test]
        public void Wood_BurnsInTheFurnace_ForWoodBurnTicks()
        {
            var world = MakeWorld();
            var furnaceTile = new GridPos(2, 2);
            world.TryPlace(BuildingType.Furnace, furnaceTile, Direction.Right);
            world.TryPlace(BuildingType.Chest, new GridPos(3, 2), Direction.Up);
            for (int i = 0; i < 10; i++) world.TryInsert(furnaceTile, ItemType.IronOre);
            Assert.IsTrue(world.TryInsert(furnaceTile, ItemType.Wood), "wood goes to the fuel slot");

            for (int i = 0; i < 2000; i++) world.Tick();
            // 80 ticks of burn at 40 ticks per plate = 2 plates.
            var chest = (Chest)world.GetBuilding(new GridPos(3, 2));
            Assert.AreEqual(2, chest.Contents.Count(ItemType.IronPlate));
            Assert.AreEqual(world.Config.WoodBurnTicks / world.Config.FurnaceTicksPerItem,
                chest.Contents.Count(ItemType.IronPlate));
        }
    }
}
