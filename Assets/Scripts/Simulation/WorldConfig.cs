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

        /// <summary>Where the player starts. The generator always puts one ore patch near it.</summary>
        public GridPos PlayerSpawn => new GridPos(Width / 2, Height / 2);
    }
}
