---
title: Road-frontage query measurements
type: experiment-report
status: active
updated: 2026-10-11
---

# Road-frontage query measurements

Issue status, October 11: the owner closed
[#998](https://github.com/ClankerWorldOrg/ClankerWorld/issues/998) after
[#1777](https://github.com/ClankerWorldOrg/ClankerWorld/pull/1777). These measurements
remain historical evidence. A slowdown observed during alpha playtesting should be
reported as a new bug.

Road-frontage scoring uses direct loops over the existing rectangular Footprint
and direction table, removing nested iterator and predicate allocations. Cardinal
neighbors, scores, reasons and the separate custom placement footprint retain
their existing rules. No cache or saved-state change is introduced.

## Native workload

The frozen baseline is c134b83f, schema 124. The final implementation is
fecd1bf1. Both use the unchanged published
[construction input query probe](construction-input-query-measurements.md).
Fresh synthetic checkpoints start at tick 256 with a completed native Council
Market. Actual resident/Road/Town-building/House counts are 4/99/24/2,
16/100/36/14 and 16/194/55/14 on a 256-by-128 east-west wrapped map.
Each provider/fixture row restores the same checkpoint, warms a separate world
for four ticks, then records 64 native ticks through 320. The local personal
provider delegates to the deterministic chooser; it is not a remote model call.

Pinned SDK 10.0.401 and .NET 10.0.12 run on the same Linux host with four effective
processors. Setup, profiling and other local validation are excluded from the
ordinary samples. Source/configuration hashes and loaded assembly hashes are
retained with the samples. Rebuilding after a commit can change generated build
metadata; those binaries have separate recorded hashes.

## Ordinary comparison

Times below are median / p95 native tick milliseconds. Allocation changes compare
the complete measured-phase process allocation totals. All six medians and
allocation totals fell, but the smallest built-in p95 increased. This supports a
bounded allocation reduction; it does not establish a uniform latency improvement
or a Windows/playtest result.

| Fixture label | Provider | Baseline ms | Direct loops ms | Allocation change |
| --- | --- | --- | --- | --- |
| 4-96 | Built-in | 118.9 / 260.3 | 106.8 / 294.4 | -0.068% |
| 4-96 | Personal | 102.5 / 126.2 | 89.6 / 109.0 | -0.277% |
| 16-96 | Built-in | 495.0 / 723.8 | 441.6 / 666.5 | -0.111% |
| 16-96 | Personal | 492.6 / 889.0 | 465.5 / 615.1 | -0.112% |
| 16-192 | Built-in | 550.4 / 830.4 | 508.6 / 697.1 | -0.014% |
| 16-192 | Personal | 536.9 / 721.7 | 503.8 / 681.6 | -0.014% |

All 24 complete request, admission, frame and final-state capture files match
byte-for-byte, without normalization. Every run has zero fallbacks and exact
final strict reload. Both dense paths admit 122 decisions.

A fresh pre-edit measured-phase profile attributes 14.36% of inclusive sampled
native-thread time to site evaluation. Inclusive samples overlap and do not
isolate the CPU cost of FacesRoad. The rejected adjacent-wrapping experiment was
reverted; its samples and traces remain separate from these results.

## Fresh-process repeats and state changes

The baseline repeat process ran on unchanged c134b83f before final frontage;
the fresh candidate process ran on 335f4fb6 with all 1,607 tracked source and
configuration files unchanged from fecd1bf1. All four allocation totals fell,
but small built-in p95 increased and dense personal median rose slightly.

| Fixture label | Provider | Baseline ms | Direct loops ms | Allocation change |
| --- | --- | --- | --- | --- |
| 4-96 | Built-in | 116.7 / 355.3 | 108.1 / 402.5 | -0.988% |
| 4-96 | Personal | 95.6 / 121.0 | 91.5 / 112.6 | -0.285% |
| 16-192 | Built-in | 497.7 / 717.6 | 480.4 / 643.7 | -0.252% |
| 16-192 | Personal | 517.2 / 694.4 | 517.6 / 691.3 | -0.252% |

All 16 complete repeated capture files match exactly, with zero fallbacks and
exact final strict reload. This and the ordinary comparison cover 1,280 measured
native ticks across both source versions, with lower allocations in all ten pairs.

The unchanged mutation probe covers control, resident movement, shared and
reserved stock, depleted resources, household land, a removed Road and native
workshop removal through both provider paths. All 16 scenario/provider pairs
match complete request/candidate and final checkpoint bytes: 32 full files.
Each source version also passes 16 refused-tick state-preservation checks and
16 paired save/reload continuations, with the affected resident actually queried.
These controlled mutations do not imply that every scenario changes a candidate.

The standard native comparison also matches all 68 generated/legacy frames
across two seeds: full checkpoints, events and observation digests, with no
normalization. The normal Release solution build has zero warnings/errors and
54 focused checks pass, including ten new frontage rows and every original case.

## Reproduce and retained evidence

The [Draft and immutable sample publication](https://github.com/ClankerWorldOrg/ClankerWorld/pull/1758)
contains every ordinary, repeated and rejected measurement sample, profile
samples, provenance and complete capture comparison manifests. Raw samples, full captures, traces,
probe sources and hash manifests are retained outside Git under
`.evidence/keep/998-9b395a8e/`. Broader construction/layout/route costs were tracked in
[#998](https://github.com/ClankerWorldOrg/ClankerWorld/issues/998).

Run the published probe on two clean checkouts, using one checkpoint directory:

```bash
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh setup BASE CHECKPOINTS OUTPUT/setup
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh replay BASE CHECKPOINTS OUTPUT/baseline
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh replay CANDIDATE CHECKPOINTS OUTPUT/frontage
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh repeat BASE CHECKPOINTS OUTPUT/baseline-repeat
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh repeat CANDIDATE CHECKPOINTS OUTPUT/frontage-repeat
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh changes BASE CHECKPOINTS OUTPUT/baseline-changes
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh changes CANDIDATE CHECKPOINTS OUTPUT/frontage-changes
```

Use separate directories or specific prefixes when comparing captures: the
published comparator selects every matching capture prefix, including repeats.
Existing Windows/F12 hands-on checks remain pending.
