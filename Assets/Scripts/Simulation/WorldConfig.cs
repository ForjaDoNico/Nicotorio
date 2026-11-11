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

        // Hand mining.
        /// <summary>Consecutive ticks of holding mine on one tile to get one item (10 = 0.5 s).</summary>
        public int HandMineTicksPerItem = 10;
        /// <summary>Max distance in tiles (Euclidean, tile centers) from the player to the mined tile.</summary>
        public int HandMineReach = 3;

        // Buildings.
        /// <summary>Ticks a miner needs to dig one item (20 = 1 item per second).</summary>
        public int MinerTicksPerItem = 20;

        /// <summary>Where the player starts. The generator always puts one ore patch near it.</summary>
        public GridPos PlayerSpawn => new GridPos(Width / 2, Height / 2);
    }
}
