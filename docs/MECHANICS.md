# POLYMULATOR — Mechanics Canon

**Mechanics version:** v0.2  
**Status:** Accepted base mechanism + runtime-reserve final chamber  
**Last updated:** 2026-11-22  
**Related story canon:** [`docs/STORY_BIBLE.md`](./STORY_BIBLE.md)

## 0. Authority and scope

This file is the source of truth for POLYMULATOR's **mechanism design**.

`docs/STORY_BIBLE.md` remains the source of truth for story, characters, language, fragments, and SDG placement. If a mechanic note here touches story, it does so only as a bridge between the two documents.

If code, assets, or old design notes conflict with this file, this file describes the intended design. Code and assets are downstream and must be updated to match.

This document covers:

- placement modes,
- abilities and lenses,
- unlock and level-gating rules,
- the mandatory Audit phase,
- the final-chamber runtime reserve,
- UI and visual language,
- story connection,
- implementation gaps,
- reserved future concepts.

It does **not** redefine level geometry, camera configuration, ribbon behaviour, passed-dot colours, palette values, or the story canon in `STORY_BIBLE.md`.

---

## 1. Design principles

1. **Two placement regimes, not two difficulties.** Freeform and Grid test different spatial competencies.
2. **One mode per chamber.** A level is assigned exactly one mode; Freeform and Grid never mix inside a chamber.
3. **Abilities are certification modules.** Rect earns them progressively; each level declares which modules are under assessment.
4. **Defaults are not bans.** A mode's default rule can be overridden per level.
5. **Information comes before action.** Scan and Blueprint let Rect read a problem before changing it.
6. **Completion requires reflection.** Every chamber ends in Audit.
7. **The mechanism stays literal.** Plates, barriers, bridges, cores, and anchors remain certification instruments.
8. **Story and mechanism stay separate.** SDG 4 lives in the mechanism. SDG 11 lives in the district-recovery story.

---

## 2. Core loop

Every chamber follows this loop:

1. **Enter chamber** — SIM opens the certification probe and declares the chamber mode.
2. **Read the space** — Rect may use Scan and Blueprint to inspect objects and anchor requirements.
3. **Accept the assigned mode** — Grid or Freeform is fixed for this chamber; Rect does not choose or switch it.
4. **Manipulate** — Rect uses the abilities permitted by its global unlocks, the level allowlist, and the assigned chamber mode.
5. **Satisfy the chamber** — load plates, open barriers or bridges, recover CORE-01, and seat it in ANCHOR-01.
6. **Clear** — anchor lock confirms the objective.
7. **Audit** — the system reviews what Rect did and what it could have done.
8. **Record** — calibration, stars, fragments, and iteration count are saved.
9. **Continue** — next chamber, replay, or return to main menu.

Audit is not optional. A chamber is not fully complete until Audit has run.

### 2.1 Runtime reserve (final chamber only)

The chamber that carries the ending beat adds one deadline that no other chamber has: a runtime reserve.

- `runtimeReserve` is a level property. It is active only in the final narrative chamber, whichever scene carries that beat.
- The reserve duration is **not decided** in this canon. It will be set during level design.
- The countdown starts when a `SYSTEM` push notification reports that the external power feed has been interrupted. The notification appears in the top-right HUD slot.
- The first notification arrives while SIM's tone is still hurried and personal. SIM's tone turns cold only after it.
- The reserve is machine state. It is not a story speaker, not SIM dialogue, and not archive corruption.
- The player has no shutdown action. There is no button and no choice UI. The erasure ending is reached when the reserve expires or when the player leaves the chamber unfinished.
- If anchor lock completes before expiry, the chamber clears normally and Audit runs.
- If the reserve expires first, the run ends in erasure. No certification is recorded and Audit does not run. The player returns to the level menu and can replay as a fresh iteration.
- Leaving the chamber before finishing has the same result as expiry.
- The reserve does not change the assigned chamber mode, object state, plate state, bridge state, or fragment state. It only bounds the time available to finish.
- A level outside the final narrative phase must not enable `runtimeReserve`.

---

## 3. Modes

There are two manipulation modes. Every chamber uses exactly one of them.

### 3.1 Grid mode

Grid mode is the **surveyed construction space**.

