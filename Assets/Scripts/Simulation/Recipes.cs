namespace Factory.Simulation
{
    /// <summary>One furnace recipe: Input -> Output, taking Ticks ticks per item.</summary>
    public readonly struct SmeltingRecipe
    {
        public readonly ItemType Input;
        public readonly ItemType Output;
        public readonly int Ticks;

        public SmeltingRecipe(ItemType input, ItemType output, int ticks)
        {
            Input = input;
            Output = output;
            Ticks = ticks;
        }
    }

    /// <summary>
    /// The single place that says what a furnace can smelt and burn. Today it is one fixed recipe;
    /// a data-driven table only needs to replace TryGetSmelting.
    /// </summary>
    public static class Recipes
    {
        public static bool TryGetSmelting(ItemType input, WorldConfig config, out SmeltingRecipe recipe)
        {
            switch (input)
            {
                case ItemType.IronOre:
                    recipe = new SmeltingRecipe(ItemType.IronOre, ItemType.IronPlate, config.FurnaceTicksPerItem);
                    return true;
                default:
                    recipe = default;
                    return false;
            }
        }

        public static bool IsSmeltable(ItemType item, WorldConfig config) => TryGetSmelting(item, config, out _);

        /// <summary>How many ticks of smelting one unit of item powers; 0 if it is not a fuel.</summary>
        public static int BurnTicks(ItemType item, WorldConfig config)
        {
            switch (item)
            {
                case ItemType.Coal: return config.CoalBurnTicks;
                default: return 0;
            }
        }

        public static bool IsFuel(ItemType item, WorldConfig config) => BurnTicks(item, config) > 0;
    }
}
