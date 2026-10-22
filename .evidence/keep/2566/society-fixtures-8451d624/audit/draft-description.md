## Summary

Remove unused Society test scaffolding while preserving the same native inputs and assertions. The sole invalid clock-rate row becomes a Fact with literal zero. The will-custody helper omits a ground-position argument that is always null for its house/custodian callers.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session 8451d624.
- Refs #2566. World/Map candidates and final full-area reconciliation remain; original target 8,364 test lines.
- Independent main base `17ccaf0cc7d2c65add79cd9601602858e5755aad`, head `e19ffa5e99f520761d920725e6f824634f750dfd`; stacked on none. All earlier own drafts are ready. No main merge.
- Lines added/removed: source **+0/−0**, tests **+4/−6 (2 fewer)**, docs **+0/−0**. Two files shrink **572→570 lines**.
- Aging keeps the exact zero rate, zero anchors, contract version 2 and Validate exception assertion, plus all distinct lifecycle, API refusal, native age/restart and save proofs.
- The private Locate helper has exactly two callers, each with only house/custodian theory data. Its removed ground expression always yields null, equal to InventoryAuthority.Relocate's unchanged default. Carrier, storage, lot ID/quantity/owner and call order remain.
- Expected **29→29 cases and 21→21 methods**, with every assertion retained. The theory-to-Fact change receives no case-removal credit.
- Whole files, relevant validation/relocation implementation, callers and prior audit notes read. Pre-edit ledger covers every method and retained proof. Source/history/consumer second-reader verification is in progress.
- All 116 open PR file lists and 232 before/after merge-tree probes pass; no changed-file overlap, moved head or new conflict.
- No hands-on check is needed for test scaffolding cleanup. Independent review remains.

## Checks

Completed: locked base restore, full source/default/caller review, pre-edit ledger, stale-doc search and whitespace check. Fresh normal base/head Release builds are running.

Pending once on this frozen pair:
- Normal builds and full discovery with the sole approved theory display-name mapping.
- One affected base/head coverage pair using both complete classes:
  `FullyQualifiedName~ClankerWorld.Tests.SocietyAgingOverrideTests.|FullyQualifiedName~ClankerWorld.Tests.MultiHeirWillCustodyTests.`
  This includes every changed helper consumer and retained proof.
- All 21 original method measurements and unique planner; official coverage comparator, per-assembly/total conservative loss bound and any required lost-line probes.
- Formatter, fresh before/after unused diagnostics and read-only second-reader source/raw reconciliation.
- Exact-head CI, including applicable Godot/export, Windows and actual native installer gates.
- Actual base/head CI case counts and test-times job logs, reading all ten growth rows.

No full-suite coverage is planned: support is private to the two selected classes. Mark ready as soon as the listed checks and head CI pass; do not refresh for moving main.

Evidence: /workspace/2566-audit/society-fixtures and .evidence/keep/2566-society-fixtures. Local SDK 10.0.401 under the latest-patch policy. No manual playtest performed.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

Strict state validation, custody, inheritance and reload proofs remain.

## Writing, docs and changelog

- [x] No product text or documentation contract changed.
- [x] Stale-doc search passed for UnsupportedClockRatesFailClosed, SocietyAgingOverrideTests, MultiHeirWillCustodyTests and Locate.
- [x] Test cleanup needs no changelog entry.
