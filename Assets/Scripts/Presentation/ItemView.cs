using System.Collections.Generic;
using Factory.Simulation;
using UnityEngine;

namespace Factory.Presentation
{
    /// <summary>
    /// Draws items that live inside buildings: miner and furnace outputs and belt contents.
    /// Items are simulation data, not GameObjects: every frame this view reuses a pool of
    /// SpriteRenderers, one per visible item, and hides the rest.
    /// </summary>
    public sealed class ItemView : MonoBehaviour
    {
        private World _world;
        private SpriteLibrary _sprites;
        private SimulationRunner _runner;
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private int _used;

        public void Init(World world, SpriteLibrary sprites, SimulationRunner runner)
        {
            _world = world;
            _sprites = sprites;
            _runner = runner;
        }

        private void LateUpdate()
        {
            if (_world == null) return;
            _used = 0;
            float alpha = _runner.Alpha;

            foreach (Building b in _world.Buildings)
            {
                if (b is Miner miner && miner.Output != ItemType.None)
                {
                    // Waiting at the output edge, between the miner and the tile in front.
                    GridPos o = miner.Facing.ToOffset();
                    Draw(miner.Output, b.Position.X + 0.5f + o.X * 0.5f, b.Position.Y + 0.5f + o.Y * 0.5f);
                }
                else if (b is Furnace furnace && !furnace.Output.IsEmpty)
                {
                    GridPos o = furnace.Facing.ToOffset();
                    Draw(furnace.Output.Item, b.Position.X + 0.5f + o.X * 0.5f, b.Position.Y + 0.5f + o.Y * 0.5f);
                }
                else if (b is Belt belt)
                {
                    // Along the belt from its entry edge (-0.5) to its exit edge (+0.5),
                    // interpolated between the last two ticks so motion is smooth at any frame rate.
                    GridPos o = belt.Facing.ToOffset();
                    foreach (BeltItem it in belt.Items)
                    {
                        float t = Mathf.Lerp(it.PreviousPosition, it.Position, alpha) / Belt.Length - 0.5f;
                        Draw(it.Item, b.Position.X + 0.5f + o.X * t, b.Position.Y + 0.5f + o.Y * t);
                    }
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
