---
title: Footprint evaluation measurements
type: experiment-report
status: active
updated: 2026-10-11
---

# Footprint evaluation measurements

Construction site evaluation enumerates its existing placement footprint lazily,
so a blocked tile can reject a site without translating and allocating the
remaining tiles. Custom offsets, land, entrances, neighbor distance, border,
routes, reasons and ranking retain their existing checks. No cache or saved-state
change is introduced. Every tile still must pass before a site can be offered.

## Native workload and profile

The frozen baseline is integrated main 725991ba, schema 124. The unchanged
[construction input query probe](construction-input-query-measurements.md)
authors fresh checkpoints at tick 256 on a 256-by-128 east-west wrapped Small map.
Actual resident/Road/Town-building/House counts are 4/99/24/2, 16/100/36/14 and
16/194/55/14. The Council Market is constructed through native actions; added
households and stock are controlled setup. These are authored fixtures, not
naturally evolved private saves.

Each row restores the same checkpoint, warms a separate world for four ticks,
and records 64 native ticks through 320. The local personal provider delegates
to the deterministic chooser; it does not call a remote model. Pinned SDK
10.0.401 and .NET 10.0.12 run on the same Linux host with four effective
processors. Timed runs execute sequentially without other local builds/tests.
Setup and profiling are excluded. Source/configuration and loaded assembly
hashes are retained with every original sample.

A fresh pre-edit measured-phase profile covers the dense built-in path.
CreateCandidates spans 95.82% and site evaluation 16.22% of inclusive sampled
native-thread time. These overlap and are not exact CPU attribution. Translated
footprint SelectIterator.ToArray leaf samples account for 735.3 ms of 26,662.9 ms
under native tick stacks; this motivated removing temporary footprint arrays.

## Ordinary comparison

Times are median / p95 native tick milliseconds. Allocation changes compare
complete measured-phase process allocation totals, not live heap size.
All six totals decrease, but median and p95 changes are mixed. This supports a
bounded allocation reduction and does not establish uniform latency improvement
or Windows gameplay performance.

| Fixture label | Provider | Baseline ms | Lazy footprint ms | Allocation change |
| --- | --- | --- | --- | --- |
| 4-96 | Built-in | 90.8 / 225.9 | 82.9 / 267.7 | -4.06% |
| 4-96 | Personal | 63.1 / 82.6 | 61.6 / 82.5 | -4.32% |
| 16-96 | Built-in | 315.6 / 412.7 | 315.9 / 417.5 | -4.77% |
| 16-96 | Personal | 305.9 / 449.7 | 299.4 / 419.6 | -4.77% |
| 16-192 | Built-in | 357.7 / 479.0 | 356.8 / 484.1 | -5.69% |
| 16-192 | Personal | 344.0 / 484.3 | 349.5 / 476.0 | -5.69% |

All 24 complete request, admission, frame and final-state capture files match
byte for byte, without normalization. All rows have zero fallbacks and exact
final strict reload. Each dense row admits 122 decisions; each small row admits
10.

## Reproduce and retained evidence

Use the unchanged published probe on two checkouts and one checkpoint directory:

```bash
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh setup BASE CHECKPOINTS OUTPUT/setup
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh replay BASE CHECKPOINTS OUTPUT/baseline/ordinary
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh replay CANDIDATE CHECKPOINTS OUTPUT/candidate/ordinary
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh repeat BASE CHECKPOINTS OUTPUT/baseline/repeat
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh repeat CANDIDATE CHECKPOINTS OUTPUT/candidate/repeat
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh changes BASE CHECKPOINTS OUTPUT/baseline/changes
DOTNET_PROCESSOR_COUNT=4 bash docs/development/construction-input-query-probe/run.sh changes CANDIDATE CHECKPOINTS OUTPUT/candidate/changes
python docs/development/construction-input-query-probe/compare-captures.py OUTPUT/baseline OUTPUT/candidate ordinary ordinary
```

Original samples, complete captures, traces, probe sources and source/configuration
hashes are retained outside Git under `.evidence/keep/998-ff19ed5a/`.
Broader construction/layout/route/input cost remains in
[#998](https://github.com/ClankerWorldOrg/ClankerWorld/issues/998).
Windows/F12 hands-on checks remain pending.
