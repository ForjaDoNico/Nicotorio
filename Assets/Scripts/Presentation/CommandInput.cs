using Factory.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Factory.Presentation
{
    /// <summary>
    /// Turns mouse/keyboard input into simulation commands. Held actions (mining) are sampled
    /// once per simulation tick via SimulationRunner.BeforeTick, so they are frame-rate independent.
    /// </summary>
    public sealed class CommandInput : MonoBehaviour
    {
        private World _world;
        private PlayerController _player;
        private Camera _camera;
        private InputAction _mine;

        /// <summary>Tile under the mouse this frame (may be out of bounds).</summary>
        public GridPos HoverTile { get; private set; }

        /// <summary>Result of the last mining step (for cursor feedback); null when not mining.</summary>
        public MineResult? LastMineResult { get; private set; }

        public void Init(World world, SimulationRunner runner, PlayerController player, Camera camera)
        {
            _world = world;
            _player = player;
            _camera = camera;

            _mine = new InputAction("Mine", InputActionType.Button, "<Mouse>/rightButton");
            _mine.Enable();

            runner.BeforeTick += IssueHeldCommands;
        }

        private void Update()
        {
            if (_world == null) return;
            HoverTile = PointerTile();
        }

        private void IssueHeldCommands()
        {
            // The runner ticks before this component's Update, so refresh the pointer here.
            HoverTile = PointerTile();
            if (_mine.IsPressed())
                LastMineResult = _world.MineStep(HoverTile, _player.Tile);
            else
                LastMineResult = null;
        }

        private GridPos PointerTile()
        {
            var mouse = Mouse.current;
            if (mouse == null) return new GridPos(-1, -1);
            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            return new GridPos(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        }

        private void OnDestroy()
        {
            _mine?.Dispose();
        }
    }
}
