from pathlib import Path
import json,re
p=Path('/workspace/2566-audit/content-support');bound=json.loads((p/'point-bound.json').read_text());reader=json.loads((p/'reader-raw.json').read_text());assert reader
rows=[]
for asm,d in bound.items():
 for metric in ['lines','branches']:assert d[metric]['lost']==d[metric]['gained']==0
 a=d['lines'];b=d['branches'];rows.append(f"| {asm} | {a['before']:,}/{a['valid']:,} | {b['before']:,}/{b['valid']:,} |")
ci=json.loads((p/'ci-final.json').read_text());assert ci['all_required_green'] and ci['head']=='ab4b213f2ef03ae9d431587cc6a95702b62fc014'
body='''## Summary

Remove unused options from Content test helpers and the unselected cache arms left by earlier pruning. All 16 cases, 13 methods, 63 assertion call sites and their production inputs remain; lifecycle operations and events keep their order.

## Type of change

- [x] Refactor or tooling

## Scope and related work

- Author: Codex session 8451d624.
- Refs #2566; broader area cleanup and its 8,364-line target remain.
- Independent main ca3a5b8d base, head ab4b213f; stacked on none. No main merge or coverage refresh.
- Source **+0/−0**; tests **+23/−49 (26 fewer)**; docs **+0/−0**. Three files426→400; frozen163-file area27,704→27,678. Full discovery stays7,394 cases/3,295 methods in exact order. No case-removal credit.
- Building helpers keep wood1/kitchen; recipe helpers keep every workstation-ID string. All Governance package versions were1.0.0; inlining its single definition forwarder preserves field values. Two direct Propose calls retain core→world order. Asset rows remain durable/GPU with identical charges, refusal/overflow diagnostics and atomicity assertions; the separate three-budget breach proof stays.
- Read whole tests/helpers, owning ContentDefinitions/ContentContracts/WorldAssetReservations, runtime/server callers and history541fcbd7/5c2562c5, including prior merged ordering/roundtrip regressions. Ledger retains all13 methods and every row; no external helper users. All99 open heads checked by metadata/filenames and198 before/after merge-tree calls: no changed-file overlap or new conflict. Current-main read-only merge-tree was clean, and GitHub reports mergeable; frozen base staysca3.
- Independent author-side source reader accepts exact committed file hashes and all63 assertion lexical slices/order. A different top-level session still reviews and merges.

## Checks

- Locked restores and fresh normal Release builds pass, zero warnings/errors each. SDK10.0.401 matchesglobal.json; CI latest-patch10.0.402. Production/config trees are unchanged. Normal tests and original head production DLL/PDBs archived before diagnostics; both coverage runs use the same four canonical base production DLL/PDB bytes.
- Exact full discovery, retained-proof ledger, formatter, stale-doc search and git diff --check pass. Temporary nonincremental unused-code comparison has no new diagnostics, and both temporary configs are gone.
- One identical filter on frozen base/head includes all three full sealed classes and all private support consumers:
  `FullyQualifiedName~ClankerWorld.Tests.ContentDefinitionTests.|FullyQualifiedName~ClankerWorld.Tests.ContentGovernanceTests.|FullyQualifiedName~ClankerWorld.Tests.WorldAssetReservationTests.`
- Both matched runs pass16/16 cases/13 methods, no skips, with identical settings, DPC4/xUnit2/nice10. `compare_coverage.py --threshold-scope each` passes; exact JSON/XML identity reconciliation confirms zero lost/gained points, including unmapped collector branches. Conservative whole-suite loss is0pp in every assembly/total,0% relative wherever baseline hits are nonzero (undefined for zero-hit scopes). No lost-line probe or full-suite coverage repeat is needed.

| Scope | Covered lines/valid, before=after | Covered branches/valid, before=after |
| --- | --- | --- |
'''+ '\n'.join(rows)+'''

- All13 original methods also pass their separate isolated reports,16 cases total; official unique-coverage planner is complete and all13 retained for their existing behavioral assertions. Individual uniqueness does not justify deleting these proofs. Collector totals above include352 unmapped unhit branches; mapped/planner lower bounds are not substituted for actual counts.
- Independent raw reader reconciles actual scoped/isolated XML/JSON/TRX, exact points, canonical hashes/settings, original archives, discovery, ledger, unused comparison and committed source acceptance; no findings. Evidence is outside the source diff, with an immutable archive linked from the area issue.
- Actual frozen-base [test-times119668667935](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39730642461/job/119668667935):3,295 shared methods15,654s vsrolling-main15,078s(+4%); all10 growth rows read. HEAD_TIMING
- Current-head [Verify39733463943](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/39733463943) is green on integration56e88c3 (ab4b into ca3). All8Linux shards:7,385passed/9platformskips of7,394cases. Windows storage/provider99/99 and documentation18/18, Godot smoke/exports, signatures/platform packages pass. INSTALLER_RESULT
- All required gates are complete. Game checked by hand: not run; no manual product check needed for private test support cleanup.

## Saves and replay

- [x] Saved world state, events and replay behavior are unchanged.

Only test support changes; all lifecycle, canonical restore, atomicity, ordering and diagnostic assertions remain.

## Writing, docs and changelog

- [x] No product text or documentation contract changed.
- Stale-doc search passes: ContentDefinitionTests, ContentGovernanceTests, WorldAssetReservationTests.
- [x] No changelog needed for test-only cleanup.
'''
body=body.replace('HEAD_TIMING',ci['timing_description']).replace('INSTALLER_RESULT',ci['installer_description']);(p/'final-body.md').write_text(body)
print('final body ready')
