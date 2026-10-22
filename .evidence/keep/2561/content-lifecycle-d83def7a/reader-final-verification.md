# Final evidence preparation: retained executions and branch identity logic

Codex session d83def7a/content-reader. Same-session second reader who helped choose consolidations; NOT an independent merge reviewer. This check reads existing artifacts only. No tests/builds, tracked-source changes, GitHub actions or comparator-output mutations were performed. Only this authorized evidence note was written.

## Available evidence

Reviewed actual-diff-review.md, ledger.json, retained-proofs.txt, compare-branch-identities.py, corrected coverage runner/commands/receipt, discovery and ledger output, binary pins, and valid baseline TRX/XML/JSON. Existing source review still concludes the sole original rollback inputs and every recipe assertion/native/host boundary remain intact.

- retained-proofs.txt equals the union of all ledger retained_proof names exactly: five methods, no missing or extra proof names.
- The valid BASE coverage TRX actually executes175 cases/82 distinct methods, all Passed.
- All five retained method names really execute in that valid TRX, comprising seven cases: RollbackPreservesRecordedProjectReferences(stage:"completed"); RollbackWithCommittedProductionReferencesRejectsWithoutDeletingWorldState(completed:false); WaterOutputIsRefusedWithoutReservingInputsAndHostKeepsSaving(false/true); LooseWoodControlCompletesAndItsOutputSurvivesHostReload; PreviouslyAdmittedWaterJobCancelsWithoutConsumingInputsAndReplays(false/true). No proof is merely discovered/skipped or running under a different class.
- Discovery comparison reports7398→7398 whole-assembly cases,3297 head methods,175 affected cases/82 methods, only two intentional theory→Fact display changes. These are discovery records, not evidence that head cases passed.
- ledger-check has no missing_proofs, other_class, not_removed, trimmed_but_gone or relies_on_held findings. Consolidate entries deliberately retain their same names; no case deletion credit is claimed.
- Scope is exactly `FullyQualifiedName~ClankerWorld.Tests.WorldRuntimeTests.|FullyQualifiedName~ClankerWorld.Tests.RecipeOutputAdmissionTests.` at both revisions. This selects the whole WorldRuntimeTests partial class and entire serializer-consuming class, not just the two changed source files.
- Corrected base/head commands share settings, filter, Linux scheduling xUnit.MaxParallelThreads=4, collector-format arguments and explicit restored coverlet.collector6.0.4 adapter path. They differ only in frozen output path/result directory. Earlier collector-unavailable runs are explicitly archived/excluded, not reused as coverage.
- Settings SHA2569bc1a6cf8d6b524e8c7bf4fc8d45d2dc15c2762d3300da4129861a3214bcd652 matches valid receipt and current runsettings. Base/head simulation/server DLL+PDB pins match exactly; test DLL/PDB differ as expected because test source changed. Linked client production remains in the test assembly and is guarded by comparison scope/identities rather than an identical test-DLL hash.

## Baseline identity validation

I parsed the existing valid baseline reports read-only, with the same source_key normalization and JSON identity definitions as the proposed comparator. JSON totals reproduce XML exactly:

| Module | Valid lines | Valid branches | Covered branches |
| --- | ---: | ---: | ---: |
| ClankerWorld.Server.dll |13010|11453|1062|
| ClankerWorld.Simulation.dll |61464|74686|21511|
| ClankerWorld.Tests.dll (linked production only) |3397|2235|0|
| Total |77871|88374|22573|

Baseline covered lines27126/77871; covered branches22573/88374. The repository's validate_report accepts the baseline XML. No branch identity collision occurred. All three modules have branches, so the comparator's iteration over branch-bearing modules omits no line-only module in this data. All raw JSON branch line numbers are positive; this check nevertheless counts every JSON branch, rather than relying on summed visible Cobertura fractions.

## Comparator logic assessment

No blocker found for this fixed-byte measurement:

