# Drill Snake Prototype

Open `Assets/DrillSnake/Scenes/DrillSnakePrototype.unity` and enter Play Mode.
The scene has one runtime component; it creates the level, camera, lighting,
snake, UI, upgrade panel, and debug overlays without manual references.

The prototype uses discrete, authoritative grid movement. A turn changes heading
at the center of a cell, immediate 180-degree reversals are rejected, and the
snake body occupies exact cells. There is no continuous steering or movement
noise.

## Controls

- **WASD / Arrow keys**: start moving and buffer 90-degree turns
- **Space**: boost while held (faster movement, extra heat)
- **F1**: generate Easy — Open Quarry with the active requested seed
- **F2**: generate Medium — Crystal Caverns with the active requested seed
- **F3**: generate Hard — Magma Fissures with the active requested seed
- **N**: generate a new requested seed with the active preset
- **R**: reset the active requested seed and discard mined terrain
- **V**: toggle the level-design and validation overlay
- **G**: toggle the cell-grid overlay
- **1 / 2**: slow test movement / restore normal movement
- **H**: toggle heat-free testing

The snake waits at the refinery until a direction or Space is pressed. Reach any
cyan refinery dock with cargo to bank it, consume the temporary cargo segments,
reset heat, and stop at the refinery. Credits and upgrades persist during Play
Mode.

Drilling is impact-based. Entering a destructible cell rams it without moving
the authoritative snake, visibly recoils only the drill head, and removes drill damage
from that cell's integrity. Every damage event recoils, including the impact
that reduces integrity to zero. That final ram opens the cell, but the following
logical tick performs the move into it and collects any ore. Soft rock and
Common, Rare, and Very Rare ore require progressively more base drill impacts.
Partial damage survives expedition failure; the Drill Motor upgrade increases
damage per impact. Bedrock remains indestructible.

## Graph-first generation

Generation is deterministic for a requested seed and preset:

1. Build a room-and-route graph with the refinery as node 0.
2. Rasterize 5x5–9x9 turning chambers and orthogonal 1–3-cell-wide routes.
3. Keep required routes open. Rasterize optional short routes as destructible
   soft rock.
4. Fill unused cells with large bedrock masses or graph-proximity soft-rock
   buffers. Partial distance bands are filled in rotational groups, not by random
   tile scatter.
5. Compute shortest safe graph distance from the refinery.
6. Place structured ore rings in rooms after the topology is valid. Ore tier is
   selected from graph distance.
7. Run full validation. Reject a failing candidate and deterministically derive
   another seed, up to 12 attempts.

The graph has 13 named nodes: one central refinery, four transfer chambers, and
eight outer mining chambers. Four refinery spokes feed the transfer chambers.
The eight outer chambers form a safe loop, so each has two return directions.
Easy and Medium also have an inner safe loop. Four optional soft-rock diagonals
provide shorter, riskier commitments. Seed variation is bounded to room sizes,
orthogonal bend choice, lane-width phase, and ore-ring phase; it never places
arbitrary isolated tiles.

### Layout preset tuning

| Preset | Diggable target | Inner rooms | Outer rooms | Spokes | Outer loop | Secondary | Inner loop | Ore per room C/R/VR |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| Easy — Open Quarry | 62–69% | 7–9 | 7–9 | 3 | 3 | 2 | Yes | 6 / 7 / 5 |
| Medium — Crystal Caverns | 50–56% | 6–7 | 6–8 | 2 | 2 | 1 | Yes | 5 / 6 / 5 |
| Hard — Magma Fissures | 40–46% | 5–6 | 5–6 | 1 | 1 | 1 | No | 4 / 5 / 6 |

The map is 45x45 with a central 9x9 refinery. The minimum ore validation counts
are three times each preset's per-room figure. Graph-distance tier thresholds are
40% of maximum distance for Common and 72% for Rare; farther rooms are Very Rare.
The non-refinery turning core is always at least 3x3. The validator requires at
least 20% bedrock, rejects more than 62% initially open floor, allows at most 20%
graph dead ends, and requires an enclosed bedrock component of at least 12 cells.

## Validation

`DrillSnakeLevelValidator` runs once before ore placement and again afterward.
It checks:

- all four docks connect to open graph routes;
- every major outer chamber has safe graph and raster routes to a dock, with two
  independent safe first-edge return choices;
- each outer region has at least two graph connections, except an explicitly
  marked ore pocket with a valid turning chamber;
