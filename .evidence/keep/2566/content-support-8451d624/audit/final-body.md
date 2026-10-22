## Summary

Remove unused options from Content test helpers and the unselected cache arms left by earlier pruning. All 16 cases, 13 methods, 63 assertion call sites and production inputs remain; lifecycle operations keep their order.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session 8451d624.
- Refs #2566; broader cleanup and the 8,364-line area target remain.
- Independent main ca3a5b8d base, head ab4b213f; stacked on none. No main merge or coverage refresh.
- Source **+0/−0**, tests **+23/−49 (26 fewer)**, docs **+0/−0**. Three files 426 → 400; frozen 163-file area 27,704 → 27,678. Full discovery stays **7,394 cases / 3,295 methods** in exact order. No case-removal credit.
- Building helpers retain wood1/kitchen; recipe helpers retain every workstation-ID string. All Governance package versions were 1.0.0. Inlining its sole definition forwarder preserves field values; direct Propose calls retain core → world order. Asset durable/GPU rows retain the same charges, diagnostics and atomicity assertions; the three-budget breach proof stays.
- Read whole tests/helpers, owning ContentDefinitions/ContentContracts/WorldAssetReservations, runtime/server callers and history 541fcbd7/5c2562c5, including earlier merged ordering/roundtrip proofs. All 13 methods and every row stay in the ledger; no external helper users.
- All 99 open heads checked by metadata/filenames and 198 before/after merge-tree calls: no changed-file overlap or new conflict. Read-only comparison with newer main was clean, and GitHub reports mergeable.
- Independent author-side source reader accepts exact committed hashes and all 63 assertion lexical slices/order. A different top-level session still reviews and merges.

## Checks

- Locked restores and fresh normal Release builds pass, zero warnings/errors each. Local SDK 10.0.401 matches global.json; CI uses latest patch 10.0.402. Production/config trees are unchanged. Normal tests and original head production DLL/PDBs were archived before diagnostics; both coverage runs use the same four canonical base production DLL/PDB bytes.
- Full discovery, ledger, formatter, stale-doc search and git diff --check pass. Nonincremental unused builds produce identical 384 records / 168 normalized groups, no new or removed diagnostics; temporary configs are gone.
- One identical filter covers all three whole sealed classes and every support consumer:
  `FullyQualifiedName~ClankerWorld.Tests.ContentDefinitionTests.|FullyQualifiedName~ClankerWorld.Tests.ContentGovernanceTests.|FullyQualifiedName~ClankerWorld.Tests.WorldAssetReservationTests.`
- Both matched runs pass **16/16 cases / 13 methods**, no skips, with identical settings and DPC 4 / xUnit 2 / nice 10. `compare_coverage.py --threshold-scope each` passes. Exact JSON/XML point identities and collector integer counters reconcile with zero lost or gained points.

| Scope | Covered lines / valid, before = after | Covered branches / valid, before = after |
| --- | --- | --- |
| Simulation | 1,131 / 61,464 | 412 / 74,686 |
| Server | 0 / 13,010 | 0 / 11,453 |
| Test-linked production | 0 / 3,397 | 0 / 2,235 |
| Total | 1,131 / 77,871 | 412 / 88,374 |

The conservative whole-suite loss bound is **0pp** per assembly and total, with 0% relative loss wherever baseline hits are nonzero (undefined for zero-hit scopes). All 352 unmapped branches are unhit. No lost-line probe or full-suite coverage repeat is needed.

- All 13 original methods pass separate isolated reports (16 cases, no skips); the complete unique-coverage planner keeps all 13. The activation-order method has zero unique lines but asserts dependency order, activation tick and exact event order, so it stays.
- Independent raw acceptance reconciles scoped/isolated XML/JSON/TRX, exact identities, canonical hashes/settings, normal archives, discovery, ledger, unused comparison and committed source. No findings. Evidence stays outside this source diff; its archive is linked from the area issue.
- Actual frozen-base [test-times 119668667935](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39730642461/job/119668667935): 3,295 shared methods, **15,654s** vs rolling main 15,078s (+4%). Actual head [test-times 119678267367](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39733463943/job/119678267367): 3,295 shared methods, **14,156s** vs rolling main 15,015s (-6%). Both logs and all ten growth rows read; head growth rows are outside these classes. Different rolling windows and native-fixture variation; no causal speedup claim.
- Exact-head [Verify 39733463943](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39733463943) passes on integration 56e88c3 (ab4b into frozen ca3). All eight Linux shards: **7,385 passed / 9 platform skips / 7,394 cases**. Windows storage/provider 99/99, documentation 18/18, Godot checks/exports, signatures and platform packages pass. [Installer 119677329059](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39733463943/job/119677329059) passes actual standard-user install/upgrade/reinstall/uninstall data/version checks (`standardUserInstallUpgradeReinstallUninstallDataAndVersions=passed`); aggregate 119679012383 is green.
- All required checks are complete. Game checked by hand: not run; private test support cleanup needs no manual product check.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

Only test support changes; lifecycle, canonical restore, atomicity, ordering and diagnostic assertions remain.

## Writing, docs and changelog

- [x] No product text or documentation contract changed.
- Stale-doc search passes: ContentDefinitionTests, ContentGovernanceTests, WorldAssetReservationTests.
- [x] No changelog needed for test-only cleanup.
