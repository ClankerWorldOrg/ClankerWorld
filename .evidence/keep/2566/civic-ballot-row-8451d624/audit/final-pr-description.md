## Summary

Use a Fact for the sole malformed civic-ballot input, moving the identical JSON string into the test body. Provider parsing and every assertion remain unchanged.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session 8451d624.
- Refs #2566. Remaining Society/Map candidates and final whole-area reconciliation stay open; original target 8,364 test lines.
- Independent main base `442fdcffefb3f97ec8bc49fa966261e7dbf7244f`, head `f82abff0d1ced0bd5d4a47522bc0a0e05fd0c78d`; stacked on none. All earlier own drafts are ready. No main merge.
- Lines added/removed: source **+0/−0**, tests **+3/−4 (1 fewer)**, docs **+0/−0**. CivicBallotProviderTests shrinks **110→109 lines**.
- Keep the exact escaped JSON string, handler/provider setup, request, exception assertion and every sibling proof: missing versus explicit null, abstention, three choices and duplicate vote rejection.
- **Six cases/four methods remain**, with all **17 assertion calls**. One theory case becomes a Fact with the same method name; no case-removal credit.
- Full class, owning parser/callers, retained proofs and Git history read. Pre-edit ledger covers all four methods. No support/configuration/production changes.
- All 116 open PR file lists and 232 before/after merge-tree probes pass, with no changed-file overlap, moved head or new conflict.
- Read-only second reader accepts exact committed diff and raw evidence. Independent top-level review remains. No hands-on check is needed for this test-only change.

## Checks

All local author checks are complete on this fixed base/head:

- Fresh locked Release builds pass with **zero warnings/errors**, base 5:28.18 and head 6:27.47. Local SDK 10.0.401; CI SDK 10.0.402, under the latest-patch policy.
- Full discovery: **7,396→7,396 cases**, **3,295→3,295 methods**. Only the approved parameterized display name becomes the same-named Fact; all other identities/order are unchanged. Ledger verification passes.
- One identical affected filter on base/head, including the whole changed class and every retained proof:
  `FullyQualifiedName~ClankerWorld.Tests.CivicBallotProviderTests.`
  Both **6/6 cases pass**, all four methods, no skips. Same settings, scheduling and canonical production DLL/PDB bytes; original independently built head production is archived.
- All **four original methods / six original cases** pass individually on the pristine base deployment, with no failed or unfinished measurements. The planner retains all four methods. Original union 630 lines equals the paired baseline; its 338/88,022 mapped-branch lower bound is separate from collector totals below.
- Official `compare_coverage.py --threshold-scope each` and the independent full raw identity/hit comparison pass. **Zero lost and zero gained points** in every assembly and total gives a conservative whole-suite loss bound of **0 percentage points**, without gain offsets. Relative change is 0% where defined; zero-hit scopes stay zero. No lost-line probe is needed.

| Scope | Covered/valid lines, before = head | Covered/valid branches, before = head |
| --- | ---: | ---: |
| Simulation | 630/61,464 | 343/74,686 |
| Server | 0/13,010 | 0/11,453 |
| Production linked into Tests | 0/3,399 | 0/2,235 |
| Total | 630/77,873 | 343/88,374 |

Collector counters supply total branches. The 352 unmapped branches are all unhit on both sides and have unchanged identities.

- Formatter, stale-doc search and whitespace checks pass. Fresh before/after unused builds each have **380 diagnostic records / 167 normalized groups**, no new or removed warnings, 4,587 existing warnings overall and zero errors. These diagnostic builds are separate from the warning-free normal builds. Temporary configs are removed; both checkouts are clean.
- Signed source/raw receipts independently reconcile all original case identities, report/unique-table/union results, discovery, settings and binary/source hashes. No measurements were repeated and no full-suite coverage was run.
- Exact-head [CI 39753553793](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39753553793) passes all required jobs. Integration `ba72204` merges this head into the exact frozen base. All eight shards total **7,396 cases: 7,387 passed and nine existing platform skips**. Windows storage/docs pass 99+18 cases; Godot smoke at both resolutions, owner lifecycle, signatures, Windows exports/packages and CodeQL pass. Actual shard 4 log 119739908826 contains exactly all six expected cases/four methods.
- Actual exact-base CI has **7,396 cases: 7,387 passed and nine platform skips**. Main push 39752724695 scope log 119737005811 confirms reuse of green exact-commit queue run 39750247508. Its [test-times log](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39750247508/job/119734535841) reports **15,829 seconds / 3,295 shared methods**, versus rolling main 15,024 seconds. All ten growth rows read, outside this class.
- Head [test-times log](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39753553793/job/119746044297): **14,049 seconds / 3,295 shared methods**, versus rolling main 16,079 seconds across its last two green runs. All ten growth rows read, outside this class. Different rolling windows and runner variation prevent a causal speedup claim; fixed-base totals are 15,829→14,049 seconds.
- Actual [native installer log](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39753553793/job/119746146412) reports `standardUserInstallUpgradeReinstallUninstallDataAndVersions=passed`. Aggregate [119748891320](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39753553793/job/119748891320) passes. Applicable raw job logs were read and preserved.
- No pending author checks. Ready for independent review; no main merge or repeat of completed measurements.
- Evidence: [separate archive branch](https://github.com/ClankerWorldOrg/ClankerWorld/tree/evidence/2566-civic-ballot-row-8451d624/.evidence/keep/2566/civic-ballot-row-8451d624), refreshed with final receipts separately from the source branch; local /workspace/2566-audit/civic-ballot-row.
- Game checked by hand: not run. Independent review/merge remains pending.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

No compatibility impact; only test invocation scaffolding changes.

## Writing, docs and changelog

- [x] No product text or documentation contract changed.
- [x] Stale-doc search passed for MalformedNonNullBallotStillFailsProviderParsing, CivicBallotProviderTests and ParseCivicBallot.
- [x] Test cleanup needs no changelog entry.
