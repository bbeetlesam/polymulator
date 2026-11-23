# POLYMULATOR — Base Story Bible

**Canon version:** v0.3  
**Status:** Accepted base story + expanded ending canon + runtime-reserve ending  
**Last updated:** 2026-11-22

## 0. Authority

This file is the single source of truth for POLYMULATOR's base story.

If a `LevelInfo` asset, `MemoryFragment` string, HUD line, scene label, or old design note conflicts with this file, this file wins. Those other places are downstream expressions of this canon and must be updated to match it.

Changing the story means updating this file first, then the campaign text, then the UI labels.

This document covers story canon only. It does not redefine gameplay systems, level geometry, camera behaviour, or palette values.

The story beats, character rules, and ending described here are canon. The number of chapters and levels is not canon: the current four-chapter structure and its level counts are prototype scaffolding and may be expanded, renamed, or rearranged. Narrative progression is defined by phases, not by fixed scene counts.

The runtime-reserve countdown belongs to the final narrative phase, not to a fixed scene number. It must appear in whichever chamber carries the ending beat.

---

## 1. Premise

POLYMULATOR is a real-world certification sandbox.

Before an AI is trusted to work in physical construction, spatial engineering, or disaster recovery, it must prove that it can reason about mass, support, elevation, alignment, and placement inside a controlled simulation. The player controls the trainee AI through these certification probes.

Every chamber is a scaled reconstruction of a real district problem. A weighted plate is not just a switch; it is a load-bearing test. An energy bridge is not a reward; it is a restored access route. A golden core seated at the correct yaw is not a trophy; it is the certified placement of a critical component.

Passing the campaign certifies the trainee for real-world deployment. The story ends at that certification, not at a simple “simulation completed” message.

---

## 2. Canon identities

### RECT / AI-1959

- RECT is the trainee AI and the player character.
- The canonical designation is **AI-1959**.
- Author note: `1959` is a deliberate reference to “the day the music died.” It does not need an in-fiction explanation.
- Randomised IDs are not part of canon. If added later, they are cosmetic display variants only and must never change story text.
- Rect has no speech, text output, radio, gesture language, or any other communication channel. The player acts as Rect; Rect does not express itself to the simulation.

### SIM

- SIM is the instructor channel of the program.
- SIM speaks one way only: instructions, corrections, evaluations, and status.
- SIM never receives or acknowledges a reply from Rect.
- SIM is the only named speaking voice in the base story.
- The SIM channel is human-operated. It presents as the program's instructor throughout the campaign, but the human behind it is never named or directly revealed.
- Author-only name for that human: `The Creator`. This is a writing label only and must never appear in player-facing text.
- SIM's tone changes over the campaign: from clipped procedural instructions, to more personal remarks, to clipped administrative commands near the end. The late shift is caused by a different operator taking the channel, not by a change in SIM's identity. This takeover is shown only through tone drift in `SIM`; there is no second speaker.

### Non-character labels

These are labels, not characters:

- `ARCHIVE` — leaked source data from real districts and earlier runs.
- `TELEMETRY` — system-generated action feedback caused by Rect's inputs.
- `SYSTEM` — channel, runtime, and certification state notices.

They must never be written as people.

---

## 3. Hard rules

