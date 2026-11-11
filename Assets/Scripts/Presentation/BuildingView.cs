using System.Collections.Generic;
using Factory.Simulation;
using UnityEngine;

namespace Factory.Presentation
{
    /// <summary>
    /// Mirrors World.Buildings as pooled SpriteRenderers. Resyncs only when
    /// World.BuildingsVersion changes; never writes to the simulation.
    /// </summary>
    public sealed class BuildingView : MonoBehaviour
    {
        private World _world;
        private SpriteLibrary _sprites;
        private int _shownVersion = -1;

        private readonly Dictionary<int, SpriteRenderer> _byId = new Dictionary<int, SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private readonly HashSet<int> _alive = new HashSet<int>();
        private readonly List<int> _toRemove = new List<int>();

        public void Init(World world, SpriteLibrary sprites)
        {
            _world = world;
            _sprites = sprites;
        }

        /// <summary>Buildings with an output side are drawn rotated; chests and tables always look the same.</summary>
        public static bool IsDirectional(BuildingType type) =>
            type != BuildingType.Chest && type != BuildingType.CraftingTable;

        public static Quaternion RotationFor(Direction facing) =>
            Quaternion.Euler(0f, 0f, -90f * (int)facing); // Directions are clockwise from Up

        private void LateUpdate()
        {
            if (_world == null || _world.BuildingsVersion == _shownVersion) return;
            _shownVersion = _world.BuildingsVersion;

            _alive.Clear();
            foreach (Building b in _world.Buildings)
            {
                _alive.Add(b.Id);
                if (_byId.ContainsKey(b.Id)) continue;

                SpriteRenderer r = Rent();
                r.sprite = _sprites.ForBuilding(b.Type);
                r.name = $"{b.Type} #{b.Id}";
                r.transform.position = new Vector3(b.Position.X + 0.5f, b.Position.Y + 0.5f, 0f);
                r.transform.rotation = IsDirectional(b.Type) ? RotationFor(b.Facing) : Quaternion.identity;
                _byId.Add(b.Id, r);
            }

            _toRemove.Clear();
            foreach (var pair in _byId)
                if (!_alive.Contains(pair.Key)) _toRemove.Add(pair.Key);
            foreach (int id in _toRemove)
            {
                Return(_byId[id]);
                _byId.Remove(id);
            }
        }

        private SpriteRenderer Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            var go = new GameObject("Building");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = SortingOrders.Buildings;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            r.gameObject.SetActive(false);
            _pool.Push(r);
        }
    }
}
