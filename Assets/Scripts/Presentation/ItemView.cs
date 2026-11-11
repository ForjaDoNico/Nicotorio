using System.Collections.Generic;
using Factory.Simulation;
using UnityEngine;

namespace Factory.Presentation
{
    /// <summary>
    /// Draws items that live inside buildings (a miner's held output now; belt contents later).
    /// Items are simulation data, not GameObjects: every frame this view reuses a pool of
    /// SpriteRenderers, one per visible item, and hides the rest.
    /// </summary>
    public sealed class ItemView : MonoBehaviour
    {
        private World _world;
        private SpriteLibrary _sprites;
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private int _used;

        public void Init(World world, SpriteLibrary sprites)
        {
            _world = world;
            _sprites = sprites;
        }

        private void LateUpdate()
        {
            if (_world == null) return;
            _used = 0;

            foreach (Building b in _world.Buildings)
            {
                if (b is Miner miner && miner.Output != ItemType.None)
                {
                    // Waiting at the output edge, between the miner and the tile in front.
                    GridPos o = miner.Facing.ToOffset();
                    Draw(miner.Output, b.Position.X + 0.5f + o.X * 0.5f, b.Position.Y + 0.5f + o.Y * 0.5f);
                }
            }

            for (int i = _used; i < _pool.Count; i++)
                if (_pool[i].enabled) _pool[i].enabled = false;
        }

        private void Draw(ItemType item, float x, float y)
        {
            SpriteRenderer r;
            if (_used < _pool.Count)
            {
                r = _pool[_used];
            }
            else
            {
                var go = new GameObject("Item");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                r.sortingOrder = SortingOrders.Items;
                _pool.Add(r);
            }
            _used++;
            r.enabled = true;
            r.sprite = _sprites.ForItem(item);
            r.transform.position = new Vector3(x, y, 0f);
        }
    }
}
