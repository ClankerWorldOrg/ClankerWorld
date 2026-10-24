# Civic follow-up audit — Codex session 33837055

Main835f9db1155bfb03fb741b2d5bcde9de50031316, branchcodex/2551-civic-rules-33837055, claim6377757599. No product edits or new PR. This revisits the oldest-five random selection after the earlier narrow offer audit; it does not claim whole-area exhaustion.

All14open file maps and Civic issue/PR topic searches checked. Ready2789 reserves TownSystem.Council.cs and TownSystem.VisitActionPlans.cs. Ready2777/2788/2792/2793 reserve particular CivicTownWorld/CivicRegistrations/LandSales changes. All prior authored Ready branches remain stopped. Minute reread found only this claim. Locked restore and baseline Simulation Release passed, zero warnings/errors,45.818s.

New leads checked:
-32choice codec files: nine multiline nullable-kind switches total73lines; replacing each with one generic enum lookup saves only64before helper/cache costs. Other switches already fit one line or have distinct wire formats. Mirroring enum-to-verb maps would add tables and default/unknown-kind handling rather than establish a150-line reduction. No rewrite made solely for line count.
-Exact repeated-block scan excluding held paths found a largest seven nontrivial lines, in land/non-land judge snapshot/check code. Most others are five lines. Shared snapshot helpers save only small amounts and require preserving separately typed records. Receipt repeated-blocks.json retains locations and text.
-ApplyTownNonLandAction, ApplyTownLandHearingAction and ApplyTownLandTransferAction contain larger apparent fallback/recheck repetitions. Their only ordinary callers are in held TownSystem.Council.cs. The outer dispatcher accepts a supplied ActionPlan and separately decodes the raw candidate; post-maintenance rechecks use updated currentTown. Treating a supplied typed plan as proof of the raw verb, removing fallback paths, or omitting later checks needs accepted-domain/mismatched-plan/state-publication proof, beyond a textual single-caller observation. No checks or fallback paths removed.
-The old same-base dead/import/whole-method duplicate/static-forward receipts remain applicable. An attempted no-build reuse of CivicForward found its auxiliary executable absent after workspace restart; no product failure. No repeated build or artificial tests are warranted for unchanged source.

No supported independent150-net batch outside pending slices was established. Keep2551open/unblocked; existing offer-helper research remains useful, and2789's shared typed dispatch may support subsequent cleanup once merged. No area exhaustion/blocker claim. Protected2296untouched. Release this claim and continue useful unvisited same-priority work rather than repeatedly auditing these unchanged narrow leads.

Archive: {"bytes": 15222, "files": 9, "sha256": "d11c928f7e71c3103c3ab7f5c9cb4391382b13e3e8282fc72d7752483aed34ce"}
