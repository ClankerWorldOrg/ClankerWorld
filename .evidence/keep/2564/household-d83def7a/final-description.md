## Summary

Keep all five household building tests while removing duplicate test bookkeeping: the selected-wood assertion already proves that wood was offered, both theories have a single fixed row, and the two household-family queries are identical. Preserve the native harvest, capacity, travel, construction, ownership and reload assertions, including the two-day normal-chooser scenario.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session d83def7a.
- Refs #2564. The broader Economy audit remains open; this is one independent main-based household slice.
- Existing behavior: identical seeds, inventory/custody preparation, calendar, tick limits, providers, candidate choices and native assertions. No game code changes.
- Other open PRs: all 113 current file lists checked; none changes these three files. Exact before/after merge probes pending.
- Stacked on: none. Fixed main base `91f4b0c7a859955211aff16182678cc0d78d84c5`; no main merge unless GitHub reports a conflict, per owner's request.
- Lines: source +0/−0; tests +14/−27 (13 net removed); docs +0/−0. Selected baseline 487 lines, five cases/five methods; 474 lines, same five cases/five methods. No case-removal credit. Lexical assertion callsites 99→98: only the offered-wood assertion implied by selected wood disappears.
- Later work: remaining Economy files and prior ready slices remain tracked in #2564. Prior running total 542 proposed net test lines; this slice is not counted until ready.
- Hands-on check: none needed for test-only consolidation.
- Draft waits for disposition of diagnostic-only coverage loss, all-open-test-PR merge probes, exact-head CI and its test-times report. This session is handing off after repeated Git credential failures.

## Checks

- SDK 10.0.401, .NET 10.0.12, Linux, Release. Base locked restore, diagnostic build and discovery passed: 7,396 discovered cases/3,295 methods.
- Final diagnostic build, full discovery (7,396/3,295 unchanged), ledger, formatter, stale-doc and diff checks passed. Unused diagnostics 167→167, zero new. Head build was repeated once solely to correct closing-brace formatting before coverage; production bytes pinned from base. One premature setup restore before worktree creation failed MSB1003; corrected setup completed locked restore/build, no tests or coverage ran in the premature attempt.
- Full original test bodies, owners, callers, history and second-reader actual-diff review completed. The retained chosen assertion is fed exclusively by candidates from the offered list. The shared query retains immediate ordinal materialization and has exactly six calls in these two nonpartial classes.
- Owner-directed coverage: one identical filter for all three complete changed classes, including every support consumer and retained proof, on fixed base/head. Explicit coverlet adapter, unchanged canonical settings; original methods measured separately. Base and head five/five passed with the identical whole-three-class filter; all five original methods passed separately. Canonical comparison passes its gate but reports 18 lost lines and five lost actual branches, all JsonIgnore DiagnosticDetail getters formerly read by the deleted eager failure-message enumeration. Retained preparation method rerun alone passes and reaches 0/18; original alone reaches 18/18. This is real diagnostic coverage loss, not timing variation; successor must explicitly accept its rationale or preserve the original diagnostics on the retained chosen assertion and remeasure affected head. Stable scope: total lines 21,359→21,341 /77,873 (−0.023115pp, −0.084274% relative); actual branches 16,814→16,809 /88,374 (−0.005658pp, −0.029737% relative). All loss is Simulation: lines /61,464 (−0.029285pp), branches /74,686 (−0.006695pp); Server and linked-source Tests zero loss. No gains offset losses. Raw branch keys match, including unmapped branches.
- Base CI run 39741455777: 7,387 pass, nine platform skips, 7,396 reported cases/3,295 declarations; all eight timing ZIP digests verified. Test-times job 119705893696: shared methods 15,368s against main median 14,088s (+9%); read all ten growth rows, including unchanged household-use scenario 79s versus 46s (+71%). No speedup claimed. Head comparison pending.
- Local full-suite coverage omitted at owner's request; CI runs the full Release suite.
- No manual playtest; native export/storage gates supplied by applicable CI. No production, save or UI changes.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

No compatibility impact. Existing strict reload, physical delivery and construction proofs remain.

## Writing, docs and changelog

- [x] No product text changed.
- [x] No documentation affected.
- Stale-text search passed with no stale sentence: HouseholdBuildingPlanTests, HouseholdBuildingPreparationDeliveryTests, HouseholdBuildingUseCoverageTests, SeenCandidates, FamiliesForHousehold.
- [x] No changelog entry required for behavior-preserving test cleanup.

Evidence: `.evidence/2564/ledger.json`, `household-reader.md`, fixed-base build/discovery and coverage receipts. Metadata, ledgers, per-assembly/stable coverage comparison and pins preserved on `codex/2564-audit-evidence-d83def7a`, under `.evidence/keep/2564/household-d83def7a/`; NEVER merge or open a PR for that evidence branch. Full raw 102-file ZIP (including original-method reports, base/head XML+JSON, TRXs, pinned binaries and eight base timing ZIPs) remains local at `/workspace/ClankerWorld-2564-continuation/.evidence/2564/household-evidence.zip`, SHA256 `702e740452bd01fc5e7881a9fb0dcf9e76c2f68d8d20bd2e41c7bb8dd3c443e4`,29MB. Isolated loss-check raw report is separately under `.evidence/2564/head-loss-check/`; metadata remote, full raw archive not uploaded because Git transport failed.

Git transport failed repeatedly during this session (missing credentials, then invalid supplied credential). Preserved through authenticated GitHub API with the exact same tree `df821d27b4f3106a0d6dfbb93d2332e220bcc83d`: local measurement commit `e924d5bf64913537affe1d6e290e63c675fcd167`, remote head `11946b63d6a4eb798e74385e43d08a332d24f427`, same fixed parent. Required all-open-test-PR merge probes remain pending because 24 head objects could not be fetched. No main merge or full-suite coverage was attempted.
