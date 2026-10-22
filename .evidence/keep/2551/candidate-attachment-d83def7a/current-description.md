## Summary

Civic candidate attachment now selects a plan through the same ordered decoders, checks it through the same typed eligibility rule, and shares one update/remove step. This replaces 31 copies of that final step without adding a helper or changing any rule.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session d83def7a.
- Refs #2551. Further Civic cleanup and the 20% target remain.
- Independent main base `e7ccf6a9da1fef954ad525025804fc2b1ee14666`, head `66ee541e2bc0cd788d351820d53849321959c48b`. Stacked on none. All earlier own drafts are ready; no main merge.
- Source **+100/−242** (142 fewer lines), tests **+0/−0**, docs **+0/−0**. One file: **290→148** lines. Civic: **9,301→9,159** on this independent branch.
- Existing behavior preserved: decoder order, lazy reuse of an existing correctly typed plan, planner arguments/defaults, one identical typed check, unknown IDs left untouched, null plans removed, backward iteration and the boundary for newly added candidates.
- All 31 plan types are sealed. Complete old/new mapping compared; every decoder, cast, planner and check matches. No allocation helper/delegate, caller, capability policy, binding, state mutation or test change.
- All 108 open PR file lists checked; none changes this file. #2747 and #2751 remain independent, with none of their changes included here.
- Prior 114 merged +42 ready #2747 +38 ready #2751 +142 here would total **336 net source lines** (3.57% of original 9,415; target 1,883 remains unmet).
- No hands-on check needed for this internal refactor. Draft waits for formatting and exact-head CI.

## Checks

Completed:
- Full before/after source read and exact ordered mapping audit: 31 decoders/planners/casts/checks preserved.
- All selected plan records verified sealed; null, unknown, wrong-type and existing-plan cases reviewed.
- Stale-text search for AttachCivicVisitPlans, AddTownCivicCandidates and TownSystem.VisitActionPlans: no sentence became false.
- Whitespace check: pass.
- Fresh locked restore and Release build with `--no-restore --no-incremental`: zero warnings/errors.
- Discovery: 7,396 cases / 3,295 methods, matching fixed-base CI; test sources and configuration are byte-identical.
- Standard native comparison: **68/68 frames equivalent**, checkpoint bytes/events/digests identical, two seeds ×16 ticks ×generated/legacy. Both run.json files identify the fixed base/head and empty working-tree status.
- Existing focused Civic/runtime filter: **255/255 cases passed**, **158 methods**, zero skips; local duration 6m33s. Run once on the fixed head.
- Base CI run 39738043046: 7,396 cases / 3,295 methods, 7,387 passed /9 platform skips; all eight timing ZIP digests verified. Focused subset: 255/255 cases, 158 methods, 591.375 summed seconds.
- Personally read all ten base test-times growth rows: 13,513 s vs main median 15,188 s (−11%). Head timing remains pending; no speedup claim.

Pending once on this fixed base/head:
- `dotnet format --verify-no-changes --no-restore`.
- Exact-head full Release CI run 39741101769, Windows/Godot/package gates and personally read head test-times.

No local full suite or coverage: tests/configuration are unchanged; CI runs the full suite. This is a source refactor, with focused tests and native equivalence locally.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

No schema, persistence or compatibility changes. Native equivalence passed as recorded above.

## Writing, docs and changelog

- [x] No game text changed.
- [x] No documented supported contract changed.
- Stale-text search names/results recorded above.
- [x] No changelog needed for an internal refactor.

