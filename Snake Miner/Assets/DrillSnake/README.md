# Drill Snake Prototype

Open `Assets/DrillSnake/Scenes/DrillSnakePrototype.unity` and enter Play Mode.
The scene contains one runtime bootstrap component; it generates the entire map,
camera, lighting, snake, UI, upgrade buttons, and debug presentation without any
manual reference assignment.

## Controls

- **WASD / Arrow keys**: start moving and buffer 90-degree turns
- **Space**: boost while held (faster movement, extra heat)
- **R**: regenerate the deterministic layout with the next seed
- **1 / 2**: slow test movement / restore normal movement
- **G**: toggle the visible grid overlay
- **H**: toggle heat-free testing

The snake waits at the refinery until a direction or Space is pressed. Immediate
180-degree reversals are rejected. Reach any cyan refinery dock with cargo to bank
it, animate the cargo train away, reset heat, and stop at the refinery. The four
upgrade buttons are only visible while the drill head is on refinery floor or a
dock. Credits and upgrades persist for the duration of Play Mode.

## Architecture

- `DrillSnakeMap` owns the mutable 45x45 cell grid and deliberate seeded layout.
- `DrillSnakeSimulation` owns authoritative grid positions, cargo, heat, movement,
  drilling, collision, and expedition reset. It has no scene or rendering
  dependencies and is covered by Editor tests.
- `DrillSnakeController` reads the Input System, applies timing and upgrades, and
  coordinates banking/failure sequences.
- `DrillSnakeWorldView` smoothly interpolates authoritative cell-to-cell movement
  and generates all URP-compatible graybox visuals from Unity primitives.
- `DrillSnakeHud` generates runtime UI and the refinery upgrade panel.
- `DrillSnakeSceneBuilder` is available at
  **Tools > Drill Snake > Build Prototype Scene**.

Mined terrain lives in `DrillSnakeMap`, so it survives expedition failure.
Pressing R intentionally replaces the current map with the next deterministic
seed.

## Tuning

Select the `Drill Snake Runtime` object in the prototype scene. All movement,
drilling, heat, ore values, banking speed, and upgrade costs are grouped in its
serialized **Tuning** field (`DrillSnakeTuning`). The initial seed is on the same
component.

The default values make body length the primary risk: heat rises slowly on normal
floor, rises sharply while drilling, receives a small per-cargo surcharge, and
receives a larger boost surcharge.
