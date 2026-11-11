# Factory prototype (Unity 6000.6.8f1, URP 2D)

A minimal top-down automation game: a 64x64 grid with iron ore, a player who mines by hand,
and three buildings (miner, belt, chest). The scene `Assets/Scenes/Factory.unity` is empty
on disk: everything is created in code when Play is pressed.

## Architecture

```
Assets/Scripts/Simulation/    Factory.Simulation.asmdef  (noEngineReferences: pure C#)
Assets/Scripts/Presentation/  Factory.Presentation.asmdef (MonoBehaviours, Input System, URP 2D, uGUI)
Assets/Tests/EditMode/        Factory.Simulation.Tests.asmdef (NUnit, tests the simulation only)
```

### Simulation (no UnityEngine)
- `World` holds all state: tiles, player inventory, hand-mining progress and buildings.
  It changes only through commands (`MineStep`, `TryPlace`, `TryRemove`, `TakeAllFromChest`,
  `TryInsert`) and `Tick()`.
- `Tick()` runs at a fixed `WorldConfig.TicksPerSecond = 20`. The systems run in a fixed
  order (hand-mining reset, then `TickMiners`, then `TickBelts`), and each walks
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
- `TryInsert(pos, item)` is the single handoff rule: a chest always accepts, and a belt accepts
  when its entry edge is free.
- Removing a building gives whatever it held (chest contents, miner output, belt items) to the
  player. Items are never lost or duplicated.
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
  - left click places, and left click that starts on a chest takes its contents;
  - X removes;
  - right mouse button mines;
  - 1/2/3 select a building, R rotates.

  Commands are queued in `Update` and applied in `BeforeTick`, so the simulation only changes between ticks.
- **Views** read the World and never write to it:
  - `WorldView` draws the ground as a Tilemap;
  - `BuildingView` keeps pooled SpriteRenderers per building Id;
  - `ItemView` draws belt items and miner outputs. Each frame it reuses a pooled list of renderers,
    because items are not GameObjects;
  - `CursorView` draws the hover frame, the mining bar and the ghost;
  - `HudView` is a uGUI overlay using the legacy `Text` with the built-in font: inventory,
    hotbar and a hovered-chest panel.
- **Art**: all sprites are generated in `SpriteLibrary` as 16x16, Point filter, 16 PPU, drawn facing Up
  and rotated by `BuildingView.RotationFor`. Every color comes from `Palette`.

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
  - a sprite in `SpriteLibrary.ForBuilding`;
  - a name in `HudView.BuildingNames`.
- One commit per feature, with tests passing.

## Running
- Open the project and the `Assets/Scenes/Factory` scene, then press Play.
- Controls:
  - WASD: move
  - Right mouse: mine ore (within 3 tiles)
  - 1/2/3: select miner/belt/chest
  - R: rotate
  - Left click (or drag): build
  - Left click on a chest: take its contents
  - X (or drag): remove

## Tests
EditMode tests are in `Assets/Tests/EditMode` (assembly `Factory.Simulation.Tests`):
- world generation;
- hand mining;
- placement;
- miner rate;
- belt movement, delivery and blocking;
- chest accumulation.

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
