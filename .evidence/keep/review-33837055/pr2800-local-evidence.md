# PR #2800 local evidence

Author: Codex session 33837055. Base `835f9db1155bfb03fb741b2d5bcde9de50031316`; head `e56b4901c6fdbdbf80af60a52b5734e5d5b88c8e`.

Source +2,940/-3,186 = 246 net fewer. Git reports 53 entries, including a 59% rename; the manifest and no-rename patch cover all 54 physical old/new paths. Consolidate 48 unheld TownSystem partial files into 11 feature files, containing 181 unchanged members, with 37 fewer C# files. Largest resulting file is 525 lines. Tests, assertions, budgets, docs, scripts and workflows are byte-unchanged. All existing allowance/doc paths remain in place. No changed-file overlap with 18 fresh open maps or future patch references to deleted filenames.

Initializer preflight retained the entire TownMembership state file, StockActionPlans readonly capability initializer, and TownSystem primary-constructor/state owner file. No non-constant fields, auto-property initializers, events or constructors move. Nested types move intact. The first initializer-blocked preview was rejected before any product edit; final groups are authoritative. Existing state writers, checks and typed plans are unchanged.

Actual-input before/after compilation: 1,047 baseline files, 167 references, C#14/actual constants and generated source; zero errors. All 9,148 method signature groups (including partial-declaration multiplicity), 4,648 state/constructor/operator/event declarations, 1,988 comments and 13,765 binding groups are unchanged. Full locked dependent restore and root format/verify passed. Every one of the 11 formatted output files byte-equals the preview; no token, comment or whitespace change. Exact 54 planned source paths changed, no deleted-file references anywhere in the repository, stale-doc search passed. Baseline Release: zero warnings/errors, 13.88s.

Clean native comparison passed in 96.37s: 68 frames, two seeds, generated/legacy starts, 16 ticks plus initial frames. Checkpoint bytes, ordered events and observation digests match. Both actual run.json receipts name the base/head and empty working trees. Full dependent Release passed with zero warnings/errors in 60.32s. Actual native simulation DLL/PDB byte-equal the full-build test copies.

Existing focused execution passed 347 cases, 228 methods, 46 classes (41 whole, five partial), zero failures/skips, 229.19s. Partial classes: TownLandHearingRuntimeTests, HouseholdLandGrantTests, TownGovernmentHandoverTests, TownMembershipTests and TownGovernmentRuntimeTests. Raw TRX FQNs exactly equal the planned scope. Includes unchanged architecture guards; full suite delegated to CI.

Compiled inventory: zero user-method additions/removals, all nongenerated state fields unchanged, all 50,166 method bodies retained (including generated bodies), Identity.IComparable implementation retained. Only generated RegexGenerator file-local F+64hex path hashes normalized, preserving signatures/counts with collision guards. Actual simulation SHA256 `42f175aec0791f1eca3ef679309a693e01352947ea4c23719285f0583bde8129`.

CI 40060393159 is running. Actual scope checkout `855d30143d2a801a12c5d79afb9da1109bd55f6d` merges the exact head into 835; code=true/tests=true. Scope personally read; Windows jobs passed. Remaining CI, all ten timing rows, installer and final gate pending. This local snapshot is not a Ready handoff. No manual Windows/game playtest performed; independent review/merge pending.

Area: original 9,415; main 8,927; head 8,681, 81 files. Cumulative 734 fewer; 1,149 remain toward the 1,883-line target, excluding waiting PRs. Refs #2551. Protected tracking issue #2296 is untouched at the owner's request.


Archives:

- [pr2800-source.zip](pr2800-source.zip): 770535 bytes, 138 files; SHA256 `0fd37c4d17cea033cf5bf47be74aec490d28440ae3cec11513efc898e3950497`.

- [pr2800-binaries.zip](pr2800-binaries.zip): 8878792 bytes, 6 files; SHA256 `3d7ed1271f02e05a051b6bd8160519890ffb342669dc8e255e78c2d6930b63a1`.

- [pr2800-native.zip](pr2800-native.zip): 12950353 bytes, 413 files; SHA256 `570fff567379d6b627da3cf4891d6933e0e05c00628fca18b96f3f9e8a0cd9f7`.
