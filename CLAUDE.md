# Factory prototype (Unity 6000.6.8f1, URP 2D)

A minimal top-down automation game: a 64x64 grid with iron and coal patches, a player who mines by hand,
and four buildings (miner, belt, chest, furnace). The loop is mine → smelt → build: buildings
cost items, which start from hand mining. The scene `Assets/Scenes/Factory.unity` is empty
on disk: everything is created in code when Play is pressed.

## Architecture

```
Assets/Scripts/Simulation/    Factory.Simulation.asmdef  (noEngineReferences: pure C#)
Assets/Scripts/Presentation/  Factory.Presentation.asmdef (MonoBehaviours, Input System, URP 2D, uGUI)
Assets/Scripts/Editor/        Factory.Editor.asmdef (sprite import rules + Factory > Sprites menu)
Assets/Resources/Sprites/     the art: one 16x16 PNG per sprite, packed by Assets/Resources/SpriteAtlas.spriteatlasv2
Assets/Tests/EditMode/        Factory.Simulation.Tests.asmdef (NUnit, tests the simulation only)
```

### Simulation (no UnityEngine)
- `World` holds all state: tiles, player inventory, hand-mining progress and buildings.
  It changes only through commands (`MineStep`, `TryPlace`, `TryRemove`, `TakeAllFromChest`,
  `LoadFurnaceInput`, `LoadFurnaceFuel`, `TakeFurnaceOutput`, `InteractWithFurnace` (all three at
  once), `TryInsert`) and `Tick()`.
- `Tick()` runs at a fixed `WorldConfig.TicksPerSecond = 20`. The systems run in a fixed
  order (hand-mining reset, then `TickMiners`, then `TickFurnaces`, then `TickBelts`), and each walks
  `World.Buildings`, which is kept in **Id (placement) order**. Never iterate the
  `Dictionary` to drive gameplay.
- Buildings (`Buildings.cs`) are plain data. Behaviour lives in World's tick systems.
  - **Miner**: every `MinerTicksPerItem` ticks it digs 1 item into `Output`, which goes to `Front`.
    When the item is refused, the miner holds it and stalls.
  - **Belt**: `Items` is a list of `BeltItem` structs with integer positions from 0 (entry edge) to
    `Belt.Length` = 1000 (exit edge), ordered front first.
    - Each item moves `BeltSpeed` per tick and keeps at least `BeltItemSpacing` from the item ahead.
    - The front item is offered to `Front`; if refused, the belt backs up.
    - A full belt refuses inserts, which blocks whatever feeds it.
  - **Chest**: `Contents` is an `Inventory` with no limit.
  - **Furnace**: `Input`, `Fuel` and `Output` are `ItemSlot`s (one item type plus a count), capped
    by `FurnaceInputCapacity`, `FurnaceFuelCapacity` and `FurnaceOutputCapacity`.
    - It smelts one item every `FurnaceTicksPerItem` ticks, only while it has smeltable input,
      room in the output and burn time; otherwise it stalls and keeps its progress.
    - Each smelting tick uses one tick of `BurnTicksLeft`. When that runs out, it lights the next
      fuel item (coal = `CoalBurnTicks` = 160 = 4 plates). Fuel never burns while idle.
    - Each tick it offers one output item to `Front`.
    - What can be smelted lives only in `Recipes.TryGetSmelting`, and what burns only in
      `Recipes.BurnTicks`. Today that is IronOre → IronPlate, with Coal as the fuel. Replace those
      functions to move to a recipe table.
- `TryInsert(pos, item)` is the single handoff rule:
  - a chest always accepts;
  - a belt accepts when its entry edge is free;
  - a furnace takes fuel into `Fuel` and smeltable items into `Input`, while there is room.
    Feed ore and coal on separate belts or from different sides. A single mixed belt can jam:
    once the fuel slot is full, a coal item at the front of the belt blocks the ore behind it.
- **Build costs**: `WorldConfig.BuildCosts` has one `BuildCost` line per (building, item).
  - `CanPlace` checks the location first, then returns `NotEnoughItems` if `CanAfford` fails.
  - `TryPlace` pays the cost from `PlayerInventory`, and `TryRemove` refunds it in full.
  - `WorldConfig.StartingInventory` is empty by default, so a run starts from hand mining.
- Removing a building gives the player its build cost plus whatever it held (chest contents,
  miner output, belt items, furnace input and output). Items are never lost or duplicated.
- Tiles: Grass, IronOre and CoalOre. `TileYield.Of` gives the item a tile mines into, and miners go
  on any tile that yields something. `WorldGenerator` stamps iron first, then coal on grass only,
  so adding a resource never moves the existing patches.
- All randomness goes through `DeterministicRandom` (SplitMix64), seeded from `WorldConfig.Seed`.
  Tunables all live in `WorldConfig`.
- Views poll version counters (`Inventory.Version`, `World.BuildingsVersion`) to redraw only on change.

### Presentation
- **`GameBootstrap`** is the only entry point. A `[RuntimeInitializeOnLoadMethod]` runs when the
  `Factory` scene loads. It creates the `World`, the `SpriteLibrary` and every component, and gives
  each one its dependencies through `Init(...)`. There are no singletons, no `FindObjectOfType`
  and no Inspector references.
