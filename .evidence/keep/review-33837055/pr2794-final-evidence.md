# PR #2794 final evidence

Codex session 33837055. Head `c2d666db6a73c43e0b3d5f4ba4cc7ad57f61d03a`, independent main `835f9db1155bfb03fb741b2d5bcde9de50031316`.

Remove 135 global aliases and 43 unused imports. Source +270/-448 = **178 net fewer** in 34 files; required test callers +72/-72 in 33 files; docs +2/-2. All 286 server and 77 test alias replacements bind to the same type and assembly, with no nameof substitution. No assertion or check weakened.

All local checks complete:
- Locked restore and baseline Server Release zero warnings/errors; final full dependent Release zero warnings/errors,135.81s, actual committed-head assemblies including the game.
- Full repository `dotnet format --no-restore` and `dotnet format --verify-no-changes --no-restore`, no include filters: passed152.59s/156.91s without further source edits.
- **284/284 normal cases,178 actual methods,22 classes,19 whole and3 partial,zero failure/skip**, exact raw TRX reconciliation (104.15s wall,1m39s reported). Partials ServerHttpTests,ProviderConfigurationStoreTests,WorldConversationTests. Existing SharedContractTests round trip through the actual game assembly passed. The earlier process was interrupted by workspace restart and is explicitly not counted; complete rerun retained.
- Expected-token proof across1,062C#files preserves37,054assertion calls,3,262Fact/Theory identities, all literals and ordinary comments. No tests removed.
- **24,763Server and107,473Simulation compiled entries exactly equal** for methods/fields/signatures/attributes/IL/user strings/references/local signatures; only expected build revision835->c2d changes. Actual test-host production DLL copies are byte-identical. Server SHA2569ce951e680b43772e769b4b58161cde827e1368cc6edeeaaf7857c97b335cb32; Simulation SHAdb27ae07690642a1a2eece499c0a6da152376831b38aec4f1caff0feceb4aeed. Native tick comparison was not repeated: Simulation compiled behavior is exactly unchanged.
- Stale docs search and diff check passed; one architecture sentence updated. All12open file maps checked; only#2741's GameUiTextTests case rows overlap a different required type-reference line; preserve both. No other PR modified.

Archives:
- [pr2794-source-proof.zip](pr2794-source-proof.zip): 4832183bytes,58files,SHA256 `6bb57a4f1d01872871a04c96e7510248e6cb859a1ec327b15d10056fc9ea2cef`. Source snapshots/expected transformations, semantic binding records, actual filter/TRX/reconciliation, source/compiled proofs, build/format receipts, tools, scope log and interrupted-run note.
- [pr2794-binaries.zip](pr2794-binaries.zip): 14935239bytes,12files,SHA256 `aaca8897a2b1ecade91e8d3ff932ee6151f9695e84ef7d990ba2ca8affc08098`. Baseline/head Server+Simulation DLL/PDB, actual head game and test assemblies. Baseline/head informational versions are pinned in compiled-proof.json. Binary file hashes are in the source archive.

CI **fully green** on the exact head: Verify [40041559752](https://github.com/ClankerWorldOrg/ClankerWorld/actions/runs/40041559752), all 16 jobs successful except the expected cancel-stale skip. All 24 head check runs completed successfully except that same expected skip, including CodeQL and labels. Windows installer120716175209 and final gate120717916680 pass. Actual scope and installer checkout596164b70978f5c56a8cce6d96d5b074911af430 merges c2d into835; both actual checkout logs and the final gate were personally read.

All ten timing rows120717117130 personally read:3,271 shared methods14,754s versus13,973s(+6%), mixed five boat/three market/one donation/one conversation rows; no aggregate timing warning or causal speed claim. Node action deprecation is unrelated. Compiled behavior is unchanged.

[pr2794-ci.zip](pr2794-ci.zip): 59074 bytes, 8 files, SHA256 `7f33add2d2bd622fd7b591a4c52e69a52da5ea58d72e7841f4f64ed4c4b66b99`. Final workflow/job/check-run metadata and complete scope/timing/installer/verify logs. The source archive above remains the immutable local evidence snapshot; this separate CI bundle completes it.

Area102C#files23,793->23,615lines,178fewer(0.75%); original20% target4,759,still4,581short. Issue2559 stays open; protected2296untouched. This evidence branch is not a product PR and must not merge to main.
