# Level 1.1 — Controls

Status: Draft

## Role

A tutorial chamber that teaches movement, pickup, and drop before the player is asked to solve a puzzle. There is no plate, bridge, core, or anchor chain in this level.

This level introduces iteration count. SIM does not explain the term here; the player only sees the count on the clear screen.

## World State

- `chamberMode`: `Freeform`
- Starting player position: centre of the chamber, facing the open space.
- Starting world: empty except for the floor, walls, and one destination platform.
- Starting HUD: hidden, then revealed when movement is unlocked.
- All audio is optional. Dialogue is delivered as text.

## Beat Table

Exact SIM text and trigger timing are defined in [SIM Dialogue and Event Timeline](#sim-dialogue-and-event-timeline). This table uses those IDs.

| Beat | Phase | SIM cue | Player action | World response | End condition |
| --- | --- | --- | --- | --- | --- |
| 1. Welcome | `Opening` | `SIM-01`, `SIM-02`, `SIM-03` | None. Movement is locked. | Nothing changes until `SIM-03` finishes. | Intro lines finish; movement unlocks. |
| 2. Movement | `Movement` | Silent. | Press `W`, `A`, `S`, or `D` and move away from the spawn point. | HUD appears with the movement unlock. | Player has moved a visible distance from the spawn point. |
| 3. Movement confirmed | `MovementConfirm` | `SIM-04` | None. | `CUBE-01` falls from above and settles in the open space ahead. | Line finishes and cube settles. |
| 4. Pickup prompt | `Pickup` | `SIM-05` | Press `E` near `CUBE-01`. | Cube attaches to the player and follows movement. | Cube is picked up. |
| 5. Placement prompt | `Placement` | `SIM-06` | Carry cube onto the destination platform and press `E`. | Cube settles on the platform. | Cube is resting inside the platform bounds. |
| 6. Repeat cycle | `Repeat` | Silent. | Pick up `CUBE-02` and place it on `PLATFORM-02`. | A second cube falls from above when beat 6 starts. A second platform rises from the floor when the first cube is placed. | Both cubes are on their platforms. |
| 7. Clear | `Clear` | Optional `SIM-07` | None. | Clear screen appears with the iteration count. | Level ends. |

## SIM Dialogue and Event Timeline

This section is canonical for the exact text SIM displays and the events tied to each line. One row is one displayed SIM line. `Before line` lists the state required before the line appears. `After line` lists what happens after the full line finishes displaying.

No other SIM lines occur in this level.

| ID | Beat | Exact SIM line | Before line | After line | Hold until |
| --- | --- | --- | --- | --- | --- |
| `SIM-01` | `Opening` | `SIM: Welcome to Polymulator, AI-1959.` | Chamber is loaded. Player movement is locked. HUD is hidden. `PLATFORM-01` is visible. No cube is present. | No world change. Advance to `SIM-02`. | `SIM-01` finishes displaying. |
| `SIM-02` | `Opening` | `SIM: Let's warm up and understand how things work in this simulation world.` | `SIM-01` has finished. Movement is still locked. | No world change. Advance to `SIM-03`. | `SIM-02` finishes displaying. |
| `SIM-03` | `Opening` | `SIM: Try to look around the world by moving with WASD keys.` | `SIM-02` has finished. Movement is still locked. | Movement unlocks. HUD appears. | Player has moved a visible distance from the spawn point. |
| `SIM-04` | `MovementConfirm` | `SIM: Good, you understand how to move.` | Movement input was confirmed. Player is no longer at the spawn point. | `CUBE-01` falls from above and settles in the open space ahead. | `CUBE-01` is settled. |
| `SIM-05` | `Pickup` | `SIM: Use [E] to pick up an object.` | `CUBE-01` is settled. Player is not carrying anything. | `E` pickup is enabled for `CUBE-01`. No world change. | `CUBE-01` is picked up. |
| `SIM-06` | `Placement` | `SIM: Move to the marked platform and press [E] again to drop it.` | Player is carrying `CUBE-01`. | `E` drop is enabled. No world change. | `CUBE-01` is resting inside `PLATFORM-01`. |
| `SIM-07` | `Clear` | `SIM: Good. You understand how to move objects.` | Both cubes are on their platforms. This line is optional. | Clear screen appears with the iteration count. No further SIM text. | Level ends. |

If `SIM-07` is omitted, the clear screen appears immediately after both cubes are placed.

## Spatial Setup

- `CUBE-01`: light cube, spawn position in front of the player.
- `CUBE-02`: light cube, spawn position in front of the player, slightly offset from the first cube.
- `PLATFORM-01`: level goal platform, visible from the start.
- `PLATFORM-02`: level goal platform, hidden below the floor until `CUBE-01` is placed on `PLATFORM-01`.
- No plate, bridge, core, anchor, or pressure logic is present.

## Rules

- SIM speaks only at the start of a beat. SIM stays silent during the task.
- Movement is locked until the first three SIM lines finish.
- A response line after a completed task is optional. If present, it plays once and does not repeat on failure.
- The repeat beat does not repeat the instruction text. The player must apply the same pickup and drop sequence without a prompt.
- `E` performs direct pickup/drop in this tutorial. It does not open Scan or create a telekinesis tether.
- There are no timers and no fail states.

## Terminal State

| State | Value |
| --- | --- |
| `status` | `Complete` |
| `iterationCount` | Number of pickup-and-place cycles used. |
| `objectTransform` | Final transform of each cube and platform. |
| `notes` | Tutorial summary. |

## Implementation Notes

- Current prototype debt: `Freeform` is currently implemented as a telekinesis tether on `E`. This level assumes direct pickup/drop on `E` and should be treated as a required implementation change when the tutorial is built.
- `chamberMode` is fixed for the level and does not switch at runtime.
- Clear condition checks object bounds only. It does not require weight, facing, or fragility.