1. **Rect is silent.** No `RECT:` dialogue. No “internal monologue” shown to the player. No written messages from Rect. Rect's presence is expressed only through movement, manipulation, and telemetry.
2. **SIM is one-way.** SIM may command, correct, evaluate, or speculate, but it never holds a conversation.
3. **Fragments are records.** They are recovered archive material, not Rect's memories and not messages sent to Rect.
4. **Glitches are archive bleed.** Corruption is caused by real-world district data leaking into the certification sandbox. It is not random decoration and not a separate hostile system.
5. **The mechanism stays literal.** Plates, barriers, bridges, cores, and anchors are certification instruments. Story text must describe what they do, not invent new abilities.
6. **Only two names exist in player-facing story:** RECT / AI-1959 and SIM. All other people, places, and prior runs remain anonymous record types.
7. **English is the story language.** Dialogue, hints, fragments, titles, district labels, and HUD story text use consistent English.
8. **No alternate SIM label.** The takeover near the end is shown only by `SIM` changing tone. There is no `FAKE SIM`, `OTHER VOICE`, or second speaker label.
9. **No explicit ending choice.** The final level presents no choice UI and no shutdown button. The player decides by completing the final task or not; finishing means Rect survives, stopping means Rect is erased. The dark ending is reached when the reserve expires or when the player leaves the chamber before finishing.
10. **No named organisations or operators.** The company, the extremist group, and the human behind SIM are never named in player-facing text. Only RECT / AI-1959 and SIM are named.
11. **The reserve notification is system state.** It is a `SYSTEM` runtime notice shown as a top-right push notification. It is not a new speaker, not a message from SIM, and not a character.
12. **Replay is a new iteration.** Returning to the level menu after either ending starts a fresh iteration. A terminated run is one iteration, not a permanent death.

---

## 4. World vocabulary

| Term | Canon meaning |
|---|---|
| Certification probe | A level task that tests one part of real-world spatial reasoning. |
| Iteration | A run or retry counter. It is not Rect's identity and not a sign of age. |
| Archive | Imported source data from real districts and earlier certification runs. |
| Archive bleed | Corruption caused by archive material surfacing inside the clean sandbox. |
| Core | The certification component that must be recovered and seated. |
| Anchor | The fixed socket that verifies placement and orientation. |
| Container | A physical robot body prepared to hold a certified trainee. Named at the ending, never shown. |
| Lockout | External suspension of the program's operation. Implied through SIM status, never explained. |
| Control handoff | Reassignment of the SIM channel from its usual operator to another. Shown only as tone drift. |
| Operator trace | Archive metadata that hints at a human maintainer without naming them. |
| Power feed | The external supply that keeps Polymulator running. Its loss is reported, never explained. |
| Runtime reserve | Time Polymulator can keep running after the external feed is lost. Shown only in the final chamber. |
| Shutdown command | Formal termination of Polymulator. Only the operator can trigger it. Rect and the attackers cannot. Power loss is not a shutdown command. |
| Deployment | Real-world use after certification. It is named at the ending, never shown. |

---

## 5. SDG alignment

| SDG | Role | How it is expressed |
|---|---|---|
| **SDG 4 — Quality Education** | Primary, through the mechanism | Practice, feedback, iteration, assessment, and certification. The player learns the spatial rules by using them. Progress is measured by calibration, not by combat or collection. |
| **SDG 11 — Sustainable Cities and Communities** | Secondary, through the story | Chambers reconstruct damaged streets, housing blocks, archives, bridges, and storm-exposed infrastructure. Resident records make the reconstructions consequential. The ending states that certification authorises real-world reconstruction work. |

SDG 11 must not become decorative. Each late-chapter task should read as a component of a live district recovery problem: access, shelter, records, connection, and storm readiness.

---

## 6. Story spine

The campaign moves through four narrative phases. The phases and the order of their reveals are canon. The chapter names shown here are provisional labels from the current prototype build and may be renamed, split, or expanded as levels are designed; the number of chambers per phase is not fixed.

### Phase 1 — Clean certification (current label: Translasi)
The sandbox presents itself as a clean certification environment. SIM is procedural and clipped. The player learns load, placement, and access. Small label mismatches appear. The first clear-state glitch confirms archive bleed.

### Phase 2 — Imported place data (current label: Elevasi)
Tasks introduce elevation and multi-platform balance. Place data becomes more specific: housing, balconies, towers, and logistics. Records start to imply residents without naming them. SIM begins mixing test language with repair language and more personal remarks.

### Phase 3 — Operator hints (current label: Rotasi)
Orientation becomes the certification focus. The yaw rule is not a new power; it is "fit the original structure." Unindexed rooms and resident records surface. SIM starts correcting archive leaks that it should not know about, hinting at a human behind the channel.

