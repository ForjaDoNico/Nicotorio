using System;
using System.Collections.Generic;

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

        // Buildings: by tile for lookups, and in Id order for deterministic iteration.
        private readonly Dictionary<GridPos, Building> _buildingsByPos = new Dictionary<GridPos, Building>();
        private readonly List<Building> _buildings = new List<Building>();
        private int _nextBuildingId = 1;

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

        /// <summary>All buildings, ordered by Id (placement order).</summary>
        public IReadOnlyList<Building> Buildings => _buildings;

        /// <summary>Bumped whenever a building is placed or removed.</summary>
        public int BuildingsVersion { get; private set; }

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
            if (_buildingsByPos.ContainsKey(target)) return MineResult.NotMineable;
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

        /// <summary>Whether a building of this type could be placed here, without placing it.</summary>
        public PlaceResult CanPlace(BuildingType type, GridPos pos)
        {
            if (!InBounds(pos)) return PlaceResult.OutOfBounds;
            if (_buildingsByPos.ContainsKey(pos)) return PlaceResult.Occupied;
            if (type == BuildingType.Miner && GetTile(pos) != TileType.IronOre) return PlaceResult.RequiresOre;
            return PlaceResult.Ok;
        }

        public PlaceResult TryPlace(BuildingType type, GridPos pos, Direction facing)
        {
            PlaceResult result = CanPlace(type, pos);
            if (result != PlaceResult.Ok) return result;

            Building building = Create(type);
            building.Id = _nextBuildingId++;
            building.Position = pos;
            building.Facing = facing;
            _buildingsByPos.Add(pos, building);
            _buildings.Add(building); // Ids only grow, so the list stays in Id order.
            BuildingsVersion++;
            return PlaceResult.Ok;
        }

        /// <summary>
        /// Removes the building on pos. Anything it held goes to the player's inventory so
        /// nothing is lost. Returns false if there was no building.
        /// </summary>
        public bool TryRemove(GridPos pos)
        {
            if (!_buildingsByPos.TryGetValue(pos, out Building building)) return false;
            if (building is Chest chest) chest.Contents.TransferAllTo(PlayerInventory);
            if (building is Miner miner && miner.Output != ItemType.None) PlayerInventory.Add(miner.Output, 1);
            if (building is Belt belt)
                foreach (BeltItem item in belt.ItemList) PlayerInventory.Add(item.Item, 1);

            _buildingsByPos.Remove(pos);
            _buildings.Remove(building);
            BuildingsVersion++;
            return true;
        }

        public Building GetBuilding(GridPos pos) =>
            _buildingsByPos.TryGetValue(pos, out Building building) ? building : null;

        private static Building Create(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Miner: return new Miner();
                case BuildingType.Belt: return new Belt();
                case BuildingType.Chest: return new Chest();
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static bool IsWithinReach(GridPos from, GridPos to, int reach)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            return dx * dx + dy * dy <= reach * reach;
        }

        // ---- simulation step ----------------------------------------------------------

        /// <summary>
        /// Advances one fixed step. Systems run in a fixed order and each one walks the
        /// buildings in Id order, so the result never depends on dictionary or frame timing.
        /// </summary>
        public void Tick()
        {
            if (!_minedSinceLastTick) _mineProgress = 0;
            _minedSinceLastTick = false;

            TickMiners();
            TickBelts();

            TickCount++;
        }

        private void TickMiners()
        {
            foreach (Building building in _buildings)
            {
                if (!(building is Miner miner)) continue;

                if (miner.Output == ItemType.None)
                {
                    miner.Progress++;
                    if (miner.Progress < Config.MinerTicksPerItem) continue;
                    miner.Progress = 0;
                    miner.Output = TileYield.Of(GetTile(miner.Position));
                }

                if (TryInsert(miner.Front, miner.Output)) miner.Output = ItemType.None;
            }
        }

        /// <summary>
        /// Moves every belt's items toward its exit, front item first, never closer than
        /// BeltItemSpacing to the item ahead. The front item, once at the exit edge, is offered
        /// to the tile in front; if that refuses, it waits there and everything behind it
        /// queues up, which is how a full belt blocks the belt feeding it.
        /// </summary>
        private void TickBelts()
        {
            int speed = Config.BeltSpeed;
            int spacing = Config.BeltItemSpacing;

            foreach (Building building in _buildings)
            {
                if (!(building is Belt belt)) continue;
                List<BeltItem> items = belt.ItemList;

                int limit = Belt.Length; // furthest this item may go: exit edge, or behind the one ahead
                for (int i = 0; i < items.Count; i++)
                {
                    BeltItem it = items[i];
                    it.PreviousPosition = it.Position;
                    it.Position = Math.Max(it.Position, Math.Min(it.Position + speed, limit));

                    if (i == 0 && it.Position >= Belt.Length && TryInsert(belt.Front, it.Item))
                    {
                        items.RemoveAt(0);
                        i--;
                        continue; // the next item now leads; limit stays at the exit edge
                    }

                    items[i] = it;
                    limit = it.Position - spacing;
                }
            }
        }

        /// <summary>
        /// Offers one item to whatever is on pos: a chest stores it, a belt takes it at its
        /// entry edge if there is room. Returns false if refused; the caller keeps the item.
        /// </summary>
        public bool TryInsert(GridPos pos, ItemType item)
        {
            if (item == ItemType.None) return false;
            switch (GetBuilding(pos))
            {
                case Chest chest:
                    chest.Contents.Add(item, 1);
                    return true;
                case Belt belt:
                    List<BeltItem> items = belt.ItemList;
                    if (items.Count > 0 && items[items.Count - 1].Position < Config.BeltItemSpacing) return false;
                    items.Add(new BeltItem { Item = item, Position = 0, PreviousPosition = 0 });
                    return true;
                default:
                    return false;
            }
        }
    }
}
