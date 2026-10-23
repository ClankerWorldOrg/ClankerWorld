# Goods and orders cleanup, Codex session 33837055

Draft PR #2783, Refs #2553. Independent base493e24c4f82e8bcb917d871449f6baf8e19a514d; head3ee90410ab27731f8e02ef2218f58126b8b6cbfb.

Source +0/-395 across96files, all within the nine issue folders. Tests/docs0. The233-file area is14,431→14,036lines; original14,452→14,036 means416removed cumulatively,2,475short of the rounded20%goal2,891. Further cleanup remains; this is not an exhaustion claim. Protected tracker2296 is not edited.

The source audit enumerates144private WorldRuntime members in the area, binds references across the full Simulation source and linked Shared files, and checks test/tool/script name literals. Its only uncalled private root without an external name seam is RecheckCartCargo, a3-line unused wrapper around the existing goods query. The baseline compiled assembly confirms no same-module incoming use. CartCargoRequest and CartCargoLots remain unchanged and live.

Compiler CS8019 identifies396unused imports. Files touched by any of the nine open PRs at claim time are excluded entirely, leaving392selected imports across95files and four held imports. The helper is in a separate96thfile with no open-PR overlap. No live target, argument, check, assertion, field, property, type or signature is edited. All source imports are removed by exact source-span text; no unrelated trivia or directives are rewritten.

An independent Roslyn proof reconstructs only the declared method/import removals against a pristine base checkout. All remaining ordered tokens in all1,033simulation source files are exact. The ad hoc diagnostic compilation lacks the normal regex source generator and records its single known CivicTickText CS8795 diagnostic; fresh real baseline and full dependent Release builds pass with zero warnings/errors (head152.35s).

Standard unchanged compare-tick-equivalence.sh uses clean actual base/head, default seeds, ticks and generated/legacy modes. All68checkpoint-byte/event/observation frames match. Pinned native base/head assemblies have50,348→50,347method bodies and the sole removed target is absent from the head. Pinned actual focused-test-output Simulation DLL also has50,347bodies and no helper. Actual local test binaries were compiled before the first source commit, so informational commit metadata can name the parent; they were compiled from the exact recorded source deletions, not patched binaries. Clean exact-head CI supplies independent commit-bound verification.

One normal focused run passes all25cases,17unique methods in7wholeclasses, zero failures/skips. Scope includes goods query/writer/command and typed-storage architecture boundaries, goods query/vessel runtime and inventory command behavior. Full suite is delegated to exact-head CI; no product tests are edited or added. Formatting verification, stale-text search and diff checks pass. RecheckCartCargo appears in no unfinished/missing/undecided documentation sentence; no docs or changelog changes are needed. No manual gameplay or performance claim.

Exact-head Verify39968894032 is still running at this evidence snapshot. Personally read scope job120465980937 checks out3388fb5db45c595597a219f9a69cf2ab85c3f753, merging exact head3ee90410ab27731f8e02ef2218f58126b8b6cbfb into mainb38f50029a5bb16f2964f7b6183fb56be255b606. Final CI receipt/timing logs are added separately before Ready.
