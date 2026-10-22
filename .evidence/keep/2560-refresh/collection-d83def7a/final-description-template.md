## Summary

Share collection access and native travel/replay scenarios across material, food and equipment inputs. Move finished-progress corruption assertions into existing resumed-completion scenarios, saving 105 net test lines while preserving the distinct inputs and assertions.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex sessions 185bf8a4 and cc8e0ec0; completed by Codex session d83def7a.
- Refs #2560. Other World* audit work remains.
- Existing behavior: the owner's behavior-preserving cleanup request in chat.
- Stacked on: none. Frozen main base `7fbe6800`; unchanged head `0bab3d6c`. GitHub reports no conflict, so main was not merged again, as the owner requested in chat (#2652).
- Lines added / removed: source 0 / 0; tests 66 / 171 (net -105); docs 0 / 0. Four files shrink from 1,493 to 1,388 lines. No helper added.
- Built discovery: 7,413 → 7,409 cases; 3,307 → 3,303 methods. Affected partial classes: 238 → 234 cases, 57 → 53 methods. The four edited files contain 197 → 193 cases, 41 → 37 methods. Consolidation earns no independent-case removal credit.
- Other open PRs: completed before/after merge-tree comparison against 50 open PR heads introduced no new conflicts; fresh actual-diff second reader approved the assertion/input transfer. Those completed checks are reused for this unchanged head.
- Work left: remaining audit and closeout in #2560.
- Hands-on check after merge: none expected for test-only cleanup.

### Retained proof

All 24 access inputs remain: eight ownership/access/kind boundaries × material, food and equipment. Exact lot/request/reservation identities, reservation clocks, unchanged-lot assertions, blocked reasons, event absence and strict reload checks survive.

Both native travel inputs remain: mixed berries/fruit with food_items and stone pickaxes with equipment_items, including the excluded iron pickaxe. Pause, refused-tick rollback, queue order, travel without progress, exact partial quantity, byte-for-byte paired replay, source location, carried total and event counts remain.

The finished-progress refusal now corrupts progress to four after existing quantity-three scenarios finish. Collection keeps wood, fruit and basket; storage keeps wood and adds basket. The owning collection/storage definitions enforce the same finite CompletedUnits≤RequestedUnits rule independently of the old quantity two and new quantity three. The numeric boundary remains one beyond the requested amount.

The ledger covers all 41 original methods and names retained proof for every consolidation. Historical origins: material #837, tile source #855, food #860, equipment #862, storage #830 and unified goods #879.

## Checks

- Locked restore, Release build, unused-code comparison (168 → 168 distinct warnings, no new warning), built discovery, ledger, formatter, stale-text search and `git diff --check`: pass.
- Owner-requested affected-test coverage (#2656): one identical filter on base and head, covering both entire affected partial classes and all ledger proofs: `FullyQualifiedName~ClankerWorld.Tests.WorldCollectionOrderTests.|FullyQualifiedName~ClankerWorld.Tests.WorldStorageOrderTests.` No shared test support changes. SDK 10.0.401, Release/Linux, DOTNET_PROCESSOR_COUNT=4 and xUnit.MaxParallelThreads=4; unchanged coverage settings. Four production DLL/PDB hashes are identical; source and test binaries were frozen throughout each run.
- @COVERAGE@
- @ISOLATED@
- Exact-head CI run [39656154895](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39656154895): all gates pass, including all eight test shards, native Windows credential/documentation/installer gates, Godot smoke/export/package checks, CodeQL and final verify119425906054.
- Eight CI timing artifacts parsed: baseline 7,413 cases / 3,307 methods (7,404 passed, 9 native skips), head CI 7,419 / 3,307 (7,410 passed, 9 skips). The CI merge includes ten unrelated new cases/four methods; the frozen local comparison above isolates this draft's four consolidations.
- Test-times logs personally read, including all ten growth rows: frozen baseline 15,811s for 3,307 shared methods versus main median 15,133s (+4%); head job119424029511 reports 14,893s for 3,306 shared methods versus 14,795s (+1%). No affected method appears among the head growth rows. Total CI case time is 15,810.993s → 14,898.031s; affected head classes take 189.718s. Runner variation prevents a speedup claim.
- Checked in game by hand: not run; no game behavior changes.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

No production, schema or compatibility changes. Native rollback, save corruption refusal and paired replay assertions remain.

## Writing, docs and changelog

- [x] No game text changed.
- [x] No documentation contracts change.
- Stale-text search: EquipmentCollectionRespectsOwnershipAccessAndExactKind, FoodCollectionKeepsItsSourceAndExactQuantityAcrossTravelQueueAndReplay, CollectionOrderRejectsFinishedProgressBeyondTheExactRequestedQuantity, StorageOrderRejectsFinishedProgressBeyondTheExactRequestedQuantity; no affected statements or CI filters.
- [x] Test-only consolidation needs no changelog entry.
