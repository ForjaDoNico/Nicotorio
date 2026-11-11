using System;
using System.Collections.Generic;

namespace Factory.Simulation
{
    public enum ItemType : byte
    {
        None = 0,
        IronOre = 1,
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

    public static class TileYield
    {
        /// <summary>The item a tile gives when mined, or None if it can't be mined.</summary>
        public static ItemType Of(TileType tile) => tile == TileType.IronOre ? ItemType.IronOre : ItemType.None;
    }
}
