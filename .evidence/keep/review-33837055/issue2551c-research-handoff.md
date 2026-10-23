# Civic remaining-offer audit — Codex session 33837055

Current main835f9db1155bfb03fb741b2d5bcde9de50031316. Claim2551c on codex/2551-civic-offers-33837055, no product changes/PR. Ready#2789already covers the concrete large planning/application dispatch slice in TownSystem.Council.cs and TownSystem.VisitActionPlans.cs. Reserve those two paths. Ready#2788also changes WorldRuntime.CivicTownWorld.cs; all nine open-file maps retained.

The remaining23Offer helpers total188lines. They are not interchangeable wrappers. In particular, NonLandFiling retains its supplied allegation/evidence report; TownSettlement captures its provided Town/layout and care group; CivicProposalVote copies supplied proposal voters/votes; land-claim/land-filing text uses the offered exact tiles; CouncilBallot checks its supplied election before building an ID. A single registered-plan path would re-resolve some of those captured inputs. Skipping the early checks in favor of the later attachment check also needs proof that candidate counts/fallback generation and all intervening generator effects are identical. None of those changes is justified solely by having another later check.

The standard codec/plan/check/add helpers are only a subset; factoring their final check/add step gives much less than150net lines. Inlining the wrappers would duplicate family-specific identity construction/token rules and grow call sites. The earlier current-base audit already found no safely unused private/internal method/import batch, no exact duplicate method bodies, and only27lines of simple static forwarding. Those receipts are under fix2551b and indexed by Ready#2789's evidence. They do not establish whole-area exhaustion.

No supported independent150net batch has been established outside the Ready slice. Preserve all checks and captured-plan semantics, leave the issue open/unblocked, release this claim and take other eligible work. More whole-area cleanup may exist; after#2789merges its canonical shared dispatch may support a further offer cleanup without adding a duplicate registry. No new blocker or exhaustion claim, and protected#2296untouched.

Baseline locked Simulation Release passes; log retained. No code changed, so no additional tests/native run or product PR is warranted. This is a research handoff, not a delivered cleanup slice.

Research archive 87973bytes/8files SHA256 1d527fde1b462cef0dd2140436da953a3c69192bab71c0b09be988761ae7f210
