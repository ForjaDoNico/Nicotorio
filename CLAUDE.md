# Factory prototype (Unity 6000.6.8f1, URP 2D)

A minimal top-down automation game: a 64x64 grid with iron, coal and trees, a player who mines and
chops by hand, a crafting table, and four crafted buildings (miner, belt, chest, furnace).
The loop is chop → crafting table → mine and smelt → craft → build. A run starts with an empty
inventory: the table is paid in wood, and every other building is an item crafted at it. The scene `Assets/Scenes/Factory.unity` is empty
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
- `World` holds all state: tiles (plus wood left per tree), player inventory, hand-mining progress
  and buildings. It changes only through commands and `Tick()`. The commands are:
  - `MineStep`;
  - `TryPlace` and `TryRemove`;
  - `TakeAllFromChest`;
  - `LoadFurnaceInput`, `LoadFurnaceFuel` and `TakeFurnaceOutput`, plus `InteractWithFurnace`,
    which does all three at once;
  - `TryCraft`;
  - `TryInsert`.
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
      `Recipes.BurnTicks`. Today that is IronOre → IronPlate, with fuel Coal (`CoalBurnTicks` = 4
      plates) or Wood (`WoodBurnTicks` = 80 = 2 plates). Replace those functions to move to a
      table.
  - **CraftingTable**: holds nothing and has no tick system. It is where `TryCraft` works.
- `TryInsert(pos, item)` is the single handoff rule:
  - a chest always accepts;
  - a belt accepts when its entry edge is free;
  - a furnace takes fuel into `Fuel` and smeltable items into `Input`, while there is room.
    Feed ore and coal on separate belts or from different sides. A single mixed belt can jam:
    once the fuel slot is full, a coal item at the front of the belt blocks the ore behind it.
- **Buildings as items**: Miner, Belt, Chest and Furnace each have an `ItemType` of the same name.
  `BuildingItems.ItemFor` holds the fixed mapping; it is identity, not balance, so it is not in
  `WorldConfig`.
  - `World.PlacementCost(type)` is the single cost rule: the building's `WorldConfig.BuildCosts`
    lines plus 1 of its own item.
  - `BuildCosts` only has the crafting table (5 wood), so a run can always start.
  - `CanPlace` checks the location first (`OutOfBounds`, `Occupied`, `Blocked` by a tree,
    `RequiresOre`), then `NotEnoughItems`.
  - `TryPlace` pays `PlacementCost`; `TryRemove` refunds it, plus whatever the building held
    (chest contents, miner output, belt items, furnace input, fuel and output). Items are never
    lost or duplicated.
  - `WorldConfig.FreeBuilding` is a sandbox switch: building costs nothing and refunds nothing.
  - `WorldConfig.StartingInventory` is empty by default.
- **Crafting**: `WorldConfig.CraftingRecipes` is the recipe table. Each `CraftingRecipe` has a stable
  `Id` (never renumber, only add), a list of ingredient `ItemAmount`s, and a result `ItemAmount`.
  - `TryCraft(tablePos, playerTile, recipeId, times)` crafts instantly from the player's
    inventory. It needs a table at `tablePos` within `CraftingReach` of `playerTile`, makes as many
    as the ingredients allow, and returns how many it made.
  - `playerTile` is a parameter, as in `MineStep`, because the player's position lives in the
    presentation.
  - `Crafting.CanCraft`, `MaxCraftable` and `Missing` are the read-only queries the window uses.
- **Tiles**: Grass, IronOre, CoalOre and Tree.
  - `TileYield.ForHand` is what the player gets: ore, coal or wood.
  - `TileYield.ForMiner` is what a miner extracts (ore and coal only). Miners need a non-None value.
  - Chopping uses `MineStep` with `HandChopTicksPerItem`. Each tree gives `WoodPerTree` wood (kept
    in a per-tile array), then turns into grass and bumps `World.TilesVersion`.
  - Nothing can be built on a tree (`PlaceResult.Blocked`); the player walks over trees.
  - `WorldGenerator` stamps iron, then coal, then trees, each only on grass, so adding a resource
    never moves the existing patches. Each resource has one patch near spawn.
- All randomness goes through `DeterministicRandom` (SplitMix64), seeded from `WorldConfig.Seed`.
  Tunables all live in `WorldConfig`.
