using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    public class HandMiningTests
    {
        private static readonly GridPos Player = new GridPos(5, 5);
        private static readonly GridPos OreTile = new GridPos(6, 5);

        private static World MakeWorld()
        {
            var world = World.CreateEmpty(16, 16);
            world.SetTile(OreTile, TileType.IronOre);
            return world;
        }

        /// <summary>Holds mine for n ticks, the way the presentation does: one step, then Tick().</summary>
        private static void HoldMine(World world, GridPos target, GridPos player, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                world.MineStep(target, player);
                world.Tick();
            }
        }

        [Test]
        public void HoldingMine_AddsOneOrePerConfiguredTicks()
        {
            var world = MakeWorld();
            int perItem = world.Config.HandMineTicksPerItem;

            HoldMine(world, OreTile, Player, perItem - 1);
            Assert.AreEqual(0, world.PlayerInventory.Count(ItemType.IronOre));

            HoldMine(world, OreTile, Player, 1);
            Assert.AreEqual(1, world.PlayerInventory.Count(ItemType.IronOre));

            HoldMine(world, OreTile, Player, perItem * 3);
            Assert.AreEqual(4, world.PlayerInventory.Count(ItemType.IronOre));
        }

        [Test]
        public void ReleasingMine_ResetsProgress()
        {
            var world = MakeWorld();
            int perItem = world.Config.HandMineTicksPerItem;

            HoldMine(world, OreTile, Player, perItem - 1);
            world.Tick(); // released for one tick
            HoldMine(world, OreTile, Player, perItem - 1);

            Assert.AreEqual(0, world.PlayerInventory.Count(ItemType.IronOre));
        }

        [Test]
        public void MiningGrass_DoesNothing()
        {
            var world = MakeWorld();
            var grass = new GridPos(4, 5);
            Assert.AreEqual(MineResult.NotMineable, world.MineStep(grass, Player));
            HoldMine(world, grass, Player, 100);
            Assert.AreEqual(0, world.PlayerInventory.Total);
        }

        [Test]
        public void MiningOutOfReach_DoesNothing()
        {
            var world = MakeWorld();
            var farPlayer = new GridPos(OreTile.X + world.Config.HandMineReach + 1, OreTile.Y);
            Assert.AreEqual(MineResult.OutOfReach, world.MineStep(OreTile, farPlayer));
            HoldMine(world, OreTile, farPlayer, 100);
            Assert.AreEqual(0, world.PlayerInventory.Total);
        }

        [Test]
        public void Inventory_TransferAllMovesEverything()
        {
            var a = new Inventory();
            var b = new Inventory();
            a.Add(ItemType.IronOre, 7);
            b.Add(ItemType.IronOre, 2);

            Assert.AreEqual(7, a.TransferAllTo(b));
            Assert.AreEqual(0, a.Total);
            Assert.AreEqual(9, b.Count(ItemType.IronOre));
        }
    }
}
