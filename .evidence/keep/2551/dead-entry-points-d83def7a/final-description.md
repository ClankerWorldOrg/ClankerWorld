## Summary

Remove five uncalled internal Civic entry points left behind by retained action plans: NonLandText, SubmitExpansionLandRequest, AcceptTownAdmission, NonLandTerms and LandHearingOutcome. Delete the cognition import freed by NonLandText. The live admission, land-filing, ruling and voluntary-remedy bind/check/apply paths stay byte-identical.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session d83def7a.
- Refs #2551; further Civic cleanup and its20% target remain.
- Independent main base `c2df5428aa774f6f688aa3df62fc3f78de8d366d`, head `31ad20fad67488e947fa594166b2dff9a1bb9e23`; stacked on none. All earlier owned drafts are ready; no main merge.
- Source **+0/−42**, tests **+0/−0**, docs **+0/−0**. Five files1,317→1,275 lines; Civic9,301→9,259. Earlier114merged lines plus42here would total156/9,415 (1.66%); the1,883-line target remains unmet.
- No caller edit outside Civic, supported API, scenario, assertion, schema, configuration or action registration changes. TownSystem is internal.
- All102inventoried open PR file lists show no changed Civic file overlap. Prior #2571/#2574/#2579 are already merged; other source cleanup stays independent.
- No hands-on gameplay check needed for uncalled-code removal.
- Draft remaining: none: all listed local checks and all27exact-head CI checks are complete. No repeat checks or main refresh. Head build, discovery, compiled reference verification and native comparison are complete and will not be repeated.

## Checks

Completed:
- Fresh locked Release baseline build with area-only unused-import diagnostics, zero errors; temporary config removed.
- Every removed method has exactly one repository-wide name occurrence, its declaration; no test, script, documentation, CI or reflection-name caller. Retained active paths read: Council bind/check/apply, admission plan and settlement, household filing/selected plot checks, ruling validation and non-land agreement binding/terms refusal.
- Compiled baseline metadata/IL scan covers **109,112method bodies** across Simulation, Server and Tests, including generated/nested methods. All five declarations exist exactly once; **zero incoming method/token references** and zero exact reflection-name literals. Full IL decoding succeeds. Raw scanner/report retained outside source diff.
- Actual diff review: only those five complete methods and the newly unused import removed. No live check, diagnostic or save rule changes.
- Whitespace and stale-text search for all five names pass; no sentence made false.

Additional completed checks:
- Fresh locked head Release build passes with zero warnings/errors. Complete discovery remains **7,394cases/3,295methods**, identical names/arguments/multiplicity. All tests/configuration stay byte-identical.
- Head IL scan decodes109,100bodies, confirms all five declarations absent and zero incoming method/token references or exact reflection-name literals. Generated lambdas belonging to the dead methods disappear too.
- Standard base/head native equivalence passes: two seeds,16ticks each, generated/legacy worlds, **all68frames** match exact checkpoint bytes, ordered events and observed cognition digests. Both run.json files record the correct commits and clean source trees.
- Personally read [base test-times119675732579](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39732743690/job/119675732579), including all ten growth rows:3,295shared methods/16,292aggregate seconds versus rolling main14,227(+15%). All8artifact ZIP digests verified:7,394cases/3,295methods,7,385pass/9platform skips; focused173cases/110methods allpass,444.759401seconds. No speedup claimed.

Additional completed focused result:
- **Focused existing Civic/land/admission/remedy tests pass173/173cases across110methods, zero failures/skips**, on the fixed head with xUnit.MaxParallelThreads=4. The original command/TRX and selected classes are preserved. No tests/configuration changed.
Formatting and final whitespace checks pass: `dotnet format --verify-no-changes --no-restore`, `git diff --check`. All local checks complete once; no source edit after first push.

Final CI: Windows installer and aggregate Verify pass; all27checks completed successfully, neutrally or skipped as appropriate. All eight CI shards pass: **7,394cases/3,295methods,7,385pass/9platform skips**, including all173focused cases/110methods. All8head timing ZIP digests verified; focused aggregate469.956129s versus base444.759401s, with no speedup claim. Personally read [head test-times119684652329](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39735439981/job/119684652329), all ten growth rows:3,295shared methods/15,649aggregate seconds versus rolling-main15,154(+3%). Full run [39735439981](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39735439981); all27exact-head checks complete without failure. CI runs the full suite; no local full-suite or coverage run is needed for uncalled source methods. No test/support/configuration changes.

[Evidence archive](https://github.com/ClankerWorldOrg/ClankerWorld/tree/codex/2551-audit-evidence-d83def7a/.evidence/keep/2551/dead-entry-points-d83def7a) preserves raw reports, all sixteen CI timing ZIPs/digests, scanner source, frozen binary hashes and native checkpoint frames. No source refresh or repeated completed local check. Independent review and merge remain.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

No reachable execution or saved field is removed. All68native replay comparison frames pass; no playtest claimed.

## Writing, docs and changelog

- [x] No game text changed.
- [x] No documentation contract affected.
- Stale-text search: NonLandText, SubmitExpansionLandRequest, AcceptTownAdmission, NonLandTerms, LandHearingOutcome; no stale sentence.
- [x] No changelog needed for internal behavior-preserving cleanup.