- Movable objects respect the world grid system.
- The floor grid indicator is visible.
- Placement ghosts show grid-aligned positions.
- Rotation is allowed.
- Throw is **disabled by default**, because ballistic placement conflicts with surveyed placement.
- A level may opt in to Throw in Grid mode if a future puzzle needs grid-respecting throws.

Story reading: Grid mode represents planned, measured work on a real construction site.

Example status line:

```text
SIM // GRID CERTIFICATION ACTIVE. PLACEMENT RESOLUTION LOCKED.
```

### 3.2 Freeform mode

Freeform mode is the **continuous recovery space**.

- Movable objects can be placed freely.
- There is no grid constraint.
- The floor has no grid visual indicator.
- Translation is continuous.
- Rotation is allowed.
- Throw is allowed by default.

Earlier discussion considered disabling Rotation in Freeform. That is superseded. Rotation is enabled in Freeform because orientation still matters for asymmetric objects, shaped gaps, and yaw-locked anchors.

Story reading: Freeform mode represents debris handling and loose material movement during recovery.

Example status line:

```text
SIM // FREEFORM CERTIFICATION ACTIVE. CONTINUOUS PLACEMENT ENABLED.
```

### 3.3 One mode per chamber

Freeform and Grid cannot be used at the same time. A chamber is assigned exactly one mode at scene load, and that mode stays fixed until the chamber ends.

- `chamberMode` is a level property with one value: `Freeform` or `Grid`.
- Rect cannot choose or switch the mode during play.
- Mode-switch input is removed from the chamber. If a legacy toggle input is pressed, TELEMETRY reports that the mode is locked by the certification probe.
- The assigned mode applies to every movable object in the chamber. There is no per-object mode exception.
- A level can still override specific mode rules, such as enabling Throw in Grid, without changing the chamber mode.
- Different chambers may use different modes; consecutive levels are free to alternate.
- If a concept needs both regimes, it must be split into two chambers rather than one mixed-mode level.

This constraint prevents state conflicts. A freely placed object would otherwise have to be snapped, rejected, or left off-grid when switching to Grid. None of those are clean outcomes.

### 3.4 Mode rules table

| Rule | Freeform | Grid |
|---|---|---|
| Placement | Continuous | Grid-respecting |
| Floor grid indicator | Hidden | Visible |
| Translation | Yes | Yes |
| Rotation | Yes | Yes |
| Throw | Yes by default | No by default, opt-in |
| Partition | Yes | Yes |
| Fusion | Yes | Yes |
| Scan | Yes | Yes |
| Blueprint | Yes | Yes |
| Audit | Mandatory after clear | Mandatory after clear |

---

## 4. Abilities and lenses

The active mechanism has seven abilities plus one mandatory evaluation phase.

Scan, Blueprint, and Audit are **non-mutating**: they do not change object state. Translation, Rotation, Throw, Partition, and Fusion are **mutating**: they change object position, orientation, or identity.

### 4.1 Translation

**Category:** Mutating, core manipulation  
**Default availability:** Both modes, baseline

Rect moves an object from one position to another.

Translation covers:

- picking up an object,
- carrying or tethering it,
- placing it,
- changing its elevation where the mode allows.

It is the simplest certification primitive and is available from the first chamber.

Story reading: basic spatial control — knowing where a component should go.

### 4.2 Rotation

**Category:** Mutating, orientation  
**Default availability:** Both modes, unlockable

Rect rotates the selected object around a defined axis.

Initial focus is yaw rotation, because anchors currently verify facing angle. Other axes may be added later if a level requires them.

Rotation rules:

- Grid mode uses discrete steps or grid-defined angles.
- Freeform mode may use continuous spin.
- Both modes allow Rotation by default.
- A level may still opt out of Rotation if its puzzle does not test orientation.

Story reading: correct fit. The same part can be wrong if it faces the wrong way.

### 4.3 Throw

**Category:** Mutating, transfer  
**Default availability:** Freeform yes, Grid no; both overridable per level

Rect launches a held object toward a predicted landing point.

Throw must include:

- a trajectory indicator,
- a landing marker,
- an adjustable distance or velocity control,
- a readable velocity indicator.

