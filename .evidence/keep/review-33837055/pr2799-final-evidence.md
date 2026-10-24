# PR #2799 final evidence

Author: Codex session33837055. Base `835f9db1155bfb03fb741b2d5bcde9de50031316`; head `dc7b8d8d05331ed22509b67d4a337178ab13cba5`.

Source+1446/-1620,174net fewer across37paths. Consolidate34unheldWorldRuntime partial files into8feature files (26fewer C#files), containing138unchanged members. Largest resulting file531lines. Tests, assertions, architecture budgets, docs, scripts and workflows are byte-unchanged. Existing allowance/doc paths remain valid; the expansion engine source remains unchanged. No overlap with17fresh open file maps or future patch references to deleted filenames. No dependency on waiting code.

Actual-input compilation before/after uses1047baseline files/167references/C#14/actualdefines+generated source and has0errors. Exact source method signature-group multiset (partial declarations retained), state/ctor/operator/event declarations, comments and simple-name symbol bindings remain unchanged:9148methodgroups/4648state declarations/1988comments/13765bindinggroups. No Root fields with runtime initializers move; nested types move intact. Full root format/verify on the final source passed; all8formatted files are byte-identical to the preview, with0changed tokens/comments/whitespace. Only37planned source paths changed; no deleted-file references. Baseline locked Release0warnings/errors13.74s; full locked dependent restore passed before formatting.

Clean native comparison passed89.67s:68frames,2seeds,generated+legacy,16ticks+initial frames each. Checkpoint bytes/events/observation digests match exactly. Both actual run.json receipts name835/dc7 and empty working trees. The actual native DLL/PDB were pinned and are byte-identical to the full-build actual test copies. Full dependent Release passed0warnings/errors61.22s.

Existing focused execution passed179cases/92methods/40classes,34whole and6partial (BuildingExpansionTests,ToolMakingRequestTests,ShopTradeTests,HandcartRuntimeTests,AnimalPipelineTests,PhysicalKnowledgePipelineTests),0failures/skips179.88s. Raw TRX actual FQNs exactly equal planned scope. Includes unchanged architecture guards. Full suite delegated to CI.

Compiled inventory passed:0user-method additions/removals, all nongenerated state fields unchanged, all50166method bodies retained (including generated bodies), Identity.IComparable retained. Only compiler-generated RegexGenerator file-local F+64hex path hashes are normalized, preserving signatures/counts with collision guards. Actual simulation SHA256`a239f4335746b900800b409a7a499765c09715aa3fbc44550805fff11de57e0e`. No product writers remain.

CI40058320101completed success. All16jobs/24headchecks complete/success except expectedcancel-stale skip. Actual scope/checks/installer checkout`c023e4b8b5c5c893eef6219f8367ba84a94f4997`mergesdc7into835;code=true/tests=true. Scope,checks,installer120768526302 and finalverify120769683633personallyread;finalgateexplicitlyconfirms scope/checks/tests/installer success. EightRelease shards,Windowsdocumentation18cases/storage98cases,CodeQL/labels passed. All10test-times120768815778rows personallyread:3271shared cases14387s vs13973s mainmedianlast3green (+3%); report says about-a-tenth same-code variation. Sixboat/onegrownmemory/onelandevidence/onereleased-materialrecovery/oneMarkethistory row; no aggregate performance warning or causal claim. No manual Windows/game playtest performed. Independent review/merge pending.

Area original9395/main8914/head8740(92files):655cumulative fewer,1224remain toward1879-line target. Refs#2552; waiting PRs excluded; protected#2296untouched per owner.


Archives:

- [pr2799-source.zip](pr2799-source.zip): 518580bytes/120files;SHA256`1ad594bbc9449ea45989d521f1c3ae08377615b2bbaf2b0caf87f2fa02f73bf1`.

- [pr2799-binaries.zip](pr2799-binaries.zip): 8879623bytes/6files;SHA256`a7b59428fb472d4b58eb998d155d5472a69e998a063524227ea8ebf5e033167e`.

- [pr2799-native.zip](pr2799-native.zip): 12950354bytes/413files;SHA256`3d1fc4a90a5d810afe3858a1f7bcd1d7b450d5719ebdf0163edee5354d39db76`.

- [pr2799-ci.zip](pr2799-ci.zip): 139395bytes/11files;SHA256`c8ad184a4cca8ba8709ca8b460b1a97193e36ce0a7e327fae0824fa9b3758693`.
