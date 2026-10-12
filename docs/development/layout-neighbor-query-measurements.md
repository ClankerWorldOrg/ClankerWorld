---
title: Layout neighbor query measurements
type: experiment-report
status: active
updated: 2026-10-11
---

# Layout neighbor query measurements

Issue status, October 11: the owner closed
[#998](https://github.com/ClankerWorldOrg/ClankerWorld/issues/998) after
[#1777](https://github.com/ClankerWorldOrg/ClankerWorld/pull/1777). These measurements
remain historical evidence. A slowdown observed during alpha playtesting should be
reported as a new bug.

Layout foot-cost searches use a value-type neighbor enumerator instead of
allocating the public iterator for each visited tile. Both paths share the
same offset order, narrow-wrap duplicate suppression and live `CanFootStep`
validation. No neighbor result, terrain or bridge legality is cached between
steps or searches. Occupied corners, Road costs, queue order, stopping rules
and action-time checks retain their existing authority.

The fresh matched dataset is in git history at commit `afbc35e8` as
`docs/development/layout-neighbor-query-probe/matched-native.json`.
It records lower allocations in all ten ordinary/repeat pairs, by about
1.1–2.2%. Timing changes are mixed, so retain this bounded allocation reduction
without claiming a consistent latency improvement. Wider construction and
layout costs were tracked in [#998](https://github.com/ClankerWorldOrg/ClankerWorld/issues/998).

## Fresh matched inputs and method

The baseline is main `effee371fb4e0563204835a5a673819dd4d0e3a6`; the candidate
is merged draft `9bd7357d199ca78b7fce775d17c7de7ab9770dcb`. Both production
versions and all seven external replay, repeat, mutation and profile probes
use normal Release builds, with analyzers enabled and zero warnings/errors.
The external probes use unchanged published sources and normal assembly
references; every copied Simulation DLL/PDB agrees with its pinned version.
The dataset retains production, probe, project and copied-assembly hashes.
Of 415 Simulation, Viewer, linked Shared, asset, project and toolchain inputs,
only `SeededWorldHarness.cs` and `PrivateWorldRuntime.Production.cs` differ.

Fresh checkpoints are authored on baseline main at tick 256. Actual
resident/Road/Town-building/House counts are 4/99/24/2, 16/100/36/14 and
16/194/55/14. The Market is Council-approved, supplied and built through
native actions. Extra households and starting stock are controlled; Houses,
workshops and Roads use native placement. This is authored growth, not a
naturally evolved private save. The dense target is 192 Roads; the separate
384-Road fixture failure remains tracked by #1599. No setup guard is bypassed.

The shared machine is Debian 13, Intel Xeon Platinum 8370C, SDK 10.0.401 and
.NET 10.0.12, with CPU quota `400000 100000` and four effective processors.
All own local builds, tests and samplers finish before ordinary measurements.
Runs are sequential, without a sampler; machine-wide idle conditions are
not established. Initial sweeps run baseline then candidate. Fresh-process
small/dense repeats reverse that order. Each fixture warms a separate restored
world for four ticks, then measures 64 blocking native `AdvanceOneTickAsync()`
calls on a fresh restore, ticks 257–320. Setup, warmup and capture serialization
are excluded. The host's separate nonblocking baseline reproduction is not
part of these matched results.

Each fixture runs with the built-in chooser and local personal-provider
admission using replies from that same chooser. No hosted model is called.
Small fixtures admit ten decisions; middle and dense fixtures admit 124.
Every ordinary/repeat run has zero fallback. Every raw tick and wall sample,
ordered request/admission capture, per-tick checkpoint/event digest and
canonical final checkpoint is retained. Medians use sorted index 31 and p95
index 60 of 64 samples. Allocation totals sum process-wide
`GC.GetTotalAllocatedBytes(true)` deltas around those tick calls; they do not
attribute allocations to individual methods.

## Ordinary results

Each timing cell shows baseline median/p95 → candidate median/p95 in
milliseconds. Allocation cells show totals across 64 ticks in MiB.

| Fixture / provider | Median / p95 ms, baseline → candidate | Allocated MiB, baseline → candidate |
| --- | ---: | ---: |
| 4-96 / built-in | 114.5 / 288.6 → 111.6 / 466.2 | 2565.3 → 2515.2 |
| 4-96 / personal | 92.7 / 108.6 → 103.8 / 148.6 | 2569.1 → 2530.8 |
| 16-96 / built-in | 458.6 / 601.1 → 515.6 / 686.0 | 12465.0 → 12257.0 |
| 16-96 / personal | 483.3 / 583.7 → 474.2 / 732.7 | 12464.5 → 12256.6 |
| 16-192 / built-in | 645.2 / 862.6 → 565.6 / 706.4 | 15610.2 → 15405.8 |
| 16-192 / personal | 517.4 / 688.1 → 591.6 / 777.6 | 15610.2 → 15405.8 |

## Fresh-process repeats

The same small/dense checkpoints run in new processes, candidate first.
Complete captures also match the baseline in these repeats.

| Fixture / provider | Median / p95 ms, baseline → candidate | Allocated MiB, baseline → candidate |
| --- | ---: | ---: |
| 4-96 / built-in | 104.4 / 302.8 → 103.8 / 269.9 | 2573.1 → 2517.2 |
| 4-96 / personal | 92.6 / 110.3 → 91.8 / 119.6 | 2555.3 → 2519.2 |
| 16-192 / built-in | 513.3 / 758.2 → 506.6 / 675.0 | 15528.7 → 15350.7 |
| 16-192 / personal | 514.8 / 693.8 → 502.8 / 817.1 | 15528.3 → 15350.1 |

Allocations fall in every pair. Three initial medians and five initial p95
values increase; repeat medians change little and personal p95 values still
increase. Baseline dense built-in median varies from 645.2 to 513.3 ms between
processes. These shared-host results do not establish a uniform speedup or
meet #998's rough 50/200 ms guidance. Every run is retained, including regressions.

## State changes, rollback and reload

All 24 ordinary and 16 repeat full-file comparisons agree byte for byte:
ordered requests/candidates, admissions, frame digests and final checkpoints.
The existing [mutation probe](construction-input-query-probe/changes.cs.txt)
uses the same middle checkpoint on both versions. Eight cases cover control,
resident movement, added household stock, reserved stock, depleted resources,
household land, removed Roads and native workshop removal. Each runs with both
providers, and the affected resident is actually queried in every case.

All sixteen baseline/candidate mutation pairs agree in complete ordered
request/candidate captures and canonical final checkpoints: another 32
full-file comparisons, 72 in total. Each version checks sixteen refused ticks
against preceding checkpoint bytes and sixteen strict paired save/reload
continuations. Every advanced tick has zero fallback. Some mutations retain
the control's offered choices; this check does not require every state change
to alter a candidate. Mutation timings are excluded from performance results.

The 150 focused terrain, route, bridge, layout, land and construction checks
and five documentation checks pass with no skips. New literal checks cover
first-offset order on one- and two-column wrapped maps and elevation changing
after enumeration starts, including blocked diagonal shoulders. Locked
restore, normal Release build, whole-solution formatting and whitespace checks
pass. No public test hook or timing-threshold test is added.

## Profiles and historical evidence

A fresh baseline-only profile uses the same main `effee371` and dense
16-192 checkpoint for 64 blocking native ticks. The collector accepted its
EventPipe session before the ready/go gate opened; no other own build, test
or sampler ran. Setup and warmup are excluded. Its 124 decisions have no
fallback. Sampled thread time under the native tick stack totals 53,197.0 ms.
Inclusive shares are 95.57% candidate creation, 49.70% Town project proposals,
18.70% layout foot-cost search, 14.15% site evaluation, 10.18% `CanFootStep`
and 9.93% neighbor enumeration. Nested spans overlap and must not be summed
or read as exact CPU costs. Profiled timings are excluded from ordinary
comparisons. The collector's legacy `main-96` trace filename does not describe
this dense fixture. This continuation profile precedes the retention decision,
not the earlier optimization edit.

The historical profile/conformance dataset is in git history at commit
`afbc35e8` as `docs/development/layout-neighbor-query-probe/conformance.json`.
Its pre-edit profile ran on main `a7bd1642`, schema 122,
with 16/100/36/14 counts and 64 nonblocking native ticks. A separate
one-processor regression suite was running then. It sampled 53,896.7 ms under
the native tick stack: 96.41% candidate creation, 52.33% Town proposals,
23.48% foot-cost search, 15.74% neighbor enumeration, 15.95% `CanFootStep`
and 13.14% site evaluation, all inclusive. Its old correctness runs used
`b1de8605` checkpoints and two effective processors while that suite continued;
they establish neither fresh timing nor allocation improvement. Their 413
input hashes and old DLLs are kept separate from the fresh 415-input record.

## Reproduce and retained files

Use the existing [probe runner](construction-input-query-probe/run.sh) and
one shared checkpoint directory. Generate inputs once on the baseline, then
run `replay`, `repeat` and `changes` on both checkouts. Compare complete captures
with [the comparison script](construction-input-query-probe/compare-captures.py).
Run sequentially without another local build, test or sampler, as described
in [Measuring Town tick time](town-tick-measurements.md). Reverse version order
for the fresh-process repeat and preserve all samples, including regressions.

Full checkpoints, captures, trace, Speedscope conversion, input manifests and
logs are retained under `.evidence/keep/998-current/effee-matched/` in Codex
session e0f464b8's workspace. Earlier records remain under the parent evidence
directory. Existing [Windows/F12 checks](../../playtest/alpha-1.md)
remain pending. These Linux records establish no Windows frame-performance
or hosted-model speedup; broader construction/layout work was still open when these
measurements were made.
