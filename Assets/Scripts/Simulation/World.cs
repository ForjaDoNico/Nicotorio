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

        // Hand-mining state. Progress survives only while MineStep is issued every tick
        // on the same tile; a tick without a MineStep (button released) resets it.
        private GridPos _mineTarget;
        private int _mineProgress;
        private bool _minedSinceLastTick;

        public WorldConfig Config { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>Number of Tick() calls so far.</summary>
        public long TickCount { get; private set; }

        public Inventory PlayerInventory { get; } = new Inventory();

        /// <summary>The tile currently being hand-mined, if any.</summary>
        public bool IsMining => _mineProgress > 0;
        public GridPos MiningTarget => _mineTarget;

        /// <summary>0..1 progress toward the next hand-mined item.</summary>
        public float MiningProgress01 => (float)_mineProgress / Config.HandMineTicksPerItem;

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

        // ---- commands -----------------------------------------------------------------

        /// <summary>
        /// One tick's worth of hand mining on target, issued while the mine button is held.
        /// Call at most once per tick, before Tick(). playerTile is where the player stands.
        /// </summary>
        public MineResult MineStep(GridPos target, GridPos playerTile)
        {
            if (!InBounds(target)) return MineResult.NotMineable;
            ItemType item = TileYield.Of(GetTile(target));
            if (item == ItemType.None) return MineResult.NotMineable;
            if (!IsWithinReach(playerTile, target, Config.HandMineReach)) return MineResult.OutOfReach;

            if (target != _mineTarget) _mineProgress = 0;
            _mineTarget = target;
            _minedSinceLastTick = true;

            _mineProgress++;
            if (_mineProgress < Config.HandMineTicksPerItem) return MineResult.InProgress;

            _mineProgress = 0;
            PlayerInventory.Add(item, 1);
            return MineResult.Produced;
        }

        public static bool IsWithinReach(GridPos from, GridPos to, int reach)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            return dx * dx + dy * dy <= reach * reach;
        }

        // ---- simulation step ----------------------------------------------------------

        public void Tick()
        {
            if (!_minedSinceLastTick) _mineProgress = 0;
            _minedSinceLastTick = false;

            TickCount++;
        }
    }
}
