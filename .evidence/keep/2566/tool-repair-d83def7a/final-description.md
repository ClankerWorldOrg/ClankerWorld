## Summary

Two multi-material repair tests repeated the same native-world and inventory setup. They now share one two-row rule test, preserving both blocked and fitting loads, every assertion, seed, material insertion order, provider, tick bound and reload. The warehouse test keeps its empty, inhabited and reserved cases while removing an unused item-kind parameter and branches left behind when #864 removed the own/hoe rows.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session d83def7a
- Refs #2566 — remaining World/root Simulation audit stays open.
- Existing behavior: repair must have room for the complete multi-material load before pickup; warehouse access and reservations still control collection.
- Other open PRs: checked all 90 open test PRs; none changes this file, and before/after `git merge-tree` finds no new conflict.
- Stacked on: none. Independent main base `8b57533137b8920b7692259dee0b006afe1cf7dd`; no main refresh planned.
- Lines added/removed: source **+0/−0**, tests **+81/−113** (32 fewer; 887→855), docs **+0/−0**.
- Cases: **23 / 9 methods → 23 / 8 methods**, all pass, no skips. Full built discovery **7,422 cases unchanged; 3,310→3,309 methods**. Zero cases count as removed toward the audit target.
- Work left: the rest of #2566; this PR claims only this one-file consolidation.
- Hands-on check: none needed for a test-only refactor.
- Unfinished work before readiness: none. Exact-head CI and all local checks pass.

## Checks

Completed:
- Locked restore, fresh Release builds, unused-code probe **169→169**, zero new diagnostics.
- Built discovery and nine-entry ledger pass; all retained proofs run.
- Identical full-class filter on base and head: `FullyQualifiedName~ClankerWorld.Tests.ToolProgressionRuntimeTests.` — **23→23 cases, 9→8 methods**, all pass, no skips.
- `compare_coverage.py --threshold-scope each` passes with identical scope and zero lost lines or reduced mapped branch counts.
- Stable JSON branch comparison reconciles every count with the collector summary and compares module/document/type/method/line/IL offsets/path/ordinal identities. All **88,374** branch identities match, including **352** outside Cobertura's mapped fractions; no previously covered branch disappears. Production DLL/PDB bytes match. The conservative full-suite loss upper bound is **0 percentage points in every assembly and total**, without offsetting losses against new hits.
- Original three methods and retained two methods measured individually: **five cases before and after, all pass**, with identical unions of **17,621 Simulation lines**, zero lost lines or reduced mapped branch lower bounds. All six DLL/PDB hashes are restored after measurements.
- Read-only second reader checked the actual diff against all original inputs/assertions and disposal scopes.
- Formatter, all90 open-PR before/after merge checks, stale-text search and `git diff --check` pass.

| Production scope | Covered lines before = after | Covered branches before = after |
| --- | --- | --- |
| Simulation | 19,041 / 61,464 | 14,207 / 74,686 |
| Server | 0 / 13,010 | 0 / 11,453 |
| Linked production in tests | 0 / 3,397 | 0 / 2,235 |
| Total | 19,041 / 77,871 (24.451978%) | 14,207 / 88,374 (16.075995%) |

All coverage changes are **0 pp / 0% relative**, with zero lost locations. No isolated lost-line run is needed.

CI completed on unchanged head `cdea064417f1be3fbadc02862320a9d00ba1c1d9`:
- [Verify run39722452399](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39722452399): all eight test shards, code/Godot/export checks, native Windows checks, installer and `verify` pass; all25 associated checks complete successfully or neutral/skipped. GitHub mergeable=true.
- Both `test-times` logs and all ten growth rows read: frozen-base run39712166176 reports **12,470 s vs main14,520 s (−14%,3,310 shared methods)**; head run reports **15,027 s vs main15,467 s (−3%,3,296 shared methods)**.
- All16 timing artifacts downloaded, digest-verified and parsed. CI merge executions: **7,422 cases /3,310 methods /7,413 pass /9 platform skips →7,399 /3,297 /7,390 pass /9 skips**. Other main merges account for the23-case and12-method reductions beyond this PR; isolated base/head discovery keeps7,422 cases and removes exactly one declaration.
- Changed class in CI: **23 cases /9 methods /95.883 s →23 /8 /112.020 s**. Local scoped case time182.4→179.7 s. No attributable speedup claimed.
- Evidence and full ledger are preserved on the separate, **never-merge** branch `codex/2566-audit-evidence-d83def7a`, under `.evidence/keep/2566/tool-repair-d83def7a/`.

Per the owner's requests (#2652/#2656), no local full-suite coverage, no main merge without a GitHub conflict, and no new PR until this draft is ready. Production, support and test configuration are unchanged; the filter includes the entire changed class and every ledger proof. SDK 10.0.401, Linux, `DOTNET_PROCESSOR_COUNT=4`; scoped runs use `xUnit.MaxParallelThreads=4`. Canonical coverage settings are unchanged; only JSON output is added for branch identities. Native Windows and Godot gates are delegated to applicable CI; no player-path playtest claimed.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

Every original strict byte-round-trip, validation, reservation and repair assertion remains. No production change.

## Writing, docs and changelog

- [x] No product text changed.
- [x] No affected docs.
- Stale-text search: `node scripts/find-stale-docs.js ToolProgressionRuntimeTests MultiMaterialToolRepair SharedWorkTools` found no stale sentence.
- [x] No changelog needed for this test-only cleanup.