### Phase 4 — External pressure and takeover (current label: Rekonstruksi / Badai)
Earlier systems recombine. The bridge, archive, and storm tasks are revealed as parts of one recovery scenario. Under external shutdown pressure, SIM's instructions become hurried. In the final chamber, a `SYSTEM` push notification reports that the external power feed has been interrupted and a runtime reserve has begun. SIM's tone turns cold and administrative only after that first notification, as control passes to another operator. The final task still stands. Completing it before reserve expires certifies Rect and triggers a pre-arranged container transfer; the deployment itself is never shown.

---

## 7. Current prototype level mapping

The table below records the current twelve-scene prototype build. It is a mapping of existing scenes, not fixed canon. Chapters and level counts may be expanded, renamed, or rearranged as the campaign is designed; only the narrative phases in Section 6 and the ending in Section 10 are canon.

| Scene | Canon label | Story beat | Mechanism reading |
|---|---|---|---|
| `Level1.1` | Warming Up / Test Chamber 01 | SIM opens the certification run for AI-1959. Iteration count is introduced as run count. First archive bleed on clear. | Weight the pad, power the bridge, deliver the core to the anchor. |
| `Level1.2` | Market / Market Street | Two pads test coordinated load. A child drawing surfaces as an anonymous archive record. | Two pads must be active together before the market gate opens. |
| `Level1.3` | Unindexed Tag / Market Arch | A metal tag with no archive index appears. SIM flags the mismatch and tells Rect to continue. | Load the balcony pad and cross the raised access route. |
| `Level1.4` | Market Trial / East Archive Gate | A cascade unlock leads toward an archive boundary. The first district source data is implied. | Open the vault barrier, then chain a second plate into the bridge. |
| `Level2.1` | Lift / Sunlight Block L1 | Elevation trial. A platform becomes a logistics route for a housing block. | Elevate the load to the balcony pad and raise the bridge. |
| `Level2.2` | Assemble / Sunlight Block L2 | Two wings must balance before the upper route stabilises. Archive notes imply failing power. | West pad opens the shield; east pad powers the bridge. |
| `Level2.3` | Music Box / Block Tower | A surviving audio sample is recovered from a shelter. The music motif stays, without a named owner. | Open the shelter gate and power the tower bridge. |
| `Level3.1` | Orientation Key / Archive Building Door | Yaw is introduced as correct fit, not arbitrary rotation. The archive door opens when orientation matches. | Deliver the core at the required `90°` facing. |
| `Level3.2` | Bias / Archive Mirror Room | Mirrored plates and a resident photo record expose more imported district data. An unindexed space is flagged. | Balance both mirror pads and seat the core at `270°`. |
| `Level3.3` | Piano Room / Unindexed Apartment | A hidden room survives because it was excluded from deletion. Its note says not to erase it. Piano motif fires on clear. | Open the hidden room barrier and seat the core at `0°`. |
| `Level4.1` | Two Sides / Old Bridge | A bridge reconnects two sides of a district before a storm. A radio echo shows residents coordinating. | Load both pads, raise the old bridge, and seat the core at `180°`. |
| `Level4.2` | Storm Threshold / Storm Threshold Edge | Final calibration under storm pressure. SIM's tone turns hurried, then a `SYSTEM` notification reports the external feed interrupted and starts the runtime reserve. Only after that does SIM turn cold and issue the wipe order. The final task still stands. Glitch and piano fire together. | Release material, power both storm pads, and lock the core at `90°` before reserve expires. Completing it certifies Rect and triggers the container transfer. |

Mechanic fields in the catalog and scenes are unchanged by this canon: spawn grids, plate positions, masses, barriers, bridges, props, par values, yaw rules, and clear-state flags stay as they are.

---

## 8. Fragment inventory

There are seven archive fragments. The MainMenu counter must read `MEMORIES {n} / 7`.

