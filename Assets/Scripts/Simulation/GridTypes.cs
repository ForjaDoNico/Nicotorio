using System;

namespace Factory.Simulation
{
    /// <summary>Integer tile coordinate. (0,0) is the bottom-left tile.</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Y + b.Y);
        public static GridPos operator -(GridPos a, GridPos b) => new GridPos(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(GridPos a, GridPos b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(GridPos a, GridPos b) => !(a == b);

        public bool Equals(GridPos other) => this == other;
        public override bool Equals(object obj) => obj is GridPos other && this == other;
        public override int GetHashCode() => unchecked((X * 73856093) ^ (Y * 19349663));
        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>Facing of a building. Values are clockwise so rotation is +1 mod 4.</summary>
    public enum Direction : byte
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,
    }

    public static class DirectionExtensions
    {
        public static GridPos ToOffset(this Direction d)
        {
            switch (d)
            {
                case Direction.Up: return new GridPos(0, 1);
                case Direction.Right: return new GridPos(1, 0);
                case Direction.Down: return new GridPos(0, -1);
                default: return new GridPos(-1, 0);
            }
        }

        public static Direction RotateClockwise(this Direction d) => (Direction)(((int)d + 1) & 3);
        public static Direction Opposite(this Direction d) => (Direction)(((int)d + 2) & 3);
    }

    public enum TileType : byte
    {
        Grass = 0,
        IronOre = 1,
        CoalOre = 2,
    }
}
