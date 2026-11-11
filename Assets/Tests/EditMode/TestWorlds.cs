namespace Factory.Simulation.Tests
{
    /// <summary>
    /// Shared test setup. Tests about placement, miners, belts, chests, furnaces, wood and
    /// crafting are about those rules, not about paying for buildings, so they build with
    /// WorldConfig.FreeBuilding. BuildCostTests covers the real costs.
    /// </summary>
    internal static class TestWorlds
    {
        /// <summary>An all-grass world where building is free.</summary>
        public static World Empty(int width, int height)
        {
            var config = new WorldConfig { Width = width, Height = height, OrePatchCount = 0, FreeBuilding = true };
            return new World(config, new TileType[width * height]);
        }
    }
}