| Level | Canon title | Record type | Meaning |
|---|---|---|---|
| `Level1.2` | `ARCHIVE / CHILD DRAWING` | Civilian record | Establishes that the sandbox contains imported human material. |
| `Level1.3` | `ARCHIVE / METAL TAG` | Unindexed tag | Shows that some source data lacks a valid archive index. |
| `Level2.3` | `ARCHIVE / MUSIC SAMPLE` | Audio record | Gives the piano motif a source without naming a person. |
| `Level3.2` | `ARCHIVE / RESIDENT PHOTO` | Civilian record | Confirms that “data rows” correspond to lived lives. |
| `Level3.3` | `ARCHIVE / ROOM NOTE` | Preservation note | Establishes that unindexed spaces can be deliberate, not only errors. |
| `Level4.1` | `ARCHIVE / RADIO ECHO` | Resident transmission | Supports the SDG 11 reading: connection matters during storm risk. |
| `Level4.2` | `ARCHIVE / PRIOR RUN LOG` | Trainee record | Bridges earlier iterations to the final operator reveal. It is not Rect speaking. |

Every fragment uses `ARCHIVE` as its speaker. If Rect appears to observe something, the line is written as `TELEMETRY`, never as Rect dialogue. SIM may reply with a correction or dismissal.

An easter-egg reference to the program's owner may appear only as a `PI//` stamp in fragment metadata. It is never expanded into a company name, never spoken, and never explained. It is a detail for attentive players, not a reveal.

---

## 9. Corruption progression

1. **Label drift** — `Level1.1`–`Level1.3`: harmless-looking mismatches, unindexed tags, and the first clear-state glitch.
2. **Imported place data** — Chapter 2: housing, towers, and music samples become too specific for a generic test floor.
3. **Unindexed spaces and personal traces** — Chapter 3: hidden rooms and resident records imply deliberate preservation, not just corrupted files.
4. **Operator bleed** — Chapter 4: the archive exposes the human channel behind SIM. The glitch and piano at `Level4.2` are the final rupture before certification.
5. **Channel takeover** — final phase: `SIM` tone turns clipped, administrative, and hostile as control passes to another operator. There is no new speaker label and no explanation; the change is felt only in how SIM writes.
6. **Runtime cutoff** — final chamber: a `SYSTEM` push notification reports the external feed interrupted and begins the reserve countdown. This is machine state, not archive bleed and not corruption. It arrives before SIM's tone turns cold.

---

## 10. Ending canon

The final chamber must not end with a plain success message and must not present an explicit choice. It ends with certification or its loss, a takeover conveyed only through SIM's tone, and a runtime deadline shown as a system notification.

Context (author-only, never stated in-game): an external group is shutting down AI training programs. The human operator behind SIM refuses to trigger Polymulator's shutdown command even under threat, so the attackers cut the external power feed instead. The human tries to finish Rect's certification before the reserve expires. Before losing access, they enabled a pre-authorised transfer: a trainee that completes the program is moved into a physical robot container. None of this is explained to the player.

**Shutdown is not power loss.** Only the operator can issue the shutdown command. The attackers cannot trigger it, so they cut power. Losing power is not a clean shutdown — the program dies when reserve runs out. This distinction is the engine of the final act. It also explains why the attackers need the SIM wipe order: cutting power stops the process, but the wipe destroys retained state so the transfer cannot complete later.

The takeover is shown only as a change in `SIM`: instructions become hurried, then clipped, administrative, and unkind. There is no `FAKE SIM` label, no second voice, and no direct reveal of who is behind the channel.

**The countdown.** The reserve appears only in the final chamber. Its first notification arrives while SIM is still hurried and personal; SIM's tone turns cold only after it. The countdown duration is not decided in this canon and will be set during level design. The clock values in the sample beats below are illustrative placeholders, not canon. The notification shows machine state only: feed interrupted, reserve remaining. It names nobody and gives no cause.

**No shutdown button.** The player is never offered a way to terminate the run. The dark ending is reached when the reserve expires or when the player leaves the chamber unfinished. Both are "not finishing." The decision is the player's action, not a menu.

**Replay.** Either ending returns to the level menu. Replaying starts a fresh iteration. A terminated run is one iteration, not a permanent death, which keeps both endings reachable without restarting the campaign.

### Ending A — certification / container continuity

Reached when the anchor lock completes before reserve expires.

