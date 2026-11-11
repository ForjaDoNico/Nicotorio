using System.Collections.Generic;
using Factory.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private enum CommandKind : byte { Place, Remove, TakeFromChest, LoadFurnaceInput, LoadFurnaceFuel, TakeFurnaceOutput, Craft }

        private struct Command
        {
            public CommandKind Kind;
            public BuildingType Type;
            public GridPos Tile;
            public Direction Facing;
            public int RecipeId;
            public int Times;
        }

        private World _world;
        private PlayerController _player;
        private Camera _camera;

        private InputAction _mine, _place, _remove, _rotate;
        private InputAction[] _selectKeys; // key N selects BuildingNames.All[N - 1]
        private readonly List<Command> _pending = new List<Command>();

        // Drag-building: while a button is held, each new tile under the mouse gets one command.
        private GridPos? _lastPlaceTile;
        private GridPos? _lastRemoveTile;

        // Set when a placement in the current drag failed for lack of items: the drag stops
        // placing until the button is released, instead of resuming if items show up.
        private bool _outOfItemsThisDrag;

        // Set when a left click was used by the UI (a click on a window, or the click that
        // closed one): that press must not also build in the world until it is released.
        private bool _clickUsedByUi;

        private FurnaceWindow _furnaceWindow;
        private CraftingWindow _craftingWindow;

        /// <summary>Tile under the mouse this frame (may be out of bounds).</summary>
        public GridPos HoverTile { get; private set; }

        /// <summary>Building chosen with the number keys, or null when building is off.</summary>
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
            _selectKeys = new InputAction[BuildingNames.All.Length];
            for (int i = 0; i < _selectKeys.Length; i++)
                _selectKeys[i] = Button("Select" + (i + 1), "<Keyboard>/" + (i + 1));

            runner.BeforeTick += ApplyCommands;
        }

        /// <summary>Connects the building windows, which are created after this component.</summary>
        public void AttachWindows(FurnaceWindow furnace, CraftingWindow crafting)
        {
            _furnaceWindow = furnace;
            _craftingWindow = crafting;
        }

        /// <summary>Queues crafting at a table; applied at the next tick like every command.</summary>
        public void QueueCraft(GridPos table, int recipeId, int times) =>
            _pending.Add(new Command { Kind = CommandKind.Craft, Tile = table, RecipeId = recipeId, Times = times });

        private bool AnyWindowOpen =>
            (_furnaceWindow != null && _furnaceWindow.IsOpen) || (_craftingWindow != null && _craftingWindow.IsOpen);

        private void CloseWindows()
        {
            if (_furnaceWindow != null) _furnaceWindow.Close();
            if (_craftingWindow != null) _craftingWindow.Close();
        }

        /// <summary>Queues a furnace window action; applied at the next tick like every command.</summary>
        public void QueueFurnaceAction(GridPos furnace, FurnaceAction action)
        {
            CommandKind kind = action == FurnaceAction.LoadInput ? CommandKind.LoadFurnaceInput
                : action == FurnaceAction.LoadFuel ? CommandKind.LoadFurnaceFuel
                : CommandKind.TakeFurnaceOutput;
            _pending.Add(new Command { Kind = kind, Tile = furnace });
        }

        /// <summary>True when the mouse is over any HUD panel or window, which take the click instead.</summary>
        private static bool PointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

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

            for (int i = 0; i < _selectKeys.Length; i++)
                if (_selectKeys[i].WasPressedThisFrame()) ToggleSelect(BuildingNames.All[i]);
            if (_rotate.WasPressedThisFrame())
            {
                Facing = Facing.RotateClockwise();
                SelectionVersion++;
            }

            bool overUi = PointerOverUi();

            // A left click that starts on a chest takes its contents; on a furnace or crafting table
            // it opens that window; anywhere else in the world it closes an open window (and only that).
            if (_place.WasPressedThisFrame())
            {
                if (overUi)
                {
                    _clickUsedByUi = true;
                }
                else if (_world.InBounds(HoverTile))
                {
                    Building clicked = _world.GetBuilding(HoverTile);
                    if (clicked is Chest)
                    {
                        _pending.Add(new Command { Kind = CommandKind.TakeFromChest, Tile = HoverTile });
                        _lastPlaceTile = HoverTile; // the drag starts here; don't also try to build on it
                    }
                    else if (clicked is Furnace && _furnaceWindow != null)
                    {
                        CloseWindows();
                        _furnaceWindow.Open(HoverTile);
                        _lastPlaceTile = HoverTile;
                    }
                    else if (clicked is CraftingTable && _craftingWindow != null)
                    {
                        CloseWindows();
                        _craftingWindow.Open(HoverTile);
                        _lastPlaceTile = HoverTile;
                    }
                    else if (AnyWindowOpen)
                    {
                        CloseWindows();
                        _clickUsedByUi = true;
                    }
                }
            }

            // Place: on press, then once per new tile while held.
            if (_place.IsPressed() && Selected.HasValue && _world.InBounds(HoverTile)
                && !_outOfItemsThisDrag && !_clickUsedByUi && !overUi)
            {
                if (_lastPlaceTile != HoverTile)
                {
                    _pending.Add(new Command { Kind = CommandKind.Place, Type = Selected.Value, Tile = HoverTile, Facing = Facing });
                    _lastPlaceTile = HoverTile;
                }
            }
            else if (!_place.IsPressed())
            {
                _lastPlaceTile = null;
                _outOfItemsThisDrag = false;
                _clickUsedByUi = false;
            }

            // Remove: same drag behaviour with X.
            if (_remove.IsPressed() && _world.InBounds(HoverTile) && !overUi)
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
                    case CommandKind.Place:
                        if (_outOfItemsThisDrag) break; // rest of this drag's queue is dropped
                        if (_world.TryPlace(c.Type, c.Tile, c.Facing) == PlaceResult.NotEnoughItems)
                            _outOfItemsThisDrag = true;
                        break;
                    case CommandKind.Remove: _world.TryRemove(c.Tile); break;
                    case CommandKind.TakeFromChest: _world.TakeAllFromChest(c.Tile); break;
                    case CommandKind.LoadFurnaceInput: _world.LoadFurnaceInput(c.Tile); break;
                    case CommandKind.LoadFurnaceFuel: _world.LoadFurnaceFuel(c.Tile); break;
                    case CommandKind.TakeFurnaceOutput: _world.TakeFurnaceOutput(c.Tile); break;
                    case CommandKind.Craft: _world.TryCraft(c.Tile, _player.Tile, c.RecipeId, c.Times); break;
                }
            }
            _pending.Clear();

            // The runner ticks before this component's Update, so refresh the pointer here.
            HoverTile = PointerTile();
            if (_mine.IsPressed() && !PointerOverUi()) _world.MineStep(HoverTile, _player.Tile);
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
            if (_selectKeys != null)
                foreach (InputAction key in _selectKeys) key?.Dispose();
        }
    }
}
