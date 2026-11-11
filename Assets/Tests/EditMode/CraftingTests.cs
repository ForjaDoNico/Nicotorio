using System.Collections.Generic;
using NUnit.Framework;

namespace Factory.Simulation.Tests
{
    public class CraftingTests
    {
        private static readonly GridPos Table = new GridPos(5, 5);
        private static readonly GridPos Near = new GridPos(5, 7);
        private const int Gear = 1; // default recipe: 2 iron plates -> 1 iron gear

        private static World MakeWorld()
        {
            var world = TestWorlds.Empty(30, 30);
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.CraftingTable, Table, Direction.Up));
            return world;
        }

        private static int Count(World w, ItemType item) => w.PlayerInventory.Count(item);

        [Test]
        public void Craft_ConsumesIngredients_AndAddsTheResult()
        {
            var world = MakeWorld();
            world.PlayerInventory.Add(ItemType.IronPlate, 5);

            Assert.AreEqual(1, world.TryCraft(Table, Near, Gear, 1));
            Assert.AreEqual(3, Count(world, ItemType.IronPlate));
            Assert.AreEqual(1, Count(world, ItemType.IronGear));
        }

        [Test]
        public void Craft_WithoutIngredients_ChangesNothing()
        {
            var world = MakeWorld();
            world.PlayerInventory.Add(ItemType.IronPlate, 1);
            int version = world.PlayerInventory.Version;

            Assert.AreEqual(0, world.TryCraft(Table, Near, Gear, 1));
            Assert.AreEqual(1, Count(world, ItemType.IronPlate));
            Assert.AreEqual(0, Count(world, ItemType.IronGear));
            Assert.AreEqual(version, world.PlayerInventory.Version);
        }

        [Test]
        public void Craft_MoreThanPossible_MakesOnlyWhatItCan()
        {
            var world = MakeWorld();
            world.PlayerInventory.Add(ItemType.IronPlate, 5);

            Assert.AreEqual(2, world.TryCraft(Table, Near, Gear, 10));
            Assert.AreEqual(1, Count(world, ItemType.IronPlate));
            Assert.AreEqual(2, Count(world, ItemType.IronGear));
            Assert.AreEqual(0, world.TryCraft(Table, Near, Gear, 0), "times 0 does nothing");
        }

        [Test]
        public void Craft_ResultCountIsMultiplied()
        {
            var config = new WorldConfig
            {
                Width = 10, Height = 10, OrePatchCount = 0, BuildCosts = TestWorlds.NoCosts,
                CraftingRecipes = new[]
                {
                    new CraftingRecipe(7, new ItemAmount(ItemType.IronGear, 2), new ItemAmount(ItemType.Wood, 1)),
                },
            };
            var world = new World(config, new TileType[100]);
            world.TryPlace(BuildingType.CraftingTable, Table, Direction.Up);
            world.PlayerInventory.Add(ItemType.Wood, 10);

            Assert.AreEqual(3, world.TryCraft(Table, Near, 7, 3));
            Assert.AreEqual(6, Count(world, ItemType.IronGear));
            Assert.AreEqual(7, Count(world, ItemType.Wood));
        }

        [Test]
        public void Craft_FarFromTheTable_OrWithoutATable_IsRefused()
        {
            var world = MakeWorld();
            world.PlayerInventory.Add(ItemType.IronPlate, 10);

            var far = new GridPos(5, 5 + world.Config.CraftingReach + 1);
            Assert.AreEqual(0, world.TryCraft(Table, far, Gear, 1), "out of reach");
            Assert.AreEqual(0, world.TryCraft(new GridPos(6, 5), new GridPos(6, 6), Gear, 1), "no table there");
            world.TryPlace(BuildingType.Chest, new GridPos(7, 7), Direction.Up);
            Assert.AreEqual(0, world.TryCraft(new GridPos(7, 7), Near, Gear, 1), "a chest is not a table");
            Assert.AreEqual(0, world.TryCraft(Table, Near, 9999, 1), "unknown recipe");
            Assert.AreEqual(10, Count(world, ItemType.IronPlate));

            var edge = new GridPos(5, 5 + world.Config.CraftingReach);
            Assert.AreEqual(1, world.TryCraft(Table, edge, Gear, 1), "exactly at reach is fine");
        }

        [Test]
        public void Queries_MatchTheInventory()
        {
            var recipe = new CraftingRecipe(42, new ItemAmount(ItemType.IronGear, 1),
                new ItemAmount(ItemType.IronPlate, 2), new ItemAmount(ItemType.Wood, 3));
            var inv = new Inventory();

            Assert.IsFalse(Crafting.CanCraft(recipe, inv));
            Assert.AreEqual(0, Crafting.MaxCraftable(recipe, inv));
            CollectionAssert.AreEqual(new List<ItemAmount>
            {
                new ItemAmount(ItemType.IronPlate, 2), new ItemAmount(ItemType.Wood, 3),
            }, Crafting.Missing(recipe, inv));

            inv.Add(ItemType.IronPlate, 7);
            inv.Add(ItemType.Wood, 4);
            Assert.IsTrue(Crafting.CanCraft(recipe, inv));
            Assert.AreEqual(1, Crafting.MaxCraftable(recipe, inv), "wood limits it to 1");
            Assert.IsEmpty(Crafting.Missing(recipe, inv));

            inv.Add(ItemType.Wood, 8); // 12 wood -> 4 by wood, 3 by plates
            Assert.AreEqual(3, Crafting.MaxCraftable(recipe, inv));
        }

        [Test]
        public void RecipeTable_IsWellFormed()
        {
            var recipes = new WorldConfig().CraftingRecipes;
            Assert.IsNotEmpty(recipes);
            var ids = new HashSet<int>();
            foreach (CraftingRecipe r in recipes)
            {
                Assert.IsTrue(ids.Add(r.Id), $"duplicate recipe id {r.Id}");
                Assert.Greater(r.Result.Count, 0, $"recipe {r.Id} result");
                Assert.AreNotEqual(ItemType.None, r.Result.Item, $"recipe {r.Id} result");
                Assert.IsNotEmpty(r.Ingredients, $"recipe {r.Id} has no ingredients");
                var items = new HashSet<ItemType>();
                foreach (ItemAmount ing in r.Ingredients)
                {
                    Assert.Greater(ing.Count, 0, $"recipe {r.Id} ingredient {ing.Item}");
                    Assert.IsTrue(items.Add(ing.Item), $"recipe {r.Id} lists {ing.Item} twice");
                }
            }
        }

        [Test]
        public void CraftingTable_IsBuiltWithWood()
        {
            var config = new WorldConfig { Width = 10, Height = 10, OrePatchCount = 0,
                StartingInventory = new[] { new ItemAmount(ItemType.Wood, 5) } };
            var world = new World(config, new TileType[100]);
            Assert.AreEqual(PlaceResult.Ok, world.TryPlace(BuildingType.CraftingTable, Table, Direction.Up));
            Assert.AreEqual(0, Count(world, ItemType.Wood));
            Assert.AreEqual(PlaceResult.NotEnoughItems, world.CanPlace(BuildingType.CraftingTable, Near));
        }
    }
}
