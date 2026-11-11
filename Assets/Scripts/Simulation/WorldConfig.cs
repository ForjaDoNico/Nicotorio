namespace Factory.Simulation
{
    /// <summary>All tunables of the simulation. Plain data; tests build their own.</summary>
    public sealed class WorldConfig
    {
        /// <summary>The simulation always advances at this fixed rate.</summary>
        public const int TicksPerSecond = 20;

        public int Width = 64;
        public int Height = 64;
        public ulong Seed = 20261111UL;

        // Ore generation.
        public int OrePatchCount = 9;
        public int OrePatchMinRadius = 2;
        public int OrePatchMaxRadius = 5;
        public int CoalPatchCount = 7;
        public int CoalPatchMinRadius = 2;
        public int CoalPatchMaxRadius = 4;
        public int TreePatchCount = 10;
        public int TreePatchMinRadius = 2;
        public int TreePatchMaxRadius = 4;

        // Hand mining.
        /// <summary>Consecutive ticks of holding mine on one tile to get one item (10 = 0.5 s).</summary>
        public int HandMineTicksPerItem = 10;
        /// <summary>Max distance in tiles (Euclidean, tile centers) from the player to the mined tile.</summary>
        public int HandMineReach = 3;
        /// <summary>Consecutive ticks of holding mine on a tree to get one wood (15 = 0.75 s).</summary>
        public int HandChopTicksPerItem = 15;
        /// <summary>Wood a tree gives before it turns into grass.</summary>
        public int WoodPerTree = 4;

        // Buildings.
        /// <summary>Ticks a miner needs to dig one item (20 = 1 item per second).</summary>
        public int MinerTicksPerItem = 20;

        // Furnaces.
        /// <summary>Ticks to smelt one item (40 = 2 s per plate).</summary>
        public int FurnaceTicksPerItem = 40;
        /// <summary>Most ore a furnace's input slot holds.</summary>
        public int FurnaceInputCapacity = 10;
        /// <summary>Most plates a furnace's output slot holds before it stops smelting.</summary>
        public int FurnaceOutputCapacity = 10;
        /// <summary>Most fuel a furnace's fuel slot holds.</summary>
        public int FurnaceFuelCapacity = 10;
        /// <summary>
        /// Ticks of smelting one coal powers (160 = 8 s = 4 plates at 40 ticks each).
        /// Fuel only burns while the furnace is actually smelting.
        /// </summary>
        public int CoalBurnTicks = 160;
        /// <summary>Ticks of smelting one wood powers (80 = 2 plates). Lets furnaces run before coal.</summary>
        public int WoodBurnTicks = 80;

        // Belts. Item positions along a belt are integers from 0 (entry edge) to
        // Belt.Length (exit edge), so belt motion is exact and deterministic.
        /// <summary>Distance an item moves per tick (75 = 1.5 tiles per second).</summary>
        public int BeltSpeed = 75;
        /// <summary>Minimum gap between two items on a belt (250 = up to 5 items per belt).</summary>
        public int BeltItemSpacing = 250;

        // Building costs and the player's start.
        /// <summary>
        /// What each building costs. Paid from the player's inventory on placement and
        /// refunded in full on removal. One line per (building, item).
        /// </summary>
        public BuildCost[] BuildCosts =
        {
            new BuildCost(BuildingType.CraftingTable, ItemType.Wood, 5),
            new BuildCost(BuildingType.Furnace, ItemType.IronOre,   5),
            new BuildCost(BuildingType.Miner,   ItemType.IronPlate, 3),
            new BuildCost(BuildingType.Belt,    ItemType.IronPlate, 1),
            new BuildCost(BuildingType.Chest,   ItemType.IronPlate, 2),
        };

        // Crafting.
        /// <summary>
        /// Everything the crafting table can make. Ids are stable: never renumber, only add.
        /// </summary>
        public CraftingRecipe[] CraftingRecipes =
        {
            new CraftingRecipe(1, new ItemAmount(ItemType.IronGear, 1), new ItemAmount(ItemType.IronPlate, 2)),
        };

        /// <summary>Max distance in tiles (Euclidean) from the player to a crafting table to use it.</summary>
        public int CraftingReach = 6;

        /// <summary>The recipe with this Id, or null.</summary>
        public CraftingRecipe FindRecipe(int id)
        {
            foreach (CraftingRecipe recipe in CraftingRecipes)
                if (recipe.Id == id) return recipe;
            return null;
        }

        /// <summary>Items the player starts with. Empty: everything starts from hand mining.</summary>
        public ItemAmount[] StartingInventory = { };

        /// <summary>Where the player starts. The generator always puts one ore patch near it.</summary>
        public GridPos PlayerSpawn => new GridPos(Width / 2, Height / 2);
    }
}
