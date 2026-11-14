---
title: Developer tools and distribution
type: architecture
status: active
updated: 2026-11-14
---

# Developer tools and distribution

This page describes developer edits, readouts and finished distribution.

## Developer edits

The F12 panel submits one signed `POST /api/v1/owner/developer-edit` command for
the selected agent. The signature binds the world ID, expected latest event ID,
agent, operation, value, amount and optional other agent. The server shares the
world-selection mutation gate, then the runtime checks that the world is paused
and the observation is current. Changes are prepared on an isolated checkpoint,
fully validated, and persisted before the live runtime accepts them. Failed
validation or persistence leaves the prior world unchanged.

Needs use whole percentages from 0 to 100. Goods use the bounded list in
`WorldRuntime.DeveloperGoods`, quantities from 1 to 100 and the existing
carrying limit. Removal consumes unreserved personal carried goods, excluding
equipped items, delivery goods, knowledge records and vessels with contents.
Skills use the four existing learned skills. Relationship edits start or end
partnerships with the existing age, availability and close-kin constraints;
parentage, guardianship and household membership retain their lifecycle rules.
Every accepted command appends a player-facing `developer_edit` event containing
the complete command, including its world and observation precondition.
See [Saves and replay](../saves-and-replay/validation-weather-and-events.md#developer-edits) for retries and replay.

## Developer tools readouts

Developer tools (**F12** in the Godot client) read two diagnostics from the owner
observation. Both come from the committed world, are never saved and are never
read back by the simulation, so they cannot change a tick, a save or replay.

- **`PlannedRoute`** on each living agent is the route `MoveToward` planned on
  the agent's latest step: its reason code, destination and the tiles still
  ahead. The observation sends at most 256 steps; `StepCount` gives the full
  count. An agent waiting out a slow step keeps the route it was walking; one
  that arrived, was blocked or did something else that tick has none. Each
  proposed tick starts without routes and its commit replaces them. Loading a
  checkpoint, switching worlds or restarting the server clears them until the
  next tick.
- **`LastTickMilliseconds`** on the snapshot is the wall-clock time to prepare
  and advance the latest committed tick, including synchronous reply admission,
  listener belief extraction, child-model selection preparation and world commit,
  rounded to 0.1 ms. The timer pauses for runtime-gate reacquisition. It leaves out
  waiting for the runtime gate, model calls between ticks and the
  checkpoint save. It is null until the first tick after start, load or a
  world switch.

The client draws only the reported route; it never plans one. Frame time is
measured in the client.

## Development and finished distribution

The world server runs the authoritative simulation; the game draws it and
sends the player's choices. By default the game starts its own world server on
the player's Windows PC, with local saves and keys, as a separate bundled
process ([#468](https://github.com/ClankerWorldOrg/ClankerWorld/issues/468)). The
game can also connect to a world server on another machine, such as the
development VPS, whose logs the agent there reads. Both ways run the same
server. See [where the game runs](../../game-design/world.md#where-the-game-runs).
