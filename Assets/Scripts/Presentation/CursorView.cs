using Factory.Simulation;
using UnityEngine;

namespace Factory.Presentation
{
    /// <summary>
    /// Corner brackets on the hovered tile, a progress bar while hand mining, and a translucent
    /// preview of the selected building (red when World.CanPlace says no).
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class CursorView : MonoBehaviour
    {
        private static readonly Color Neutral = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color CanMine = Palette.Accent;
        private static readonly Color TooFar = new Color(1f, 1f, 1f, 0.25f);

        private World _world;
        private CommandInput _input;
        private PlayerController _player;
        private SpriteRenderer _frame;
        private SpriteRenderer _ghost;
        private SpriteLibrary _sprites;
        private Transform _barRoot;
        private Transform _barFill;

        public void Init(World world, CommandInput input, PlayerController player, SpriteLibrary sprites)
        {
            _world = world;
            _input = input;
            _player = player;
            _sprites = sprites;

            _ghost = new GameObject("Ghost").AddComponent<SpriteRenderer>();
            _ghost.transform.SetParent(transform, false);
            _ghost.sortingOrder = SortingOrders.Cursor - 1;

            _frame = gameObject.AddComponent<SpriteRenderer>();
            _frame.sprite = sprites.CursorFrame;
            _frame.sortingOrder = SortingOrders.Cursor;

            _barRoot = new GameObject("MineBar").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = new Vector3(0f, -0.62f, 0f);
            var back = MakeBarPart("Back", sprites.WhitePixel, Palette.Shadow, SortingOrders.Cursor);
            back.localScale = new Vector3(0.9f, 0.16f, 1f);
            _barFill = MakeBarPart("Fill", sprites.WhitePixel, Palette.Accent, SortingOrders.Cursor + 1);
        }

        private Transform MakeBarPart(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_barRoot, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            return go.transform;
        }

        private void LateUpdate()
        {
            if (_world == null) return;
            GridPos tile = _input.HoverTile;
            bool visible = _world.InBounds(tile);
            _frame.enabled = visible;
            _barRoot.gameObject.SetActive(false);
            _ghost.enabled = false;
            if (!visible) return;

            transform.position = new Vector3(tile.X + 0.5f, tile.Y + 0.5f, 0f);

            if (_input.Selected.HasValue)
            {
                BuildingType type = _input.Selected.Value;
                bool ok = _world.CanPlace(type, tile) == PlaceResult.Ok;
                _ghost.enabled = true;
                _ghost.sprite = _sprites.ForBuilding(type);
                _ghost.transform.rotation = type == BuildingType.Chest
                    ? Quaternion.identity
                    : BuildingView.RotationFor(_input.Facing);
                _ghost.color = ok ? new Color(1f, 1f, 1f, 0.6f) : Palette.Invalid;
                _frame.color = ok ? Neutral : Palette.Invalid;
                return;
            }

            bool mineable = TileYield.ForHand(_world.GetTile(tile)) != ItemType.None;
            bool inReach = World.IsWithinReach(_player.Tile, tile, _world.Config.HandMineReach);
            _frame.color = !mineable ? Neutral : inReach ? CanMine : TooFar;

            if (_world.IsMining && _world.MiningTarget == tile)
            {
                _barRoot.gameObject.SetActive(true);
                float p = _world.MiningProgress01;
                // Fill grows from the left edge of the 0.9-wide bar.
                _barFill.localScale = new Vector3(0.84f * p, 0.1f, 1f);
                _barFill.localPosition = new Vector3(-0.42f + 0.42f * p, 0f, 0f);
            }
        }
    }
}
