namespace Factory.Simulation
{
    /// <summary>Builds the tile layout from the config's seed. Same seed, same map.</summary>
    public static class WorldGenerator
    {
        public static TileType[] Generate(WorldConfig config)
        {
            var tiles = new TileType[config.Width * config.Height];
            var rng = new DeterministicRandom(config.Seed);

            // Iron first, then coal, then trees: each new resource is stamped after the older ones
            // and only on grass, so adding one never moves the patches that already exist.
            var spawn = config.PlayerSpawn;
            StampPatches(config, tiles, rng, TileType.IronOre, config.OrePatchCount,
                config.OrePatchMinRadius, config.OrePatchMaxRadius, new GridPos(spawn.X + 6, spawn.Y + 3));
            StampPatches(config, tiles, rng, TileType.CoalOre, config.CoalPatchCount,
                config.CoalPatchMinRadius, config.CoalPatchMaxRadius, new GridPos(spawn.X - 6, spawn.Y - 4));
            StampPatches(config, tiles, rng, TileType.Tree, config.TreePatchCount,
                config.TreePatchMinRadius, config.TreePatchMaxRadius, new GridPos(spawn.X - 4, spawn.Y + 6));
            return tiles;
        }

        /// <summary>
        /// Stamps roughly round patches of type. The first patch is centered on firstCenter (near
        /// spawn, so it is on screen at start); the rest are random. Only grass is overwritten.
        /// </summary>
        private static void StampPatches(WorldConfig config, TileType[] tiles, DeterministicRandom rng,
            TileType type, int count, int minRadius, int maxRadius, GridPos firstCenter)
        {
            int width = config.Width;
            int height = config.Height;

            for (int patch = 0; patch < count; patch++)
            {
                int radius = rng.Range(minRadius, maxRadius + 1);
                int cx, cy;
                if (patch == 0)
                {
                    cx = firstCenter.X;
                    cy = firstCenter.Y;
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
                        int i = y * width + x;
                        if (tiles[i] == TileType.Grass && dx * dx + dy * dy <= r2 + jitter)
                            tiles[i] = type;
                    }
                }
            }
        }
    }
}
