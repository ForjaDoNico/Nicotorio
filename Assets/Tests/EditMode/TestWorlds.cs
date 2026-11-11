namespace Factory.Simulation.Tests
{
    /// <summary>
    /// Shared test setup. Tests about placement, miners, belts, chests and furnaces are about
    /// those rules, not about paying for buildings, so they use a config with no build costs.
    /// BuildCostTests covers the costs with the real table.
    /// </summary>
    internal static class TestWorlds
    {
        public static readonly BuildCost[] NoCosts = new BuildCost[0];

        /// <summary>An all-grass world where building is free.</summary>
        public static World Empty(int width, int height)
        {
            var config = new WorldConfig { Width = width, Height = height, OrePatchCount = 0, BuildCosts = NoCosts };
            return new World(config, new TileType[width * height]);
        }
    }
}
