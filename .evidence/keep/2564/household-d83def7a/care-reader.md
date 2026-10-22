# Clinic, care and food-capacity audit

Codex session d83def7a/care-reader. Read-only test-audit second reader/scout under parent's #2564 claim, not an independent-session merge review.

Pristine source read at /workspace/ClankerWorld-2564-continuation-base, main91f4b0c7. No source changes, tests, builds, GitHub writes or branches. Evidence-only file saved in head worktree with permission. Current household batch was not revisited.

## Baseline catalog and conclusions

All four complete files, rows and helpers read. All four classes nonpartial; no additional class declarations elsewhere. Counts static source-derived, not executed TRX results.

| File | Lines | Cases/methods | Assert callsites | Decision |
|---|---:|---:|---:|---|
| CareProductionTests.cs |417|7/4|77|Retain all rows; dead branches already in#2708, raw setter#2740. Additional possible shared At/Stock support below.|
| ClinicTradeCareTests.cs |227|1/1|71|Retain unique paid-manufacture/trade/permission/treatment/reload proof; raw setter#2740. Additional duplicate PutAt/AddStock support below.|
| FoodCapacityRoutingTests.cs |347|10/4|70|Retain all rows/native route and capacity inputs; impossible Adult arms owned#2712.|
| WaterJugReturnSelectionTests.cs |210|10/4|42|Retain six positive selection rows, two refusals and independent fixture-copy/allocation proofs; raw setter#2740.|
| Total |1201|28/13|260|No case/row deletion recommended.|

## Additional support-only candidate, conditional on open-PR compatibility

CareProductionTests.At and ClinicTradeCareTests.PutAt are ten-line identical helpers after only method-name and local-variable normalization (`previous`/`oldPosition`). CareProductionTests.Stock and ClinicTradeCareTests.AddStock are three-line identical after method/parameter-name normalization (`storage`/`building`). Verified exact normalized source equality for both pairs.

Smallest extraction: expose existing Care.At and Care.Stock as internal static; qualify Clinic's three PutAt and four AddStock call sites as CareProductionTests.At/Stock; delete the two Clinic duplicate helpers and adjacent blanks. No new helper, class, wrapper, mode or cache. Nominal net15 fewer lines if formatter leaves existing statements on one line. A static import instead costs one line and saves14. Exposing only At and qualifying its three calls saves11 nominal lines.

Preservation checklist:

- At first reads the same actor's original Position with Single; maps the same state.Agents array in the same order; actor receives supplied Position, FullnessBasisPoints9000, LastDecisionContext null; any other actor occupying target receives the original actor's position; all other fields/actors unchanged; exact ToArray allocation and record-copy shape retained.
- Stock calls the same raw state replacement with InventoryAuthority.AddLot and exactly the same owner/kind/quantity/storage; no StockHoldTestState.Custody substitution.
- All Clinic arguments are positional; no named parameter compatibility hazard.
- Clinic's third PutAt call is followed by its existing `with { Agents = state.Agents.Select(...) }`; preserve that expression and evaluation exactly even though it overwrites the helper's Agents value. Do not opportunistically remove this call/check/allocation.
- No static fields or static constructor on Care (only constants); static access does not construct its ITestOutputHelper instance. No output/provider/lifetime dependency introduced.
- Care has four At calls across all four methods and many Stock calls; Clinic has three At/PutAt calls and four Stock/AddStock calls in its one Fact. No external private-helper consumer, no partial class.
- All seeds, placement ordering, actor swaps, native construction/production/trade/treatment durations, reload/disposal scopes and assertions stay exact.

