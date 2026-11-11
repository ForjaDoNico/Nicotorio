using Factory.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Factory.Presentation
{
    /// <summary>
    /// Moves the player avatar with WASD. Movement is presentation-only (continuous position);
    /// the simulation receives the player's tile inside commands and validates reach itself.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        private const float Speed = 6f; // tiles per second
        private const float HalfSize = 0.35f;

        private World _world;
        private InputAction _move;

        public Vector2 Position => transform.position;
        public GridPos Tile => new GridPos(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y));

        public void Init(World world, SpriteLibrary sprites)
        {
            _world = world;

            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites.Player;
            renderer.sortingOrder = SortingOrders.Player;

            var spawn = world.Config.PlayerSpawn;
            transform.position = new Vector3(spawn.X + 0.5f, spawn.Y + 0.5f, 0f);

            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.Enable();
        }

        private void Update()
        {
            if (_world == null) return;
            Vector2 input = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            Vector2 p = (Vector2)transform.position + input * (Speed * Time.deltaTime);
            p.x = Mathf.Clamp(p.x, HalfSize, _world.Width - HalfSize);
            p.y = Mathf.Clamp(p.y, HalfSize, _world.Height - HalfSize);
            transform.position = new Vector3(p.x, p.y, 0f);
        }

        private void OnDestroy()
        {
            _move?.Dispose();
        }
    }
}
