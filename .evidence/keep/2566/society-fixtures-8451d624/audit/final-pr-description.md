## Summary

Remove unused Society test scaffolding while preserving the same native inputs and every assertion. A sole invalid clock-rate row becomes a Fact with literal zero; a private custody helper omits a ground-position argument that always equals its existing null default.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session 8451d624.
- Refs #2566. The remaining World/Map candidates and final area reconciliation stay open; original target 8,364 test lines.
- Independent main base `17ccaf0cc7d2c65add79cd9601602858e5755aad`, head `e19ffa5e99f520761d920725e6f824634f750dfd`; stacked on none. No main merge.
- Lines added/removed: source **+0/−0**, tests **+4/−6 (2 fewer)**, docs **+0/−0**. Two files shrink **572→570 lines**.
- **29 cases, 21 methods and 141 assertion calls remain.** No case-removal credit; only the approved parameterized display name becomes the same-named Fact.
- Aging keeps rate zero, zero anchors, contract version 2, the Validate exception assertion and all distinct lifecycle, API refusal, native age/restart and save proofs.
- Locate's two callers supply only house/custodian rows. Omitting its always-null ground expression preserves Relocate's null default, carrier/storage expressions, lot/quantity/owner, argument order, container family, estate allocation and reload assertions.
- Whole files, owners, callers and history read; ledger covers every method. Second reader accepts exact source and raw evidence. History records the prior removal of rate 2/−1 and both ground rows.
- All 116 open PR file lists and 232 before/after merge-tree comparisons pass: no changed-file overlap, moved head or new conflict.
- No hands-on check needed for this test-only cleanup. Independent top-level review remains.

## Checks

All author checks are complete on this fixed pair:

- Fresh locked Release builds pass with **zero warnings/errors**, base 4:22.16/head 4:32.38. Local SDK 10.0.401; CI SDK 10.0.402 under the latest-patch policy.
- Full discovery **7,395→7,395 cases / 3,293→3,293 methods**; only the approved Fact display mapping changes. Ledger verification passes.
- One identical affected filter on base/head, including every private helper consumer and retained proof:
  `FullyQualifiedName~ClankerWorld.Tests.SocietyAgingOverrideTests.|FullyQualifiedName~ClankerWorld.Tests.MultiHeirWillCustodyTests.`
  **29/29 cases pass on each revision**, all 21 methods, no skips. Same settings, scheduling and canonical production DLL/PDB bytes; original head production and normal test archives are preserved.
- All **21 original methods / 29 original cases** pass individually, with no failed or unfinished measurements. Planner retains all 21. Their 17,336-line union equals the paired baseline; its 11,661/88,022 mapped-branch lower bound is separate from collector totals below.
- Official `compare_coverage.py --threshold-scope each`, exact raw identity/hit comparison and signed reader reconciliation pass. **Zero lost and zero gained points** in every assembly and total establish a conservative whole-suite loss bound of **0 percentage points**, without gain offsets. Relative change is 0% where defined; zero-hit scopes stay zero. No lost-line probe needed.

| Scope | Covered/valid lines, before = head | Covered/valid branches, before = head |
| --- | ---: | ---: |
| Simulation | 16,439/61,464 | 11,239/74,686 |
| Server | 897/13,010 | 533/11,453 |
| Production linked into Tests | 0/3,399 | 0/2,235 |
| Total | 17,336/77,873 | 11,772/88,374 |

Collector totals include 352 unmapped branches, 74 hit on both sides; their identities/hits are unchanged.

- Formatter, stale-doc search and whitespace checks pass. Fresh before/after unused-code builds each report **380 diagnostic records / 167 normalized groups**, no new or removed warnings, 4,585 existing warnings overall and zero errors. These diagnostic builds are separate from the warning-free normal builds. Temporary configs are removed; both checkouts are clean.
- Exact-head [CI 39757170160](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39757170160) passes all required jobs, including CodeQL, Godot smoke at both resolutions, owner lifecycle, signatures, exports/packages, Windows storage/docs (99+18 cases), [native installer](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39757170160/job/119757371420) and [aggregate gate](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39757170160/job/119759996746). Actual installer log reports `standardUserInstallUpgradeReinstallUninstallDataAndVersions=passed`.
- Integration `8b025ca` merges this head into the exact frozen base. Actual base/head CI each reports **7,395 cases: 7,386 passed, nine existing platform skips**. All 29 affected case identities/21 methods match discovery in the actual logs on both revisions. Applicable raw job logs read and retained.
- Actual exact-base [test-times log](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39752759403/job/119742827166): **15,862 seconds / 3,293 shared methods**, versus rolling main 15,125 seconds. Main push scope 119746179423 confirms exact-commit queue reuse. Head [test-times log](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39757170160/job/119757798279): **15,981 seconds / 3,293 shared methods**, versus rolling main 15,044 seconds. All ten growth rows in each log read, outside these classes. Different rolling windows and runner variation prevent a causal performance claim.
- No full-suite coverage: these two sealed classes contain all changed private-support consumers; production, configuration and outside-filter tests are unchanged. No completed measurements were repeated.
- Evidence: [separate archive branch](https://github.com/ClankerWorldOrg/ClankerWorld/tree/evidence/2566-society-fixtures-8451d624/.evidence/keep/2566/society-fixtures-8451d624), refreshed with final receipts separately from source; local /workspace/2566-audit/society-fixtures.
- Game checked by hand: not run. Independent review/merge remains pending.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

Strict validation, custody, inheritance and reload proofs remain; no compatibility impact.

## Writing, docs and changelog

- [x] No product text or documentation contract changed.
- [x] Stale-doc search passed for UnsupportedClockRatesFailClosed, SocietyAgingOverrideTests, MultiHeirWillCustodyTests and Locate.
- [x] Test cleanup needs no changelog entry.