- every required corridor remains open after rasterization;
- every turning chamber meets its configured dimensions and keeps a clear 3x3
  core;
- a raster route that becomes a mandatory dead end still has a clear turning
  core;
- Common, Rare, and Very Rare ore meet their preset minimums and increase in
  average graph distance;
- the safe graph has a cycle and encloses a substantial bedrock island;
- at least one optional shortcut still contains soft rock;
- the map is neither one giant open room nor primarily dead ends.

The HUD shows requested and accepted seeds, generation attempt, validation
summary, and the number of findings from rejected candidates.

## Level-design overlay

Press **V** to inspect the generated structure:

- spheres are room graph nodes;
- colored room outlines are turning chambers and ore-value zones;
- labels show room ID, dimensions, graph distance, and distance tier;
- white lines are central required routes;
- cyan lines are safe long routes;
- orange lines are risky soft-rock shortcuts;
- green, blue, and magenta room outlines are Common, Rare, and Very Rare zones;
- bright red cell markers are validation failures.

This overlay is intentionally generated from the same graph metadata used to
rasterize and validate the map.

## Long-snake design diagnostic

Run **Tools > Drill Snake > Run Long-Snake Diagnostics** to generate Easy,
Medium, and Hard maps at seed `240628` and print a Console table for virtual
snake lengths 5, 15, 30, and 60.

`DrillSnakeDesignDiagnostics` performs bounded best-first pathfinding over the
room graph, converts each candidate to exact cell-by-cell routes, creates a
full-length non-overlapping body inside the refinery, and advances that virtual
body through outbound/return route pairs. A route is rejected when the head
would enter any occupied body cell. This is a conservative design diagnostic;
it does not alter the live simulation or generator.

The report contains:

- accessible graph-room and route cells as a percentage of all non-bedrock
  interior cells;
- ore chambers with at least one body-safe round trip;
- distinct viable return graph paths;
- minimum width among routes used by a viable round trip;
- chambers with a clear 5x5-or-larger footprint and enough usable cells for the
  tested body length.

The default-seed report is:

| Preset | Length | Accessible | Ore chambers | Return routes | Min width | Turning chambers |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Easy | 5 | 67.4% | 9 | 72 | 1 | 13 |
| Easy | 15 | 67.4% | 9 | 72 | 1 | 13 |
| Easy | 30 | 67.4% | 9 | 72 | 1 | 13 |
| Easy | 60 | 67.4% | 9 | 66 | 1 | 8 |
| Medium | 5 | 66.4% | 9 | 72 | 1 | 13 |
| Medium | 15 | 66.4% | 9 | 72 | 1 | 13 |
| Medium | 30 | 66.4% | 9 | 72 | 1 | 13 |
| Medium | 60 | 66.4% | 9 | 66 | 1 | 4 |
| Hard | 5 | 59.3% | 9 | 23 | 1 | 13 |
| Hard | 15 | 59.3% | 9 | 23 | 1 | 13 |
| Hard | 30 | 59.3% | 9 | 23 | 1 | 11 |
| Hard | 60 | 59.3% | 9 | 22 | 1 | 1 |

## Runtime architecture

- `DrillSnakeLevelGraph` owns room nodes, route edges, preset values, and graph
  metadata.
- `DrillSnakeMap` owns the mutable 45x45 tile grid and graph-first rasterizer.
- `DrillSnakeLevelValidator` gates generated candidates.
- `DrillSnakeSimulation` owns authoritative positions, cargo, heat, drilling,
  buffered turns, collision, docking, and expedition reset without scene
  dependencies.
- `DrillSnakeSession` owns banked credits independently of disposable expedition
  cargo and prevents the same cargo from being banked twice.
- `DrillSnakeDesignDiagnostics` is the pure virtual-body pathfinding diagnostic.
- `DrillSnakeController` reads the Input System and coordinates generation,
  timing, upgrades, banking, and failure sequences.
- `DrillSnakeWorldView` builds the hand-painted industrial mine presentation,
  mechanical snake modules, warm mine lamps, refinery dressing, and both debug
  overlays.
- `DrillSnakeHud` creates the dark gunmetal resource, objective, heat, controls,
  and horizontal refinery-upgrade panels.
- `DrillSnakeSceneBuilder` is available at **Tools > Drill Snake > Build
  Prototype Scene**.

Mined terrain survives expedition failure. It is replaced only when a preset,
seed, or explicit reset generates the level again.

## Visual presentation

The world intentionally keeps the simulation grid exact while presenting it as
a dark top-down industrial mine:

