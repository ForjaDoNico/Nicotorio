namespace Factory.Simulation
{
    /// <summary>An amount of one item, e.g. for the starting inventory.</summary>
    public readonly struct ItemAmount
    {
        public readonly ItemType Item;
        public readonly int Count;

        public ItemAmount(ItemType item, int count)
        {
            Item = item;
            Count = count;
        }
    }

    /// <summary>
    /// One line of the build cost table: building Building takes Count of Item from the
    /// player. A building that needs several items has one line per item.
    /// </summary>
    public readonly struct BuildCost
    {
        public readonly BuildingType Building;
        public readonly ItemType Item;
        public readonly int Count;

        public BuildCost(BuildingType building, ItemType item, int count)
        {
            Building = building;
            Item = item;
            Count = count;
        }
    }
}
