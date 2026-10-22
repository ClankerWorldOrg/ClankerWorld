## Summary

Share the direction loops in native wrapped-map checks while keeping every held-key step and middle-drag assertion.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session 5f03c0da (implementation and completed local checks); Codex session 8451d624 (released-draft continuation and CI completion).
- Refs #2568; partial cleanup toward its original 7,656-line target. Independent main base `e61a1d8dc3f4b2ca3105e1e50477be077eb902f4`; stacked on none. All older owned drafts are ready. No main merge.
- Source +21/−35 (14 net removed, all native check code); tests +0/−0; docs +0/−0. Main.UiChecks.cs 4,303 → 4,289 lines; native checks 18,898 → 18,884; managed client tests 6,634 unchanged.
- Right 512 steps then left 512 steps remain ordered, with no reset between directions, delta 0.1 and per-step modular distance 1.5/tolerance 0.01. Keep each direction's original subtraction operand order and diagnostic text.
- East four drags then west four drags remain ordered, each with 293-tile relative motion, middle-button mask and per-motion modular distance 37/tolerance 0.01. One uninterrupted middle-button press/release spans both directions. No helper or extra wait.
- The entire prefix, key-return assertion, middle press, and suffix from middle release through all final camera/selection/marker and cleanup assertions remain byte-identical.
- Original 230 before/after checks against 115 open heads passed. Continuation refresh: all 236 before/after checks against 118 other open heads pass, as does integration with main 4e0d56f5. #2752 shares Main.UiChecks.cs with a separate order-summary change; no new conflict. Further area work, including the pushed child-model checkpoint, stays in #2568.
- Fresh counts confirm all 15 named Verify declarations in the changed file remain. GitHub REST reports mergeable=true/clean. At the owner's request, the continuation keeps exact existing head `5cdfacf7313709394099bc83ad4a3ce997a685fc` and base e61; no main merge, source edit, repeated local run or managed coverage.

## Checks

- Whole original native file and owning PanCameraForFrame/HandleMapInput/PanCamera/geometry/PositiveMod implementations read. History e5ed0bb1 introduced wrapped travel; 907d3222 changed held keys to frame-time movement; d9ae8ef8 organized the files.
- Two-entry pre-edit ledger and original full file/blocks preserved. Read-only second reader approved exact head `5cdfacf7313709394099bc83ad4a3ce997a685fc`, preserving all 1,024 key steps, eight drags and final assertions. Independent PR review remains.
- Locked restore, Release native unused build (154 → 154 diagnostics, none new), pinned Godot 4.7.2 C# build/import and full --ui-smoke-test pass. Fresh XDG directories, strict tick failures, executable /tmp and no shutdown leaks recorded. Formatter and diff checks also pass; exact Debug DLL/PDB hashes bind this run to the reviewed head. Verified untracked import UID removed after checks; checkout clean.
- Reuse the completed exact e61 native baseline from #2752 with Debug DLL/PDB hashes verified. Original noexec setup failure and matched corrected retry remain preserved. Full native UI smoke 122.320 → 102.218 seconds across separate runs; no causal speedup claim.
- ClientChecks are not linked into xUnit collector coverage. Managed tests, production rules and test configuration are unchanged; no managed full-suite coverage was run or equivalence fabricated.
- Frozen-base actual CI 39727220070: the original author's completed baseline reports 7,398 managed cases / 7,389 passed / nine existing skips. Its actual test-times log 119658135757 was reread: 3,297 shared methods at 14,308 seconds versus median 14,722 (−3%); all ten growth rows are outside this native method.
- Exact-head [CI 39751625753](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39751625753) is green. Actual scope log 119733343364 reports `code=true`, `tests=false`; managed shards, Windows protected storage and test-times are correctly skipped, with no head managed execution claimed. Managed source/configuration is byte-identical. Windows documentation, all CodeQL analyses, native Godot checks, offline signatures, exports/packages, [installer 119739638239](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39751625753/job/119739638239) and [aggregate 119742010778](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39751625753/job/119742010778) pass. Actual installer log reports `standardUserInstallUpgradeReinstallUninstallDataAndVersions=passed`; aggregate confirms tests skipped and installer/checks success.
- Actual base/head client-check command spans are **345.589→351.577 seconds**, from adjacent command-group timestamps in [base checks 119653892003](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39727220070/job/119653892003) and [head checks 119733404426](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39751625753/job/119733404426). Both raw logs confirm pinned Godot 4.7.2, full UI smoke, Profile at 1280×720 and 1920×1080, all 19 owner lifecycle checks and launcher success. Different runs/runners prevent a causal performance claim. The head client command was not rerun.
- Fresh continuation source reader accepted exact head: replacing just the two consolidated blocks with their originals reconstructs the entire base file byte-for-byte. Raw CI, counts, ledger and signed receipt are preserved on the [separate evidence branch](https://github.com/ClankerWorldOrg/ClankerWorld/tree/evidence/2568-continue-2767-8451d624/.evidence/keep/2568/continue-2767-8451d624). Prior local checks above remain attributed to their original author; no unavailable local raw receipt is claimed as independently verified.
- Automated native checks are reported above; no manual playtest claimed or required for this check-only change.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

## Writing, docs and changelog

- [x] No player text changed.
- [x] No docs affected.
- Stale-text search for PanCameraForFrame, HandleMapInput and PositiveMod found no sentence made false or unfinished.
- [x] Native check cleanup needs no changelog entry.