Grid mode disables Throw by default. A level can opt in if needed. If it does, the throw must either snap to grid on landing or otherwise respect the level's Grid rule.

Story reading: moving loose material under uncertainty. It belongs naturally to Freeform recovery spaces.

### 4.4 Partition

**Category:** Mutating, decomposition  
**Default availability:** Both modes, unlockable

Rect divides a selected object into two separate objects along a defined axis.

Partition needs these rules before implementation:

- allowed axes,
- mass distribution between the two results,
- resulting display names,
- inherited colour and material,
- whether the object is eligible,
- whether Partition is reversible through Fusion,
- how it affects par and Audit.

Story reading: decomposing a salvaged component into usable parts.

### 4.5 Fusion

**Category:** Mutating, composition  
**Default availability:** Both modes, unlockable

Rect selects two eligible objects and combines them into one resulting object.

Fusion needs these rules before implementation:

- eligible pairs or object classes,
- resulting shape,
- summed mass,
- inherited colour and material,
- resulting display name,
- whether it restores a partitioned object or creates a new one,
- how it affects par and Audit.

Story reading: recombining parts into a usable component.

### 4.6 Scan

**Category:** Non-mutating, inspection  
**Default availability:** Both modes, baseline

Rect inspects the targeted object without changing it.

Scan reports:

- display name,
- mass,
- dimensions or half-extents,
- current yaw,
- grid alignment state,
- allowed operations,
- Partition eligibility,
- Fusion eligibility,
- anchor compatibility where relevant.

Scan is a baseline ability. It exists to teach observation and reduce guesswork.

Story reading: Rect surveys material before acting.

### 4.7 Blueprint

**Category:** Non-mutating, inspection lens  
**Default availability:** Both modes, baseline

Blueprint overlays measurement information over the chamber.

It shows:

- local X/Y/Z axes,
- anchor vector,
- required facing yaw,
- mass and bounds of the targeted object,
- grid cells in Grid mode,
- continuity guides in Freeform mode,
- ability eligibility where useful.

Blueprint never moves or modifies objects.

Story reading: SIM exposing the measurement layer of the certification sandbox.

### 4.8 Audit

**Category:** Mandatory evaluation phase  
**Default availability:** Every chamber, after clear

Audit is not a normal unlockable ability and not optional. Every level must be completed with Audit.

Audit shows what Rect did, what it could have done, and how efficient the solve was.

It may report:

- translations used,
- rotations used,
- throws or tethers used,
- assigned chamber mode,
- actions vs par,
- iteration count,
- abilities available but unused,
- fragments recovered,
- star result,
- SIM's one-way evaluation.

Mode is recorded as context, not as a decision, because Rect cannot switch it. On performance-efficiency focused levels, Audit should emphasise action count, par comparison, and unused or redundant actions. Other levels may emphasise orientation correctness, fragmentation, or adherence to the assigned mode's rules.

Audit is the canonical proof that Rect is not merely completing tasks. It records that the certification system is evaluating its decision-making.

Because Rect cannot communicate, Audit is generated by SIM or SYSTEM telemetry. It never becomes Rect dialogue.

Example line:

```text
SYSTEM // AUDIT COMPLETE. DECISION TRACE RECORDED.
```

---

## 5. Mode and ability matrix

Defaults are starting rules, not permanent bans.

| Ability | Baseline / unlockable | Freeform default | Grid default | Level override |
|---|---|---:|---:|---|
| Translation | Baseline | Yes | Yes | Not needed |
| Rotation | Unlockable | Yes | Yes | Yes |
| Throw | Unlockable | Yes | No | Yes |
| Partition | Unlockable | Yes | Yes | Yes |
| Fusion | Unlockable | Yes | Yes | Yes |
| Scan | Baseline lens | Yes | Yes | Not needed |
| Blueprint | Baseline lens | Yes | Yes | Not needed |
| Audit | Mandatory phase | Always after clear | Always after clear | Emphasis only |

An ability is usable when:

```text
globally unlocked or baseline
AND allowed by the level ability allowlist
AND allowed by the assigned chamber mode, either by default or by level override
AND allowed by the targeted object's own capability flags
```

The chamber mode is fixed. A level override changes a rule inside that mode; it does not switch the chamber to the other mode.

