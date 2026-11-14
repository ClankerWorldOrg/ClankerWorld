---
title: How the game works
type: architecture
status: active
updated: 2026-11-14
---

# How the game works

This page indexes the implementation boundaries and rules of the current game.

The current private alpha has three components. These are implementation
boundaries; [game design](../game-design/README.md) owns the intended experience.

| Component | Owns |
| --- | --- |
| `src/ClankerWorld.Simulation` | Rules, accepted world state, content, society, persistence and replay |
| `src/ClankerWorld.Server` | Headless server, signed owner API, clock, provider adapters and private installation stores |
| `src/ClankerWorld.GodotClient` | Windows game view, local preferences, observations and signed requests |

The server references the simulation, never the reverse. The simulation runs and
tests without Godot, a window manager, a live model or network access after
dependencies are restored. Godot does not reference the simulation: it uses the
server's versioned HTTP contract. Legacy web assets are diagnostic tools.

## Core rules

`WorldHistory.Council.cs` partitions closed repair-input reservations into a
32-record recent window, retained cart-order and hearing proof, and older audit
records. The normal checkpoint writer archives the audit records before
installing the smaller live inventory. Active claims and physical goods stay
with inventory authority; [Saves and replay](saves-and-replay/checkpoints-and-recovery.md#checkpoints-history-and-backups)
owns the archive count and recovery contract.

- **The server commits world state.** Client requests, model replies, mods and
  save files are untrusted input. Deterministic rules validate movement, work,
  resources, occupancy, ownership and effects. Use seeded random paths and
  keep external I/O outside authoritative transitions.
  Named PCG streams retain their exact UTF-8/HMAC seed derivation and algorithm
  metadata while using a fixed stack buffer for temporary digest outputs.
- **A tick commits all its accepted changes together.** A tick is one step of
  world time. Rejected, cancelled or stale model work cannot leave a half-applied
  action. Pause and loss of authenticated client presence invalidate in-flight
  work. A failed or unusable reply uses only explicit `safe_idle`;
  it cannot execute a strategic candidate or complete an instruction.
- **Time and spending require presence.** The server remains reachable while
  paused. Ticks and model calls require an authenticated presence lease;
  the last disconnect closes the gate after about five seconds. Reconnecting
  performs no offline catch-up and does not clear a manual pause.
- **Knowledge belongs to each agent.** The player's map and observations are
  not automatically agent knowledge. Preserve ownership, source, confidence
  and correction history. A belief can be wrong without changing world facts.
  Long descendant identities remain intact in saved discovery provenance;
  knowledge keys and model-facing discoverer references use stable hashes
  only when the original identifier exceeds their size limits.
- **Credentials are installation state.** Keep keys and device authority out
  of saves, exports, observations and telemetry. See
  [device pairing](device-pairing.md) for transport and access rules.
- **Content is bounded and validated.** Current content definitions are data-only.
  Fixed Town Hall and Market metadata is shared, with read-only costs and tags.
  Definitions still undergo their normal validation; no world state is retained.
  Arbitrary executable agent code is disabled. The intended restricted script
  system remains design work, not permission to run generated code today.
  `StarterContent` owns the lazy, read-only shipped catalogue used by world creation
  and production-order naming. Each world imports definitions and runs its complete
  package and asset lifecycle separately.
- **Recovery preserves evidence.** Unsupported or corrupt saves must fail
  visibly without replacing valid state or inventing replacement credentials.
  [Saves and replay](saves-and-replay.md) owns formats, migration and backup rules.


## Topics

- [Source layout and identities](how-it-works/source-layout.md): code areas, shared contracts and derived identities.
- [Runtime system ownership](how-it-works/runtime-ownership.md): system ownership and validation.
- [Action definitions](how-it-works/action-definitions.md): typed choices and shared checks.
- [Action plans and capabilities](how-it-works/action-plans.md): retained plans and actor eligibility.
- [Runtime action checks](how-it-works/runtime-action-checks.md): system boundaries and retained action checks, including civic and trade choices.
- [System boundaries and runtime contracts](how-it-works/system-boundaries.md): interfaces, caches and refactor contracts.
- [Client presentation](how-it-works/client-presentation.md): roster and panel layout.
- [Finding goods](how-it-works/finding-goods.md): inventory queries and usable goods.
- [Clock and asynchronous decisions](how-it-works/clock-and-decisions.md): clock, pending model work and order completion.
- [Owner orders](how-it-works/owner-orders.md): order grammar and physical actions.
- [Time of day and building completion](how-it-works/day-and-night.md): day, night and visual effects.
- [Model requests and identity](how-it-works/model-requests.md): prompts, identity and provider choices.
- [Conversation and memory](how-it-works/conversation-and-memory.md): conversations, memory, written knowledge and exploration.
- [Model selection and usage](how-it-works/model-usage.md): model selection, testing and usage accounting.
- [Maps and movement](how-it-works/maps-and-movement.md): maps, movement, weather and terrain presentation.
- [Terrain and mountains](how-it-works/terrain-and-mountains.md): terrain layers and mountain generation.
- [Town governance and land](how-it-works/town-governance.md): marriage history, governance and land cases.
- [Town membership and government](how-it-works/town-membership.md): admission, government and elections.
- [Town building sites](how-it-works/town-building-sites.md): layouts, construction sites and household planning.
- [Housing and population continuity](how-it-works/housing-and-continuity.md): departure, housing and continuity.
- [Production and markets](how-it-works/production-and-markets.md): production inspection, household work, barter and Markets.
- [Household work and care](how-it-works/household-work-and-care.md): tool requests, medical care and household production.
- [Wills and estates](how-it-works/wills.md): wills, estates and final words.
- [Fertility and household fields](how-it-works/fields.md): fertility, field work and food recovery.
- [Roads and Town projects](how-it-works/roads-and-projects.md): roads, bridges and street lanterns.
- [Assets and map editing](how-it-works/assets-and-map-editing.md): approved assets, map editing and building storage history.
- [Runtime logging](how-it-works/runtime-logging.md): logs, problem reports and event descriptions.
- [Developer tools and distribution](how-it-works/developer-tools.md): developer edits, readouts and finished distribution.
- [Material gathering](how-it-works/material-gathering.md): physical gathering and tools.
- [Boats and handcarts](how-it-works/boats-and-handcarts.md): communal boats and physical handcarts.
- [Trees and generation controls](how-it-works/trees-and-generation.md): trees, planting and generation controls.
- [Skills and practical lessons](how-it-works/skills-and-lessons.md): learned skills and lessons.
- [Launcher hosting and developer runs](how-it-works/launcher.md): launcher hosting, developer runs and world-list requests.
- [Animals and horse travel](how-it-works/animals.md): animal care, trade and horse travel.
