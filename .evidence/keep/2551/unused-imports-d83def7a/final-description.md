## Summary

Remove 37 unused import directives and one empty line from 14 Civic source files. Roslyn IDE0005 identifies these imports. Every remaining non-empty, non-import line stays in the same order.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session d83def7a.
- Refs #2551. Further Civic cleanup and its 20% target remain.
- Independent main base `f82570db918e057ff49a1155f6ab9e7bdb2172e8`; head `80c25e1065f43c142477244ccc9d1ea2668dcffc`. Stacked on none; no main merge.
- Source **+0/−38**, tests **+0/−0**, docs **+0/−0**. Changed files: 2,887→2,849 lines. Civic: 9,301→9,263 on this independent branch.
- Prior 114 merged +42 ready in #2747 +38 here would total 194 net source lines (2.06% of the original 9,415). The 1,883-line target remains unmet. This branch includes none of #2747's method removals.
- All 105 open PR file lists checked. The selected patch merges cleanly with #2747. Three unused imports in TownSystem.NonLandHearings.cs were retained because removing them introduced a conflict with #2747; revisit after it merges.
- No caller, declaration, statement, test, configuration or supported contract changes.

## Checks

Completed once on this fixed head:
- Fresh locked restore; Release build with `--no-restore --no-incremental`: zero warnings/errors.
- Discovery: **7,399 cases / 3,295 methods**. Test sources/configuration are byte-identical to base.
- Existing Civic filter: **54/54 passed**, zero skips. Filter covers TownSystemTests, GovernmentInvariantTests, CivicInvariantTests, CaseAuthorityInvariantTests and CouncilInvariantTests.
- `dotnet format --verify-no-changes --no-restore`, whitespace check and stale-text search: pass.
- IDE0005 semantic fix with temporary area configuration removed before commit/build; complete diff reviewed. Identical ordered non-import code in all 14 files; no conditional compilation.
- Exact #2747 conflict probes: clean both before and after the selected patch.

- Exact-head CI run [39738668229](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39738668229): all 24 checks completed successfully or skipped, including Verify, all eight Linux shards, Windows storage/docs/installer, Godot/export and CodeQL.
- Base and head CI artifacts: identical **7,399 cases / 3,295 methods**, **7,390 passed / 9 platform skips** on each. All 16 timing ZIP digests verified. Focused CI subset: 54 cases / 22 methods, all passed on both.
- Personally read all ten growth rows in both test-times logs. Base: 15,962 s vs main median 15,015 s (+6%). Head: 16,773 s vs main median 14,088 s (+19%); fixed-base comparison is +5.1%. Growth rows span boats, shops, family, lighting, clinic and food cases, with no changed executable source or tests; this is runner variation, not evidence of a runtime slowdown from unused imports. Focused summed CI times: 0.0736→0.1497 s. No speedup claim.
- No pending author checks. Ready for independent review; no main merge or repeat of completed checks.

No local full-suite, coverage or native tick probe: only compiler-proven unused imports/one blank line change; executable source and tests are unchanged. CI runs the full suite.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

No schema, compatibility, persistence or recovery change.

## Writing, docs and changelog

- [x] No game text or documentation contract changed.
- Stale-text search covered every changed file stem and IDE0005; no sentence became false.
- [x] No changelog or hands-on playtest needed for unused imports.