Audit bypasses this chain after anchor lock. It always runs.

---

## 6. Progressive unlock plan

Exact scene assignment is a level-design decision. The recommended rollout is:

| Stage | Ability introduced | Reason |
|---|---|---|
| First chamber | Translation, Scan, Blueprint | Rect must first move and read objects. |
| Early Chapter 1 | Throw | Loose material handling before complex placement. |
| Chapter 2 | Rotation formalised | Axis control becomes a graded skill. |
| Chapter 3 | Partition | Decomposing imported components fits the archive and orientation chapters. |
| Chapter 4 | Fusion | Recombination closes the campaign's mechanical arc. |
| Every clear | Audit | Reflection is required from the first chamber onward. |

A later level may reuse early abilities while withholding newer ones. Unlock order and level usability are separate layers.

---

## 7. Level configuration model

Each level should be able to declare its own mechanism scope.

Recommended fields:

| Field | Purpose |
|---|---|
| `chamberMode` | Exactly one value: `Grid` or `Freeform`. Fixed at scene load. |
| `levelAbilityAllowlist` | Abilities usable in this chamber. |
| `modeOverrides` | Rule exceptions inside the assigned mode, such as `allowThrowInGrid`. |
| `auditEmphasis` | What Audit highlights: efficiency, orientation, fragments, mode-rule adherence, etc. |
| `throwSettings` | Distance range, velocity steps, landing rules, Grid snap behaviour if enabled. |
| `rotationSettings` | Allowed axes and step size. |
| `partitionSettings` | Allowed axes and eligibility rules. |
| `fusionSettings` | Eligible pairs and result rules. |
| `blueprintSettings` | Which overlays are visible. |
| `runtimeReserve` | Final chamber only. Enables the deadline. Duration is set at level design time. |
| `reserveNotification` | Final chamber only. Push-notification text and escalation thresholds. |

Resolution order:

1. Is the ability globally unlocked or baseline?
2. Is it in the level allowlist?
3. Is it allowed by the assigned chamber mode?
4. Does a level override enable or disable it inside that mode?
5. Does the target object allow that operation?

If any gate fails, the action is rejected with TELEMETRY feedback. SIM may add a short one-way correction.

---

## 8. UI and visual language

### 8.1 Mode feedback

- Grid mode shows the grid indicator.
- Freeform mode hides the grid indicator.
- The assigned mode is announced once at chamber open with a SIM status line.
- There is no mode-switch input. If a legacy toggle input is pressed, TELEMETRY reports that the mode is fixed by the certification probe.
- The HUD shows the chamber mode as a fixed badge, not a toggle.

### 8.2 Ability feedback

- Mutating actions produce object pulse, ring, and sound.
- Non-mutating actions produce a target-card readout or status banner.
- Rejected actions report the missing gate: not unlocked, not allowed here, disabled in mode, or object ineligible.

### 8.3 Throw feedback

Throw must display:

- trajectory arc,
- landing marker,
- distance or velocity value,
- direction.

### 8.4 Blueprint feedback

Blueprint uses measurement visuals only. It must not look like a placement ghost or imply that an object has moved.

### 8.5 Audit feedback

Audit appears after anchor lock and before or within the clear overlay.

It should be readable as a certification record, not a conversation.

Approved labels come from the story bible:

| Label | Use in mechanics |
|---|---|
| `SIM` | Instructions and evaluation. |
| `TELEMETRY` | Action confirmations and rejections. |
| `SYSTEM` | Audit, certification, and runtime state. |
| `ARCHIVE` | Fragment and corruption material only. |

No `RECT:` label is used.

### 8.6 Runtime reserve notification

The reserve is presented as a top-right push notification, not as a HUD badge or a story line.

- Source is `SYSTEM`. It is machine state, not dialogue.
- The first notification states that the external feed was interrupted and gives the reserve value.
- Later notifications may restate only the reserve value at set thresholds. Escalation thresholds are set during level design.
- Text is clipped, neutral, and gives no cause, name, or explanation.
- The notification must not be styled as a real desktop or phone notification. It belongs to the game's HUD and palette.
- The notification must not appear in any chamber outside the final narrative phase.

---

## 9. Story connection

