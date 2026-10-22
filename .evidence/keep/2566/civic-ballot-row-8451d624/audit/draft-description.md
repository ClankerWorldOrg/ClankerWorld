## Summary

Use a Fact for the sole malformed civic-ballot input, moving the identical JSON string into the test body. Provider parsing and every assertion remain unchanged.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session 8451d624.
- Refs #2566. Remaining Society/Map candidates and final full-area reconciliation stay open; original target 8,364 test lines.
- Independent main base `442fdcffefb3f97ec8bc49fa966261e7dbf7244f`, head `f82abff0d1ced0bd5d4a47522bc0a0e05fd0c78d`; stacked on none. All earlier own drafts are ready. No main merge.
- Lines added/removed: source **+0/−0**, tests **+3/−4 (1 fewer)**, docs **+0/−0**. CivicBallotProviderTests shrinks **110→109 lines**.
- Keep the exact escaped JSON string, HTTP handler/provider setup, request, exception assertion and every sibling proof: missing versus explicit null, abstention, three choices and duplicate vote rejection.
- Six cases/four methods remain; no case-removal credit. One theory case becomes a Fact with the same method name.
- Full class, owning parser/caller and Git history read. Pre-edit ledger covers all four methods. No support/configuration/production changes.
- All 116 open PR file lists and 232 before/after merge-tree probes pass, no changed-file overlap or moved head.
- No hands-on check is needed for this test-only change. Independent review remains.

## Checks

Completed: locked base restore, source/history/ledger review, stale-doc search and whitespace check. Fresh base/head normal Release builds are running.

Pending once on this frozen pair:
- Normal builds and full discovery/approved Fact identity mapping.
- One affected base/head coverage pair with `FullyQualifiedName~ClankerWorld.Tests.CivicBallotProviderTests.`, all six cases/four methods and every retained proof.
- Four original method measurements and unique planner, official coverage comparator, conservative per-assembly/total loss bound and any required lost-line probes.
- Formatter, fresh before/after unused diagnostics and read-only second-reader source/raw reconciliation.
- Exact-head CI including applicable Godot/export, Windows and native installer gates.
- Actual base/head test-times job logs, all ten growth rows, and count/line/time report.

No full-suite coverage is planned: the whole changed class is the complete affected scope. Mark ready as soon as these checks and head CI pass; no refresh for moving main.

Evidence: /workspace/2566-audit/civic-ballot-row and .evidence/keep/2566-civic-ballot-row. Local SDK 10.0.401 under the latest-patch policy. No manual game playtest performed.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

No compatibility impact; only test invocation scaffolding changes.

## Writing, docs and changelog

- [x] No product text or documentation contract changed.
- [x] Stale-doc search passed for MalformedNonNullBallotStillFailsProviderParsing, CivicBallotProviderTests and ParseCivicBallot.
- [x] Test cleanup needs no changelog entry.
