# Household Economy audit: second reader

Signed: Codex session d83def7a/household-reader. This is same-session test-audit second reading, not independent-session merge review.

Base: 91f4b0c7a859955211aff16182678cc0d78d84c5. Worktree: /workspace/ClankerWorld-2564-continuation.
Read-only source work: no tracked edits, builds, tests, claims, push or GitHub comments. This file is an explicitly authorized evidence-only write. Parent runs measurements separately.

## Actual-diff check

Reviewed the complete uncommitted three-file diff against the fixed base, after reading the complete original bodies/helpers and relevant owners/history. No behavioral blocker found. Known closing brace in UseCoverage is on the same line as the final Holds expression; parent will restore the newline after the active build. That formatting correction does not change behavior.

Static exact-transformation checks succeeded:

- PreparationDelivery working source equals baseline with exactly the proposed five lines removed.
- Plan working source equals baseline with exactly the sole-row Fact/input substitution and private-to-internal helper change.
- UseCoverage working source, after only the known closing-brace newline correction, equals baseline with exactly the sole-row Fact/input substitution, static import and duplicate-helper removal.
- No production file or other tracked source differs.

Observed current diff: +15/-29, net -14; expected after closing-brace newline restoration: +15/-28, net -13. Current line counts: Plan123, Preparation219, Use131; final Use expected132. Parent's earlier +14/-26 was not the actual diff inspected.

## Counts and required filter

Counts below are source-derived cases/methods, distinct from executed TRX measurements. Assert counts are lexical `Assert.` callsites, including nested calls, not the number of lines containing assertions.

| File | Baseline lines | Cases/methods | Assert callsites | Proposed final |
|---|---:|---:|---:|---|
| HouseholdBuildingPlanTests.cs |125|3/3|27|123 lines, same cases/methods/assertions|
| HouseholdBuildingPreparationDeliveryTests.cs |224|1/1|47|219 lines, same cases/methods,46 assertions|
| HouseholdBuildingUseCoverageTests.cs |138|1/1|25|132 lines, same cases/methods/assertions|
| Total |487|5/5|99|474 lines,5/5,98 assertions|

No case-removal credit. Sole theory rows become Facts with the exact same input, not fewer executed cases.

Filter must include all tests in all three complete nonpartial classes:

`FullyQualifiedName~ClankerWorld.Tests.HouseholdBuildingPlanTests.|FullyQualifiedName~ClankerWorld.Tests.HouseholdBuildingPreparationDeliveryTests.|FullyQualifiedName~ClankerWorld.Tests.HouseholdBuildingUseCoverageTests.`

Exact retained proof names:

1. ClankerWorld.Tests.HouseholdBuildingPlanTests.MissingProductiveBuildingIsOfferedWhenItsMaterialsAreInHand
2. ClankerWorld.Tests.HouseholdBuildingPlanTests.OnlyTheFarmhouseHouseholdPlansASiloAndOnlyBesideItsFarmhouse
3. ClankerWorld.Tests.HouseholdBuildingPlanTests.HouseholdHarvestMustBeCarriedFromTheFieldToItsSilo
4. ClankerWorld.Tests.HouseholdBuildingPreparationDeliveryTests.BuildingPreparationLoadIsMarkedHauledAndFreesCapacityAcrossReload
5. ClankerWorld.Tests.HouseholdBuildingUseCoverageTests.HouseholdsWorkAtTheBuildingsTheyHoldAndPlanOnlyWhatTheyLack

Global references: exactly six FamiliesForHousehold calls, three in Plan and three in Use. No indirect/partial consumers. No named CI routing of these tests/classes/helpers found. No current PR snapshot overlap in these three files; final exact merge-tree checks remain parent's responsibility.

## Candidate ledger

### Preparation bookkeeping: consolidate

File: tests/ClankerWorld.Tests/Simulation/World/Economy/HouseholdBuildingPreparationDeliveryTests.cs
Test/retained proof: ClankerWorld.Tests.HouseholdBuildingPreparationDeliveryTests.BuildingPreparationLoadIsMarkedHauledAndFreesCapacityAcrossReload. Original attribute `[Fact]` remains.

Removed only:
- Two-line SeenCandidates wood assertion with diagnostic message.
- One-line private-provider HashSet property.
- Two-line foreach population.

The immediately following retained ChosenCandidates wood assertion uses the exact same prefix and StringComparison.Ordinal. Every chosen entry is selected.Id. Every selection is FirstOrDefault or Single from the same request.Observation.Candidates; no fabricated fallback exists. No await occurs between list binding, selection and recording. Selection/response never reads SeenCandidates. Global reference search confirms the private property has exactly one assertion consumer and this foreach writer. Same-named properties elsewhere have different owners and are untouched.

