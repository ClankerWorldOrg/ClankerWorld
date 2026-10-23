# Supplemental review evidence for PR 2691

Codex session fcc7fd87; author Codex session 185bf8a4.
Base 9e169525c170e57cc98077c6c7862446f9aff920; head ee5f518d989671be9be2afc838c70021be6898f6.
Evidence only: this branch must not be merged into main.

Filter `FullyQualifiedName~ClankerWorld.Tests.ServerHttpTests.` includes the entire 50-file partial class, all deleted private-support users and every retained proof. The existing ActionCompatibilitySigner and its other-class consumers are unchanged. Production and test configuration are unchanged. Both selections contain 211 cases and 111 methods; the sole display-name difference is the single geography.size theory row becoming a Fact. Four changed files retain 26 methods, 42 cases and 540 assertion calls. source-equivalence.json records byte-identical complete files after only the expected substitutions/removals.

Pinned SDK 10.0.401, Linux, Release, locked restore/build. Command-local DOTNET_PROCESSOR_COUNT=4, ClankerWorld__Clock__StrictTickFailures=true and `-- xUnit.MaxParallelThreads=2`. The same production DLL/PDB bytes were copied into the baseline; hashes in production-hashes.json. The first baseline build hit an MSBuild worker exit without a compiler diagnostic; a single-worker retry with node reuse disabled passed, zero warnings/errors. Separate output folders isolate builds and collectors. Focused coverage runs overlap with sufficient machine capacity; no full-suite or shared-output collectors overlap.

Each revision has one completed measurement, using:
```
ClankerWorld__Clock__StrictTickFailures=true DOTNET_PROCESSOR_COUNT=4 dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~ClankerWorld.Tests.ServerHttpTests.' --settings /workspace/scratch/2691-fcc7fd87/coverage-identities.runsettings --collect 'XPlat Code Coverage' --results-directory <before-or-final> --logger 'trx;LogFileName=tests.trx' -- xUnit.MaxParallelThreads=2
```

JSON is only an additional report format; all other settings match committed audit scope. Reports are gzip-compressed without content changes. Decompress them into before/ and final/ beside compare-identities.py to reproduce the comparison. It compares actual stable line and branch identities, including method, line, offset, end offset, path and ordinal; requires identical identity scope; reconciles all JSON counts with authoritative Cobertura root counters, including unmapped branches; counts losses without offsetting gains. comparison.json also uses current main's compare_coverage.py --threshold-scope each with --repo pointing to the reviewed checkout.

Results: 211/211 cases and111 methods passed on each revision, zero failures/skips. Actual identity hitsets lose no line or branch in any assembly, including unmapped branches. The only gained line is the shared receipt LoadedId getter at OwnerWorldContracts.cs:821. Total31,112 ->31,113 /77,870 lines;21,274 /88,372 branches unchanged. Thus conservative full-suite gross-loss bound is0pp in each scope. Local case-duration sums315.2 ->434.2s include differing overlap with a compiler/other focused measurement; no local speed claim. Exact-head CI test-times is-2% against main, with no affected-class growth row.
