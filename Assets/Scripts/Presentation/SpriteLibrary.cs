using UnityEngine;

namespace Factory.Presentation
{
    /// <summary>Generates every sprite in code. No art assets on disk.</summary>
    public sealed class SpriteLibrary
    {
        public const int PixelsPerTile = 16;
        private const int N = PixelsPerTile;

        public Sprite Grass { get; }
        public Sprite GrassAlt { get; }
        public Sprite IronOre { get; }
        public Sprite Player { get; }

        public SpriteLibrary()
        {
            Grass = MakeGrass(1);
            GrassAlt = MakeGrass(2);
            IronOre = MakeOre();
            Player = MakePlayer();
        }

        // ---- builders ---------------------------------------------------------------

        private static Sprite MakeGrass(int variant)
        {
            var px = Fill(Palette.Grass);
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                uint h = Hash(x, y, variant);
                if (h % 11 == 0) px[y * N + x] = Palette.GrassDark;
                else if (h % 17 == 0) px[y * N + x] = Palette.GrassLight;
            }
            return ToSprite(px, "Grass" + variant);
        }

        private static Sprite MakeOre()
        {
            var px = Fill(Palette.Grass);
            // A few chunky rocks: dark outline, ore body, one highlight pixel.
            DrawRock(px, 2, 3, 5, 4);
            DrawRock(px, 8, 8, 6, 5);
            DrawRock(px, 9, 1, 4, 3);
            DrawRock(px, 2, 10, 4, 4);
            return ToSprite(px, "IronOre");
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

        private static Sprite MakePlayer()
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
            return ToSprite(px, "Player");
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
