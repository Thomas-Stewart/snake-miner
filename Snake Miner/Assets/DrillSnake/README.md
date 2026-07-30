# Drill Snake Prototype

Open `Assets/DrillSnake/Scenes/DrillSnakePrototype.unity` and enter Play Mode.
The scene has one runtime component; it creates the level, camera, lighting,
snake, turret, pickups, UI, and debug overlays without manual references.

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
- **T**: instantly toggle Illustrated PNG / Procedural Cel art modes

The snake waits at the refinery until a direction or Space is pressed. Reach any
cyan refinery dock with cargo to bank it, consume the temporary cargo segments,
reset heat, and stop at the refinery. Banked credits persist during Play Mode.
The previous upgrade shop is hidden while this new loop is evaluated.
While stopped at a departure prompt, forward and both 90-degree directions
restart immediately; only the direction directly backward into the chassis is
rejected.

The turret on the drill head automatically targets the nearest visible ore
within range. Solid cells block line of sight, and shots cannot squeeze
diagonally through touching rock corners. Common, Rare, and Very Rare deposits
take progressively more shots. Destroying a deposit converts its cell to floor
and scatters three collectible ore fragments into nearby open cells. Each
fragment adds one cargo segment when the snake's head comes within its 1.5-cell
pickup radius; the fragments divide the deposit's total value exactly. At most
one fragment is absorbed per logical movement tick so every new cargo segment
receives a valid tail position. On collection, the fragment visibly arcs into
the moving drill head and finishes with an ore-colored flash, ring, and spark
burst.

Normal contact with soft rock, bedrock, or intact ore simply blocks movement and
does not damage the cell. Four deterministic drill-charge pickups are placed in
the transfer chambers. Collecting one activates the drill for 10 seconds. While
active, entering any in-bounds solid cell destroys it immediately and advances
the snake into that cell; destroyed ore still scatters collectible fragments.
The HUD countdown and pulsing orange aura show the active window.

Heat is pressure rather than a failure state. Movement, cargo, and boosting add
heat, and the snake accelerates continuously up to a +140% speed bonus at 100
heat. Heat can continue rising without ending an expedition; the speed bonus is
capped, while the hotter cadence makes long, cargo-heavy return trips harder to
control. Banking at the refinery resets heat.
The entire snake also shifts progressively toward a hot red tint along the
same 0–100 heat curve, providing an immediate visual warning before the
accelerated movement becomes difficult to manage. The tint smoothly cools back
to the normal art colors when heat resets. Above 70 heat, the drill head begins
venting pale steam; above 86 heat, darker smoke joins it. Both effects grow
denser toward maximum heat and leave short world-space trails behind the moving
snake.

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
- `DrillSnakeSimulation` owns authoritative positions, cargo, heat, turret
  targeting and damage, scattered ore pickups, timed drill power, buffered
  turns, collision, docking, and expedition reset without scene dependencies.
- `DrillSnakeSession` owns banked credits independently of disposable expedition
  cargo and prevents the same cargo from being banked twice.
- `DrillSnakeDesignDiagnostics` is the pure virtual-body pathfinding diagnostic.
- `DrillSnakeController` reads the Input System and coordinates generation,
  movement timing, automatic turret cadence, banking, and failure sequences.
- `DrillSnakeWorldView` builds the hand-painted industrial mine presentation,
  turret and projectiles, collectible fragments, drill charges, mechanical
  snake modules, warm mine lamps, refinery dressing, and both debug overlays.
- `DrillSnakeHud` creates the dark gunmetal resource, objective, heat, controls,
  and drill-power countdown panels. Upgrade controls are currently hidden.
- `DrillSnakeSceneBuilder` is available at **Tools > Drill Snake > Build
  Prototype Scene**.

Mined terrain survives expedition failure. It is replaced only when a preset,
seed, or explicit reset generates the level again.

## Visual presentation

The world intentionally keeps the simulation grid exact while offering two
complete, interchangeable art passes. Press **T** at any time. Toggling rebuilds
only the presentation from the live mutable map; the snake position, cargo,
banked credits, turret damage, scattered fragments, unused drill charges,
active seed, and mined terrain are preserved.

### Illustrated PNG

This mode presents the mine with authored raster textures and atlases:

- `Assets/Resources/Art/DrillSnakeMineFloor.png` is the repeating worn slab
  floor beneath the generated routes.
- `Assets/Resources/Art/DrillSnakeBedrock.png` and
  `DrillSnakeSoftRock.png` distinguish permanent blue-black rock masses from
  warmer soft rock.
- `Assets/Resources/Art/DrillSnakeMachineAtlas.png` provides the illustrated
  drill vehicle, conveyor chassis, cargo wagon, and refinery platform.
- `Assets/Resources/Art/DrillSnakeOreAtlas.png` provides the three ore clusters
  and warm wall lantern.
- `Assets/Resources/Art/DrillSnakeUpgradeAtlas.png` provides large refinery
  upgrade icons retained for the currently hidden progression UI.