Both helper pairs were introduced together in4d79b3fd (#752), original physical Clinic production/trade/care feature. Later rename9c920264 preserves normalized semantics. At has no special Clinic-only behavior. Care's placement helper intentionally sorts near House after4933cd78 while Clinic's placement enumerates unsorted; do not share those different placements or alter their geometry. Preferred/Choices providers also differ: Care diagnostic Decisions/Offers and Clinic TreatmentCandidate/Offered are live; do not merge them.

Conditional availability: #2740 removes WithInventory immediately after Stock/AddStock and before Clinic.PutAt. Proposed helper deletions do not intentionally remove that owned raw setter, but are adjacent to its patch hunks; exact synthetic merge-tree comparison must confirm before any edit/build. If a new conflict appears, hold the corresponding helper reduction until#2740 merges. Reader did not prototype a tracked change or run merge-tree mutations. No new proposal to redo#2708/#2712/#2740.

Affected filter for proposed support sharing must select BOTH COMPLETE classes, all8cases/5methods:

`FullyQualifiedName~ClankerWorld.Tests.CareProductionTests.|FullyQualifiedName~ClankerWorld.Tests.ClinicTradeCareTests.`

Exact retained proofs:

- ClankerWorld.Tests.CareProductionTests.HouseAndTailorCutOnlyTheirOwnedOnSiteClothIntoBandagesAcrossReload (false,true)
- ClankerWorld.Tests.CareProductionTests.MedicineCannotUseRemotePrivateReservedOrBrokenVesselInputs (reserved-water,broken-jug)
- ClankerWorld.Tests.CareProductionTests.ClinicSupplyMovesTheWholeFilledJugOnlyWithActualRoomAndUnreservedContentsAcrossReload (reserved-water,storage-room)
- ClankerWorld.Tests.CareProductionTests.OrdinarySupplyWalksFromTheRealHerbPatchAndCarriesFreshWaterHomeToTheClinicBeforeProducingMedicine
- ClankerWorld.Tests.ClinicTradeCareTests.AnIllVisitorBuysActualClinicMedicineAndUsesOnlyThePurchasedDoseAcrossReload

Suggested ledger decisions are consolidate on each retained method above, with its original rows unchanged and retained_proof naming that same method. All methods remain; no removed-case credit. No production changes. Parent should independently read helpers and exact patch compatibility before accepting.

## CareProduction retained contracts/history

HouseAndTailorCutOnlyTheirOwnedOnSiteClothIntoBandagesAcrossReload(false/true): same cloth-to-bandage recipe at distinct House/Tailor workstations, actual funded Tailor placement and input reservation,8tick duration, partial-job strict reload/parallel progress, exact completed inputs/output/bytes. Keep both workstation inputs.

MedicineCannotUseRemotePrivateReservedOrBrokenVesselInputs(reserved-water/broken-jug): a live content reservation versus damaged vessel are different promise/container eligibility causes; StartProduction refused atomically, jobs empty, complete state bytes and exact quantities unchanged.

ClinicSupplyMovesTheWholeFilledJugOnlyWithActualRoomAndUnreservedContentsAcrossReload(reserved-water/storage-room): family claim versus destination capacity4 for physical family5; actor has remaining carry5, cooking reserve separate; no supply candidate/pickup, exact jug+water owner/storage/container/quantity, untouched reservation and reserve, strict reload. Keep distinct conditions.

OrdinarySupplyWalksFromTheRealHerbPatchAndCarriesFreshWaterHomeToTheClinicBeforeProducingMedicine: native jug production pays clay/fuel; starts supply chooser immediately by clearing idle context; real herb patch >1tile and reachable, actual ecology depletion, actual fill/return/stock pickup, paid medicine, skill-adjusted duration12, unchanged House cooking water claim, running production reload and exact bytes. Do not replace physical production/routes with premanufactured stock.

History4d79b3fd introduces feature;8e54e1b7 named meals/cooking reserve;56f2c5a9 (#992) removed redundant medicine positive Fact and remote/foreign/available/carrying-room rows, transferring needed duration/input assertions into native supply proof. Thus residual dead branches belong#2708. cf2c4e2d (#1423) skill-adjusted work duration. db155a30 (#2443) deliberately clears LastDecisionContext and asserts immediate decision.4933cd78 (#2479) moves placement near House but explicitly retains distant real-herb travel assertion. Existing runtime/geometry setup already intentionally shortened; no further nearer setup proposed.

## ClinicTradeCare retained contract

AnIllVisitorBuysActualClinicMedicineAndUsesOnlyThePurchasedDoseAcrossReload: funded physical clinic, two medicine batches paying herb/fuel/water at16ticks each, two distinct output lots, foreign visitor cannot produce or treat before purchase, seller physically walks4..6tiles to buyer, open and settled real barter reservations, exact2medicine-for1wood transfer and private second batch untouched, same household and no consent mutation, then admitted self-treatment spends exactly purchased dose and resumes through strict reload to completion. CareProduction's own-household production does not prove public shop transfer/foreign permission/paid dose provenance. Retain.

History4d79b3fd (#752) adds full proof;9c920264 rename only. Provider TreatmentCandidate toggling is live. Setup PutAt/state-copy/record overrides must preserve their exact order; no timing or manufacture shortcuts proposed.

## FoodCapacityRouting retained contracts

ANearerFiveUnitOrchardDoesNotHideAReachableFourUnitBerryPickAcrossReload: native planted orchard with real seed consumption, fruiting+resource eligibility calibration, closer reachable5-unit yield versus farther4-unit berry batch, reserved cargo leaves4room, exact berry ecology decrement/consumption, no orchard pick/carry failure, replay. Its orchard calibration prevents false pass from inaccessible/nonfruiting competitor; keep.

AFullCarrierFreesOneSharedServingSpaceInsteadOfTheLargerWildHarvestLoadAcrossReload: full8load with7reserved+1movable, reachable wild control, stores exactly1stone and collects1shared serving rather than freeing4wild units, exact stock/provenance/claim/replay. Distinct source-choice minimum-load rule, not duplicate orchard input.

SharedPotServingUsesExistingAgePermissionAndRealTakeThenConsumptionAcrossReload: rows Child/pot, Adolescent/pot (valid legacy config), Child/loose. Real household take-one then consume, council membership repaired after age fixture, pot parent remains and container_contents_taken exact; child/adolescent never offered adult TakeFood vessel action. Distinct permission/generation/container boundaries; keep all.

SharedPotRefusalsPreserveActualContentsFamilyRightsAndReservations: reserved child, broken parent, spoiled content, full protected personal load, foreign household each independently block shared serving with exact retained family fields, no container take and replay. None is merely another seed or equivalent value; keep all five.

Historyfd3aab1e (#779) adds food movement/capacity rules;56f2c5a9 (#992) removes Adult/pot row, explaining residual Adult branches owned#2712.9c920264 rename.

FoodCapacityTestFixture fully read (89lines). Generated8native ticks, actual first adult and House, clear weather/survival; WithInventory deliberately normalizes Custody; Restore strictly roundtrips codec; AssertReplay advances BOTH current+restored one tick and checks byte equality. Choices records all offered arrays, filters allowed prefixes and delegates deterministic selection; not equivalent to Care's first-prefix chooser. No support change recommended because consumers span31files plus their partial-class consumers; avoid broad support scope for little gain.

## WaterJugReturnSelection retained contracts

AnOfferedReturnMovesItsEligibleJugAndPreservesTheOtherFamilyAcrossReload six exact rows:

1. true,contents,false,false,-1: reserved earlier family must not hide eligible later family.
2. false,contents,false,false,-1: reversed ID ordering.
3. true,none,false,false,-1: unreserved-family control; no extra3ticks because then other jug becomes eligible.
4. true,contents,true,false,-1: full selected jug, different refill-versus-return selection.
5. true,contents,false,true,-1: actual walk first; family remains carried until home.
6. true,contents,false,false,2: exactly2room fits selected two-unit family while larger other family cannot fit.

All retain prepare/discard rollback, exact admitted movement/return event, strict reload+parallel replay, original family fields except intended ownership/storage/time, preserved other family and claim, total conservation/single event/Validate. Distinct ordering, promise, movement and exact-capacity inputs; keep.

AReturnWithoutAnUnreservedFamilyOrHouseRoomMovesNothing(all-reserved/full-house): different family availability versus destination refusal, offered list nonempty calibration, no return event, exact all actor lots/claims, strict reload. Keep.

ReturnFixtureCopiesKeepIndependentPeopleStockAndTerrain verifies deep independence of cached strict bytes, mutation isolation of people/lots/elevations and later fresh decode. RepeatedReturnSetupFitsItsAllocationBudget verifies24MiB warm-copy bound, a different property. Neither is duplicate behavior proof or dispensable as mere fixture testing.

History60b7c9bd (#1231) fixes eligible jug selection without halting world;0f7b0506 (#1917) controls hosted inputs;dd8acbfc (#2487) introduced private strict-byte cache and both independence/allocation tests. HeldPart values contents/none both live; convert-to-bool would save no lines. chooseReturn true/false both live across actor/control policies. Restore wrapper routes actor versus idle others and is reused; inlining would multiply code.

## Owners/callers and existing PR patches reconciled

Read owners: CareContent recipes; WorldRuntime.CareSupplies; WorkstationSupply full (on-site target, reserved/usable stock, family carry and storage capacity, live source reserve); Containers full (refill/return jug selection/family quantity/claims, pot food); VesselPlanning, VesselChecks and VesselActions full; relevant Runtime.Actions food source ordering/carry/minimum load/serving/consume and FoodPlans checks; MedicalCare full adapter/validation with FamilySystem treatment ownership. Runtime registrations explicitly route vessel/food action candidates through these plans. No production seam proposed.

#2708 head59239a17/base8b575331: selected Care patch specializes remaining Medicine rows and removes unreachable available/carrying-room branch and happy-path tail. No cases removed there. This reader does not claim those lines.

#2712 head3d8afaec/based6f6116b: selected FoodCapacity patch removes impossible Adult guards while preserving adolescent legacy config/council/child checks. No competing edit proposed.

#2740 headcd72b0ea/baseb50f1b27: selected Care/Clinic/Water patches import RawInventoryTestState and delete exactly their raw WithInventory wrappers. Same behavior setter, distinct from FoodCapacityTestFixture.Custody. Proposed helper sharing must survive exact merge-tree checks beside those hunks or be held.

No selected named tests are referenced by CI filters/docs; global searches found no targeted route. Parent still owns final before/after discovery, per-method retained proof, coverage, formatter, unused checks, open-PR compatibility and CI. This read-only judgment supplies no execution evidence.

## Shared fixture consumer inventory (no change proposed)

- tests/ClankerWorld.Tests/Simulation/World/AnimalProductActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Community/MentorOwnerOrderTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/BlacksmithSupplyRefreshTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/EquipmentRepairActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/FarmStockActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/FilledVesselHouseHaulingTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/FoodActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/FoodCapacityRoutingTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/FoodCargoActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/HandcartBuildMaterialActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/HandcartCargoActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/OrnamentActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/PrivateEquipmentActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/ProjectHouseholdDeliveryTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/RareMiningActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/SmithDeliveryActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/SmithGatherActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/SpoiledHouseholdDeliveryContinuationTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/StoreStockActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/ToolRepairActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/TreeActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/VesselActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/WarmthActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/WorkstationDestinationReachabilityTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Economy/WorkstationSupplyActionPlanTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Family/CaregiverCapacityRecoveryTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Family/ChildFoodCapacityRecoveryTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Knowledge/SwimmingExplorationReturnTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Runtime/SwimmingCartRepairReturnTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Runtime/SwimmingStockDeliveryTests.cs
- tests/ClankerWorld.Tests/Simulation/World/Towns/TownReleasedMaterialRecoveryTests.cs
