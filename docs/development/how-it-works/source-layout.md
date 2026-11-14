---
title: Source layout and identities
type: architecture
status: active
updated: 2026-11-14
---

# Source layout and identities

This page describes the code layout and derived identities.

## Source layout

The current code is split by area, with
`WorldRuntime` partials forwarding to the systems already extracted. The
[code structure](../code-structure.md) page describes the refactor target.

| Area | Current code |
| --- | --- |
| Tick preparation, commit, model admission and owner orders | [World/Runtime](../../../src/ClankerWorld.Simulation/World/Runtime/), including `WorldRuntime.Tick.cs`, `WorldRuntime.Cognition.cs`, `WorldRuntime.ExpansionOrders.cs` and `OrderTaskParser.cs`; request contracts live in [World/Instructions](../../../src/ClankerWorld.Simulation/World/Instructions/) |
| Saved world encoding and history | `WorldRuntimeState.cs`, `WorldRuntimeCodec.cs` and `WorldHistory.cs` in `World/Runtime`; [server Saves](../../../src/ClankerWorld.Server/Saves/) owns files and recovery |
| Inventory authority, goods and food | [Kernel](../../../src/ClankerWorld.Simulation/Kernel/); [World/Goods](../../../src/ClankerWorld.Simulation/World/Goods/) owns selection and rechecks |
| People, relationships and household transactions | [Society](../../../src/ClankerWorld.Simulation/Society/); [World/Family](../../../src/ClankerWorld.Simulation/World/Family/) integrates care, birth, continuity, medicine and wills |
| Housing, household departure, skills and lessons | [World/Community](../../../src/ClankerWorld.Simulation/World/Community/) |
| Production, equipment, barter, shops, containers and handcarts | [World/Economy](../../../src/ClankerWorld.Simulation/World/Economy/), with production and order coordination in `World/Runtime` |
| Town membership, land, law, councils and setup | [World/Civic](../../../src/ClankerWorld.Simulation/World/Civic/) owns `TownSystem`; [World/Towns](../../../src/ClankerWorld.Simulation/World/Towns/) holds its coordinator forwarders, contracts, layout, roads, projects and boats |
| Weather, ecology, chunks and exposure | [World/Environment](../../../src/ClankerWorld.Simulation/World/Environment/); map generation and terrain live in [World/Map](../../../src/ClankerWorld.Simulation/World/Map/) |
| Field work, crops and forestry | [World/Farming](../../../src/ClankerWorld.Simulation/World/Farming/); `World/Economy` and `World/Runtime` retain planner and order adapters |
| Animals, riding and physical supply trips | [World/Animals](../../../src/ClankerWorld.Simulation/World/Animals/) |
| Conversations and marriage | [World/Conversations](../../../src/ClankerWorld.Simulation/World/Conversations/); pending model work stays in `World/Runtime` |
| Memories, beliefs, exploration and writing | [World/Knowledge](../../../src/ClankerWorld.Simulation/World/Knowledge/); model requests and reply validation use [Cognition](../../../src/ClankerWorld.Simulation/Cognition/) |
| Content definitions, recipes and imports | [Content](../../../src/ClankerWorld.Simulation/Content/) owns declarative validation and imports; [World/Content](../../../src/ClankerWorld.Simulation/World/Content/) owns built-in definitions and world-content simulation |
| Shared physical obstacles | [World/Queries](../../../src/ClankerWorld.Simulation/World/Queries/) owns `GroundOccupancy` and its runtime adapter |
| HTTP, pairing, clock, models and observations | [server Endpoints](../../../src/ClankerWorld.Server/Endpoints/), [Pairing](../../../src/ClankerWorld.Server/Pairing/), [Runtime](../../../src/ClankerWorld.Server/Runtime/), [Providers](../../../src/ClankerWorld.Server/Providers/) and [Observation](../../../src/ClankerWorld.Server/Observation/); [Worlds](../../../src/ClankerWorld.Server/Worlds/) owns world selection and [Content](../../../src/ClankerWorld.Server/Content/) owns approved assets and owner content requests |
| Game screens, requests, local server and launcher | [client Screens/Main](../../../src/ClankerWorld.GodotClient/Screens/Main/), [UI](../../../src/ClankerWorld.GodotClient/UI/), [Networking](../../../src/ClankerWorld.GodotClient/Networking/), [Protocol](../../../src/ClankerWorld.GodotClient/Protocol/), [Pairing](../../../src/ClankerWorld.GodotClient/Pairing/), [ClientState](../../../src/ClankerWorld.GodotClient/ClientState/), [Launcher](../../../src/ClankerWorld.GodotClient/Launcher/) and [Diagnostics](../../../src/ClankerWorld.GodotClient/Diagnostics/) |

The server and game compile the same records from
`ClankerWorld.Shared/Contracts`. Server-only partial record members keep native
projection and thumbnail factories beside server code. The server uses those
record names directly; the game has no separate DTO copy.
Rendering conveniences are excluded from JSON. A strict round-trip check fills
every wire record and reads it through the actual game assembly, comparing the
complete JSON and refusing an unmapped field. This contract belongs to the game
and world server of the same build; it changes no saved world schema. The shared
text validator also compiles into both programs; timeline references and
optional build text keep their existing validation rules.

The server adds need-state labels to the existing decision-factor collection
and writes the displayed activity into the existing public-intention summary.
The roster and cards read those labels; they do not infer need states or
activities from numbers and choice prefixes. Cold uses the same threshold as
urgent warmth in the simulation. The shared contract also owns packed terrain
codes/layer names and calendar formatting; the server supplies the saved
calendar pace, and the game applies its date and 12/24-hour display preferences.
No new gameplay or saved fields are introduced.

## Derived identities

`DerivedId` owns the 128 UTF-16-unit limit for identities assembled from other
identities. A short complete value keeps its spelling; a longer value uses a
SHA-256 digest of the complete value, retaining a bounded semantic prefix.
Inventory splits, repeated milk sales, native births and family records,
work reservations and receipts, memories and public Town record handles use
this boundary. Separate source, owner and recipient fields retain their exact
references. Names remain display text and do not become identifiers.

The [derived-identity inventory](../derived-identities.md) lists the native
transition and production-factory matrices and their strict reload/recall
checks.

Numbered Town records keep their sequence suffix after hashing the complete
long value. Strict validation recomputes the full handle from its original
scope and counter; it does not search an unbounded sequence or trust a digest
alone. Parsed action payloads retain their fields and existing model-facing
aliases. Content package identifiers retain their import rules.
