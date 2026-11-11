namespace Factory.Simulation
{
    public enum BuildingType : byte
    {
        Miner = 0,
        Belt = 1,
        Chest = 2,
    }

    public enum PlaceResult : byte
    {
        Ok,
        OutOfBounds,
        Occupied,
        RequiresOre,
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

    public sealed class Belt : Building
    {
        public override BuildingType Type => BuildingType.Belt;
    }

    public sealed class Chest : Building
    {
        public override BuildingType Type => BuildingType.Chest;
        public Inventory Contents { get; } = new Inventory();
    }
}