- Views poll version counters (`Inventory.Version`, `World.BuildingsVersion`, `World.TilesVersion`) to
  redraw only on change.

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
  - left click that starts on a furnace opens the `FurnaceWindow`, and on a crafting table the
    `CraftingWindow` (only one window is open at a time);
  - a left click elsewhere in the world while a window is open only closes it;
  - clicks over any UI never reach the world. `EventSystem.IsPointerOverGameObject` is used, and
    the bootstrap creates the EventSystem with an `InputSystemUIInputModule`;
  - a drag that fails for lack of items stops placing until the button is released;
  - X removes;
  - right mouse button mines;
  - number keys select a building: key N selects `BuildingNames.All[N-1]` (miner, belt, chest,
    furnace, crafting table); R rotates.

  Commands are queued in `Update` and applied in `BeforeTick`, so the simulation only changes between ticks.
- **Views** read the World and never write to it:
  - `WorldView` draws the ground as a Tilemap, and redraws when `TilesVersion` changes;
  - `BuildingView` keeps pooled SpriteRenderers per building Id;
  - `ItemView` draws belt items and miner outputs. Each frame it reuses a pooled list of renderers,
    because items are not GameObjects;
  - `CursorView` draws the hover frame, the mining bar and the ghost;
  - `HudView` is a uGUI overlay using the legacy `Text` with the built-in font: inventory,
    the hotbar, and a panel for the hovered chest or furnace. The hotbar shows how many of each
    building item you have ("x3"; the table shows its wood cost) and greys out what you can't
    place;
  - `FurnaceWindow` is a uGUI window with ore, coal and plate slots plus smelting and flame bars.
    - Clicking a slot calls `CommandInput.QueueFurnaceAction`, so the change is applied at the
      next tick like every command.
    - It closes with Esc or E, on a click outside, when the player walks more than 6 tiles away,
      or when the furnace is removed. The fuel slot shows whichever fuel is in it.
  - `CraftingWindow` is a uGUI recipe list on the right side of the screen, with two filters:
    "Posso fazer" (craftable now, with "xN") and "Todas" (the rest greyed out, with what is
    missing in red).
    - In a `RecipeRow`, left click crafts 1, Shift+click crafts 5, and right click crafts the max.
      The craft goes through `CommandInput.QueueCraft`.
    - The list is rebuilt when `Inventory.Version` changes.
    - It closes like the furnace window, using `CraftingReach`.
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
- New tunables and tables (costs, recipes) go in `WorldConfig`. Tests build their own config
  with readable numbers.
- Use integer math in the simulation for anything that must be exact (belt positions, ticks).
- New buildings need:
  - a `BuildingType` value and its `Building` subclass;
  - a case in `World.Create`;
  - a tick system or `TryInsert` case if it moves items;
  - a matching `ItemType`, mapped in `BuildingItems.ItemFor`, and a crafting recipe in
    `WorldConfig.CraftingRecipes` with a new Id. Use a `BuildCosts` line only for something that
    must be buildable from raw materials, like the table;
  - a sprite: an entry in `SpriteLibrary.Art` plus its PNG, wired in `ForBuilding` and in `ForItem`
    for the item;
  - a name in `HudView.BuildingNames`, the item in `ItemNames.Of`, and a place in
    `BuildingNames.All`, which also gives it its number key;
  - `BuildingView.IsDirectional` if it has no output side.
- New items need: an `ItemType` value, `ItemNames.Of` and `Short`, and a sprite in `SpriteLibrary.Art`
  with `ForItem`.
- Tests that are not about costs build with `TestWorlds.Empty` or `FreeBuilding = true`.
  `BuildCostTests` uses the real costs.
- One commit per feature, with tests passing.

## Running
- Open the project and the `Assets/Scenes/Factory` scene, then press Play.
- Controls:
  - WASD: move
  - Right mouse: mine ore or coal, or chop a tree (within 3 tiles)
  - 1/2/3/4/5: select miner/belt/chest/furnace/crafting table
  - R: rotate
  - Left click (or drag): build
  - Left click on a chest: take its contents
  - Left click on a furnace: open its window. Click the ore or coal slot to load yours, click the
    plates to take them. Esc or E closes it.
  - Left click on a crafting table: open the recipe list. Click a recipe to craft 1, Shift+click
    for 5, right click for the max.
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
- wood: trees on the map, chopping, trees turning into grass, wood as fuel;
- crafting: recipes, reach, queries, and a check that the table is well formed;
- build costs and building items, including the full loop from an empty inventory to a placed
  miner using only wood as fuel.

If Play shows an empty map after a CLI test run, the Editor was left on an untitled scene.
Reopen `Assets/Scenes/Factory`.

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
