using System;
using System.Collections.Generic;
using UnityEngine;

namespace Factory.Presentation
{
    /// <summary>
    /// Every sprite the game uses. Each one is loaded from a PNG in
    /// Assets/Resources/Sprites/&lt;FileName&gt;.png (16x16, drawn facing Up). If that file is
    /// missing, the sprite is drawn in code instead, so the game always runs. The same
    /// drawings seed the PNGs: menu Factory > Sprites > Exportar sprites gerados.
    /// </summary>
    public sealed class SpriteLibrary
    {
        public const int PixelsPerTile = 16;
        private const int N = PixelsPerTile;

        /// <summary>Folder under any Resources/ folder that holds the sprite PNGs.</summary>
        public const string ResourcesFolder = "Sprites";

        /// <summary>File name (without .png) and code fallback of every art sprite.</summary>
        public static readonly (string FileName, Func<Color32[]> Draw)[] Art =
        {
            ("grass_1", () => DrawGrass(1)),
            ("grass_2", () => DrawGrass(2)),
            ("iron_ore", DrawOre),
            ("player", DrawPlayer),
            ("cursor_frame", DrawCursorFrame),
            ("miner", DrawMiner),
            ("belt", DrawBelt),
            ("chest", DrawChest),
            ("item_iron_ore", DrawOreItem),
            ("furnace", DrawFurnace),
            ("item_iron_plate", DrawPlateItem),
        };

        public Sprite Grass { get; }
        public Sprite GrassAlt { get; }
        public Sprite IronOre { get; }
        public Sprite Player { get; }
        public Sprite CursorFrame { get; }

        /// <summary>One tile-sized white square for bars and tints. Always code: it is not art.</summary>
        public Sprite WhitePixel { get; }

        // Buildings are drawn facing Up; views rotate them to their Direction.
        public Sprite Miner { get; }
        public Sprite Belt { get; }
        public Sprite Chest { get; }
        public Sprite Furnace { get; }

        // Items (on belts, in miner outputs): small, centered, transparent around.
        public Sprite IronOreItem { get; }
        public Sprite IronPlateItem { get; }

        /// <summary>File names that had no PNG and were drawn in code this run.</summary>
        public List<string> Fallbacks { get; } = new List<string>();

        public SpriteLibrary()
        {
            var byName = new Dictionary<string, Sprite>();
            foreach (var (fileName, draw) in Art)
            {
                var loaded = Resources.Load<Sprite>(ResourcesFolder + "/" + fileName);
                if (loaded == null)
                {
                    Fallbacks.Add(fileName);
                    loaded = ToSprite(draw(), fileName);
                }
                byName[fileName] = loaded;
            }
            if (Fallbacks.Count > 0)
                Debug.Log($"[SpriteLibrary] Sem PNG em Resources/{ResourcesFolder}, desenhados por código: {string.Join(", ", Fallbacks)}");

            Grass = byName["grass_1"];
            GrassAlt = byName["grass_2"];
            IronOre = byName["iron_ore"];
            Player = byName["player"];
            CursorFrame = byName["cursor_frame"];
            Miner = byName["miner"];
            Belt = byName["belt"];
            Chest = byName["chest"];
            IronOreItem = byName["item_iron_ore"];
            Furnace = byName["furnace"];
            IronPlateItem = byName["item_iron_plate"];
            WhitePixel = ToSprite(Fill(new Color32(255, 255, 255, 255)), "white_pixel");
        }

        public Sprite ForBuilding(Factory.Simulation.BuildingType type)
        {
            switch (type)
            {
                case Factory.Simulation.BuildingType.Miner: return Miner;
                case Factory.Simulation.BuildingType.Belt: return Belt;
                case Factory.Simulation.BuildingType.Furnace: return Furnace;
                default: return Chest;
            }
        }

        public Sprite ForItem(Factory.Simulation.ItemType item)
        {
            switch (item)
            {
                case Factory.Simulation.ItemType.IronPlate: return IronPlateItem;
                default: return IronOreItem;
            }
        }

