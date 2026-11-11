using System;

namespace Factory.Simulation
{
    /// <summary>
    /// The whole simulation state. Pure C#: no UnityEngine. Advances only through Tick(),
    /// which is deterministic given the same config and the same sequence of commands.
    /// </summary>
    public sealed class World
    {
        private readonly TileType[] _tiles;

        public WorldConfig Config { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>Number of Tick() calls so far.</summary>
        public long TickCount { get; private set; }

        public World(WorldConfig config)
            : this(config, WorldGenerator.Generate(config))
        {
        }

        /// <summary>Builds a world from an explicit tile layout (row-major, y * Width + x).</summary>
        public World(WorldConfig config, TileType[] tiles)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Width = config.Width;
            Height = config.Height;
            if (tiles == null || tiles.Length != Width * Height)
                throw new ArgumentException("Tile array must be Width * Height long.", nameof(tiles));
            _tiles = tiles;
        }

        /// <summary>A world with no ore at all; tests paint the tiles they need.</summary>
        public static World CreateEmpty(int width, int height)
        {
            var config = new WorldConfig { Width = width, Height = height, OrePatchCount = 0 };
            return new World(config, new TileType[width * height]);
        }

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public TileType GetTile(GridPos p)
        {
            if (!InBounds(p)) throw new ArgumentOutOfRangeException(nameof(p), p.ToString());
            return _tiles[p.Y * Width + p.X];
        }

        public void SetTile(GridPos p, TileType type)
        {
            if (!InBounds(p)) throw new ArgumentOutOfRangeException(nameof(p), p.ToString());
            _tiles[p.Y * Width + p.X] = type;
        }

        public void Tick()
        {
            TickCount++;
        }
    }
}
