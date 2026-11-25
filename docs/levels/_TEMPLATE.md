# Level X.Y — [Title]

> Copy this file for a new level. This is a template, not a playable level.

Status: Draft

## Role

[What this level teaches or tests. State clearly if it is a tutorial chamber and whether the plate/bridge/core/anchor chain is omitted.]

## World State

- `chamberMode`: `Grid` or `Freeform`
- Starting player position:
- Starting world:
- Starting HUD:
- Audio: optional / required
- Other starting state:

## Beat Table

Exact SIM text and trigger timing are defined in [SIM Dialogue and Event Timeline](#sim-dialogue-and-event-timeline). This table uses those IDs.

| Beat | Phase | SIM cue | Player action | World response | End condition |
| --- | --- | --- | --- | --- | --- |
| 1. [Name] | `[Phase]` | `SIM-01` |  |  |  |
| 2. [Name] | `[Phase]` | Silent |  |  |  |

## SIM Dialogue and Event Timeline

This section is canonical for the exact text SIM displays and the events tied to each line. One row is one displayed SIM line. `Before line` lists the state required before the line appears. `After line` lists what happens after the full line finishes displaying.

[List every SIM line in this level. Add `No other SIM lines occur in this level.` when complete.]

| ID | Beat | Exact SIM line | Before line | After line | Hold until |
| --- | --- | --- | --- | --- | --- |
| `SIM-01` | `[Beat]` | `SIM: [exact player-facing text]` |  |  |  |
| `SIM-02` | `[Beat]` | `SIM: [exact player-facing text]` |  |  |  |

Timeline rules:

- SIM speaks at the start of a beat and stays silent during the task.
- No mid-task reminders or hints.
- Response lines are optional by default. Mark a line as optional in `Before line`.
- Use `SIM` only. Do not add `RECT:` lines or named speakers.
- Keep player-facing text in English.

## Spatial Setup

- [Object name]: [role, starting position, relevant properties]
- [Goal object]: [role, starting visibility, activation rule]

## Rules

- [Rule that governs the beat flow.]
- [Rule that governs clear conditions.]
- [Rule that governs implementation constraints.]

## Terminal State

| State | Value |
| --- | --- |
| `status` | `Complete` |
| `iterationCount` |  |
| `objectTransform` |  |
| `notes` |  |

## Implementation Notes

- [Current prototype debt or required implementation change.]
- [Fixed mode statement.]
- [Clear condition statement.]
