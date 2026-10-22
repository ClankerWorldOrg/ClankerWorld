## Summary

Share the three identical Road proposal assertion blocks through one smaller private helper. Keep deterministic tie ordering, fresh borrowed terrain and Road-set queries, and every original expected route/refusal check.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session 8451d624.
- Refs #2566; the 8,364-line area target and remaining handoff candidates stay open.
- Stacked on: none. Frozen main ancestor e61a1d8d, head c7b8fb47.
- Lines added/removed: source +0/-0; **tests +16/-33**; docs +0/-0. File 95→78 lines; 163-file area 27,718→27,701 at this base/head.
- Keep all nine cases/three methods and all 98 source-derived xUnit assertion executions (15 successful proposals, four blocked queries). No test/case removal credit.
- Existing ready area slices remain with reviewers. All 97 open PR file lists have no candidate-file overlap; all 194 base/head merge-tree checks found no moved head or new conflict.
- No product behavior, save format, layout, test routing or configuration changes. No hands-on check needed.
- Local execution/static checks pass. Pending: independent raw-evidence acceptance and final CI.

## Retained proof and checks

- Equal routes: all reuse/reversal combinations and reversed/duplicate entrance data remain.
- Narrow wrapped maps: widths one/two and both Road policies keep open→block elevation→refuse→clear→open on the same request and borrowed array.
- Borrowed Roads: direct→discounted→direct expectations keep UnionWith/Add/Add/Clear on the same set/request.
- Independent source reader accepts the actual diff without findings. The helper retains Plan plus the same six assertions, with identical expected routes and mutation order. Pure expected arrays now materialize before Plan; they have no request/map/Road aliases or side effects. Failure locations move to the helper; row identity and expected/actual diagnostics remain.
- Locked restores and fresh normal Release builds pass on both revisions with zero warnings/errors (SDK 10.0.401). Full discovery stays 7,398 cases/3,297 methods, with identical names/order. Ledger, formatter, whitespace and stale-doc checks pass; unused comparison is identical at 384 records/168 normalized groups, with no new warnings. Temporary config removed.
- Common affected filter on both revisions: `FullyQualifiedName~ClankerWorld.Tests.RoadCostPolicyTests.` — the whole nonpartial changed class, nine cases/three methods, including every ledger proof. The new helper is private to that class; no other support consumer is changed.
- The one matched scoped comparison passes 9/9 cases and all three methods on both revisions, zero skips. Identical canonical production DLL/PDB bytes, settings and scheduling (DPC 4/xUnit 2/nice 10) are recorded; normal test binaries were archived before diagnostic builds. `compare_coverage.py --threshold-scope each` passes.

| Scope | Covered lines / valid, before = after | Covered branches / valid, before = after |
| --- | --- | --- |
| Simulation | 263 /61,464 | 239 /74,686 |
| Server | 0 /13,010 | 0 /11,453 |
| Test-linked production | 0 /3,397 | 0 /2,235 |
| Total | 263 /77,871 | 239 /88,374 |

Exact JSON/XML point identities and integer counters, including 352 unmapped branches, reconcile: zero lost or gained lines or branches. Conservative whole-suite loss bound is 0pp per assembly and total, with 0% relative loss where baseline hits are nonzero. No loss probe or full-suite coverage repeat is needed.

- All three original isolated methods pass, selecting 4/4/1 rows. Their unique-line counts are 0/9/0; all stay because their asserted ordering and live-input contracts differ. The official planner is complete and keeps all three. Its mapped-branch denominator is not substituted for the collector totals above.
- Actual base [test-times 119658135757](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39727220070/job/119658135757), exact e61a1d8d: 3,297 shared methods, 14,308s versus rolling main 14,722s (-3%). All ten growth rows read. Head [test-times 119669191638](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39730732399/job/119669191638): 3,296 shared methods, 15,689s versus rolling main 15,078s (+4%). Both actual logs and all ten growth rows were read. The shared-method intersections and rolling reference windows differ; no causal speedup/slowdown claim. All head growth rows are outside RoadCostPolicyTests; this change adds no native work and keeps every planner/validator call and input.
- Current-head CI [Verify 39730732399](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39730732399), integration cb7d441 (head into ed544834): all eight Linux shards pass, 7,385 passed/9 platform skips out of 7,394 cases; Windows storage/provider 99/99 and documentation 18/18, Godot checks/exports, offline signatures and platform packages pass. [Native Windows installer 119669123790](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39730732399/job/119669123790) passes actual standard-user install/upgrade/reinstall/uninstall data/version checks (`standardUserInstallUpgradeReinstallUninstallDataAndVersions=passed`); final aggregate 119670835505 is green. Local base/head discovery stays 7,398/3,297; CI integrates main's #2609 consolidation (35 old rows moved into existing matrices with 31 additions), accounting for four fewer cases and one fewer method. #2610 changes separate Market test support. None changes this class; no case reduction is credited here. No main refresh.
- Independent raw-evidence acceptance passes for exact c7b8fb47: all local raw reports, binary provenance, exact point identities, discovery and unused-code checks reconcile. All required CI is green on this head. Game checked by hand: not run.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

Only duplicate test assertion plumbing changes; all native planner/validation calls remain.

## Writing, docs and changelog

- [x] No product text changed.
- [x] No documentation contract changed.
- Stale-doc search passes: RoadCostPolicyTests and its three retained test names.
- [x] No changelog needed for test-only cleanup.