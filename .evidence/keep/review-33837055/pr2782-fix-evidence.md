# Economy cleanup, Codex session 33837055

Draft PR #2782, Refs #2552. Base bf7cba09a5c93be89c70fb3853d28d2dc86d203d; final head 7e8e92b1f0bac5df2cda9c4fea046878660436f3.

Source +114/-320 = 206 net removed across 54 files. Economy itself +76/-282 = 206 net removed, required caller replacements elsewhere +38/-38. Tests/docs unchanged. Original issue baseline 9,395 lines; base 9,120; head 8,914 across 118 files. Cumulative reduction 481, 1,398 short of the 1,879-line area goal. No claim that the area is exhausted.

The bound source audit found four unused methods, four unused private constants and 31 CS8019 unused imports. The four method roots have no baseline same-module IL callers. Other live checking/apply methods remain: in particular the checked town-stock and rare-mining routes retain their checks. Constants are not saved fields.

Fifty private one-operation forwarding methods are replaced at 116 bound call sites. Targets are static, nongeneric methods with identical parameter types and return types. Every original expression is evaluated once in its original parameter order. Optional defaults are made explicit with the same null, bool, string or integer values; int.MaxValue keeps its readable spelling. No ref/out/params, attributed parameters or conditional methods are transformed. Five port getters allocate adapters whose primary constructors capture only the same runtime; they have no field initializers, explicit constructors or base effects. Creating the adapter before the original arguments captures no state snapshot. Two useful documented methods, HandcartToBuild and SpareCarriedQuantity, remain.

The independent post-edit Roslyn proof replays the planned removals and exact invocation receipts against pristine base source, then compares ordered token streams of every retained simulation source file. All 1,032 retained files match after only those changes; the sole deleted file is the now-empty DirectBarterPlans partial. All 116 replacement receipts bind to their intended system-method signatures. The ad hoc compilation intentionally lacks the regex generator implementation and records only its known CivicTickText CS8795 diagnostic; complete actual dependent builds pass without warnings or errors.

Native comparison used the unchanged repository script, clean actual base/head worktrees and all default modes, seeds and ticks. All 68 checkpoint/event/observation frames match. Pinned native DLL/PDB pairs were scanned independently: base has 50,359 method bodies and all 54 removed targets; head has 50,305 and none of those targets. No target operation body, saved state, event or schema is changed.

Open-source overlap snapshot: #2781 only removes an ExplorationGoals import; #2780 changes different CivicTownWorld and MarketWorld members; #2775 changes a different CommunityWorld member. Read-only merge-tree probes pass for those three immutable heads. #2774 changes the adjacent ProcessBoatTransport call and conflicts in the Tick hunk; compose by retaining both direct TransportSystem operations and the direct MarketSystem maintenance call. These are independent branches from main. Current main at draft check merged cleanly. No ready branch was edited or watched for CI.

Stale-text search returned how-it-works recipe progress and handcart task-credit paragraphs, plus two historical proposed-art notes. Their statements are not made false by this private-code cleanup; no documentation correction is needed.

Final focused test, exact-head CI and evidence archive results are recorded separately when complete. No manual game playtest or performance improvement is claimed.
