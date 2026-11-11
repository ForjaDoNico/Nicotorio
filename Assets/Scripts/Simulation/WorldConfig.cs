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

        // Furnaces.
        /// <summary>Ticks to smelt one item (40 = 2 s per plate).</summary>
        public int FurnaceTicksPerItem = 40;
        /// <summary>Most ore a furnace's input slot holds.</summary>
        public int FurnaceInputCapacity = 10;
        /// <summary>Most plates a furnace's output slot holds before it stops smelting.</summary>
        public int FurnaceOutputCapacity = 10;

        // Belts. Item positions along a belt are integers from 0 (entry edge) to
        // Belt.Length (exit edge), so belt motion is exact and deterministic.
        /// <summary>Distance an item moves per tick (75 = 1.5 tiles per second).</summary>
        public int BeltSpeed = 75;
        /// <summary>Minimum gap between two items on a belt (250 = up to 5 items per belt).</summary>
        public int BeltItemSpacing = 250;

        /// <summary>Where the player starts. The generator always puts one ore patch near it.</summary>
        public GridPos PlayerSpawn => new GridPos(Width / 2, Height / 2);
    }
}
