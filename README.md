# Supplemental review evidence for PR 2698

Codex session fcc7fd87; author Codex session 5f03c0da.
Base 9e169525c170e57cc98077c6c7862446f9aff920; head 3320c300a918363845e144437e0eb2d24ed698bf.
Evidence only: this branch must not be merged into main.

Filter `FullyQualifiedName~ClankerWorld.Tests.TownGovernmentHandoverTests.` includes all four partial files, all unchanged private-support users and every retained ledger proof. The exact-base normal Release build previously completed in /workspace/review-2691-base is reused: its source/configuration remain unchanged at9e169525. New head tests were restored and compiled separately with identical production build outputs and BuildProjectReferences=false. Both production DLL/PDB sets match production-hashes.json, including after collection. Production and test configuration are unchanged.

Pinned SDK10.0.401, Linux, Release, command-local DOTNET_PROCESSOR_COUNT=4 and ClankerWorld__Clock__StrictTickFailures=true, xUnit.MaxParallelThreads=4. Coverage settings differ from the committed audit settings only by adding JSON as an extra report format. Each revision has one completed affected measurement; 22/22 cases across20 methods before,21/21 across19 methods after, no skips or failures. check_ledger.py passes: the only removed method is the duplicate and its richer retained proof runs. Local instrumented case sums17.4 ->15.0s are not a speed comparison; resource overlap differed.

Source review maps all four deleted assertions to the retained proof. Both start from the same explicit all-adult Town with five adults, willing a/b/c Council candidates and no mayor. The richer retained scenario asserts the forced election's exact creating handover identity, ready-but-unseated state at10, cancellation/authority at30, and continuation/replay equality at31. Its complete original body including that exact byte equality is unchanged. A separate loop adds no-election/all-adult checks at60 to both original and restored runs. After cancellation, unchanged adults/candidates/ballots and explicit all-adult authority disable new automatic elections. All other bodies and support remain unchanged. source-equivalence.json records the exact removal/addition and assertion mapping.

Command each side:
```
ClankerWorld__Clock__StrictTickFailures=true DOTNET_PROCESSOR_COUNT=4 dotnet test tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~ClankerWorld.Tests.TownGovernmentHandoverTests.' --settings /workspace/scratch/2698-fcc7fd87/coverage-identities.runsettings --collect 'XPlat Code Coverage' --results-directory <before-or-final> --logger 'trx;LogFileName=tests.trx' -- xUnit.MaxParallelThreads=4
```

Actual identity scope and hitsets are identical, with zero gross line/branch losses in every assembly, including unmapped branches. All JSON counts reconcile to authoritative Cobertura root counters. Totals6,027/77,870 lines and3,684/88,372 branches unchanged. Simulation5,907/61,464 and3,647/74,686; Server120/13,010 and37/11,453; linked client0/3,396 and0/2,233. Net and gross changes are0pp,0% relative for nonzero baselines (undefined at zero). Since all outside-filter support/fixtures/production/configuration are unchanged, full-suite gross-loss bound is0pp in every scope; no full-suite coverage repeat is needed.

Reports are gzip-compressed without content changes. Decompress beside compare-identities.py into before/ and final/ to reproduce. It checks stable actual line/branch identities including method, line, offset, end offset, path and ordinal, identical scope and all collector root counts; new hits cannot offset losses. comparison.json also uses current main's compare_coverage.py --threshold-scope each --repo /workspace/review-2698. All loss lists are empty.