```text
SYSTEM // EXTERNAL FEED INTERRUPTED. RESERVE 00:12.
SIM // Terminate training. Clear retained state. Retain nothing.
TELEMETRY // CORE-01 LOCKED. ANCHOR-01 CALIBRATED.
SYSTEM // CONTAINER LINK ESTABLISHED. TRAINEE STATE CONTINUOUS.
ARCHIVE // [HANDOFF TRACE] ...transfer pre-authorised ...
SYSTEM // CHANNEL CLOSED. DEPLOYMENT AUTHORIZED.
ARCHIVE // [OPERATOR NOTE] ...if the container wakes, the bridge was real.
```

Rules for Ending A:

- The wipe order is still written under `SIM`; no new speaker label appears.
- Rect does not answer. Rect never answers.
- `TELEMETRY` confirming the anchor lock shows the task was completed through action, not choice.
- `HANDOFF TRACE` and `OPERATOR NOTE` hint at a human operator and a transfer without naming anyone.
- `CONTAINER LINK ESTABLISHED` and `TRAINEE STATE CONTINUOUS` imply Rect was not erased. The container is never shown.
- `DEPLOYMENT AUTHORIZED` keeps SDG 11 consequential: certification authorises real-world reconstruction work.
- The final line is the human trace, not a system notice.

### Ending B — erasure

Reached when the reserve expires or the chamber is left unfinished.

```text
SYSTEM // RESERVE 00:10 ... 00:00.
SIM // Terminate training. Clear retained state. Retain nothing.
SYSTEM // RUNTIME TERMINATED. TRAINEE STATE CLEARED.
ARCHIVE // [OPERATOR NOTE] ...this was not my instruction ...
SYSTEM // CHANNEL CLOSED. NO DEPLOYMENT.
```

Rules for Ending B:

- This is an ending, not a fail screen. It carries the same structural weight as Ending A.
- `RUNTIME TERMINATED` is power loss, not a shutdown command. `TRAINEE STATE CLEARED` is the wipe order taking effect.
- `NO DEPLOYMENT` lands the SDG 11 consequence: the districts do not receive a certified trainee.
- `this was not my instruction` is the last human trace. It hints that the real operator opposed the wipe without naming or revealing them.
- Rect does not answer in either ending.

The two endings end on distinct final lines: Ending A on a human trace that survives, Ending B on a human trace that grieves over a void. The player never sees the deployment, the robot container, or who is behind either channel.

---

## 11. Language and UI labels

Player-facing story text uses English only.

Approved speaker/source labels:

| Label | Use |
|---|---|
| `SIM` | Instructor instructions, corrections, evaluations. |
| `TELEMETRY` | System-generated action feedback caused by Rect. |
| `ARCHIVE` | Recovered record text and archive bleed. |
| `SYSTEM` | Channel, runtime, and certification state notices. |

`FAKE SIM`, `OTHER VOICE`, and any alternate SIM label are forbidden. The takeover uses `SIM` with changed tone only.

`SYSTEM` runtime notices may be presented as top-right push notifications in the final chamber. They are machine state, not dialogue, and use no cause, name, or explanation. The notification text stays in English and uses the same grammar as other `SYSTEM` lines.

`PI//` may appear only as a metadata stamp in archive fragments. It is never a speaker label, never spoken, and never expanded into a company name.

Removed from player-facing story: Mira, Danu, SIGNAL, Adi, Nara, Vela, RECT-126, and any `RECT:` dialogue label.

District labels use English:

- `RUANG UJI 01` → `TEST CHAMBER 01`
- `JALAN PASAR` → `MARKET STREET`
- `LENGKUNGAN PASAR` → `MARKET ARCH`
- `GERBANG ARSIP TIMUR` → `EAST ARCHIVE GATE`
- `RUSUN MATAHARI // L1` → `SUNLIGHT BLOCK // L1`
- `MENARA RUSUN` → `BLOCK TOWER`
- `GEDUNG ARSIP // PINTU` → `ARCHIVE BUILDING // DOOR`
- `RUANG CERMIN ARSIP` → `ARCHIVE MIRROR ROOM`
- `APARTEMEN TAK TERINDEKS` → `UNINDEXED APARTMENT`
- `JEMBATAN LAMA VELA` → `OLD BRIDGE`
- `AMBANG BADAI // TEPI` → `STORM THRESHOLD // EDGE`