Therefore chosen wood proves offered wood under identical fixture/input/effect. HashSet insertion has no side effect consumed by runtime or selection. Only redundant diagnostic text is lost. Provider Kind deterministic, ProviderEpoch0, response metadata, choice priorities, descriptions, input order and probability dictionary remain exact.

Keep all fixture inputs/native proof: probe-a, household second, actual actor/House/initial-building IDs; inventory removal, fiber/tool/axe/pickaxe, StockHoldTestState.Custody normalization, fullness9500, position/equipment/project/context/travel resets; native stone and wood harvest, capacity4 then5, exact deliveries/tree-seed carry, actual legal travel, same chooser through explicit disposal/reload, bounds160/160/1000, paid building completion, spare stored tool and strict byte-equality validations.

Owners reviewed: WorldRuntime.HouseholdPlanning.cs and WorldRuntime.BuildingMaterialPlans.cs, including material-source selection/route capacity/harvest and tool-storage prerequisites. No owner change proposed.

History: 8e7e1c7a added both offered and chosen assertions plus the same offered-list-only producer together. c2b99e1d changed physical tool tier setup;05ce74a1 household lifecycle;275741f7 introduced custody/holds;ce4a1ad4 (#2106) added retained native BuildingMaterialActionPlan/ecology/physical-lot assertions;d6f6116b (#2592) replaced only the idle provider. The redundant bookkeeping is original, not a later regression's sole assertion.

Risk: loss of verbose duplicate failure diagnostic, no unique asserted contract. Focused proof is this unchanged Fact, with all three full classes included for the batch.

### Plan sole row: consolidate

File: tests/ClankerWorld.Tests/Simulation/World/Economy/HouseholdBuildingPlanTests.cs
Test/retained proof: ClankerWorld.Tests.HouseholdBuildingPlanTests.MissingProductiveBuildingIsOfferedWhenItsMaterialsAreInHand.
Exact original row: `[InlineData(Alpha, "tailor-shop-1x1", "farmhouse-1x1", "first-town-house-a")]`.

Actual candidate replaces only attribute/signature/input expressions: Alpha remains household:first, missing tailor, already-held farmhouse, actual first-town-house-a storage. Concatenated fixed expected strings become equal literal strings at the same assertion positions. Both independent40-tick worlds, initial state reuse, raw inventory assignment, chooseIdle recorders and all assertions remain. Do not add redundant locals or change fixture to bypass prerequisites.

History: a3c98ffe introduced household planning;e88c5658 (#864) removed the other row;b2a983db (#2351) deliberately changed remaining missing-building input to tailor under Town population limits. Current exact values remain important.

### FamiliesForHousehold: consolidate

Existing Plan and UseCoverage five-line helpers are byte-identical; SHA256 cf44680171b6ff6367c907a8df4dedfd26fdcad04b12d0e31bed178d26cdeca5. Actual candidate exposes the existing Plan helper internal, adds static import in Use, and deletes Use's duplicate. No new helper/class/wrapper/options.

Three callers per class: Plan missing unstocked, missing stocked, Beta silo exclusion; Use farm, smith and each household. Every argument/execution position unchanged. Immediate ToHashSet Ordinal keeps current-world household membership and recorded offered-family translation semantics. Plan has only const fields, no static constructor/cache; importing its method creates no initializer/provider/lifetime dependency. No external/partial helper consumer exists.

Retained proofs: Plan MissingProductiveBuildingIsOfferedWhenItsMaterialsAreInHand and OnlyTheFarmhouseHouseholdPlansASiloAndOnlyBesideItsFarmhouse; Use HouseholdsWorkAtTheBuildingsTheyHoldAndPlanOnlyWhatTheyLack. Full Plan class also includes harvest method in filter.

ActionCoverageRecorder.FamiliesOfferedTo implementation read: translates actually recorded IDs into family names from supplied current content. Shared method performs the same current Society.Agents enumeration and content lookup as each original. No contract/assertion lost.

### UseCoverage sole row: consolidate

File: tests/ClankerWorld.Tests/Simulation/World/Economy/HouseholdBuildingUseCoverageTests.cs
Test/retained proof: ClankerWorld.Tests.HouseholdBuildingUseCoverageTests.HouseholdsWorkAtTheBuildingsTheyHoldAndPlanOnlyWhatTheyLack.
Exact row `[InlineData("probe-a")]` becomes Fact and exact seed literal at original NormalPathWorld.CreateGenerated evaluation site.

Keep recorder normal chooser (not chooseIdle), ticksPerDay144, CoverageTicks288, milling grain, native shortage FeedFarmTownFromAvailableStock, all household/workstation/candidate/duplicate-building checks and all using scopes. No tick reduction or nearer synthetic setup proposed.

History:9469df35 introduced normal-world household-use proof;541fcbd7 removed probe-b;4abf314b (#2457) intentionally preserves two civil days on shorter calendar;215ecf99 (#1409) adds demand setup;1c099e57 (#901) approved building variants. Exact calendar/demand inputs stay.

## Complete assigned-file catalog

All complete bodies, data and helpers read at frozen base. Counts source-derived.

| File | Lines | Cases/methods | Decision/evidence |
|---|---:|---:|---|
| HouseholdBuildingPlanTests |125|3/3|Consolidate sole row/shared pure helper as above; retain silo geometry and actual harvest transport proofs.|
| HouseholdBuildingPreparationDeliveryTests |224|1/1|Consolidate redundant offered bookkeeping only.|
| HouseholdBuildingUseCoverageTests |138|1/1|Consolidate sole row/shared helper only; retain288ticks.|
| HouseholdPlanningGoodsTests |59|3/1|Retain borrowed true/member false, false/false, true/true. Native paid-market communal stock exclusion, unborrowed control and personal-stock bypass are distinct; stock fields/reload remain.|
| HouseholdStockActionPlanTests |100|4/2|Retain selected-lot promise false/true, short/128-character identity rollback/replay/reload. Existing provider cleanup belongs to ready#2644.|
| ProjectHouseholdDeliveryTests |338|5/4|Retain full-House delayed capacity/resumption, House vs workstation delivery, active claim preservation and foreign-house retarget refusal. Both Prepared parameters live; no removal.|
| ProjectAssistanceSupplyTripTests |122|2/1|Retain real native animal supply promise false/true versus assistance. Partial AnimalPipelineTests; all its indirect consumers would be required for shared support change.|
| SpoiledHouseholdDeliveryTests |248|2/1|Retain reassign false/true public lifecycle boundaries after actual two4-unit pickups spoil; actual walk/reload/all quantity/claims preserved.|
| SpoiledHouseholdDeliveryBuiltInTests |75|1/1|Retain unfiltered built-in chooser admission after real pot spoil; not equivalent to directed DeliveryChoices.|
| SpoiledHouseholdDeliveryContinuationTests |176|3/2|Retain continued useful goods delivery without repick; mixed pot usable/spoiled sibling controls whole-family vs grain extraction.|
| SpoiledHouseholdDeliveryFamilyTests |181|2/2|Retain atomic pot+child family recovery/rollback and usable-child claim protecting family while another shipment recovers.|
| SpoiledHouseholdDeliveryOrderRecoveryTests |253|2/2|Retain urgent order's recovery then food receipt/credit and independently held-model request's urgent local recovery.|
| Total |2039|29/21|No complete test/row removal recommended.|

HouseholdPlanningGoodsTests history5dd29d55 (#1894): planning via actually available goods. HouseholdStockActionPlanTests a98881e9 (#1936): stock-bound selected shipment; e9a2367d (#1925) and275741f7 promise lifecycle.

ProjectHouseholdDeliveryTests history71380e32 (#1810) goods consolidation and275741f7 holds. Its FullHouse test has includeAxe true and delivery preparation false, deliberately different ballast/carry setup; active-project two destination rows use both toWorkstation inputs. Native claims/corrupt pointer refusal remain unique.

ProjectAssistanceSupplyTripTests history627fcc60 (#2107) retained assistance promises and275741f7. False row actually delivers household grain and progresses same plan; true row reserves carried supply under AnimalSupplyActionPlan and must not fulfill assistance. No equivalent-input row.

Spoiled partial class spans all five named files,933lines,10cases/8methods. Core/family/built-in/continuation historyfd3aab1e (#779); OrderRecovery history6a2f1084 (#1913), typedIDs bd14f414/ce1bb7a5. Whole-family helper mode is live in pot cases; DeliveryPolicy haul-before-recover and provider Offers are consumed across partial files. RestoreWithBuiltIn raw-state vs normal strict codec restore is deliberate. No forwarding helper removal recommended without these full consumers.

## Current PR overlap and setter semantics

Read .evidence/2564/open-pr-files.json:111 current open PR file lists/patches. No three-candidate-file overlap. #2644 already changes HouseholdStockActionPlanTests provider. #2740 introduces raw inventory setter with seven wrapper removals;#2722 reuses custody-normalizing setter in four separate classes. Raw state replacement and StockHoldTestState.Custody normalization remain semantically distinct and must not be interchanged. Other per-file retain judgments propose no competing tracked edits.

## Validation limits

This reader ran only source/history/reference/static transformation inspection. Parent baseline discovery reports7396cases3295methods; this is not a reader-executed measurement. Scoped baseline/head proof execution, production identity, coverage line/branch losses, formatting, unused support checks, merge-tree and CI remain parent responsibilities. Recommendation is conditional on those gates and correction of the known brace newline. No timing/retry or behavior changes proposed.