- **`SimulationRunner`** accumulates time and calls `World.Tick()`. It raises `BeforeTick` and
  `AfterTick`, and its `Alpha` lets views interpolate between ticks.
- **`CommandInput`** turns Input System actions into commands:
  - left click places;
  - left click that starts on a chest takes its contents;
  - left click that starts on a furnace opens the `FurnaceWindow`;
  - a left click elsewhere in the world while the window is open only closes it;
  - clicks over any UI never reach the world. `EventSystem.IsPointerOverGameObject` is used, and
    the bootstrap creates the EventSystem with an `InputSystemUIInputModule`;
  - a drag that fails for lack of items stops placing until the button is released;
  - X removes;
  - right mouse button mines;
  - 1/2/3/4 select a building (order in `BuildingNames.All`), R rotates.

  Commands are queued in `Update` and applied in `BeforeTick`, so the simulation only changes between ticks.
- **Views** read the World and never write to it:
  - `WorldView` draws the ground as a Tilemap;
  - `BuildingView` keeps pooled SpriteRenderers per building Id;
  - `ItemView` draws belt items and miner outputs. Each frame it reuses a pooled list of renderers,
    because items are not GameObjects;
  - `CursorView` draws the hover frame, the mining bar and the ghost;
  - `HudView` is a uGUI overlay using the legacy `Text` with the built-in font: inventory,
    the hotbar (each building's cost, greyed out when unaffordable), and a panel for the hovered
    chest or furnace;
  - `FurnaceWindow` is a uGUI window with ore, coal and plate slots plus smelting and flame bars.
    - Clicking a slot calls `CommandInput.QueueFurnaceAction`, so the change is applied at the
      next tick like every command.
    - It closes with Esc or E, on a click outside, when the player walks more than 6 tiles away,
      or when the furnace is removed.
- **Art**: `SpriteLibrary` loads each sprite with `Resources.Load` from `Assets/Resources/Sprites/<file>.png`.
  The file names are listed in `SpriteLibrary.Art`.
  - Sprites are 16x16 and drawn facing Up; `BuildingView.RotationFor` rotates them.
  - If a PNG is missing, the sprite is drawn in code from the same table, using colors from `Palette`.
  - `SpriteImportRules` (an AssetPostprocessor) forces pixel-art import settings on that folder:
    16 PPU, Point filter, uncompressed, no mipmaps. Replacing a PNG needs no Inspector work.
  - Use the menu **Factory > Sprites** to re-export the code drawings or rebuild the atlas.
  - Do not put other files such as docs in `Resources/`: everything there ships in the build.

## Conventions
- Gameplay rules go in the Simulation assembly, with a test. Presentation only draws and issues commands.
- Do not hand-edit `.unity`, `.prefab` or `.meta` files. Unity generates the `.meta` files.
  Scene and settings changes go through the Editor or a script.
- New tunables go in `WorldConfig`. Tests build their own config with readable numbers.
- Use integer math in the simulation for anything that must be exact (belt positions, ticks).
- New buildings need:
  - a `BuildingType` value and its `Building` subclass;
  - a case in `World.Create`;
  - a tick system or `TryInsert` case if it moves items;
  - a sprite (an entry in `SpriteLibrary.Art` plus its PNG, wired in `ForBuilding`);
  - a name in `HudView.BuildingNames`, and a place in `BuildingNames.All` with a key in `CommandInput`;
  - a line in `WorldConfig.BuildCosts`.
- Tests that are not about costs build with `TestWorlds.Empty` or `BuildCosts = TestWorlds.NoCosts`.
  `BuildCostTests` uses the real table.
- One commit per feature, with tests passing.

## Running
- Open the project and the `Assets/Scenes/Factory` scene, then press Play.
- Controls:
  - WASD: move
  - Right mouse: mine ore (within 3 tiles)
  - 1/2/3/4: select miner/belt/chest/furnace
  - R: rotate
  - Left click (or drag): build
  - Left click on a chest: take its contents
  - Left click on a furnace: open its window. Click the ore or coal slot to load yours, click the
    plates to take them. Esc or E closes it.
  - X (or drag): remove

## Tests
EditMode tests are in `Assets/Tests/EditMode` (assembly `Factory.Simulation.Tests`):
- world generation;
- hand mining;
- placement;
- miner rate;
- belt movement, delivery and blocking;
- chest accumulation;
- furnace smelting;
- coal: fuel burn, coal patches, separate feed lines;
- build costs, including the full loop from an empty inventory to a built miner.

Run them in either of these ways:
- In the Editor: Window > General > Test Runner > EditMode > Run All.
- From the CLI, with the Editor open:
  ```bash
  export UNITY_PROJECT_PATH="$PWD"
  ~/.unity/bin/unity command test run --mode editor \
    --filter Factory.Simulation.Tests --filter-type Assembly --timeout 240
  ```

**Leave Play mode first.** EditMode tests cannot run during Play: the run hangs until it times out.
To stop Play from the CLI, run `~/.unity/bin/unity command editor.playmode stop`.