1. Each side permits multiple report copies only if byte-identical; divergent reports fail instead of silently choosing one.
2. Line identity `(module,normalized source path,line)` merges method contributions using maximum hit count, which correctly implements covered-if-any-method-hit for source-line coverage.
3. Branch identity `(module,normalized path,class,method,source line,IL Offset,EndOffset,Path,Ordinal)` distinguishes branch arms and locations. Duplicate identities fail. With identical production DLL/PDB bytes, these IL identities can be compared directly rather than assuming that equal branch counts cover the same arms. Changed test serialization can reorder anonymous/generated linked code; equal identity-set assertions refuse that change rather than hiding it.
4. JSON valid/covered line and branch identity totals are checked against XML collector summary counters before comparison. This covers branches beyond a cautious mapped-line bound; no visible-fraction summation replaces total counters.
5. Both line and branch identity sets must match between revisions. Scope drift therefore fails. Lost hits are counted from before-covered/after-uncovered identities without offsetting newly hit identities. Per-module percentage loss uses that module's complete instrumented denominator; unchanged support/config/production and complete consumer filter are prerequisites for treating filtered lost hits as an upper bound on whole-suite loss.
6. Script is supplemental to the repository comparator/receipt validation, not a replacement for their malformed-data, assembly exclusion, scope or coverage-threshold checks. It reports loss numbers but does not enforce a threshold itself. The parent must run the normal comparator and examine actual losses; an empty or favorable numeric result alone is not behavioral proof.

## Pending HEAD data

At this inspection, after/tests.trx and head-coverage-completion.json were absent: corrected head coverage was still running. Therefore I have NOT verified head execution outcomes, expected175/82 counts, seven retained cases, exact base/head branch identity-set equality, actual lost line/branch identities, completion binary restoration or final comparator/CI results. No statement of zero loss or head pass is made here.

After completion, require successful head receipt and restored six pinned hashes; head175Passed/82methods and all five ledger proofs/seven cases (two rollback Facts with unchanged names); normal per-assembly comparator; then stable-branch-comparison.json with equal valid sets and explicit lost identities. Re-read any lost identity; do not offset losses with new hits or rely only on rounded rates. The source review remains conditional on these required checks and CI readiness.

Signed: Codex session d83def7a/content-reader; same-session evidence second reader, not independent merge review.

## Completion addendum: corrected head and stable identities verified

This addendum supersedes the earlier pending-head paragraph. After the authorized grown/animal audit, head-coverage-completion.json, final after/tests.trx and stable-branch-comparison.json became available. I read them and independently reconstructed both JSON line/branch identity maps without running a test or writing comparator output.

- Final corrected HEAD TRX:175 cases,82 distinct methods, ALL Passed. All five retained proofs actually executed, seven cases: both rollback methods now parameterless Facts, wood control unchanged, and both false/true rows in each water-output theory. No missing, skipped, renamed-class or unexecuted proof.
- Full valid scope sets are identical:77,871 source-line identities and88,374 branch identities. Actual covered identity sets are identical as well: ZERO lost lines, ZERO gained lines, ZERO lost branches, ZERO gained branches. This is stronger than equal aggregate counters/rates.
- Independently computed losses match parent's stable-branch-comparison.json: Server1,500 covered lines/1,062 branches; Simulation25,626 lines/21,511 branches; linked-production test module0/0; all valid per-module denominators match baseline and every lost list is empty. Total27,126 covered lines/22,573 branches at both revisions; conservative whole-suite loss upper bound0 in every module and total for this affected-filter comparison.
- Actual post-run SHA256 of ALL SIX head-output-collector DLL/PDB files matches head-pins.json; collector instrumentation restored original bytes. Successful head receipt has the identical settings hash/filter/collector path and expected175 cases.

No retained-execution or actual branch-identity blocker remains. This does not replace the parent's normal comparator/formatter/CI/open-PR readiness checks, and is not independent merge review. No tracked source, build/test run or GitHub action performed by this reader.

Completion signature: Codex session d83def7a/content-reader.