The mechanism supports the story without adding new characters or dialogue.

- **Grid mode** is the measured construction layer.
- **Freeform mode** is the recovery and debris layer.
- **One assigned mode per chamber** is SIM's assessment condition; Rect does not choose the rules of the probe.
- **Abilities** are certified manipulation modules.
- **Progressive unlocks** are the curriculum.
- **Per-level allowlists** are the assessment scope.
- **Audit** is the evaluation and decision trace.
- **The runtime reserve** is the final chamber's deadline. It is system state, not a second speaker and not a choice menu.
- **Iteration, par, and stars** are certification metrics.

SDG 4 stays primary through practice, feedback, iteration, and assessment.

SDG 11 remains story-facing. Districts, bridges, housing, archives, and storm readiness give the certification consequence, but the abilities themselves are generic spatial tools.

---

## 10. Current implementation baseline

This section records the current code state as design debt, not as canon changes.

- `SimulationMode.Limited` roughly maps to Grid.
- `SimulationMode.Flexible` roughly maps to Freeform.
- Current Limited has carry, 90° rotation, throw, and placement ghost.
- Current Flexible has free-aim telekinesis, elevation, spin, and push.
- Throw is currently available in Limited and must be disabled by default for Grid.
- Rotation is available in both conceptual modes, matching the updated rule.
- `InteractableObject` already has capability flags for rotation, throw, telekinesis, Partition, and Fusion.
- Partition and Fusion are placeholder hooks only and currently emit `MODULE LOCKED`.
- Blueprint exists through `SpatialBlueprintVisualizer`.
- Scan is not implemented.
- Audit is not implemented as a decision review; the current clear overlay only shows basic metrics.
- Global unlocks and per-level ability allowlists are not implemented.
- `SimulationModeController.ToggleMode()` and its `TAB` binding support runtime switching; this must be removed or locked so each chamber keeps its assigned mode.
- No runtime reserve, countdown, or `SYSTEM` push notification exists yet. The final chamber currently has no deadline.

Implementing this canon is a separate mechanics pass. It must not be mixed into the story-text pass.

---

## 11. Reserved future concepts — not in the mechanism

These concepts are drafted for possible future use. They are **not active**, **not unlocked**, **not implemented**, and **must not appear in player-facing text** in the base mechanism.

### Brace / Lock

Temporarily fix one object in place while Rect manipulates another.

Possible future use: temporary shoring during reconstruction tasks.

Blocked by: physics state handling, release rules, visual feedback, and failure cases.

### Mirror / Reflect

Reflect an object's orientation across an axis or plane.

Possible future use: asymmetric or chiral polyhedra, especially around the archive mirror rooms.

Blocked by: need for objects where reflection is meaningfully different from rotation.

### Explicitly excluded

These are not planned for the base mechanism:

- Scale — colliders, mass, and anchor compatibility become unstable.
- Gravity toggle — trivialises plates and bridges.
- Time rewind — breaks deterministic scoring and certification logic.
- Duplicate / clone — undermines Fusion and most placement puzzles.
- Extra manipulation modes — Freeform and Grid already cover the core distinction.

---

## 12. Change log

- **v0.1 — 2026-11-22:** Established the base mechanism: Freeform and Grid modes, Translation, Rotation, Throw, Partition, Fusion, Scan, Blueprint, mandatory Audit after every chamber, opt-in mode overrides, progressive unlock model, per-level allowlists, and reserved Brace/Mirror concepts excluded from v0.1.
- **v0.1 amendment — 2026-11-22:** Locked each chamber to exactly one mode. Freeform and Grid cannot be mixed inside a chamber, runtime mode switching is removed, `allowedModes` becomes `chamberMode`, and Audit records mode as context instead of as a player decision.
- **v0.2 — 2026-11-22:** Added the final-chamber runtime reserve. A `SYSTEM` push notification starts a deadline before SIM's tone turns cold. The reserve is machine state, has no shutdown button or choice UI, and duration is left undecided. Completion before expiry clears the chamber and runs Audit; expiry or leaving early ends the run in erasure with no certification or Audit, followed by replay as a fresh iteration. Added `runtimeReserve` and `reserveNotification` level fields and top-right notification UI rules.
