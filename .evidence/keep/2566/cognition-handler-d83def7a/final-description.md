## Summary

Model-need wording and memory-summary tests each copied the same recording HTTP handler. Both now alias the existing `CognitionTests.RecordingHandler`, deleting the copies and their unused imports. Every test body, reply string, assertion, independent client/handler lifetime and throwing provider guard remains unchanged.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session d83def7a
- Refs #2566 — remaining area audit stays open.
- Existing behavior: real Jev, Decisions and personal-model wire contracts, need wording and private summary attribution/refusal remain independently checked.
- Other open PRs: checked all92 open test PRs; none changes these files; before/after `git merge-tree` reports no new conflicts.
- Stacked on: none. Independent frozen main base `8b57533137b8920b7692259dee0b006afe1cf7dd`; this does not include #2719.
- Lines added/removed: source **+0/−0**, tests/support **+3/−24** (21 fewer;828→807), docs **+0/−0**. Test bodies **+0/−0**.
- Counts: **44 cases /22 methods before and after**, all pass with no skips. Full built discovery **7,422 cases /3,310 methods unchanged**. No cases count as removed.
- Work left: the rest of #2566.
- Hands-on check: none for test-support reuse.
- Draft work remaining: none; ready for independent review.

## Checks

Completed on `0b98961bc0f5362975e377b0f89978bce2763b15`:
- Locked restore. Reused the immutable, fresh8b baseline build and its full unused-code/discovery records; all six DLL/PDB hashes verified. New affected coverage was collected for this PR.
- Release head build; unused-code probe **169→169**, zero new diagnostics. Production DLL/PDB bytes match.
- One identical filter on base and head: `FullyQualifiedName~ClankerWorld.Tests.CognitionTests.|FullyQualifiedName~ClankerWorld.Tests.ModelNeedWordsTests.|FullyQualifiedName~ClankerWorld.Tests.MemorySummaryProviderTests.` It includes all three complete changed classes, every handler consumer and all22 ledger proofs.
- **44→44 cases /22→22 methods**, all pass, no skips; executed method names and case counts match exactly. Discovery, ledger and all22 executed proofs checked.
- `compare_coverage.py --threshold-scope each` passes: identical scope, no lost lines or reduced mapped branch counts.
- Stable JSON branch identities reconcile all collector counters, including unmapped branches. All88,374 identities match; zero covered branches disappear. Conservative full-suite loss upper bound **0 pp in every assembly and total**, without offsetting losses against new hits.
- Read-only second reader checked actual diff, valid synthetic header inputs, extra unused Method/Authorization captures, initialization and disposal. Test bodies/replies/retained helper code are byte-identical after excluding imports and removed duplicate handlers.
- Formatter, overlap/merge checks, stale-text search and `git diff --check` pass.

| Production scope | Covered lines before = after | Covered branches before = after |
| --- | --- | --- |
| Simulation | 1,151 /61,464 | 872 /74,686 |
| Server | 0 /13,010 | 0 /11,453 |
| Linked production in tests | 0 /3,397 | 0 /2,235 |
| Total | 1,151 /77,871 | 872 /88,374 |

Coverage changes: **0 pp /0% relative**, zero lost locations; no isolated lost-line run needed. No test methods are removed, so the complete affected-class comparison verifies every retained proof directly.

CI on this exact head is green: [run39725655221, attempt2](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39725655221/attempts/2), including verify, installer and CodeQL. Only failed shard5 was retried once after an unchanged launcher test hit its existing5s HTTP deadline; it passes on retry and alone locally. Successful jobs, frozen base and coverage were not rerun. No main merge.

Personally read [test-times job119656963341](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39725655221/job/119656963341), including all10 growth rows: **3,297 shared methods took16,209s vs main14,722s (+10%)**; none of the growth rows is an affected class. All8 final timing ZIP digests verified (reuse seven successful artifacts, replacement shard5): **7,399 cases /3,298 methods,7,390 pass /9 platform skips /0 failures**, summed16,208.613211s. These three classes: **44 cases /22 methods,0.411761s** versus frozen-base **44/22,0.128271s**. No speedup claimed.

Frozen-base [run39712166176](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39712166176):7,422 cases/3,310 methods,7,413 pass/9 platform skips; its personally read test-times report12,470s vs main14,520s(−14%). CI's23-case/12-method difference comes from intervening main changes in GitHub's test merge (base26098edb), not this PR: frozen local base/head discovery is unchanged and all affected44cases/22methods match.

Per the owner's scoped-coverage request (#2656), no local full-suite coverage. No production or test configuration change; shared support affects only these three classes. SDK10.0.401, Linux, `DOTNET_PROCESSOR_COUNT=4`, scoped runs `xUnit.MaxParallelThreads=4`; canonical measurement settings unchanged, with JSON output added for stable branch evidence. Native Windows/Godot/export checks are delegated to applicable CI. No in-game playtest claimed.

Audit evidence (raw base/head coverage and binary pins, ledger/executed proofs, formatter and unused-code records, reader notes and first-attempt CI artifacts): [immutable evidence archive](https://github.com/ClankerWorldOrg/ClankerWorld/tree/a3a22d66/.evidence/keep/2566/cognition-handler-d83def7a). Evidence branch is never merged into game source.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

Production is unchanged; no compatibility impact.

## Writing, docs and changelog

- [x] No product text changed.
- [x] No affected docs.
- Stale-text search: `node scripts/find-stale-docs.js RecordingHandler MemorySummaryProviderTests ModelNeedWordsTests` found no stale sentence.
- [x] No changelog needed for test-support cleanup.