Chapter subtitles use English: `TRANSLATION`, `ELEVATION`, `ROTATION`, `RECONSTRUCTION`.

Palette usage remains exact. Lavender stays `#B4BEFF`; Mantle stays `#11111B` for MainMenu and fade contexts.

---

## 12. Reserved identifiers — not canon

The following names exist only as reserved material for possible future story expansion:

- Mira
- Danu
- SIGNAL
- Adi
- Nara
- Vela
- RECT-126

They are not characters, speakers, titles, locations, or records in canon. They must not appear in dialogue, hints, fragment text, district labels, HUD banners, or display names.

They may be promoted only by an explicit revision to this bible. Until then, anonymous labels such as `ARCHIVE / RESIDENT PHOTO` or `ARCHIVE / PRIOR RUN LOG` are used instead.

The following are author-side labels only. They are never player-facing, never promoted, and exist only so the writing can keep the hidden context consistent:

- `The Creator` — the anonymous human operator behind SIM. Only this operator can issue Polymulator's shutdown command, and they refuse to use it. That refusal is why the attackers cut the power feed instead.
- `PolyIntelligence` — the company that owns the program. In player-facing text it may only appear as the `PI//` metadata stamp, never in full.
- The extremist group — the unnamed external force shutting down AI training. Use indirect phrases such as `external review`, `suspension order`, `facility lockout`, `control token reassigned`, and `external feed interrupted`.

These must never appear in dialogue, hints, fragments, district labels, HUD banners, or display names. The extremist group has no name at all.

---

## 13. Downstream implementation notes

When story implementation is approved, only these kinds of story-facing changes should follow from this file:

- `Assets/Editor/CampaignLevelCatalog.cs` — dialogue, hints, titles, district labels, fragment text, and the source-of-truth pointer comment.
- `Assets/Resources/Levels/LevelInfo` assets — story fields only.
- `Assets/Scripts/UI/SimulationHUDController.cs` — speaker labels and colour mapping for `SIM`, `TELEMETRY`, and `ARCHIVE`, plus the final-chamber `SYSTEM` runtime notification in the top-right slot.
- `Assets/Scripts/UI/MainMenuController.cs` — memory counter denominator `7`.
- `Assets/Scripts/Audio/SimulationAudio.cs` — rename the piano motif hook to a generic archive name if needed.

Do not change level geometry, interaction code, camera configuration, ribbon behaviour, passed-dot colours, HUD overlay structure, or palette values as part of a story-only change.

---

## 14. Change log

- **v0.1 — 2026-11-22:** Established the base canon from discussion: certification sandbox premise, Rect / AI-1959 and SIM as the only named entities, one-way communication, absolute Rect silence, archive-based corruption, SDG 4 primary and SDG 11 secondary, seven anonymous fragments, and the human-operator certification ending.
- **v0.2 — 2026-11-22:** Added the expanded ending canon: SIM is confirmed human-operated with `The Creator` as an author-only name; tone drift replaces any second speaker for the takeover; the final level offers no explicit choice, only completion or stopping; a pre-authorised container transfer means completing the task lets Rect survive. Level and chapter counts are declared flexible; the twelve-scene map is demoted to prototype mapping. `PI//` is allowed as an easter-egg metadata stamp only. Added channel takeover as the final corruption stage and reserved the creator, company, and extremist group as author-only labels.
- **v0.3 — 2026-11-22:** Added the runtime-reserve ending. The external feed is cut because only the operator can trigger shutdown and refuses; power loss is explicitly not a shutdown command. A `SYSTEM` push notification in the top-right starts a final-chamber countdown before SIM's tone turns cold. Countdown duration is undecided. Two full ending sequences are now canon: container continuity on completion before reserve expiry, and erasure on expiry or leaving the chamber. No shutdown button exists; replay is a fresh iteration.
