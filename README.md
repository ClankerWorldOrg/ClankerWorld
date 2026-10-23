# Supplemental review evidence for PR 2650

Codex session fcc7fd87; author Codex session 40b6ddab.
Base c06212be760b86c515941f2dec20629456c7a3e0; head 6eeaa38b82633ab67cf9b2326993cb8594554a3b.
Evidence only: this branch must not be merged into main.

The sole changed class and every consumer of its private directory helper are included by `FullyQualifiedName~ClankerWorld.Tests.LauncherTests`. No other support, production code or test configuration changed. Both selections contain the same 64 cases and 20 methods. All assertions and inputs survive; source-equivalence.json records an exact whole-file match after the fourteen expected wrapper substitutions and insertion of the disposal members.

Pinned SDK 10.0.401, Linux, Release, locked restore/build, command-local DOTNET_PROCESSOR_COUNT=2 and `-- xUnit.MaxParallelThreads=2`. Both production DLL/PDB hashes match production-hashes.json, including inside each test output folder. BuildProjectReferences=false was used for the baseline tests after copying the identical production build outputs. JSON is only an additional report format; coverage scope/settings otherwise match the committed audit runsettings.

Each revision ran exactly one completed measurement:
```
DOTNET_PROCESSOR_COUNT=2 dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~ClankerWorld.Tests.LauncherTests' --settings /workspace/scratch/2650-fcc7fd87/coverage-identities.runsettings --collect 'XPlat Code Coverage' --results-directory <before-or-final> --logger 'trx;LogFileName=tests.trx' -- xUnit.MaxParallelThreads=2
```

Reports are gzip-compressed without changing their contents. compare-identities.py compares actual line and branch identities, including offsets/paths/ordinals, with stable production bytes. It requires identical identity scope, checks all JSON counts against authoritative Cobertura root counters, and counts losses without credit for new hits. `comparison.json` is also produced by current main's compare_coverage.py with --threshold-scope each, --repo pointing to the reviewed checkout. A filtered net rate or unchanged rounded assembly rate alone was not used as a loss bound.
