---
title: Developer guide
type: development-index
status: active
updated: 2026-10-11
---

# Developer guide

These pages are for people changing the code or running a world server.
For the game itself, start with [playing](../playing.md),
[what works today](../what-works.md) or [game design](../game-design/README.md).

| I need to… | Read |
| --- | --- |
| Understand the components and rules to preserve | [How the game works](how-it-works.md) |
| Add a derived ID or check its native save and recall boundary | [Derived identities](derived-identities.md) |
| Restore dependencies, build and check a change | [Build and test](build-and-test.md) |
| Read the scoped automated results for build `31aae869` | [Automated verification — 2026-10-10](verification-2026-10-10.md) |
| Compare construction query costs and native state equivalence | [Construction query measurements](construction-query-measurements.md) |
| Compare repeated route-step checks and native replay | [Route step cost measurements](route-step-cost-measurements.md) |
| Compare walking-route neighbor allocations and native replay | [Walking-route neighbor measurements](route-neighbor-query-measurements.md) |
| Measure wild-animal forage searches and allocation | [Wild forage measurements](wild-forage-measurements.md) |
| Measure cached server observations and allocation | [Cached observation measurements](cached-observation-measurements.md) |
| Draw, review or change game art | [Pixel-art style guide](art-style.md) |
| Approve devices or understand request security | [Device pairing](device-pairing.md) |
| Change saves, recovery or replay | [Saves and replay](saves-and-replay.md) |
| Update a remote world server with backups and rollback | [Remote world server deployment](remote-server-deployment.md) |
| Choose versions and publish a release | [Releasing](releasing.md) |
| Change a label or a label workflow | [Labels](labels.md) |
| See measurements from merged prototypes | [Survival priorities](survival-priority-prototype.md), [Weather episodes](weather-episode-prototype.md), [Need wording](need-wording-comparison.md) |

[CONTRIBUTING.md](../../CONTRIBUTING.md) owns the rules every contributor
shares, including writing; each job's own rules are in its
[skill](../../skills/README.md). [AGENTS.md](../../AGENTS.md) adds coding-agent
instructions and links to those rules. Issues own work tracking; these pages describe the present
implementation. Check dated operational claims against the running installation
before changing it.
