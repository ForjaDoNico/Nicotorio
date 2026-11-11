using System.Collections.Generic;
using Factory.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Factory.Presentation
{
    /// <summary>
    /// Turns mouse/keyboard input into simulation commands. Discrete commands (place, remove)
    /// are queued as they happen and applied at the next tick boundary; held actions (mining)
    /// are sampled once per tick. Both go through SimulationRunner.BeforeTick, so the simulation
    /// only ever changes between ticks, in a fixed order.
    /// </summary>
    public sealed class CommandInput : MonoBehaviour
    {
        private enum CommandKind : byte { Place, Remove, TakeFromChest, InteractWithFurnace }

        private struct Command
        {
            public CommandKind Kind;
            public BuildingType Type;
            public GridPos Tile;
            public Direction Facing;
        }

        private World _world;
        private PlayerController _player;
        private Camera _camera;

        private InputAction _mine, _place, _remove, _rotate, _select1, _select2, _select3, _select4;
        private readonly List<Command> _pending = new List<Command>();

        // Drag-building: while a button is held, each new tile under the mouse gets one command.
        private GridPos? _lastPlaceTile;
        private GridPos? _lastRemoveTile;

        /// <summary>Tile under the mouse this frame (may be out of bounds).</summary>
        public GridPos HoverTile { get; private set; }

        /// <summary>Building chosen with 1/2/3, or null when building is off.</summary>
        public BuildingType? Selected { get; private set; }

        /// <summary>Facing for the next placed building, changed with R.</summary>
        public Direction Facing { get; private set; } = Direction.Up;

        /// <summary>Bumped when Selected or Facing changes, for views.</summary>
        public int SelectionVersion { get; private set; }

        public void Init(World world, SimulationRunner runner, PlayerController player, Camera camera)
        {
            _world = world;
            _player = player;
            _camera = camera;

            _mine = Button("Mine", "<Mouse>/rightButton");
            _place = Button("Place", "<Mouse>/leftButton");
            _remove = Button("Remove", "<Keyboard>/x");
            _rotate = Button("Rotate", "<Keyboard>/r");
            _select1 = Button("Select1", "<Keyboard>/1");
            _select2 = Button("Select2", "<Keyboard>/2");
            _select3 = Button("Select3", "<Keyboard>/3");
            _select4 = Button("Select4", "<Keyboard>/4");

            runner.BeforeTick += ApplyCommands;
        }

        private static InputAction Button(string name, string binding)
        {
            var action = new InputAction(name, InputActionType.Button, binding);
            action.Enable();
            return action;
        }

        private void Update()
        {
            if (_world == null) return;
            HoverTile = PointerTile();

            if (_select1.WasPressedThisFrame()) ToggleSelect(BuildingType.Miner);
            if (_select2.WasPressedThisFrame()) ToggleSelect(BuildingType.Belt);
            if (_select3.WasPressedThisFrame()) ToggleSelect(BuildingType.Chest);
            if (_select4.WasPressedThisFrame()) ToggleSelect(BuildingType.Furnace);
            if (_rotate.WasPressedThisFrame())
            {
                Facing = Facing.RotateClockwise();
                SelectionVersion++;
            }

            // Left click that starts on a chest or furnace interacts with it instead of building.
            if (_place.WasPressedThisFrame() && _world.InBounds(HoverTile))
            {
                Building clicked = _world.GetBuilding(HoverTile);
                CommandKind? interact = clicked is Chest ? CommandKind.TakeFromChest
                    : clicked is Furnace ? CommandKind.InteractWithFurnace
                    : (CommandKind?)null;
                if (interact.HasValue)
                {
                    _pending.Add(new Command { Kind = interact.Value, Tile = HoverTile });
                    _lastPlaceTile = HoverTile; // the drag starts here; don't also try to build on it
                }
            }

            // Place: on press, then once per new tile while held.
            if (_place.IsPressed() && Selected.HasValue && _world.InBounds(HoverTile))
            {
                if (_lastPlaceTile != HoverTile)
                {
                    _pending.Add(new Command { Kind = CommandKind.Place, Type = Selected.Value, Tile = HoverTile, Facing = Facing });
                    _lastPlaceTile = HoverTile;
                }
            }
            else
            {
                _lastPlaceTile = null;
            }

            // Remove: same drag behaviour with X.
            if (_remove.IsPressed() && _world.InBounds(HoverTile))
            {
                if (_lastRemoveTile != HoverTile)
                {
                    _pending.Add(new Command { Kind = CommandKind.Remove, Tile = HoverTile });
                    _lastRemoveTile = HoverTile;
                }
            }
            else
            {
                _lastRemoveTile = null;
            }
        }

        private void ToggleSelect(BuildingType type)
        {
            Selected = Selected == type ? (BuildingType?)null : type;
            SelectionVersion++;
        }

        /// <summary>Runs right before each World.Tick().</summary>
        private void ApplyCommands()
        {
            foreach (Command c in _pending)
            {
                switch (c.Kind)
                {
                    case CommandKind.Place: _world.TryPlace(c.Type, c.Tile, c.Facing); break;
                    case CommandKind.Remove: _world.TryRemove(c.Tile); break;
                    case CommandKind.TakeFromChest: _world.TakeAllFromChest(c.Tile); break;
                    case CommandKind.InteractWithFurnace: _world.InteractWithFurnace(c.Tile); break;
                }
            }
            _pending.Clear();

            // The runner ticks before this component's Update, so refresh the pointer here.
            HoverTile = PointerTile();
            if (_mine.IsPressed()) _world.MineStep(HoverTile, _player.Tile);
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
            _place?.Dispose();
            _remove?.Dispose();
            _rotate?.Dispose();
            _select1?.Dispose();
            _select2?.Dispose();
            _select3?.Dispose();
            _select4?.Dispose();
        }
    }
}
