# Nicotório

🇧🇷 [Leia em português](README.pt-BR.md)

A top-down automation game prototype made in **Unity 6** (URP 2D). You start by chopping trees and building a crafting table. Then you mine iron and coal, smelt the ore into plates in a furnace, and craft the miners, belts and chests that automate everything.

The in-game text is in Brazilian Portuguese. The UI names are given in parentheses below.

![Factory: iron miners on a belt running down to a furnace, a coal line feeding it from the side, and plates going out to a chest](docs/images/fabrica.png)

## Features

- **64x64 map** with groves of trees and patches of iron ore and coal, generated from a seed (the map is always the same).
- **Hand gathering**: hold the right mouse button on nearby ore, coal or a tree.
  - Each tree gives 4 wood and then disappears.
  - You can't build on a tree, but you can walk over it.
- **Crafting table** (*Bancada*): costs 5 wood and is the first thing you build.
  - Click it to open the recipe list.
  - The **"Posso fazer"** (*Can make*) filter shows what you can make right now. **"Todas"** (*All*) shows everything, with what is missing in red.
  - Click a recipe to make 1, Shift+click to make 5, right-click to make as many as possible.
- **Miner** (*Mineradora*): goes only on iron ore or coal. It produces 1 item per second and delivers it to the tile its arrow points at. With nowhere to deliver, it holds the item and stops.
- **Belt** (*Esteira*): carries items the way its arrow points and delivers them to the next belt or into a chest. When a belt fills up, the belt behind it stops too.
- **Chest** (*Baú*): stores everything it receives. Click it to take its contents, or hover over it to see what is inside.
- **Furnace** (*Fornalha*): turns 1 iron ore into 1 iron plate every 2 seconds and delivers the plate to the tile its arrow points at.
  - It burns **coal** (1 coal = 4 plates) or **wood** (1 wood = 2 plates). Fuel is only used while the furnace is smelting.
  - It holds up to 10 ore, 10 fuel and 10 plates.
  - Miners and belts can feed it.
  - Bring ore and coal in on **separate belts**, from different sides of the furnace. On a single mixed belt, once the fuel slot is full, a coal item waiting at the front blocks the ore behind it.
  - **Click it to open its window.** Click the ore or fuel slot to put yours in, and the plates slot to take the finished plates.
- **Buildings are items crafted at the table.** Placing one uses 1 item, and removing it gives the item back:

  | Recipe | Ingredients |
  |---|---|
  | Iron gear | 2 iron plates |
  | Chest | 4 wood |
  | Furnace | 5 iron ore + 2 wood |
  | Belt (makes 2) | 1 gear + 1 iron plate |
  | Miner | 3 gears + 2 iron plates + 2 wood |

  You start with nothing:
  1. Chop trees and build the **crafting table** (5 wood).
  2. Mine 5 ore and craft a **furnace**.
  3. Fill the furnace with ore and use **wood as fuel**. You don't need coal yet.
  4. Take the plates, make **gears**, then your first **miner**.

  The hotbar shows how many of each building you have ("x3"; for the crafting table, its cost in wood). An entry is greyed out when you have none.

| Start: crafting table and a wood-burning furnace | Furnace window |
|---|---|
| ![The player next to a grove with a freshly built crafting table and furnace](docs/images/inicio.png) | ![The furnace window with the ore, fuel and plate slots and the smelting and flame bars](docs/images/fornalha.png) |

| Crafting table |
|---|
| ![The crafting window showing the recipe list; recipes missing ingredients are greyed out, with what is missing in red](docs/images/bancada.png) |

## Controls

| Key | Action |
|---|---|
| **WASD** | move |
| **Right mouse** (hold) | mine iron or coal, or chop a tree, under the mouse (3 tiles reach) |
| **1 / 2 / 3 / 4 / 5** | select miner / belt / chest / furnace / crafting table (press again to deselect) |
| **R** | rotate the building |
| **Left mouse** | build; hold and drag to build a line |
| **Left mouse on a chest** | take everything in it |
| **Left mouse on a furnace** | open the furnace window (click the slots to put in ore or fuel and to take plates) |
| **Left mouse on the crafting table** | open the recipe list (click: 1, Shift+click: 5, right-click: as many as possible) |
| **Esc / E** | close the furnace or crafting window |
| **X** | remove the building under the mouse; whatever it held comes back to you |

## Running

1. Open the folder in **Unity 6000.6.8f1**.
2. Open the `Assets/Scenes/Factory` scene and press **Play**.

The scene is empty on purpose. The camera, map, player and UI are all created in code when the game starts.

## Art

The graphics live in **`Assets/Resources/Sprites/`**, one 16x16 PNG each:

| File | What it is |
|---|---|
| `grass_1.png`, `grass_2.png` | two grass variants |
| `iron_ore.png`, `coal_ore.png`, `tree.png` | iron ore, coal and tree tiles |
| `player.png` | the player |
| `miner.png`, `belt.png`, `chest.png`, `furnace.png`, `crafting_table.png` | miner, belt, chest, furnace and crafting table, **drawn facing up** |
| `item_iron_ore.png`, `item_iron_plate.png`, `item_coal.png`, `item_wood.png`, `item_iron_gear.png` | ore, plate, coal, wood and gear |
| `cursor_frame.png` | the cursor frame, in white (the game tints it) |

**To change a graphic, overwrite its PNG and keep the same name.** Unity applies the pixel-art import settings by itself: 16 pixels per tile, Point filter, no compression, no mipmaps. You don't need to touch the Inspector. All sprites are packed into one **Sprite Atlas** (`Assets/Resources/SpriteAtlas.spriteatlasv2`), a single texture, so the game draws everything in few draw calls.

From the Unity menu **Factory > Sprites**:
- **Exportar sprites gerados** (*Export generated sprites*) recreates any missing PNGs.
- **Restaurar todos os sprites originais** (*Restore all original sprites*) goes back to the original drawings.

If a PNG goes missing, the game still works: it draws that sprite in code instead.

## How it is built

- **Simulation and presentation are separate.**
  - The simulation (`Assets/Scripts/Simulation`) is plain C# with no `UnityEngine`. It runs at a fixed 20 ticks per second and is deterministic.
  - The presentation (`Assets/Scripts/Presentation`) only draws the simulation state and turns input into commands.
- **Items on belts are data**, not GameObjects. Each item has an integer position along its belt, and the presentation just draws it.
- **There is a single entry point**, `GameBootstrap`, which creates the simulation and hands it to whatever needs it. There are no scattered singletons.

Architecture and conventions are documented in [`CLAUDE.md`](CLAUDE.md).

## Tests

The EditMode tests (`Assets/Tests/EditMode`) cover:
- map generation;
- hand mining;
- building rules;
- miner and furnace rates;
- coal: burning, coal patches on the map, and separate ore and coal lines;
- wood: trees on the map, chopping, trees turning into grass, and wood as fuel;
- crafting: recipes, crafting table reach, and the queries for what can be made;
- belt movement and blocking;
- chests filling up;
- buildings as items, including the full path from an empty inventory to the first miner using only wood as fuel.

To run them in Unity, use **Window > General > Test Runner > EditMode > Run All**. Leave Play mode first.
