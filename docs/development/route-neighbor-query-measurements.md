---
title: Walking-route neighbor measurements
type: experiment-report
status: active
updated: 2026-10-11
---

# Walking-route neighbor measurements

Default shared walking-route searches use the existing value-type foot-neighbor
enumerator instead of allocating a public iterator for each expanded tile.
Custom neighbor callbacks, including swimming, retain their callback path.
Both paths share the same neighbor relaxation, with a JIT inlining directive.
Live terrain and bridge checks, narrow-wrap order, occupied corners, checked
costs, queue ties, stopping rules and action-time authority are unchanged.
No new cache or saved state is introduced.

The [complete dataset](route-neighbor-query-probe/samples.json) records lower
allocations in all ten ordinary/repeat pairs for the final candidate, by
0.698–1.257%. Timing changes are mixed, including worse p95 values;
this supports a bounded allocation reduction, not a uniform latency improvement.
Broader construction costs remain in [#998](https://github.com/ClankerWorldOrg/ClankerWorld/issues/998).

## Inputs and method

Baseline main is `afbc35e8cbeaf76c6a0aa79478838e54594593c2`. Fresh checkpoints are authored at
tick 256 using the unchanged published setup probe. Actual resident/Road/
Town-building/House counts are 4/99/24/2, 16/100/36/14 and 16/194/55/14.
Council approval, supplied construction and native Market completion remain
required. Extra households and starting stock are controlled; buildings and
Roads use native placement. This is authored growth, not a private natural save.
The separate 384-Road setup work in #1599/#1707 is not copied or used.

Both experiments use normal Release builds with analyzers enabled and zero
warnings/errors. Measurements were collected before publication; the dataset
pins the exact source and compiled DLL/PDB bytes rather than claiming a later
commit's rebuilt binaries were measured. Only `UnoccupiedRouteSearch.cs`
differs among the 343 recorded Simulation source, project and toolchain inputs.
Those inputs are not a complete manifest of the Viewer or external dependencies.
All external replay/repeat/mutation/profile programs are unchanged copies of
[the published probe sources](construction-input-query-probe/run.sh).

The shared host is Debian 13, Intel Xeon Platinum 8573C, SDK 10.0.401 and
.NET 10.0.12. CPU quota is `400000 100000`, with four effective processors.
Own builds, tests, formatting and samplers finish before timing; runs are
sequential and machine-wide idle conditions are not established.

Each fixture warms a separate restored world for four ticks, then measures
64 blocking native `AdvanceOneTickAsync()` calls on a fresh restore,
ticks 257–320. Setup, warmup and capture encoding/I/O are outside the timed
intervals. This differs from the playable host's nonblocking measurement
boundary. Captures can still influence later GC; both versions use the same
probe. Built-in and local personal-provider admissions use the real built-in
chooser's replies, with no HTTP or remote model. Small cases admit ten
choices; middle/dense cases admit 124. Every ordinary/repeat run has zero
fallback. Allocation totals sum process-wide GC deltas around native calls;
they do not attribute bytes to individual methods. Median uses sorted index
31 and p95 index 60 of 64 samples.

## Final candidate results

Initial ordinary comparisons reuse the fresh baseline sweep collected before
the first source edit. Fresh-process small/dense repeats run the final
candidate first and then baseline. Every raw tick and wall sample is retained.

| Fixture / provider | Median / p95 ms, baseline → candidate | Allocated MiB, baseline → candidate |
| --- | ---: | ---: |
| 4-96 / built-in | 107.8 / 280.2 → 105.5 / 300.3 | 2507.4 → 2489.7 |
| 4-96 / personal | 83.7 / 111.7 → 89.4 / 236.1 | 2506.6 → 2484.9 |
| 16-96 / built-in | 408.2 / 543.7 → 414.5 / 536.2 | 12020.3 → 11915.8 |
| 16-96 / personal | 419.2 / 565.0 → 411.2 / 547.7 | 12019.9 → 11915.4 |
| 16-192 / built-in | 476.4 / 622.0 → 477.5 / 791.6 | 15118.2 → 15012.6 |
| 16-192 / personal | 472.7 / 625.9 → 459.0 / 609.8 | 15118.2 → 15012.6 |

### Fresh-process repeats

| Fixture / provider | Median / p95 ms, baseline → candidate | Allocated MiB, baseline → candidate |
| --- | ---: | ---: |
| 4-96 / built-in | 148.7 / 361.8 → 102.8 / 290.3 | 2504.4 → 2483.7 |
| 4-96 / personal | 119.1 / 169.8 → 85.3 / 115.7 | 2507.5 → 2476.0 |
| 16-192 / built-in | 501.7 / 897.7 → 483.1 / 1169.2 | 15166.9 → 15002.5 |
| 16-192 / personal | 480.9 / 656.1 → 452.7 / 650.7 | 15166.5 → 15002.2 |

Ordinary medians and p95 values are mixed. Repeat medians fall in all four
pairs, while dense built-in p95 rises from 897.7 to 1169.2 ms. Small baseline
medians vary substantially between sweeps. These results do not establish a
consistent speedup, meet #998's rough 50/200 ms guidance, or measure Windows
frame presentation or hosted-model latency.

## First experiment retained

The first experiment extracted the same relaxation helper without the JIT
inlining directive. Allocations fell in all ten pairs, but dense repeat
medians/p95s rose. The final candidate adds that directive. Shared-host runs
do not isolate its effect from scheduling and GC variation; retain both
experiments rather than attributing every timing difference to inlining.
The same initial baseline sweep is used below and above, not two independent
baseline sweeps.

| Fixture / provider | Median / p95 ms, baseline → candidate | Allocated MiB, baseline → candidate |
| --- | ---: | ---: |
| 4-96 / built-in | 107.8 / 280.2 → 110.6 / 272.3 | 2507.4 → 2490.3 |
| 4-96 / personal | 83.7 / 111.7 → 86.6 / 115.0 | 2506.6 → 2487.6 |
| 16-96 / built-in | 408.2 / 543.7 → 426.4 / 566.7 | 12020.3 → 11943.4 |
| 16-96 / personal | 419.2 / 565.0 → 453.1 / 576.9 | 12019.9 → 11943.0 |
| 16-192 / built-in | 476.4 / 622.0 → 513.0 / 2063.9 | 15118.2 → 15056.2 |
| 16-192 / personal | 472.7 / 625.9 → 464.4 / 624.0 | 15118.2 → 15056.1 |

| Fixture / provider | Median / p95 ms, baseline → candidate | Allocated MiB, baseline → candidate |
| --- | ---: | ---: |
| 4-96 / built-in | 125.7 / 387.5 → 105.8 / 269.0 | 2510.1 → 2476.9 |
| 4-96 / personal | 85.0 / 123.6 → 85.1 / 118.9 | 2501.4 → 2478.9 |
| 16-192 / built-in | 509.6 / 645.4 → 527.2 / 741.8 | 15134.3 → 15001.3 |
| 16-192 / personal | 478.9 / 619.8 → 536.7 / 813.9 | 15133.8 → 15000.9 |

The initial dense built-in p95 of 2063.9 ms is retained. No outlier or slower
run is discarded. All 34 ordinary/repeat runs, 2176 timed ticks, are in the
dataset; profiled and mutation timings are separate.

## Native equivalence and state changes

All 24 ordinary and 16 repeat full-file comparisons match for the final
candidate: ordered observations/candidates, actual admissions, per-tick
checkpoint/event digests and canonical final checkpoints. The first
experiment's same 40 comparisons also match and remain in the dataset.
All final checkpoints pass strict codec round trips and runtime validation.

The unchanged [mutation probe](construction-input-query-probe/changes.cs.txt)
runs control, resident movement, shared stock, reserved stock, resource
depletion, household land, Road removal and native workshop removal, each
with both provider paths. The affected resident is queried in every case.
All sixteen baseline/candidate pairs match full requests and final saves,
another 32 full-file comparisons. Each version checks sixteen refused ticks
against preceding checkpoint bytes and sixteen paired strict save/reload
continuations. Every advanced tick has zero fallback. Some changes may keep
the control's offered choices; no assertion requires every mutation to change
a candidate. These cases are not performance samples.

Quick route checks compare every destination on one- and two-column wrapped
maps with the original fresh-search algorithm. A separate custom callback
crosses a Lake ordinary walking cannot cross. Existing route, terrain,
occupied-corner and swimming checks retain their assertions. Final local and
hosted check results are recorded on the pull request.

## Pre-edit profile and reproduction

The fresh baseline-only dense profile completed before choosing an optimization.
Its EventPipe session was accepted before the ready/go measurement gate opened.
It covers 64 native ticks, 124 admitted choices and zero fallbacks, with the
same complete captures as the corresponding ordinary run. Inclusive sampled
thread time under the native tick stack totals 48,522.1 ms: candidate creation
95.62%, Town proposals 50.39%, layout foot-cost search 17.88%, site evaluation
15.04%, shared routes 10.09%, route search 9.03% and route expansion 8.35%.
Nested spans overlap; do not sum them or treat them as exact CPU costs.
`dotnet-trace` is pinned to 10.0.750501. Profiled timings are excluded from
ordinary comparisons.

Use the existing [probe runner](construction-input-query-probe/run.sh) with
one checkpoint directory generated on baseline main. Run `replay`, `repeat`
and `changes` on both versions sequentially; reverse version order for the
fresh-process repeat. Compare complete captures with
[the comparison script](construction-input-query-probe/compare-captures.py).
Retain all samples and source/assembly hashes, including slower runs.

Full checkpoints, ordered captures, raw trace, Speedscope conversion, input
manifests and logs are retained under `.evidence/keep/998-construction-profile/`
in Codex session d9925a9c's workspace. The public dataset includes their
comparison hashes and trace hashes, not private saves or model payloads.
Existing [Windows/F12 checks](../../playtest/998-route-step-costs.md) remain
pending. Independent review and full green CI are required before readiness.