- `Assets/Resources/Art/DrillSnakeMineFloor.png` is the repeating worn slab
  floor beneath the generated routes.
- `Assets/Resources/Art/DrillSnakeBedrock.png` and
  `DrillSnakeSoftRock.png` distinguish permanent blue-black rock masses from
  warmer destructible sedimentary rock.
- `Assets/Resources/Art/DrillSnakeMachineAtlas.png` provides the illustrated
  drill vehicle, conveyor chassis, cargo wagon, and refinery platform.
- `Assets/Resources/Art/DrillSnakeOreAtlas.png` provides the three ore clusters
  and warm wall lantern.
- `Assets/Resources/Art/DrillSnakeUpgradeAtlas.png` provides large refinery
  upgrade icons.
- Ore remains strongly color-coded—orange common, blue rare, magenta very
  rare—while embedded in a dark rock matrix.
- The camera follows the head at a close gameplay scale with two cells of
  forward lead instead of framing the entire 45x45 layout at once.
- The normal HUD uses compact flat charcoal resource and objective panels.
  Controls and validation details appear only while a debug mode is active.
- Warm point lights are placed deterministically at selected graph rooms. They
  are presentation-only and never change navigation or validation.

Art textures and atlases are loaded through `Resources/Art`; missing assets fall
back without changing the grid model, so level logic does not depend on art
imports. The exact ImageGen prompt set is preserved in
`Assets/Resources/Art/DrillSnakeArtPrompts.md`.

## Edit Mode coverage

`Assets/DrillSnake/Tests/Editor/DrillSnakeSimulationTests.cs` covers single-cell
ticks, buffered turns, reversal rejection, segment following, ore growth,
banking and permanent chassis retention, body/bedrock collision, drilling,
material-specific ram counts, persistent partial rock damage, drill-motor
damage, credit persistence, validator fault cases, deterministic content, ore
distance, heat, and the long-snake report. The generator test validates 50
deterministic seeds for each of the three presets (150 generated maps per test
run).

## Gameplay tuning defaults

Select `Drill Snake Runtime` in the scene. The initial seed, layout preset, and
the following serialized `DrillSnakeTuning` values are editable:

| Group | Parameter | Default | Effect |
| --- | --- | ---: | --- |
| Movement | Movement tick | 0.200 s | Normal cell interval |
| Movement | Boost tick | 0.105 s | Boosted cell interval cap |
| Movement | Slow-test multiplier | 3.0x | Debug slowdown |
| Movement | Speed upgrade reduction | 0.018 s/level | Normal interval reduction; 0.07 s floor |
| Movement | Bank segment time | 0.090 s | Tail-consumption animation per cargo |
| Drilling | Soft-rock health | 2 | Base rams required |
| Drilling | Common-ore health | 2 | Base rams required |
| Drilling | Rare-ore health | 3 | Base rams required |
| Drilling | Very-Rare-ore health | 4 | Base rams required |
| Drilling | Base drill damage | 1 | Integrity removed per impact |
| Drilling | Drill Motor damage | +1/level | Additional integrity removed per impact |
| Drilling | Impact recovery | 0.300 s | Minimum interval before another logical action |
| Drilling | Recoil duration | 92% | Portion of the impact interval used by recoil; 0.4 s cap |
| Drilling | Recoil distance | 0.52 cells | Maximum backward visual displacement |
| Heat | Base maximum | 100 | Failure threshold |
| Heat | Cooling capacity | +18/level | Maximum-heat increase |
| Heat | Movement heat | 0.55/cell | Base heat per move |
| Heat | Drilling heat | 4.5/impact | Heat added by every drill impact |
| Heat | Cargo heat | 0.055/segment/cell | Long-body heat surcharge |
| Heat | Boost heat | 1.8/cell | Additional boost heat |
| Ore | Common value | 15 CR | Base cargo value |
| Ore | Rare value | 50 CR | Base cargo value |
| Ore | Very Rare value | 140 CR | Base cargo value |
| Ore | Scanner bonus | +15%/level | Multiplicative ore-value bonus |
| Upgrades | Cooling base cost | 100 CR | Level-0 purchase cost |
| Upgrades | Drill Motor base cost | 130 CR | Level-0 purchase cost |
| Upgrades | Drive Speed base cost | 160 CR | Level-0 purchase cost |
| Upgrades | Ore Scanner base cost | 140 CR | Level-0 purchase cost |
| Upgrades | Cost growth | 1.75x/level | Multiplicative cost curve |