- Ore remains strongly color-coded—orange common, blue rare, magenta very
  rare—while embedded in a dark rock matrix.
- The orthographic camera follows the head from a fixed 64-degree
  three-quarter angle, with forward lead and smooth damping instead of framing
  the entire 45x45 layout at once. Its trailing offset exposes model height,
  steam, smoke, and mine-wall depth.
- Each cargo segment smoothly increases the orthographic view size by 0.055,
  up to a safe maximum of 11.25. Banking eases the camera back toward its
  original 7.5 framing as temporary cargo segments are consumed.
- Grid cells remain authoritative, but visible snake modules travel along
  arc-length-parameterized cubic paths. Straight cells move at constant speed;
  90-degree corners preserve their incoming tangent and bend gradually into
  the new direction. The camera locks to this already-smooth visual path rather
  than adding a second catch-up layer.
- The normal HUD uses compact flat charcoal resource and objective panels.
  Controls and validation details appear only while a debug mode is active.
- Warm point lights are placed deterministically at selected graph rooms. They
  are presentation-only and never change navigation or validation.

Art textures and atlases are loaded through `Resources/Art`; missing assets fall
back without changing the grid model, so level logic does not depend on art
imports. The exact ImageGen prompt set is preserved in
`Assets/Resources/Art/DrillSnakeArtPrompts.md`.

### Procedural Cel

This is the default presentation and does not sample any PNG art in the world.
It is built entirely from generated meshes, Unity primitives, flat palettes, and
`Assets/Resources/Shaders/DrillSnakeProceduralCel.shader`.

- Three-step lighting bands, ambient probes, soft directional shadows, cool rim
  light, and warm local lamps create the dimensional diorama lighting.
- Multi-octave world-space stone mottling, mineral breakup, hatching, and
  deterministic color flecks provide surface texture without texture maps or
  random per-frame noise.
- Generated chamfered meshes give rock tiles, refinery plates, machinery, and
  train modules broad edge highlights instead of perfectly sharp cube
  silhouettes.
- Rock height, top chips, and navigable-floor debris vary deterministically by
  cell while preserving the exact collision grid.
- Soft sandstone and permanent blue basalt remain immediately distinguishable.
- Generated pointed crystal meshes and orange, cobalt, and magenta emission
  communicate ore value.
- The snake is a generated low-poly machine: faceted drill cone, gunmetal body,
  orange collar, automatic turret, tracks, couplers, drive gears, and visible
  cargo crystals.
- Turret projectiles, scattered ore fragments, drill-charge pickups, and the
  active-drill aura are generated in both art modes.
- The refinery is a generated steel deck with a turntable, loading recess,
  warning pylons, safety markings, and dock beacons.
- Mine lamps use simple generated housings plus warm emission and point lights.
- The upgrade interface is hidden in both modes during this loop test.

`DrillSnakeArtMode` selects the initial mode on `DrillSnakeController`; newly
bootstrapped prototype scenes default to `ProceduralCel`. The custom shader is
stored under `Resources/Shaders`, ensuring runtime builds retain it even though
all visual objects and materials are created dynamically.

## Edit Mode coverage

`Assets/DrillSnake/Tests/Editor/DrillSnakeSimulationTests.cs` covers single-cell
ticks, buffered turns, reversal rejection, segment following, fragment growth,
banking and permanent chassis retention, body collision, blocked normal contact,
turret shot counts, persistent turret damage, fragment value conservation,
10-second drill charges, powered bedrock destruction, credit persistence,
validator fault cases, deterministic content, ore distance, turret line of
sight, non-fatal heat acceleration, and the long-snake report. The generator
test validates 50
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
| Movement | Bank segment time | 0.090 s | Tail-consumption animation per cargo |
| Turret | Range | 5.5 cells | Automatic nearest-visible-ore acquisition radius |
| Turret | Fire interval | 0.620 s | Delay between automatic shots |
| Turret | Damage | 1 | Ore integrity removed per shot |
| Turret | Projectile travel | 0.260 s | Visible shot travel time |
| Turret | Projectile size | 0.26 cells | Projectile diameter |
| Turret | Ore fragments | 3 | Collectibles scattered by a destroyed deposit |
| Pickup | Ore radius | 1.5 cells | Head-centered fragment collection radius |
| Drill charge | Duration | 10.0 s | Contact-destruction window |
| Ore health | Common | 2 | Turret shots required |
| Ore health | Rare | 3 | Turret shots required |
| Ore health | Very Rare | 4 | Turret shots required |
| Heat | Full-speed heat | 100 | Heat where acceleration reaches its cap |
| Heat | Maximum speed bonus | +140% | Capped high-heat movement bonus |
| Heat | Movement heat | 0.55/cell | Base heat per move |
| Heat | Cargo heat | 0.055/segment/cell | Long-body heat surcharge |
| Heat | Boost heat | 1.8/cell | Additional boost heat |
| Ore | Common value | 15 CR | Base cargo value |
| Ore | Rare value | 50 CR | Base cargo value |
| Ore | Very Rare value | 140 CR | Base cargo value |
