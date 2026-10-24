# PR #2797 final evidence

Codex session33837055. Head `90cae3cb5cde33b3e09e683e7dad875dc1b2d03f`, independent main `835f9db1155bfb03fb741b2d5bcde9de50031316`.

Source +0/-175 across two client files; tests/docs0. Remove ten uncalled public convenience wrappers and the private SendContentLifecycleAsync helper called only by three of them. Both classes are ordinary System.Object services, with no interface, virtual or attributed callback root. Three compiler-certified unused imports removed. Shared DTOs, server endpoints, live signing/origin/deadline checks, all screens and every assertion/check stay intact. This deliberately narrows the client implementation method surface; wire contracts stay unchanged.

Local validation complete:
- Baseline locked Godot Release0warnings/errors41.519s, actual Game DLL/PDB retained; emitted real generated Godot code for complete semantic analysis.494actual source/generated files,165actual framework/Godot references and real C#14constants compile with0errors before/after.
- Source references: only3calls to the private helper, all from selected wrappers. Actual compiled baseline Game and retained test copy each contain all11definitions and those same3closed helper calls, with no incoming call/function pointer from retained code. All-source/asset/test/script searches find no other consumer; reflection-method enumeration in tests targets WorldRuntime, not these services.
- Full locked dependent Release0warnings/errors145.72s. Full repository format/verify without include filters pass167.98s/158.53s; no source edits or dirty tree after format.
- **72normal cases,35actual methods,9whole classes,0partial classes,0failure/skip**,12.61s. Exact planned/executed raw TRX reconciliation covers pairing, body deadlines, world-edit refusal, catalog/settings compatibility, device joining, observation sessions and actual game-assembly wire round trips.
- Source proof preserves **69retained methods and67state/property/constructor/operator declarations** exactly. Two files match only planned AST deletions/import removals; all other product/test sources, shared contracts and Simulation unchanged. No CallerLineNumber use; no saved-data or schema change.
- Compiled inventory: **exactly11user methods removed and0added in both Game and Tests**, all state fields unchanged. Game bodies25,934->25,923; Tests48,563->48,552. Game revision835->90cae and test baselinea351->90cae verified. Prior immutable test binary has byte-identical relevant client source/test project; the earlier vehicle graphics change is outside its linked sources.
- The auxiliary comparison initially counted generated RegexGenerator file-local path names as differences between worktrees. Diagnosis proves all60extra signature differences are only that path hash. The corrected comparison normalizes precisely its `F` discriminator plus64hex hash, preserving every pattern name, ordinal, signature and count, with collision guards. No generated method is dropped from the inventory for this fix; failed prototype log and diagnosis retained. Product assertions/checks never changed.
- Head Game SHA256 `8b809932d6bbafc882d7ee4f1e68689e4e9d87a370926281d28ff3565b807883`; actual Tests SHA256 `b56323823dd1c17dd1d106d246df2b77d0055f4d183f7b0bcff22a7f80d6d2c8`, pinned after the client run.
- Complete `bash scripts/verify-godot-client.sh` passed304.35s: grown native world/profiles at both1280x720 and1920x1080, UI/native pixel/motion/navigation checks, Developer options and launcher, strict native shutdown leak checks. Negative owner-action tests intentionally log refusal/I/O warnings. Unrelated imported baseline SigstoreReleaseVerifier.cs.uid saved externally and excluded; product tree clean.
- Stale-docs search for all11retired method names returns no affected sentence; diff check passes. No new Windows playtest or Simulation tick probe: active flows/screens and Simulation source are unchanged. Automated checks do not establish a Windows playtest or a performance gain.

Archives:
- [pr2797-source.zip](pr2797-source.zip):408,527bytes,285files,SHA256 `b7790b41cf7a752da7fa24920b611e142087e77b609566caa7fc4a82fd3cf02e`. Source snapshots/expected deletion, actual generated compiler inputs, semantic/IL incoming/API/source proofs and tools, exact filter/TRX/reconciliation, full build/format/client logs, worktree-regex diagnosis and binary provenance/hashes.
- [pr2797-binaries.zip](pr2797-binaries.zip):13,277,762bytes,6files,SHA256 `49382ee85f66cb841ff631e0ac0c4db6e96de94075cefcda5f22f5c144292e60`. Baseline/head Game DLL/PDB, before/after actual test assemblies.
- [pr2797-ci.zip](pr2797-ci.zip): complete final scope/checks/timing/installer/gate logs and workflow/job/check-run/PR metadata; exact bytes/SHA receipt is appended below when packed. Local archives above remain immutable.

Exact-head Verify [40051016180](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/40051016180) fully green: all16jobs and23head checks complete/success except expected cancel-stale skip; all8Release shards,Windows documentation/provider storage,CodeQL/labels pass. Checks120743555806,installer120746480849 and finalverify120747677280pass. Actual scope/checks/installer checkout `d698fa80ec7ee0a439a5b1b1eba182be06c071fa` merges90caeinto835; checkout logs and final gate personally read.

All10timing rows120746381080 personally read:3,271shared methods14,162s versus13,973s(+1%). Mixed sixboat/onegrown-memory/onepottery/onelantern/oneunreachable rows; no aggregate timing warning or causal speed claim. Node action deprecation is unrelated.

Area93C#files: original24,225lines, main24,235, proposed24,060. This PR removes175from main, leaving165fewer than original(0.68%);20% target4,845,still4,680short. Refs issue2558stays open; protected tracking issues untouched. Evidence branch has no product PR and must never merge to main.

CI archive receipt: {
  "file": "pr2797-ci.zip",
  "bytes": 133348,
  "files": 9,
  "sha256": "bb706a9fc9039d795ba381f12c021ec6ebf5da1a9f45f977c3e322d2a9e87cc5"
}
