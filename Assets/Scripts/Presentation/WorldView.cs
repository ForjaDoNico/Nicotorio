using Factory.Simulation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Factory.Presentation
{
    /// <summary>Draws the ground grid into a single Tilemap. Tile (x,y) covers world [x,x+1) x [y,y+1).</summary>
    public sealed class WorldView : MonoBehaviour
    {
        private World _world;
        private Tilemap _tilemap;
        private Tile _grass, _grassAlt, _ore;

        public void Init(World world, SpriteLibrary sprites)
        {
            _world = world;

            var grid = gameObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            var tilemapGo = new GameObject("Ground");
            tilemapGo.transform.SetParent(transform, false);
            _tilemap = tilemapGo.AddComponent<Tilemap>();
            var tilemapRenderer = tilemapGo.AddComponent<TilemapRenderer>();
            tilemapRenderer.sortingOrder = SortingOrders.Ground;

            _grass = MakeTile(sprites.Grass);
            _grassAlt = MakeTile(sprites.GrassAlt);
            _ore = MakeTile(sprites.IronOre);

            RedrawAll();
        }

        public void RedrawAll()
        {
            int count = _world.Width * _world.Height;
            var positions = new Vector3Int[count];
            var tiles = new TileBase[count];
            int i = 0;
            for (int y = 0; y < _world.Height; y++)
            for (int x = 0; x < _world.Width; x++)
            {
                positions[i] = new Vector3Int(x, y, 0);
                tiles[i] = TileFor(new GridPos(x, y));
                i++;
            }
            _tilemap.SetTiles(positions, tiles);
        }

        private TileBase TileFor(GridPos p)
        {
            if (_world.GetTile(p) == TileType.IronOre) return _ore;
            // Checker-ish variation so the grass doesn't read as one flat sheet.
            return ((p.X * 7 + p.Y * 13) % 5 == 0) ? _grassAlt : _grass;
        }

        private static Tile MakeTile(Sprite sprite)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.name = sprite.name;
            return tile;
        }
    }

    /// <summary>Draw order of every layer, in one place.</summary>
    public static class SortingOrders
    {
        public const int Ground = 0;
        public const int Buildings = 10;
        public const int Items = 20;
        public const int Player = 30;
        public const int Cursor = 40;
    }
}
