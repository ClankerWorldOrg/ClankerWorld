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
- No hands-on check needed for this internal refactor. Draft waits for build, existing focused tests, native equivalence, formatting and exact-head CI.

## Checks

Completed:
- Full before/after source read and exact ordered mapping audit: 31 decoders/planners/casts/checks preserved.
- All selected plan records verified sealed; null, unknown, wrong-type and existing-plan cases reviewed.
- Stale-text search for AttachCivicVisitPlans, AddTownCivicCandidates and TownSystem.VisitActionPlans: no sentence became false.
- Whitespace check: pass.

Pending once on this fixed base/head:
- Fresh locked restore and Release build; global discovery and existing focused Civic/runtime tests.
- Standard native tick-equivalence comparison.
- `dotnet format --verify-no-changes --no-restore`.
- Exact-head full Release CI, Windows/Godot/package gates and personally read test-times.

No local full suite or coverage: tests/configuration are unchanged; CI runs the full suite. This is a source refactor, with focused tests and native equivalence locally.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

No schema, persistence or compatibility changes. Native equivalence is pending above.

## Writing, docs and changelog

- [x] No game text changed.
- [x] No documented supported contract changed.
- Stale-text search names/results recorded above.
- [x] No changelog needed for an internal refactor.

