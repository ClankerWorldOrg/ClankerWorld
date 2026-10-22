## Summary

Share equivalent delivery, parser and fire-order scenarios, remove a redundant harvest restore and drop four equivalent retained-completion gate combinations. This saves 68 net test lines while preserving distinct input layouts, rollback, exact quantity, native timing and save-integrity assertions.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex sessions 185bf8a4 and cc8e0ec0; completed by Codex session d83def7a.
- Closes #2560. The complete area audit stops at the evidence-supported limit explained below.
- Existing behavior: the owner's behavior-preserving cleanup request in chat.
- Stacked on: none. Frozen main base `7fbe6800`; unchanged head `5330a4cd`. GitHub reports no conflict, so main was not merged again, as the owner requested in chat (#2652).
- Lines added / removed in this PR: source 0 / 0; tests 52 / 120 (net -68); docs 0 / 0. Six files shrink from 1,631 to 1,563 lines; the emptied delivery-derived-ID file is deleted. No new helper.
- Built discovery: 7,413 → 7,408 cases; 3,307 → 3,304 methods. Affected partial classes: 286 → 281 cases, 130 → 127 methods. Edited files: 84 → 79 cases, 33 → 30 methods. Four equivalent gate rows are removed; the other lost execution consolidates fire assertions and earns no independent-removal credit. All delivery and bandage inputs remain.
- Other open PRs: completed before/after merge-tree comparisons against 50 open heads introduced no new conflicts. Fresh readers approved the actual six-file diff and retained proof, including the sequential gate argument; completed checks are reused for this unchanged head.
- Earlier independent area batches #2595, #2604, #2608, #2609, #2616, #2624, #2628 and #2631 have completed validation and are ready. They are not included in this diff. Review and merge remain with other sessions.
- Work left in this area: review and merge of the independent PRs.
- Hands-on check after merge: none expected for test-only cleanup.

### Why the area stops below 30%

Two readers fully read all 82 World*.cs files, totaling 21,671 baseline test lines. The 30% target is 6,502 lines. These nine validated independent batches add 359 and remove 942 test lines, a net reduction of 583 (2.69%), leaving 21,088 if all merge. Source and documentation changes total zero.

The total includes the exact live crop provider/factory restored in #2624 after scoped coverage exposed a lost first-tick construction path; the earlier 619-line proposal is superseded. Further deletion would discard distinct input kinds, custody/reservation rules, cancellation/consent/timing boundaries, historical identities or current-save integrity assertions. Zero unique mapped coverage alone does not justify dropping these behavioral proofs. The audit stops here because the evidence cannot support the requested 30% without weakening tests.

### Retained proof

Delivery keeps all four original layouts: short contained Farmhouse/Silo and bounded loose/contained Farmhouse. Exact source/pot identities, request keys, shipment hash/provenance, source immutability, no early credit, conservation, strict replay and once-only events remain.

Bandages retain all three request texts, receipt key, two-item output yield and exact recipe, quantity, progress-unit and explicitness assertions. The retained matrix additionally checks waiting state, zero progress, no repetition and unrelated null targets.

Harvest uses the already-restored runtime its factory returns. The removed second restore has no intervening tick, pending model work or observed chooser counter; the unsupported request and every native refusal assertion remain.

Fire keeps rejected-tick byte/fuel/fire assertions, an immediate strict reload of the waiting state, paired ignition, native paid-fuel proof, every corruption input and a separate final strict reload.

Retained completion keeps synchronous/synchronous and delayed/delayed rows. Preparation is explicitly awaited before guidance/native work; actual wrapper completion is awaited before admission. There is no intervening native tick, owner action, cancellation or provider change that distinguishes the off-diagonal phase combinations after those barriers. Every assertion remains, along with both separate timing Facts and other runtime-safety timing rows. History0a4a3158 (#1947) and the owning completion polling were read before choosing these rows.

The recovered ledger covers all 33 original methods across six files. Detailed histories, input/assertion transfer and fresh reader evidence are preserved outside the repository diff.

## Checks

- Locked restore, Release build, unused-code comparison (168 → 167 distinct warnings; no new warnings), discovery, 33-entry ledger, formatter, stale-text search and `git diff --check`: pass.
- Owner-requested affected-test coverage (#2656): one identical filter on base and head covers every affected partial class and ledger proof: `FullyQualifiedName~ClankerWorld.Tests.WorldDeliveryOrderTests.|FullyQualifiedName~ClankerWorld.Tests.WorldRuntimeTests.|FullyQualifiedName~ClankerWorld.Tests.WorldProductionOrderParserTests.|FullyQualifiedName~ClankerWorld.Tests.WorldFireOrderTests.` No shared support changes. SDK 10.0.401, Release/Linux, DOTNET_PROCESSOR_COUNT=4 and xUnit.MaxParallelThreads=4; unchanged coverage settings. Identical four production DLL/PDB hashes and frozen sources throughout collection.
- Scoped comparison passes with `--threshold-scope each --max-drop-pp 0`: Simulation 27,759/61,634 lines (45.0385%), Server 1,434/13,010 (11.0223%), linked client 0/3,396; all identical. Total 29,193/78,040 lines (37.4077%) and collector 25,080/88,598 branches (28.3076%), unchanged. Per-assembly branch rates remain 32.06% / 9.23% / 0%. Zero lost lines or reduced mapped branches; every absolute delta is 0pp and every defined relative delta is 0%.
- Individual coverage: nine original methods / 25 cases and six retained methods / 20 cases all pass without skips. Production-line unions are identical at 22,476 Simulation lines, with no lost line or reduced mapped-branch lower bound; Server/linked client unions remain zero. All six production/test DLL/PDB hashes are restored after collection.
- Exact-head CI [39656825540](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39656825540): all gates pass, including eight test shards, native Windows credential/documentation/installer gates, Godot smoke/export/package checks, CodeQL and final verify119428522389.
- Eight CI timing artifacts parsed: baseline 7,413 cases / 3,307 methods (7,404 passed, 9 native skips), head CI 7,418 / 3,308 (7,409 passed, 9 skips). The CI merge includes ten unrelated new cases/four methods; frozen local discovery isolates this draft's five fewer executions and three fewer declarations.
- Test-times logs personally read, including all ten growth rows: frozen baseline 15,811s for 3,307 shared methods versus main median 15,133s (+4%); head job119426791799 reports 14,725s for 3,308 shared methods versus 14,790s (0%). No affected method appears among the head growth rows. Total CI case time is 15,810.993s → 14,725.212s; affected head classes take 390.878s. No speedup claimed because runner variation is substantial.
- Checked in game by hand: not run; no game behavior changes.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

No production, schema or compatibility changes. Native rollback, replay, payment and corruption refusal assertions remain.

## Writing, docs and changelog

- [x] No game text changed.
- [x] No documentation contracts change.
- Stale-text search: OrderedPartialDeliveryTracksTheActualBoundedLotAcrossStrictReload, ProductionKeepsItemCountsDistinctFromBatchesOfTwo, RefusedPreparedIgnitionCannotConsumeFuelCreateFireOrCreditTheOrder; no affected statements or CI filters.
- [x] Test-only cleanup needs no changelog entry.
