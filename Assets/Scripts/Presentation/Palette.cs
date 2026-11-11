using UnityEngine;

namespace Factory.Presentation
{
    /// <summary>The only place colors are defined. Every sprite and UI color reads from here.</summary>
    public static class Palette
    {
        public static readonly Color32 Grass = Hex(0x5A8F4E);
        public static readonly Color32 GrassDark = Hex(0x4C7C43);
        public static readonly Color32 GrassLight = Hex(0x6BA05C);
        public static readonly Color32 Path = Hex(0xC2A26B);
        public static readonly Color32 PathLight = Hex(0xD8BC88);
        public static readonly Color32 BeltBed = Hex(0x3D3552);
        public static readonly Color32 Stone = Hex(0x6E6A7A);
        public static readonly Color32 Ore = Hex(0x8C8FA6);
        public static readonly Color32 OreHighlight = Hex(0xC9CCDA);
        public static readonly Color32 HeroPrimary = Hex(0xD9583B);
        public static readonly Color32 HeroSecondary = Hex(0xF2E2C4);
        public static readonly Color32 Props = Hex(0x3E6B6E);
        public static readonly Color32 Shadow = Hex(0x2B2440);
        public static readonly Color32 Accent = Hex(0xF2C14E);
        public static readonly Color32 Brick = Hex(0x8A4B3C);
        public static readonly Color32 BrickDark = Hex(0x5E3029);
        public static readonly Color32 Fire = Hex(0xF28C38);
        public static readonly Color32 Plate = Hex(0xB8BCCB);
        public static readonly Color32 PlateDark = Hex(0x7D8296);
        public static readonly Color32 Coal = Hex(0x34303D);
        public static readonly Color32 CoalHighlight = Hex(0x6A6478);
        public static readonly Color32 Leaf = Hex(0x2F6338);
        public static readonly Color32 LeafLight = Hex(0x4A8A4A);
        public static readonly Color32 Bark = Hex(0x6B4A2F);
        public static readonly Color32 BarkLight = Hex(0x9A7048);
        /// <summary>Text for things the player can't afford right now.</summary>
        public static readonly Color32 Disabled = Hex(0x8A8794);
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        /// <summary>Tint for a building preview that can't be placed.</summary>
        public static readonly Color Invalid = new Color(1f, 0.35f, 0.3f, 0.6f);

        /// <summary>Warm late-afternoon key light.</summary>
        public static readonly Color SunLight = Hex(0xFFF1DC);

        private static Color32 Hex(int rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);
    }
}
