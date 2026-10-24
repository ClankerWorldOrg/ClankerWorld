# PR #2795 final evidence

Codex session 33837055. Final head `038958624bab0c95358e2a12e0d58f5f5d286a1f`, independent base `835f9db1155bfb03fb741b2d5bcde9de50031316`.

Source +5/-167 = **162 net fewer lines** across ten core files. Remove ten uncalled entry points, merge two private canonical content builders into their existing digest methods, and remove five unused imports. Tests +0/-1: only the obsolete organization-ID allowance disappears; the identity guard and all remaining allowances are unchanged. Docs +0/-0; the retained asset XML comment describes naming rules. No live assertion/check weakened. Interface-invoked Identity.CompareTo, actual inventory writers, saved organization fields/validators and used dispatch overloads remain.

Local validation complete:
- Locked restore and baseline Simulation Release passed; final full dependent Release including the actual game assembly passed with zero warnings/errors (66.231s).
- Full repository format and verify without include filters passed (161.783s/146.775s); no subsequent C# edits.
- **290 normal cases, 180 actual methods, 32 whole classes, zero partial classes, zero failures/skips**, exact planned/executed TRX reconciliation, 65.754s wall.
- **512 baseline/head content comparisons**: 256 building and 256 recipe cases, including Unicode/optional values/invalid values; canonical IDs, payload digests and validation results/exceptions match.
- **68 native frames** match checkpoint bytes, ordered events and observation digests (103.643s). Both actual run.json files show the correct clean base/head. Native, dependent-build and actual test-host Simulation DLL/PDB files are byte-identical; DLL SHA256 `c9c102693b3c5a461ee23954415cc5f2b96ec985287d183eb27dc221054b9490`.
- Source proof across 1,031 Simulation files preserves 3,005 state/constructor/operator declarations and 9,103 method definitions exactly; only the two planned digest methods change bodies. Both retired private builders have precisely one caller, identical parameter forwarding, and retain their ordered builder statements and same hash helper.
- Compiled proof: precisely 12 non-generated methods removed (ten dead methods and two private payload helpers), zero added, all non-generated fields unchanged, Identity.CompareTo retained. Actual body count 50,166 -> 50,143 including removed generated closures. Baseline compiled incoming-reference scan found all ten dead definitions and zero calls/function pointers in Simulation, Server, actual tests and game assemblies.
- Stale-docs search and git diff check passed. No open core file-map overlap found. No manual game playtest; bounded native scenarios do not establish every possible path or a performance gain.

The first CI run caught an obsolete hand-built identity allowance left after removing CreateOrganization. The final head removes that one allowance, reducing exceptions. Final native/build/tests ran on the revised head. Final CI passed as detailed below.

Archives:
- [pr2795-source.zip](pr2795-source.zip): 3,693,533 bytes, 91 files, SHA256 `9bf8bddd1ce2455d9d9a7413f0ec166c8632d3930e0aa5edec9b4bf0cc291387`. Actual source snapshots/patch, transformations and semantic/dead-reference proofs, tools, filter/TRX/reconciliation, digest cases, receipts, format/stale logs.
- [pr2795-binaries.zip](pr2795-binaries.zip): 5,919,847 bytes, 4 files, SHA256 `e2c310a0fc6548f1bf26c0d4a70458e4e4d339c6498b5013ceed05e4c28d0f73`. Baseline/head Simulation DLL/PDB; head matches native and actual test copies.
- [pr2795-native.zip](pr2795-native.zip): 12,944,572 bytes, 413 files, SHA256 `4ca2bd19624bdb86122c2971cdd52b5cb7167b53089207da91a4760d6f4ccd0f`. Complete final native probe source/logs/run metadata/checkpoints/events/digests.

Area 55 C# files, 13,028 -> 12,866 lines: 162 fewer (1.24%); original 20% target 2,606, still 2,444 short. Issue #2556 stays open; protected tracking issues untouched. Evidence branch has no product PR and must never merge to main.

Final CI Verify [40046100987](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/40046100987) fully green on03895862: all16jobs complete/success except expected cancel-stale skip; all25head check runs complete/success except that same skip, including CodeQL and labels. Installer120731884903 and finalverify120733163535 pass. Actual scope and installer checkout737a0fd4e0ad8182e7c011c19f8bc4278767a459 merges038 into835; checkout logs and final gate personally read.

All10timing rows120731860187 personally read:3271shared methods15208s versus13973s(+9%). Mixed fourboat/twomarket/twomemory/oneborrowedtool/oneunreachable rows; no aggregate timing warning or causal speed claim. Node-action deprecation is unrelated.

CI archive: {"file": "pr2795-ci.zip", "bytes": 65761, "files": 9, "sha256": "04134a5a710b54ca108de29260a9636be37d21bc629224cfa99b3f22b4aea761"}. Full scope/timing/installer/finalgate logs, first CI failure and final workflow/jobs/check metadata retained. Local source/binary/native archives above remain immutable.
