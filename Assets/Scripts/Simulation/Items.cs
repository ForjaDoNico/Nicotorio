using System;
using System.Collections.Generic;

namespace Factory.Simulation
{
    public enum ItemType : byte
    {
        None = 0,
        IronOre = 1,
        IronPlate = 2,
        Coal = 3,
        Wood = 4,
        IronGear = 5,
    }

    /// <summary>
    /// A single-item slot with a quantity, like a furnace's input or output. Holds one item
    /// type at a time; Item is None whenever Count is 0.
    /// </summary>
    public sealed class ItemSlot
    {
        public ItemType Item { get; private set; }
        public int Count { get; private set; }

        public bool IsEmpty => Count == 0;

        /// <summary>Whether amount of item fits without exceeding capacity or mixing types.</summary>
        public bool CanAdd(ItemType item, int amount, int capacity) =>
            item != ItemType.None && (IsEmpty || Item == item) && Count + amount <= capacity;

        internal void Add(ItemType item, int amount)
        {
            if (item == ItemType.None) throw new ArgumentException("Cannot store ItemType.None.", nameof(item));
            if (!IsEmpty && Item != item) throw new InvalidOperationException($"Slot holds {Item}, not {item}.");
            Item = item;
            Count += amount;
        }

        /// <summary>Removes up to amount; returns how many were removed.</summary>
        internal int Take(int amount)
        {
            int taken = Math.Min(amount, Count);
            Count -= taken;
            if (Count == 0) Item = ItemType.None;
            return taken;
        }
    }

    /// <summary>Item -> quantity. Enumerates in ItemType order so output is deterministic.</summary>
    public sealed class Inventory
    {
        private static readonly int SlotCount = Enum.GetValues(typeof(ItemType)).Length;
        private readonly int[] _counts = new int[SlotCount];

        /// <summary>Bumped on every change; views redraw only when it moves.</summary>
        public int Version { get; private set; }

        public int Count(ItemType item) => _counts[(int)item];

        public int Total
        {
            get
            {
                int total = 0;
                for (int i = 1; i < _counts.Length; i++) total += _counts[i];
                return total;
            }
        }

        public void Add(ItemType item, int amount)
        {
            if (item == ItemType.None) throw new ArgumentException("Cannot store ItemType.None.", nameof(item));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            _counts[(int)item] += amount;
            Version++;
        }

        /// <summary>Removes up to amount; returns how many were actually removed.</summary>
        public int Remove(ItemType item, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int removed = Math.Min(amount, _counts[(int)item]);
            if (removed == 0) return 0;
            _counts[(int)item] -= removed;
            Version++;
            return removed;
        }

        /// <summary>Moves everything into another inventory; returns the number of items moved.</summary>
        public int TransferAllTo(Inventory other)
        {
            int moved = 0;
            for (int i = 1; i < _counts.Length; i++)
            {
                int n = _counts[i];
                if (n == 0) continue;
                other.Add((ItemType)i, n);
                _counts[i] = 0;
                moved += n;
            }
            if (moved > 0) Version++;
            return moved;
        }

        /// <summary>Non-empty stacks, in ItemType order.</summary>
        public IEnumerable<KeyValuePair<ItemType, int>> Stacks()
        {
            for (int i = 1; i < _counts.Length; i++)
                if (_counts[i] > 0) yield return new KeyValuePair<ItemType, int>((ItemType)i, _counts[i]);
        }
    }

    public enum MineResult : byte
    {
        NotMineable,
        OutOfReach,
        InProgress,
        Produced,
    }

    /// <summary>
    /// What a tile gives. The hand and the miner are separate on purpose: the player can chop
    /// trees, but a miner only extracts ore.
    /// </summary>
    public static class TileYield
    {
        /// <summary>The item the player gets by hand-mining (or chopping) this tile, or None.</summary>
        public static ItemType ForHand(TileType tile)
        {
            switch (tile)
            {
                case TileType.IronOre: return ItemType.IronOre;
                case TileType.CoalOre: return ItemType.Coal;
                case TileType.Tree: return ItemType.Wood;
                default: return ItemType.None;
            }
        }

        /// <summary>The item a miner placed on this tile extracts, or None (the miner can't go there).</summary>
        public static ItemType ForMiner(TileType tile)
        {
            switch (tile)
            {
                case TileType.IronOre: return ItemType.IronOre;
                case TileType.CoalOre: return ItemType.Coal;
                default: return ItemType.None;
            }
        }
    }
}
