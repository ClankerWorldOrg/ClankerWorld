# Project inventory copy second reading

Signed: Codex session 40b6ddab/cross-lane-reader. Same-session test-audit second reading, not independent-session merge approval.

Fixed base4e0d56f5739a1cc12ad3f9d54d967903135bfb3e; actual reviewed headc5606874d809fbe571ed4f4a5c5741f892fbe701 in /workspace/ClankerWorld-2564-care.

Decision: approve actual diff. Only AgentProjectTests and new10line RawInventoryTestState change. Source/docs/config unchanged. CareProductionTests, ClinicTradeCareTests and DirectBarterTests independently compared byte-for-byte with fixed base: restored and outside scope. The unpublished preceding barter commit is not this head's diff.

Read complete AgentProjectTests (all methods, theory data, support and providers), actual fixed-base diff, new helper and full consumer search, project-ledger.json, project material definitions, household planning/building material and project assistance plan/check owners, InventoryAuthority.AddLot and state record semantics, barter/project docs as relevant, historyc9aee0ea#2122 anddae690f7#2115. New helper byte-compared with2740's cd72b0ea copy through git show: exact equality.

Static independent checks: AgentProjectTests669→640lines, all104Assert callsites and exact method/attribute lists unchanged. Nine methods/ten source cases remain. Existing six unaffected method bodies/providers remain untouched; four copy sites occur in only three methods. New helper10lines plus static-import line are included in combined tests+20/-39=19net reduction. No case-removal credit.

Four preserved transformations:

- CarriedProjectWoodAndHouseholdHelpAreDeliveredAtHome first site evaluates original state.Agents transform (same actor, storage position,9000fullness, project identity/site/ticks) before AddLot on the precomputed filtered inventory. Same personal-project-wood/wood/actor/4 input.
- Same method second site evaluates original helping.Agents transform (same helper/home/9000/context) before AddLot on original helping.Society.Society.Inventory, helper-project-wood/wood/helper/4. It does not reuse the earlier state inventory.
- FinishedWorkBuildsAtTheCurrentValidSiteInsteadOfRetargetingAnEarlierTile performs original site-tool AddLot before the original Agents transform, using WithInventory(...) with {Agents=...}. Same tool/actor/1, same reference equality person==actor, position and working-project fields.
- AChosenProjectAcquiresAndUsesCarriedWoodWhenThereIsNoHouse still filters only wood from original initial inventory; all other inventory fields/references remain. Same world Restore/200ticks and construction/reload assertions.

WithInventory is plain nested state/Society/SocietyCheckpoint record-with replacement. It has no custody normalization, hold/stock-market changes, validation, cache, policy or static initialization. RuntimeState/Society state records use ordinary init/get data; no custom copy side effects. Refactoring may add an extra shallow outer state allocation and move pure clone allocations around the original expressions. Original Agents-before-AddLot or Inventory-before-Agents evaluation order is retained at each site. Final retained state fields and referenced inputs are equivalent; no intermediate clone escapes.

All original actual delivery/home storage/assistance quantities, no-House in-person travel, current-site construction, invalid-checkpoint refusal, dynamic built-in multi-input construction, selected plans/material receipts, stored artifacts, carrying capacity and byte-reload proof remains. #2122's source-lot/quantity/recheck proof in the first method and #2115's retained construction identity/paid-material assertions in the free-running method are unchanged.

Required fixed filter:

FullyQualifiedName~ClankerWorld.Tests.AgentProjectTests.

Only this class imports the new support at the current head; four references consume it. Entire nonpartial class covers all changed support consumers and every ledger proof. Other2740 consumers are absent from this independent source tree; parent owns exact merge compatibility with that PR, required analyzer/build/discovery, isolated baseline and paired coverage/frozen bytes, formatter/CI. The analyzer-only untracked .editorconfig belongs to the active parent build and was not edited.

Retained proof declarations:

- ClankerWorld.Tests.AgentProjectTests.FirstHouseMaterialsAreDeliveredInLoadsAcrossReloadWithoutExceedingCarryCapacity
- ClankerWorld.Tests.AgentProjectTests.ExistingHouseEndsAStaleSecondHouseProjectWithoutConsumingMaterials
- ClankerWorld.Tests.AgentProjectTests.CarriedProjectWoodAndHouseholdHelpAreDeliveredAtHome
- ClankerWorld.Tests.AgentProjectTests.HouseLessHelperHandsMaterialsToTheRequesterInPersonAcrossReload
- ClankerWorld.Tests.AgentProjectTests.FinishedWorkBuildsAtTheCurrentValidSiteInsteadOfRetargetingAnEarlierTile
- ClankerWorld.Tests.AgentProjectTests.UnregisteredMapAdditionIsRejectedEvenWithARecomputedDigest
- ClankerWorld.Tests.AgentProjectTests.InvalidProjectCheckpointFailsClosed
- ClankerWorld.Tests.AgentProjectTests.DefaultTownGathersDifferentInputsSharesAndCompletesVisibleProjects
- ClankerWorld.Tests.AgentProjectTests.AChosenProjectAcquiresAndUsesCarriedWoodWhenThereIsNoHouse
