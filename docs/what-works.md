---
title: What works today
type: product-status
status: active
updated: 2026-10-18
---

# What works today

This describes the **current repository build**, not every feature in the
[game design](game-design/README.md). The private alpha uses a Windows 11 x64
Godot client connected to a world server. Repository changes may still
need deployment or testing on the owner's laptop. Check the tested build on an
issue before treating a source fix as a verified game fix.

**Settings → Game** and **Developer tools (F12, in Developer mode)** show the client build, for
example `Build 0.1.0-dev+abc1234`. Include that line in a bug or playtest report.
Hover over it for the full source commit. The server reports its own version and
full commit at `/api/v1/status`; client and server builds can differ.

## Status labels

- **Available in the game:** connected to the normal world server and Godot path.
- **Basic version:** connected, but the experience or rules are still limited.
- **Built but not connected to normal play:** code and isolated tests exist,
  but players do not yet encounter the complete feature.
- **Not built yet:** an intended feature still needs implementation.

Availability and verification are separate. A feature can be connected in the
repository build while its Windows playtest is pending. A passing isolated
test alone does not make it available in the game.


- [World and controls](what-works/world-and-controls.md): World creation, controls and the game interface.
- [Agents and their models](what-works/agents-and-models.md): Agents, their models, memories and conversations.
- [Orders and goods](what-works/orders-and-goods.md): Instructions, gathering, storage, collection and deliveries.
- [Orders for work](what-works/orders-for-work.md): Instructions for production, building, repairs, fields and shelter.
- [Animals](what-works/animals.md): Animal care, products and riding.
- [Life, work and society](what-works/life-work-and-society.md): Survival, work, households and society in the current build.
- [Life stages and estates](what-works/life-stages-and-estates.md): Life stages, births, deaths, inheritance and Town founding.
- [Town government](what-works/town-government.md): Councils, offices, land rights, laws and hearings.
- [Town projects](what-works/town-projects.md): Town buildings, transport, markets, growth and departure.
- [Households and buildings](what-works/households-and-buildings.md): Housing, equipment, construction and building sizes.
- [Maps, weather and appearance](what-works/maps-weather-and-appearance.md): Terrain, travel, weather and the game’s appearance.
- [Saves, keys and inventions](what-works/saves-keys-and-inventions.md): Saves, recovery, keys, inventions and deletion.
