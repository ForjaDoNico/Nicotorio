using System.Collections.Generic;

namespace Factory.Simulation
{
    public enum BuildingType : byte
    {
        Miner = 0,
        Belt = 1,
        Chest = 2,
        Furnace = 3,
    }

    public enum PlaceResult : byte
    {
        Ok,
        OutOfBounds,
        Occupied,
        RequiresOre,
        NotEnoughItems,
    }

    /// <summary>
    /// A placed building: plain data. Behaviour lives in the World's tick systems, not here.
    /// Id is assigned in placement order and is the fixed iteration order of every system.
    /// </summary>
    public abstract class Building
    {
        public int Id { get; internal set; }
        public GridPos Position { get; internal set; }
        public Direction Facing { get; internal set; }
        public abstract BuildingType Type { get; }

        /// <summary>The tile this building outputs into.</summary>
        public GridPos Front => Position + Facing.ToOffset();
    }

    public sealed class Miner : Building
    {
        public override BuildingType Type => BuildingType.Miner;

        /// <summary>Ticks of digging toward the next item. Frozen while Output is held.</summary>
        public int Progress { get; internal set; }

        /// <summary>
        /// The dug item waiting to leave through Front, or None. While it can't be delivered
        /// the miner stalls: it holds at most one item and stops digging.
        /// </summary>
        public ItemType Output { get; internal set; }
    }

    /// <summary>
    /// Smelts what is in Input into Output (see Recipes), burning Fuel while it works, and
    /// pushes Output through Front. Stops when Input is empty, Output is full or it runs out of fuel.
    /// </summary>
    public sealed class Furnace : Building
    {
        public override BuildingType Type => BuildingType.Furnace;

        public ItemSlot Input { get; } = new ItemSlot();
        public ItemSlot Output { get; } = new ItemSlot();
        public ItemSlot Fuel { get; } = new ItemSlot();

        /// <summary>Smelting ticks left from the fuel item already burning. 0 = nothing lit.</summary>
        public int BurnTicksLeft { get; internal set; }

        /// <summary>Ticks of burn the current fuel item started with, for the fuel bar.</summary>
        public int BurnTicksTotal { get; internal set; }

        /// <summary>Ticks spent on the item being smelted. Frozen while the furnace can't work.</summary>
        public int Progress { get; internal set; }
    }

    /// <summary>One item riding a belt. Plain data: never a GameObject.</summary>
    public struct BeltItem
    {
        public ItemType Item;
        /// <summary>Distance from the entry edge, 0..Belt.Length.</summary>
        public int Position;
        /// <summary>Position at the start of the last tick, for smooth drawing only.</summary>
        public int PreviousPosition;
    }

    public sealed class Belt : Building
    {
        /// <summary>Fixed-point length of one belt tile.</summary>
        public const int Length = 1000;

        public override BuildingType Type => BuildingType.Belt;

        /// <summary>Items ordered front first: Items[0] is the closest to the exit edge.</summary>
        public IReadOnlyList<BeltItem> Items => ItemList;
        internal readonly List<BeltItem> ItemList = new List<BeltItem>();
    }

    public sealed class Chest : Building
    {
        public override BuildingType Type => BuildingType.Chest;
        public Inventory Contents { get; } = new Inventory();
    }
}
