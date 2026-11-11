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
        // Wood left in each tree tile, parallel to _tiles (0 for anything that isn't a tree).
        private readonly int[] _woodLeft;

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
        public float MiningProgress01 =>
            InBounds(_mineTarget) ? (float)_mineProgress / HandTicksFor(GetTile(_mineTarget)) : 0f;

        /// <summary>Bumped whenever a tile changes (e.g. a chopped tree turns into grass).</summary>
        public int TilesVersion { get; private set; }

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
            _woodLeft = new int[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
                if (tiles[i] == TileType.Tree) _woodLeft[i] = config.WoodPerTree;

            foreach (ItemAmount start in config.StartingInventory)
                if (start.Count > 0) PlayerInventory.Add(start.Item, start.Count);
        }

        /// <summary>A world with no ore at all; tests paint the tiles they need.</summary>
        public static World CreateEmpty(int width, int height)
        {
            var config = new WorldConfig { Width = width, Height = height, OrePatchCount = 0, CoalPatchCount = 0, TreePatchCount = 0 };
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
            int i = p.Y * Width + p.X;
            _tiles[i] = type;
            _woodLeft[i] = type == TileType.Tree ? Config.WoodPerTree : 0;
            TilesVersion++;
        }

        /// <summary>Wood a tree tile still holds (0 if it is not a tree).</summary>
        public int WoodLeft(GridPos p)
        {
            if (!InBounds(p)) throw new ArgumentOutOfRangeException(nameof(p), p.ToString());
            return _woodLeft[p.Y * Width + p.X];
        }

        /// <summary>Ticks of holding mine on this tile per item: trees are chopped, ore is mined.</summary>
        public int HandTicksFor(TileType tile) =>
            tile == TileType.Tree ? Config.HandChopTicksPerItem : Config.HandMineTicksPerItem;

        // ---- commands -----------------------------------------------------------------

        /// <summary>
        /// One tick's worth of hand mining on target, issued while the mine button is held.
        /// Call at most once per tick, before Tick(). playerTile is where the player stands.
        /// </summary>
        public MineResult MineStep(GridPos target, GridPos playerTile)
        {
            if (!InBounds(target)) return MineResult.NotMineable;
            if (_buildingsByPos.ContainsKey(target)) return MineResult.NotMineable;
            TileType tile = GetTile(target);
            ItemType item = TileYield.ForHand(tile);
            if (item == ItemType.None) return MineResult.NotMineable;
            if (!IsWithinReach(playerTile, target, Config.HandMineReach)) return MineResult.OutOfReach;

            if (target != _mineTarget) _mineProgress = 0;
            _mineTarget = target;
            _minedSinceLastTick = true;

            _mineProgress++;
            if (_mineProgress < HandTicksFor(tile)) return MineResult.InProgress;

            _mineProgress = 0;
            PlayerInventory.Add(item, 1);
            if (tile == TileType.Tree)
            {
                int i = target.Y * Width + target.X;
                _woodLeft[i]--;
                if (_woodLeft[i] <= 0)
                {
                    _tiles[i] = TileType.Grass;
                    _woodLeft[i] = 0;
                    TilesVersion++;
                }
            }
            return MineResult.Produced;
        }

        /// <summary>Whether a building of this type could be placed here, without placing it.</summary>
        public PlaceResult CanPlace(BuildingType type, GridPos pos)
        {
            if (!InBounds(pos)) return PlaceResult.OutOfBounds;
            if (_buildingsByPos.ContainsKey(pos)) return PlaceResult.Occupied;
            if (GetTile(pos) == TileType.Tree) return PlaceResult.Blocked;
            if (type == BuildingType.Miner && TileYield.ForMiner(GetTile(pos)) == ItemType.None) return PlaceResult.RequiresOre;
            if (!CanAfford(type)) return PlaceResult.NotEnoughItems;
            return PlaceResult.Ok;
        }

        /// <summary>Whether the player's inventory covers every line of the building's cost.</summary>
        public bool CanAfford(BuildingType type)
        {
            foreach (BuildCost cost in Config.BuildCosts)
            {
                if (cost.Building != type) continue;
                // Sum lines for the same item, in case the table splits one item over two lines.
                int needed = 0;
                foreach (BuildCost other in Config.BuildCosts)
                    if (other.Building == type && other.Item == cost.Item) needed += other.Count;
                if (PlayerInventory.Count(cost.Item) < needed) return false;
            }
            return true;
        }

        public PlaceResult TryPlace(BuildingType type, GridPos pos, Direction facing)
        {
            PlaceResult result = CanPlace(type, pos);
            if (result != PlaceResult.Ok) return result;

            foreach (BuildCost cost in Config.BuildCosts)
                if (cost.Building == type) PlayerInventory.Remove(cost.Item, cost.Count);

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
        /// Removes the building on pos. Its build cost is refunded and anything it held goes
        /// to the player's inventory, so nothing is lost. Returns false if there was no building.
        /// </summary>
        public bool TryRemove(GridPos pos)
        {
            if (!_buildingsByPos.TryGetValue(pos, out Building building)) return false;
            foreach (BuildCost cost in Config.BuildCosts)
                if (cost.Building == building.Type && cost.Count > 0) PlayerInventory.Add(cost.Item, cost.Count);
            if (building is Chest chest) chest.Contents.TransferAllTo(PlayerInventory);
            if (building is Miner miner && miner.Output != ItemType.None) PlayerInventory.Add(miner.Output, 1);
            if (building is Belt belt)
                foreach (BeltItem item in belt.ItemList) PlayerInventory.Add(item.Item, 1);
            if (building is Furnace furnace)
            {
                if (!furnace.Input.IsEmpty) PlayerInventory.Add(furnace.Input.Item, furnace.Input.Count);
                if (!furnace.Output.IsEmpty) PlayerInventory.Add(furnace.Output.Item, furnace.Output.Count);
                if (!furnace.Fuel.IsEmpty) PlayerInventory.Add(furnace.Fuel.Item, furnace.Fuel.Count);
            }

            _buildingsByPos.Remove(pos);
            _buildings.Remove(building);
            BuildingsVersion++;
            return true;
        }

        /// <summary>
        /// Moves everything stored in the chest on pos into the player's inventory.
        /// Returns how many items moved (0 if there is no chest there).
        /// </summary>
        public int TakeAllFromChest(GridPos pos)
        {
            if (!(GetBuilding(pos) is Chest chest)) return 0;
            return chest.Contents.TransferAllTo(PlayerInventory);
        }

        /// <summary>
        /// Shortcut for all three furnace actions at once: takes the finished items, then loads the
        /// player's smeltable items and fuel. Returns how many items moved (0 if there is no furnace).
        /// </summary>
        public int InteractWithFurnace(GridPos pos) =>
            TakeFurnaceOutput(pos) + LoadFurnaceInput(pos) + LoadFurnaceFuel(pos);

        /// <summary>Moves every finished item from the furnace's output to the player. Returns how many.</summary>
        public int TakeFurnaceOutput(GridPos pos)
        {
            if (!(GetBuilding(pos) is Furnace furnace) || furnace.Output.IsEmpty) return 0;
            ItemType product = furnace.Output.Item;
            int n = furnace.Output.Take(furnace.Output.Count);
            PlayerInventory.Add(product, n);
            return n;
        }

        /// <summary>Loads the player's smeltable items into the input, up to its capacity. Returns how many.</summary>
        public int LoadFurnaceInput(GridPos pos)
        {
            if (!(GetBuilding(pos) is Furnace furnace)) return 0;
            return LoadSlot(furnace.Input, Config.FurnaceInputCapacity, item => Recipes.IsSmeltable(item, Config));
        }

        /// <summary>Loads the player's fuel into the fuel slot, up to its capacity. Returns how many.</summary>
        public int LoadFurnaceFuel(GridPos pos)
        {
            if (!(GetBuilding(pos) is Furnace furnace)) return 0;
            return LoadSlot(furnace.Fuel, Config.FurnaceFuelCapacity, item => Recipes.IsFuel(item, Config));
        }

        /// <summary>Moves the first matching item type the player has (in ItemType order) into slot.</summary>
        private int LoadSlot(ItemSlot slot, int capacity, Func<ItemType, bool> accepts)
        {
            ItemType chosen = ItemType.None;
            foreach (var stack in PlayerInventory.Stacks())
            {
                bool fits = slot.IsEmpty || slot.Item == stack.Key;
                if (fits && accepts(stack.Key)) { chosen = stack.Key; break; }
            }
            if (chosen == ItemType.None) return 0;

            int n = PlayerInventory.Remove(chosen, Math.Max(0, capacity - slot.Count));
            if (n > 0) slot.Add(chosen, n);
            return n;
        }

        /// <summary>
        /// Crafts recipeId up to times times at the crafting table on tablePos, instantly, from the
        /// player's inventory. Refused (returns 0) without a table there, with the player
        /// (standing on playerTile) farther than CraftingReach, or with an unknown recipe.
        /// Makes as many as the ingredients allow; returns how many it made.
        /// </summary>
        public int TryCraft(GridPos tablePos, GridPos playerTile, int recipeId, int times)
        {
            if (times <= 0) return 0;
            if (!(GetBuilding(tablePos) is CraftingTable)) return 0;
            if (!IsWithinReach(playerTile, tablePos, Config.CraftingReach)) return 0;
            CraftingRecipe recipe = Config.FindRecipe(recipeId);
            if (recipe == null) return 0;

            int n = Math.Min(times, Crafting.MaxCraftable(recipe, PlayerInventory));
            if (n <= 0) return 0;
            foreach (ItemAmount ing in recipe.Ingredients) PlayerInventory.Remove(ing.Item, ing.Count * n);
            PlayerInventory.Add(recipe.Result.Item, recipe.Result.Count * n);
            return n;
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
                case BuildingType.Furnace: return new Furnace();
                case BuildingType.CraftingTable: return new CraftingTable();
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
            TickFurnaces();
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
                    miner.Output = TileYield.ForMiner(GetTile(miner.Position));
                }

                if (TryInsert(miner.Front, miner.Output)) miner.Output = ItemType.None;
            }
        }

        /// <summary>
        /// Each furnace smelts one item at a time from Input into Output, only while it has
        /// something smeltable, room for the result and fuel burning; each smelting tick uses one
        /// tick of burn time. Then it offers one finished item to Front.
        /// </summary>
        private void TickFurnaces()
        {
            foreach (Building building in _buildings)
            {
                if (!(building is Furnace furnace)) continue;

                if (!furnace.Input.IsEmpty
                    && Recipes.TryGetSmelting(furnace.Input.Item, Config, out SmeltingRecipe recipe)
                    && furnace.Output.CanAdd(recipe.Output, 1, Config.FurnaceOutputCapacity)
                    && TryKeepLit(furnace))
                {
                    furnace.BurnTicksLeft--;
                    furnace.Progress++;
                    if (furnace.Progress >= recipe.Ticks)
                    {
                        furnace.Progress = 0;
                        furnace.Input.Take(1);
                        furnace.Output.Add(recipe.Output, 1);
                    }
                }

                if (!furnace.Output.IsEmpty && TryInsert(furnace.Front, furnace.Output.Item))
                    furnace.Output.Take(1);
            }
        }

        /// <summary>
        /// Makes sure the furnace has burn time for this tick, lighting one fuel item if the
        /// last one ran out. Returns false (the furnace stalls) when there is nothing to burn.
        /// </summary>
        private bool TryKeepLit(Furnace furnace)
        {
            if (furnace.BurnTicksLeft > 0) return true;
            if (furnace.Fuel.IsEmpty) return false;
            int burn = Recipes.BurnTicks(furnace.Fuel.Item, Config);
            if (burn <= 0) return false;
            furnace.Fuel.Take(1);
            furnace.BurnTicksLeft = burn;
            furnace.BurnTicksTotal = burn;
            return true;
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
        /// entry edge if there is room, a furnace takes fuel into its fuel slot and smeltable items into its input, while there is room. Returns false if refused; the caller keeps the item.
        /// </summary>
        public bool TryInsert(GridPos pos, ItemType item)
        {
            if (item == ItemType.None) return false;
            switch (GetBuilding(pos))
            {
                case Chest chest:
                    chest.Contents.Add(item, 1);
                    return true;
                case Furnace furnace:
                    if (Recipes.IsFuel(item, Config))
                    {
                        if (!furnace.Fuel.CanAdd(item, 1, Config.FurnaceFuelCapacity)) return false;
                        furnace.Fuel.Add(item, 1);
                        return true;
                    }
                    if (!Recipes.IsSmeltable(item, Config)) return false;
                    if (!furnace.Input.CanAdd(item, 1, Config.FurnaceInputCapacity)) return false;
                    furnace.Input.Add(item, 1);
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
