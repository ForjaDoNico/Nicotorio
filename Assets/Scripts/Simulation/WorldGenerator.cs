namespace Factory.Simulation
{
    /// <summary>Builds the tile layout from the config's seed. Same seed, same map.</summary>
    public static class WorldGenerator
    {
        public static TileType[] Generate(WorldConfig config)
        {
            int width = config.Width;
            int height = config.Height;
            var tiles = new TileType[width * height];
            var rng = new DeterministicRandom(config.Seed);

            for (int patch = 0; patch < config.OrePatchCount; patch++)
            {
                int radius = rng.Range(config.OrePatchMinRadius, config.OrePatchMaxRadius + 1);
                int cx, cy;
                if (patch == 0)
                {
                    // First patch sits a few tiles from spawn so ore is on screen at start.
                    cx = config.PlayerSpawn.X + 6;
                    cy = config.PlayerSpawn.Y + 3;
                }
                else
                {
                    cx = rng.Range(radius, width - radius);
                    cy = rng.Range(radius, height - radius);
                }

                int r2 = radius * radius;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        // Roll the jitter before the bounds check so the random sequence
                        // never depends on where a patch landed.
                        int jitter = rng.Range(-radius, radius + 1);
                        int x = cx + dx;
                        int y = cy + dy;
                        if (x < 0 || y < 0 || x >= width || y >= height) continue;
                        if (dx * dx + dy * dy <= r2 + jitter)
                            tiles[y * width + x] = TileType.IronOre;
                    }
                }
            }

            return tiles;
        }
    }
}
