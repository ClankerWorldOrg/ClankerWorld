## Summary

Keep distinct weather/daylight proofs while removing three interior theory rows and a repeated night-count assertion. Production code and game behavior are unchanged.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session 8451d624.
- Refs #2566; broader cleanup and the 8,364-line area target remain.
- Independent main c2df5428 base, head 45f66c6b; stacked on none. No main merge or repeated coverage.
- Source **+0/−0**, tests **+0/−8**, docs **+0/−0**. Three files **377 → 369**; frozen area **27,681 → 27,673** across 163 files. Whole discovery **7,394 → 7,391 cases**, all **3,295 methods**. Affected **36 → 33 cases**, all **15 methods**.
- Rain conversion retains 100 and int.MaxValue for Cold and Polar: ordinary/exact division and maximum/rounding inputs. Combined existing-snow and storm-cooldown proofs stay; only the two intermediate billion-weight rows go.
- Storm import retains zero-offset tick50, morning-offset ticks0/179 and next-day300, the cutoff theory and full cooldown/reload Fact. Only morning-offset tick50 goes.
- All nine shown-clock assertions stay. The retained 360-tick/day-zero theory asserts `2*darker + balanced = 288`, implying the removed `darker + balanced/2 = 144` with the same function, inputs and constants.
- Read whole tests, theory data, helpers, owning weather/calendar/daylight code, runtime/server callers and history079d918a/a422a12f/708d9f20/5017fe97. Independent source review pins all committed hashes and accepts exactly the approved rows/block; every other source byte stays. Ledger keeps all15 methods. No support/config changes or external consumers.
- All103 open PR heads checked, including three beyond GitHub's100-result cap; no changed-file overlap or new conflict in206 before/after merge-tree comparisons. GitHub reports mergeable.

## Checks

- Locked restores and fresh nonincremental Release test builds pass, zero warnings/errors each. Local SDK10.0.401 matches global.json; CI uses latest patch10.0.402. Reused production outputs from ca3 after independently checking production/config trees identical through c2df; provenance is recorded. Normal tests and original head production DLL/PDBs archived before diagnostics.
- Discovery removes exactly the three approved rows and preserves remaining order. Ledger, formatter, stale-doc search and git diff --check pass. Required unused builds have384 records/168 normalized groups before=after, no new or removed diagnostics; temporary configs are gone.
- One identical filter covers all three whole sealed classes and every ledger proof:
  `FullyQualifiedName~ClankerWorld.Tests.WeatherWeightBoundaryTests.|FullyQualifiedName~ClankerWorld.Tests.MorningWeatherEpisodeTests.|FullyQualifiedName~ClankerWorld.Tests.DaylightRulesTests.`
- Matched runs pass36/36 before and33/33 head, no skips, same four canonical production DLL/PDB bytes/settings and DPC4/xUnit2/nice10. `compare_coverage.py --threshold-scope each` passes. Exact JSON/XML identities and integer collector counters reconcile with zero lost or gained points.

| Scope | Covered lines / valid, before = after | Covered branches / valid, before = after |
| --- | --- | --- |
| Simulation | 423 / 61,464 | 188 / 74,686 |
| Server | 0 / 13,010 | 0 / 11,453 |
| Test-linked production | 0 / 3,397 | 0 / 2,235 |
| Total | 423 / 77,871 | 188 / 88,374 |

Conservative whole-suite loss is **0pp** per assembly and total, with0% relative loss where baseline hits are nonzero (undefined for zero-hit scopes). All352 unmapped branches are unhit. No lost-line probe or full-suite coverage repeat is needed.

- All15 original isolated method reports pass36 cases; their423-line union and unique.tsv reconcile, and the complete planner keeps all15 for distinct assertions. Its mapped rounded lower bound184 branches is not substituted for actual188 collector hits.
- Independent raw acceptance verifies scoped/isolated reports, binary provenance/settings, exact discovery and committed source, unused/format/comparison receipts; no findings. Two parent-driver corrections happened before any head measurement (discovery must remove three rows; archive folder must match scope); their failed premeasurement logs are preserved. Exactly one actual base/head pair ran.
- Actual frozen-base [test-times119675732579](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39732743690/job/119675732579):3,295 shared methods, **16,292s** vs rolling main14,227s(+15%). Actual head [test-times119687351450](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39736258704/job/119687351450):3,295 shared methods, **14,495s** vs rolling main15,154s(-4%). Both logs and all ten growth rows read. Head growth rows are outside the three changed classes; different rolling windows, native-fixture variation and main's hosting fix prevent a causal speedup claim.
- [Verify39736258704](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39736258704) passes exact head45f66 on integration7892a99 into newer mainf82570db. All eight Linux shards: **7,387 passed / 9 platform skips / 7,396 cases**; all33 retained weather cases pass. The five-case difference from frozen head7,391 is main's #2734: hosting Fact→two-row theory(+1), hosting retry route(+1), timeout/cancellation hosting rows(+3). It changes LocalServer and separate hosting tests; these weather classes are unchanged. Frozen local base staysc2df.
- Windows storage/provider99/99, documentation18/18, Godot smoke/exports, signatures and platform packages pass. INSTALLER_RESULT
- All required checks complete. Evidence is outside this source diff, archived separately and linked on the area issue. Game checked by hand: not run.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

Weather initialization, cutoff, overflow, codec/replay and seasonal/clock contracts retain their distinct assertions.

## Writing, docs and changelog

- [x] No product text or documentation contract changed.
- Stale-doc search passes: WeatherWeightBoundaryTests, MorningWeatherEpisodeTests, DaylightRulesTests, SixMinuteDayFollowsTheShownClock.
- [x] No changelog needed for test cleanup.
