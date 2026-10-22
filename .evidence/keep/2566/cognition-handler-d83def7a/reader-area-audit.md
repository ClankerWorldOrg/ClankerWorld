# #2566 reader area audit

Author: Codex session d83def7a/reader-area-audit. Base: 8b575331. This is the author's required second-reader/scouting work under the parent's area claim, not an independent merge review.

All files below were read completely, including theory rows, helpers and shared partial-class consumers where relevant. Ownership, overlapping tests, relevant history and the open-PR snapshot were consulted for concrete candidates. This document preserves the accumulated readings across this session; a candidate is a static recommendation, not authorization to omit the test-audit measurements, conflict checks or final review. I did not edit tracked source, run builds/tests, make GitHub changes or claim an issue. The parent authorized this evidence-only file. Retained proof must be reconciled again before any later deletion.

The frozen current checkout has the three-file cognition handler support change for #2725. The open-PR snapshot is in the tool-repair worktree at `.evidence/2566-tool-repair/open-pr-test-files.json`. The parent independently read the other World-root files and recorded them in `parent-world-audit.md`.

## Earlier full World readings

| File under Simulation/ | Decision and evidence |
| --- | --- |
| World/Content/ConcreteMealTests.cs (653 lines) | Retain all cases. Simple-meal inputs differ; house/restaurant custody and workstations differ. Missing ingredients, interrupted projects, full house, shared jug, cold hearth, dead-member demand, full harvest journey and exact cooking order are separate inputs/assertions. Native caches already shared by 4a06fd62 (#2009). Partial-class consumers use helper options. |
| World/AnimalPipelineTests.cs (651) | Retain. Species, owned/wild death, paid/free transfers and recipes differ. Native paid-yard cached fresh-decode fixture came from cdcebffe (#2467); CreateYard(seed,ticksPerDay) options are live. Avoiding repeated At evaluation would add support rather than save lines. |
| World/HandcartRuntimeTests.cs (641) | Retain all cases: food thresholds/ownership, condition80 clamp/100 exact/199 truncation/zero, craft/cancelled build, blocked travel, breakage, inheritance and hauling differ. Tiny rejected standalone option: broken-cart fixture creates condition0, sets10000, loads, sets0; start healthy and load/break could save 1–2 lines but loses incidental broken AddLot execution. Parent explicitly declined pursuing it. AddLot event records ID/kind/quantity, storage history records storage/quantity; no distinct entering-runtime state was identified. |
| World/WorldMapRulesTests.cs (452) | Retain river widths, legacy/layered, seams, narrow widths, live elevation and north/south. Ten repeated fresh-local A* tie-break iterations could collapse, and one vegetation layer is unused by assertions, but neither is a useful independent batch. Layer removal also changes layer-presence input. Relevant edge-rule history 19a9a54b (#2370). |
| World/MountainMassifTests.cs (342) | Retain crest/count/connectivity/resources, client hill parity, replay and old-save byte proofs. Helpers intentionally differ: eight-neighbor unwrapped versus four-neighbor component labeling. 53567c1b (#707, closes #683). |
| World/HouseToolsBehaviorTests.cs (335) | Retain tool selection/reservations, all five production obstruction inputs and repair of each obstruction. Repeated successful reserved-job assertions are coupled to each repaired obstruction. Shared native eight-tick setup already exists; placement helper overhead consumes savings. Relevant history includes d6dfe2b7. |

The choosers/advance helpers in the first three files differ in priority, probability shape, diagnostics and per-tick validation; do not merge them merely because they advance a world.

## Cognition: 17 complete files

Paths are `Simulation/Cognition/`.

| File | Decision |
| --- | --- |
| CognitionTests.cs | Retain behavioral proofs. RecordingHandler support-sharing candidate accepted; see actual-diff review below. |
| NeedWordingComparisonTests.cs | Retain report fields/settings/counters/categories, real adapter, formats, paired outcomes, bounded paid calls and reload. Opt-in report is documented functionality, not dead code because ordinary CI skips it. |
| CognitionRuntimeTests.cs | Overlaps #2613; no competing one-row/dead-arm change. |
| ModelNeedWordsTests.cs | Retain all cutpoints, defaults and unknown conditions; only RecordingHandler sharing accepted. |
| ModelWorkHarnessTests.cs | Retain stages, actor keys, registered-versus-entered cancellation, failure wrapper, paired ticks, identity deadline and timer-disposal race. The 64 attempts ×4096 timers are deliberate regression pressure. |
| ModelRequestWordingTests.cs | Retain personal/Jev protocols, delayed body read and recipe-owner privacy. Every mode is used. |
| MemorySummaryProviderTests.cs | Retain attribution, both protocols, choice/refusal and provider restriction. Only handler sharing accepted; keep PersonalProvider guard. |
| ModelThinkingTests.cs | Retain normalization/default/accepted/rejected values, HTTP omission, host client and malformed usage. |
| CivicNonLandProviderTests.cs | Retain malformed shapes, missing versus JSON null and sibling poisoning. |
| OpenAiDecisionsTests.cs | Retain array protocol, missing key versus cancellation precedence, refusal, duplicate, unoffered and bad_score failures. |
| CivicBallotProviderTests.cs | Retain cases. Tiny optional support simplification: sole-row MalformedNonNullBallotStillFailsProviderParsing(string ballot) could become a Fact inlining `"\"candidate:1\""`; saves roughly one line, not a useful standalone PR. |
| CivicLandClaimProviderTests.cs | Retain absent/null/empty, coordinates, malformed object/type/int bounds and >64 size. |
| CivicLandHearingProviderTests.cs | Overlaps #2647; no competing change. |
| ProviderResponseBodyTests.cs | Retain declared/streamed/misreported limits, exact maximum and cancellation. |
| ModelTextBoundaryTests.cs | Retain invisible formatting, scalar lengths, assignment and serialization. |
| ProviderHttpExceptionTests.cs | Retain remote secret-text removal and read cancellation. |
| ModelFacingWordingTests.cs | Contains shared RetiredWording whole-word matcher; retain, not an empty obsolete suite. |

Relevant history: need wording 962b4be7 (#696), documented in docs/development/need-wording-comparison.md; memory summaries dd2e80ea (#1528); boundary control 7a5783d9 (#1909); work clocks 201d90ca (#1919); provider exceptions bdaf50ee (#2419); prior ModelNeedWords audit e88c5658 (#864); many files renamed by 9c920264 (#1797).

### Accepted actual handler-sharing diff (#2725)

Three files, tests +3/-24, net -21. CognitionTests.RecordingHandler becomes internal; ModelNeedWordsTests aliases its existing RecordingHandler name to that class; MemorySummaryProviderTests aliases Handler to it. Both exact local copies and newly unused System.Net usings disappear. Test bodies, attributes/rows, reply strings and client/disposal scopes are unchanged.

The existing shared handler additionally reads request.Method and request.Headers.Authorization. All new consumers supply fixed valid synthetic bearer strings; these read-only typed properties add no body parsing, logging or validation behavior. Content.ReadAsStringAsync still takes cancellation; HTTP200 and default StringContent UTF8/text/plain remain exact. Each test creates a fresh handler/client, with no static initializer, type-name consumer or discovery dependency.

Keep MemorySummaryProviderTests.PersonalProvider: it has LLM kind, epoch1 and throwing DecideAsync. ScriptedProvider has equivalent current metadata, and current IssueRequest/ApplyResponse do not call DecideAsync, but replacing it loses an accidental-invocation guard. Parent accepted this retain decision.

Parent's baseline/head reports: all 44 cases/22 methods, no skips, identical actual production line/branch identities. I did not run those checks. Actual-diff signature: d83def7a/cognition-handler-reader; not independent merge review.

## Kernel: 12 complete files

Paths are `Simulation/Kernel/`.

| File | Decision |
| --- | --- |
| InventoryAuthorityTests.cs | Overlaps #2674; do not compete on one-row/dead-state support. |
| InventoryContainerTests.cs | Retain family movement, borrowing, storage/ground, reservation, refill/capacity and broken-vessel distinctions. |
| InventoryHoldTests.cs | Retain purposes/exact holders, family cross-holds, prior reservation/new promise, three vessel wear/repair kinds, partial/full detachment and native hold serialization. Native market/yard/delivery/borrow fixture branches are live. |
| InventorySpoiledContainerTests.cs | Retain physical withdrawal versus consume/reserve/trade prohibitions, locations, failed checks, sibling claims and refill. |
| DeterministicMovementTests.cs | Retain occupied/terrain shoulders, contention/wait/order, swaps/cycles, wrapped diagonal and capability/transport cache epochs. |
| InventoryCustodyTests.cs | Retain partial/full transfer, reserved remainder, storage history, borrowed broken return and malformed destination. |
| TypedInventoryCompatibilityTests.cs | Retain spelling/domain admission and settlement versus every saved codec/null ID boundary. |
| InventoryHandcartTests.cs | Retain partial capacity/reserved remainder, owners/borrowers, broken/spoiled family transfer and reservation travel/unload refusals. |
| InventoryEquipmentTests.cs | Retain per-unit wear/repair, material owner/purpose/expiry and later-input-failure atomicity. |
| InventoryIndexTests.cs | Retain complete index lifecycle, old snapshot identity and family/reservation/storage/inbound indexes. |
| KernelContractTests.cs | Retain exact NFC/NFD/long-seed PCG vectors, stream separation and allocation bound. |
| InventoryStoreTests.cs | Retain detached preparation publication, invalid quantity and duplicate IDs. |

Relevant history: holds 275741f7 (#1887), daf2263a (#1857); preserved IDs bd14f414 (#1893); store centralization cad3fb21 (#2243); PCG allocation de947141 (#2498).

## Society: 11 complete files

Paths are `Simulation/Society/`.

| File | Decision |
| --- | --- |
| AgentLookupAllocationTests.cs | Retain count2/count8 allocation budgets and verified index sizes. 5167e755 (#2465). |
| ChildChosenNameTests.cs | Retain biological surnames, canonical duplicate, wrong surname, placeholder, dead parent, owner naming, last allowed surname guard, infant-to-child retry saved queue and naming closure. 20b82eef (#1736), waits ff8b1b2e (#1902). |
| DeferredPlanAdmissionTests.cs | Retain stockAvailable/order combinations, missing action plan, wrong provider epoch and planned/unplanned malformed fallback. Scheduler partial helpers are shared/live. 726fccbf (#1961). |
| MultiHeirWillCustodyTests.cs | Retain property locations, vessel family, communal fallback, living/dead custody, frozen-allocation corruption, heir-death fallback and final-words privacy/archive255/256/300. Tiny optional cleanup: Locate's ground arm has no caller (house/custodian only); not standalone. |
| NamingStateIntegrityTests.cs | Retain live/dead uniqueness, placeholder naming-closed versus chosen, corrupt world/society/runtime codecs, missing fields, old formats and schema bytes. |
| PartnershipFamilyUnitTests.cs | Retain four proposal closures, former-caregiver new partner and actual120/121 expiry. Already slimmed on current main by 8b575331 (#2593); no extra useful candidate. |
| SchedulerDeferredObservationTests.cs | Retain guidance versus new choice ×reload, reverse/remove legal choice, older retry trigger, naming retry versus first attempt. Helpers also used by DeferredPlanAdmission. |
| SocietyAgingOverrideTests.cs | Retain day boundaries, varied founders/adult arrivals, death curve, calendar/persistence/biological clocks, accelerated newborn/mortality and minor old-role gate. Tiny optional sole-row UnsupportedClockRatesFailClosed(0) -> Fact; no distinct case removal. |
| SocietyDeathReservationIntegrityTests.cs | Overlaps #2674. Completed receipt surviving death differs from releasing an unused claim. |
| SocietyMultiLotBirthTests.cs | Retain cooking outputs/pot, retry changes, failed final contribution types, four partial claims, malformed command and legacy receipt. |
| SocietyTests.cs | Overlaps #2683; no competing change. |

## Direct Simulation: three complete files

| File | Decision |
| --- | --- |
| DerivedFactoryTests.cs | Retain fresh snapshot allocation, factory failures, cached identity and recompute-every-hit. 7e19807d (#2502). |
| DerivedIdTests.cs | Retain short/long exact hashes, Unicode/long prefix, URL-safe spelling and numbered full-original identity. 7f60ec75 (#1720). |
| IdentityTextTests.cs | Retain JSON/dictionary bytes, strict parse versus create, scalar lengths0..4, normalization and suffix policies. 350124d0 (#1851). |

#2635 in the snapshot changes five World/Runtime/Derived files, not these three root files.

## Content: six complete files

Paths are `Simulation/Content/`.

| File | Decision |
| --- | --- |
| ContentDefinitionTests.cs | Candidate support specialization: Building tags/costs and Recipe workstation-object parameters are never supplied. Remove options/coalesces, preserve exact kitchen/wood1 arrays and workstationBuildingId. No test change. |
| ContentGovernanceTests.cs | Candidate support specialization: all Package versions are1.0.0. Private Definition forwards constructor fields and has one caller; inline it. LocalId/DisplayName may become two equal strings instead of the same referenced string; no identity/allocation assertion exists. Preserve lifecycle event order and version strings. Optional five-line two-package Propose loop can become two calls (three lines saved), not essential. |
| ContentIdImportTests.cs | Retain definition/resource/package512/513 boundaries, rejection atomicity and codec. |
| ContentPackageManifestCodecTests.cs | Retain canonical byte ordering versus pretty-JSON refusal and registry digest corruption. |
| ContentStorageLimitTests.cs | Retain six limits, full registry/removal capacity/history and persist-before-live publication. |
| WorldAssetReservationTests.cs | Candidate dead-arm removal: two switches have cache arms, but rows only durable/gpu. Replace with the same exercised branches, preserving values, diagnostics and atomicity. No row removal. |

Parent confirmed the coherent three-file Content support candidate for consideration after #2725 is ready; estimated 15–20 fewer lines. Relevant prior pruning 541fcbd7/5c2562c5. ContentDefinitions constructors copy exact inputs. WorldAssetReservationLedger storage/cache/GPU charging remains meaningful; this proposal does not remove a charging test.

## World/Content: 29 complete files

ConcreteMeal, WarehouseContent and OrnamentProduction reuse earlier complete readings. Others were read in the later lane audit. Open-PR overlap does not itself make a whole file unavailable, but no competing changed lines were proposed.

| File | Decision |
| --- | --- |
| BlacksmithContentTests.cs | Overlap #2659. Retain delivery, ore, collection, fallen wood and borrowed repair. |
| BuildingDesignTests.cs | Retain invalid name/purpose/cost, oversized and wrong package. |
| BuildingExpansionTests.cs | Overlap #2615. Permissions/current targets/materials/capacity/worker/replay inputs distinct. |
| BuildingStoreTests.cs | Retain snapshot owner/history, replacement worker and invalid atomic write. |
| BuildingVariantContentTests.cs | Retain five families, exact recipes/dependencies, native activation/quarantine. |
| BuildingVariantRestaurantTests.cs | Retain native porridge buyer/seller exact offer/payment/conservation. |
| BuildingVariantRuntimeTests.cs | Retain five output families, construction replay, active-family conflict, land pending/right and worker/workstation/input/capacity gates. |
| CivicBuildingMetadataTests.cs | Retain hall/market/stall allocation and immutable lists. |
| ConcreteMealTests.cs | Retain; detailed earlier reading above. |
| ConstructionSiteProjectionTests.cs | Overlap #2621. Thresholds/descriptions distinct. |
| ContentIdFixture.cs | Retain all options. Custom localId is used for import/recipe boundaries. Idle default LLM versus explicit Deterministic used by Economy/ContentIdWill tests. Shared consumers include World Knowledge/Towns/Runtime/Economy. |
| ExpansionBuildingIdentityTests.cs | Overlap #2615. Rebuilt same visible fields with newer PlacedTick detects stale targets. |
| FarmContentTests.cs | Overlap #2621. Flour routes, whole pot and actual household harvest distinct. |
| HouseContentTests.cs | Overlap #2621/#2668; no competing setup extraction. |
| HouseGuestIdentityTests.cs | Overlap #2615. Same/rebuilt/expanded invitation plans differ. |
| HouseholdExpansionPendingPlanTests.cs | Retain simultaneous exact different plots, pending request stopping prior action, rollback and replay. Shares expansion partial support. |
| MealDemandHouseholdTests.cs | Retain own-household3/4 threshold, untouched other stock/offer and reload. |
| MedicinalHerbResourceTests.cs | Retain wrapped sparse versus bounded abundant, regrowth and sand refusal. |
| NamedCookingOrderTests.cs | Retain all20 text/recipe/workstation variants and two-serving native job replay. |
| OrnamentProductionTests.cs | Retain distinct production/material inputs from prior full reading. |
| PotteryContentTests.cs | Overlap #2600/#2621; all1488 lines read, no competing cleanup. |
| ProductionAgeTests.cs | Overlap #2621; all support age branches and production/crop partial consumers live. |
| RetiredBuildingTests.cs | Retain completed old buildings, uncompleted old project and old resident study without new study. Idle recording differs from other providers. |
| ShippedManifestSnapshotTests.cs | Retain catalogue replacement, nested collection mutation, per-world lifecycle and allocation bound. |
| StarterClayBankTests.cs | Retain no-site island versus connected bank, rocky mountain regression and actual host save/replay/checkpoint. Shoreline helper overhead consumes savings. |
| StarterContentTests.cs | Retain next-tick staging, idempotency and restore. Do not arbitrarily remove five native ticks. |
| TailorContentTests.cs | Tiny forwarding candidate: sole WorldWithTailorShop("tailor-flow",fiberInHouse:6) can call TailorTestWorld.Create with same args/default allowedSite=null, deleting three-line wrapper. Roughly three lines, not standalone. All gather/weave/sew/reload accounting remains distinct. |
| UsableMealDemandTests.cs | Retain broken/usable bread/porridge, no stock, order override and finished restaurant dish. |
| WarehouseContentTests.cs | Retain communal ownership, stock/capacity and collection boundaries from prior full reading. |

History: metadata 9db697b6 (#2493), c51a8585 (#2495), 19143ead (#2496); variants 1c099e57 (#901); Tailor e88c5658 (#864); starter clay 0c75e0b0 (#1897).

## World/Map: three complete files

| File | Decision |
| --- | --- |
| RiverStepValidationTests.cs | Retain widths1/2, live far-bank elevation/water kind/bridge axes mutation and recovery, both directions and costs. |
| ValidatedFootCostTests.cs | Retain borrowed elevation0..255 query sequence, reset/recovery, thresholds215/245 and refusal parameter names. |
| WorldMapDiagonalStepTests.cs | Retain narrow widths1/2, wrapped/unwrapped, cardinal/nonadjacent, negative and int extremes. |

History: 51b8d01c (#2477), 22726150 (#2505), 1e3fcde2 (#2531).

## Latest World-root assignment: indexes16–35, 20 complete files

The index is `.evidence/2566-cognition-handler/world-root-remaining.json`. Parent owns indexes0–15 and36–75. Paths are `Simulation/World/`.

| Index | File | Decision |
| --- | --- | --- |
| 16 | WeatherWeightBoundaryTests.cs | Candidate remove only accepted-rain rows `(1_000_000_000,Cold,400_000_000)` and `(1_000_000_000,Polar,800_000_000)`. Retain100 ordinary mapping and int.MaxValue overflow/truncation per climate. Billion is interior without a magnitude branch. RegionalWeights uses `(int)((long)rainWeight*(Polar?4:2)/5)`. Combined existingSnow/cooldown rows stay. Wide-intermediate fix 079d918a (#857). No measurements yet. |
| 17 | WildWaterOccupancyTests.cs | Retain open approach, blocked approaches and occupied shore route inputs. Native movement validates occupied shoulders and30-tick replay. |
| 18 | TerrainPlacementTests.cs | Overlap #2663; unused Matrix baseline belongs to that PR. |
| 19 | TerrainGridWriterTests.cs | Retain malformed dimensions/positions/kinds, duplicate+unknown error precedence, order and borrowed mutable array. |
| 20 | AnimalTripReplacementTests.cs | Retain pending/finish/cancel plus pre-replacement reload and later care clearing. Repeated At evaluation cannot save lines usefully. |
| 21 | MapRectangleValidationTests.cs | Retain errors/order/mutable tiles and Small/Medium allocation sizes; writer boundary does not replace these. |
| 22 | WildForageOccupancyTests.cs | Retain open/blocked near cells, exact depletion and24 native ticks. |
| 23 | LandFertilityTests.cs | Retain candidate attempts0/1/2 ×wrap, rainfall layers, seam freshwater versus ocean and legacy rainfall seed. |
| 24 | CoLocatedHandcartUnloadTests.cs | Retain empty-cart ordering both ways, ground/carried partial, reserved/foreign/distant and exact cargo/zero-condition/replay. |
| 25 | RoadCostPolicyTests.cs | Support candidate: replace three exact successful-result assertion copies (first body plus local AssertOpen/AssertProposal) with one private helper `(RoadRouteRequest request,IReadOnlyList<GridPoint> expected)` or array. Preserve Plan→Connected→Proposal type→RoadTiles→empty NewCrossings→empty UsedBridgeIds→Validate null; every request, Plan/Validate count/order and elevation/road-set mutation must stay. No mode switches/wrappers. Estimated10–15 lines. 62f6acd0 (#2519). |
| 26 | MilkJugReturnOrderTests.cs | Retain hasStore false/true and available versus promised/reserved/delivery differences, physical jug staying yard and milk-house route. |
| 27 | TerrainWeatherTuningTests.cs | Retain cactus desert constraints and full old/new climate/weather160-day/four-seed aggregate comparison. |
| 28 | MapLayerValidationTests.cs | Retain all four packed enums0..255, reversed tile order, last-cell mutation and optional missing height versus required other layers. |
| 29 | BlacksmithHarvestCapacityTests.cs | Retain stone/iron axes ×basket. Same unassisted wood4 but different wear8750/9000; basket yields7/8. |
| 30 | EcologyBatchValidationTests.cs | Retain bounded WeatherProfile reads/resource order, later-batch/standalone mutations, invalid resource, duplicates, IDs and max count. |
| 31 | AnimalPipelineCareReturnTests.cs | Retain care/saddle ×swim blocked5 versus fit4 and land control, full-family cargo route, supply trip/finish/reload. ad860ba9 (#1926). |
| 32 | YardSupplyQuantityTests.cs | Retain ground/personal/borrowed, jug versus grain family, distant route, remainder property/custody/destination capacity/holds. 275741f7. |
| 33 | PickupReturnCareTests.cs | Retain remembered-patient/consent medicine trip versus nonprimary caregiver infant; swim blocked4+1 versus fit3+1 and land control; actual supply consumption. |
| 34 | OrdinaryMilkCollectionTests.cs | Retain no-jug native positive, pot versus loose spoilage rate28/cadence and paid native market pipeline. Potential overlap with capacity0 considered below; preserve initial ready-product codec proof. |
| 35 | ToolProgressionRulesTests.cs | Retain tree/fallen wood, generic-tool refusal, broken/stored/delivery/nested tools, hammer/hoe tiers/wear/reservations and seven repair-material rows. |

## Exact row recommendations outside the above assignment

### AnimalMilkCapacityTests.MilkCollectionNeedsRoomForTheWholeBatchButNoVessel

Recommend delete only `(5,true)`; retain `(0,true)`, `(6,true)`, `(7,false)`. All four use the same paid-yard seed animal-milk-sale, actor/home, ready cow product2, providers/order/reloads; ballast amount is the only changed input. Carry capacity8, no equipment, mount or shop reservation. Zero gives free8 broad positive; six gives free2 exact fit with nonempty cargo; seven gives free1 and refuses the whole batch. PlanAnimalProduct captures actual lot.Quantity2. CheckAnimalProductPlan's capacity branch is only FreeCarryCapacity<plan.Quantity. GoodsRecheck for this ready product has no container/storage/carrier and checks the exact reservation. Completion relocates all2 without splitting and clears product/supply trip.

The retained rows preserve milk owner/type/quantity, no vessel/supply trip, event, stone conservation and strict reload assertions. Unique ballast5 adds no separate failure category;6 retains nonempty ballast. History a87c9d44 (#1734) replaced the old jug-selection inputs from18544ecd (#1348) with0/5/6/7 when milk became ordinary food;5's former jug-space distinction is gone.

OrdinaryMilkCollectionTests.NativeCowMilkCanBeCollectedWithoutAJugAndReloadedAsHouseholdGoods also proves empty-load native two-milk success and explicitly checks no precollection carried jug. However its seed/cow IDs/nonactor fullness differ, and it lacks the capacity theory's initial ready-product encode/decode/byte-equality assertion (it saves after collection). KEEP capacity0 unless that initial codec assertion is migrated into the retained native Fact and measured. Neither zero row is being unconditionally recommended removed. No tests/coverage run by this reader.

### MorningWeatherEpisodeTests.ImportedStormKeepsOnlyTheRemainingCivilDayAllowance

Recommend delete only `(90,50,0,180)`; KEEP `(0,50,0,270)`. Other rows are `(0,0,0,270)`, `(90,0,0,180)`, `(90,179,0,180)`, `(90,300,270,540)`. Zero-offset/nonzero raw tick uniquely imports an aged ordinary-civil storm: a faulty fix limited to nonzero offsets can escape all other zero cases and every offset90 row. The retained90,179 case has nonzero-current-day import, clamped start0/end180, cooldown180 gap, Validate, codec and idempotent Initialize at the cutoff boundary.90,0 plus MorningStormEndsAtCivilEveningAndItsCooldownSurvivesReload retain in-storm advance and cooldown;90,300 retains positive next-day start270.

Initialize first returns when already regional; otherwise calendar tick-of-day determines start Max(0,WorldTick-tickOfDay) and duration3/4day-tickOfDay+(WorldTick-start). There is no interior50 branch. Although next tick of90,179 crosses evening, inside-storm progression remains in90,0 and the full Fact. All rows originated together in a422a12f (#863), no independent90,50 regression history. Keep all InitializationAfterTheCivilStormCutoffDoesNotRestartAStorm rows0/270,90/180,359/0: distinct offsets/cutoff inputs.

## Limits and follow-up

These are read-only recommendations. Before implementing any future removal the parent must record exact declarations/rows and retained names in its machine-readable batch ledger, measure required per-method and scoped comparisons, inspect lost line/branch identities, retain all distinct assertions, check current open-PR changed lines and CI filters, and complete CI/readiness. This document does not replace those checks. The only accepted implemented support diff recorded here is the cognition handler batch; the parent controls it. No row removals were implemented by this reader.