        /// <summary>Encodes a code-drawn sprite as PNG bytes, for the export tool.</summary>
        public static byte[] EncodePng(Color32[] px)
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return png;
        }

        // ---- builders ---------------------------------------------------------------

        private static Color32[] DrawGrass(int variant)
        {
            var px = Fill(Palette.Grass);
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                uint h = Hash(x, y, variant);
                if (h % 11 == 0) px[y * N + x] = Palette.GrassDark;
                else if (h % 17 == 0) px[y * N + x] = Palette.GrassLight;
            }
            return px;
        }

        private static Color32[] DrawOre()
        {
            var px = Fill(Palette.Grass);
            // A few chunky rocks: dark outline, ore body, one highlight pixel.
            DrawRock(px, 2, 3, 5, 4);
            DrawRock(px, 8, 8, 6, 5);
            DrawRock(px, 9, 1, 4, 3);
            DrawRock(px, 2, 10, 4, 4);
            return px;
        }

        private static void DrawRock(Color32[] px, int x0, int y0, int w, int h)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                bool corner = (x == x0 || x == x0 + w - 1) && (y == y0 || y == y0 + h - 1);
                if (corner) continue;
                bool edge = x == x0 || x == x0 + w - 1 || y == y0 || y == y0 + h - 1;
                Set(px, x, y, edge ? Palette.Shadow : Palette.Ore);
            }
            Set(px, x0 + 1, y0 + h - 2, Palette.OreHighlight);
        }

        private static Color32[] DrawPlayer()
        {
            var px = Fill(Palette.Clear);
            // Body 10x12 with outline, a trim band, and two eyes looking up.
            for (int y = 1; y <= 12; y++)
            for (int x = 3; x <= 12; x++)
            {
                bool corner = (x == 3 || x == 12) && (y == 1 || y == 12);
                if (corner) continue;
                bool edge = x == 3 || x == 12 || y == 1 || y == 12;
                Set(px, x, y, edge ? Palette.Shadow : Palette.HeroPrimary);
            }
            for (int x = 4; x <= 11; x++) Set(px, x, 5, Palette.HeroSecondary);
            Set(px, 6, 9, Palette.Shadow);
            Set(px, 9, 9, Palette.Shadow);
            Set(px, 6, 10, Palette.HeroSecondary);
            Set(px, 9, 10, Palette.HeroSecondary);
            // Little backpack nub for silhouette.
            for (int x = 6; x <= 9; x++) Set(px, x, 13, Palette.Shadow);
            return px;
        }

        private static Color32[] DrawOreItem()
        {
            var px = Fill(Palette.Clear);
            DrawRock(px, 5, 5, 6, 6);
            Set(px, 7, 7, Palette.OreHighlight);
            return px;
        }

        private static Color32[] DrawPlateItem()
        {
            var px = Fill(Palette.Clear);
            Box(px, 4, 5, 8, 6, Palette.Shadow, Palette.Plate);
            for (int x = 5; x <= 10; x++) Set(px, x, 6, Palette.PlateDark); // bottom bevel
            Set(px, 5, 9, Palette.OreHighlight);
            Set(px, 6, 9, Palette.OreHighlight);
            return px;
        }

        private static Color32[] DrawFurnace()
        {
            var px = Fill(Palette.Clear);
            Box(px, 1, 1, 14, 13, Palette.Shadow, Palette.Brick);
            // Mortar lines in a running-bond pattern.
            for (int y = 3; y <= 12; y += 3)
            for (int x = 2; x <= 13; x++)
                Set(px, x, y, Palette.BrickDark);
            for (int y = 2; y <= 12; y++)
            {
                int offset = (y / 3) % 2 == 0 ? 4 : 7;
                if (y % 3 != 0) { Set(px, offset, y, Palette.BrickDark); Set(px, offset + 6, y, Palette.BrickDark); }
            }
            // Fire mouth in the middle.
            Box(px, 5, 4, 6, 5, Palette.Shadow, Palette.Fire);
            Set(px, 7, 5, Palette.Accent);
            Set(px, 8, 6, Palette.Accent);
            // Output side: nozzle + arrow on the top edge, like the miner.
            for (int x = 6; x <= 9; x++) Set(px, x, 14, Palette.Shadow);
            Chevron(px, 13, Palette.Accent);
            return px;
        }

        private static Color32[] DrawMiner()
        {
            var px = Fill(Palette.Clear);
            Box(px, 1, 1, 14, 13, Palette.Shadow, Palette.Props);
            // Inner housing and two bolts.
            Box(px, 4, 3, 8, 6, Palette.Shadow, Palette.Stone);
            Set(px, 2, 2, Palette.HeroSecondary);
            Set(px, 13, 2, Palette.HeroSecondary);
            // Output nozzle + arrow at the top edge: the side it delivers to.
            for (int x = 6; x <= 9; x++) Set(px, x, 14, Palette.Shadow);
            Chevron(px, 12, Palette.Accent);
            return px;
        }

        private static Color32[] DrawBelt()
        {
            var px = Fill(Palette.Shadow);
            // Rails on both sides, a lighter bed, and two chevrons pointing along the belt.
            for (int y = 0; y < N; y++)
            {
                Set(px, 0, y, Palette.Stone);
                Set(px, 1, y, Palette.Stone);
                Set(px, N - 2, y, Palette.Stone);
                Set(px, N - 1, y, Palette.Stone);
                for (int x = 3; x <= N - 4; x++) Set(px, x, y, Palette.BeltBed);
            }
            Chevron(px, 13, Palette.Path);
            Chevron(px, 6, Palette.Path);
            return px;
        }

        private static Color32[] DrawChest()
        {
            var px = Fill(Palette.Clear);
            Box(px, 1, 2, 14, 12, Palette.Shadow, Palette.Path);
            for (int x = 2; x <= 13; x++) Set(px, x, 9, Palette.Shadow);   // lid seam
            for (int x = 2; x <= 13; x++) Set(px, x, 12, Palette.PathLight); // lid highlight
            Box(px, 7, 7, 2, 4, Palette.Shadow, Palette.Accent);           // latch
            return px;
        }

        /// <summary>A "^" pointing up with its apex on row top, 2 px thick, centered.</summary>
        private static void Chevron(Color32[] px, int top, Color32 c)
        {
            for (int k = 0; k < 4; k++)
            {
                Set(px, 7 - k, top - k, c); Set(px, 8 + k, top - k, c);
                Set(px, 7 - k, top - k - 1, c); Set(px, 8 + k, top - k - 1, c);
            }
        }

        /// <summary>Filled rectangle with a 1 px outline and clipped corners.</summary>
        private static void Box(Color32[] px, int x0, int y0, int w, int h, Color32 outline, Color32 fill)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                bool left = x == x0, right = x == x0 + w - 1, bottom = y == y0, top = y == y0 + h - 1;
                if ((left || right) && (bottom || top) && w > 2 && h > 2) continue;
                Set(px, x, y, left || right || bottom || top ? outline : fill);
            }
        }

        /// <summary>White corner brackets; tinted per state by the cursor view.</summary>
        private static Color32[] DrawCursorFrame()
        {
            var px = Fill(Palette.Clear);
            var w = new Color32(255, 255, 255, 255);
            for (int i = 0; i < 4; i++)
            {
                Set(px, i, 0, w); Set(px, 0, i, w);
                Set(px, N - 1 - i, 0, w); Set(px, N - 1, i, w);
                Set(px, i, N - 1, w); Set(px, 0, N - 1 - i, w);
                Set(px, N - 1 - i, N - 1, w); Set(px, N - 1, N - 1 - i, w);
            }
            return px;
        }


        // ---- helpers ----------------------------------------------------------------

        private static Color32[] Fill(Color32 c)
        {
            var px = new Color32[N * N];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            return px;
        }

        private static void Set(Color32[] px, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= N || y >= N) return;
            px[y * N + x] = c;
        }

        private static uint Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + salt * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        private static Sprite ToSprite(Color32[] px, string name)
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), PixelsPerTile);
            sprite.name = name;
            return sprite;
        }
    }
}
