## Summary

Finish unused setup left behind when older content rollback rows were pruned: the completed-project and running-job cases become Facts with their sole original inputs, and an unreachable two-tick setup branch disappears. Share two identical recipe-payload serializers at their original evaluation sites. Every existing case, native job, assertion, host-save check and lifetime remains.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session d83def7a.
- Refs #2561; remaining area audit and its30% target stay open.
- Independent fixed main base `63e5970fd131a341d9ae86cc26fa6c43fac327bf`, head `7fa228e5891bdbbdaa05975013c57e46157ffee3`. Stacked on: none. Prior ready slices are not included or refreshed.
- Lines added/removed: source **+0/−0**, tests **+19/−32** (**13 fewer;299→286** across two files), docs **+0/−0**. Added helper code is smaller than duplicated code removed.
- Source case/method counts remain3/3 rollback and5/3 recipe. No case-removal credit. Fresh base discovery: **7,398 cases /3,297 methods**, affected **175 cases /82 methods**; head discovery/execution remains pending.
- Original area baseline19,390lines; current main19,447across121files, this patch19,434. The difference includes independent new regressions and merged earlier cleanup; do not treat the prior360-line proposed total as actual merged reduction.
- All96open PR file lists checked (95testPRs), none touches these files. All190before/after merge-tree comparisons pass with no new conflict, using fetched heads and their actual changed-file sets.
- Hands-on check: none expected for test-support cleanup. Work left: #2561.
- Draft work remaining: finish the fixed-base affected coverage, head build/discovery/unused/formatter, matching affected coverage/loss bounds and exact-head CI/timing evidence. No new PR until this is ready.

## Checks

Completed:
- Locked restore and fresh Release baseline build, zero errors. Per-file unused diagnostic records168; all six baseline DLL/PDB hashes saved and immutable output copied. No production/configuration edits.
- Before-edit ledger, all sole original rows, complete source/helper/owner/history reads, and actual-diff second-reader check. This is the author's second reader, not independent merge review.
- `git diff --check` and190open-PR merge comparisons pass.
- Personally read base [test-times job119655821060](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39726539591/job/119655821060), including all10growth rows: **3,297 shared methods /14,934s vs main14,631s (+2%)**. No speedup claimed. Raw eight timing artifacts downloaded; digest/count parsing pending.

The single filter for both coverage runs is `FullyQualifiedName~ClankerWorld.Tests.WorldRuntimeTests.|FullyQualifiedName~ClankerWorld.Tests.RecipeOutputAdmissionTests.` It includes the entire17-file WorldRuntimeTests partial class (including World-prefixed files outside the edited area), all private-support consumers and every ledger proof. Base run is in progress; no coverage result claimed yet. Per the owner's #2656 instruction, no local full-suite coverage. Same SDK10.0.401/Linux/Release, DOTNET_PROCESSOR_COUNT=4, xUnit.MaxParallelThreads=4 and canonical all-production scope; collector also emits JSON for stable branch evidence.

Rollback history:5c2562c5 removed acquiring, and e88c5658(#864) removed completed=true; only dead parameter setup remains. Keep completed/WorkDone10, false/running-job behavior, paid workshop/job, unrelated crop activation, Pause, migration diagnostic, exact unchanged bytes, strict reload, Resume and both later completion ticks. WithdrawingUnusedPackage and WorldWithRollbackJob are byte-identical.

Recipe history:a8863fe0(#1631). Both water-only and mixed-output rows remain at admission and saved-job completion boundaries, alongside the valid wood-output control. Public test bodies, CheckHostContinuation and IdleChoices are byte-identical. Constructor and JSON property order/schema/Web options are exact; recipe.Version is the same readonly value originally passed to each recipe. Keep evaluation order recipe→definition→control→controlDefinition; no new options/cache/provider/fixture class.

Pending checks will be recorded here when complete. No game playtest claimed; applicable native Windows/Godot/package gates run in CI.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

Production is unchanged; no compatibility or recovery impact. Every native replay/save/refusal proof stays.

## Writing, docs and changelog

- [x] No product text changed.
- [x] No documentation contract changed.
- Stale-text search pending for ContentRollbackSafetyTests, RecipeOutputAdmissionTests and Definition.
- [x] No changelog needed for test-support cleanup.
