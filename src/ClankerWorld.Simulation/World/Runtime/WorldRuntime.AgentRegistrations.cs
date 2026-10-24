using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // Registration order is part of saved observation and event ordering.
    private enum ActionPhase
    {
        ProjectInput,
        FreshAdmission,
        BeforeAge,
        Ordinary
    }

    internal enum ChoiceGroup
    {
        Prelude,
        Adult,
        Child,
        UnhurriedAdult,
        UrgentAdult,
        Final
    }

    private readonly record struct ActionContext(string AgentId, AgentState State, string CandidateId, bool ReportIdle, ActionPlan? Plan = null);
    internal readonly record struct ChoiceContext(List<CognitionCandidate> Candidates, string AgentId, AgentState State, bool HasConversation, SocietyAgent? Agent = null, Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>? ReachableTools = null, Lazy<PlacedBuilding[]>? AccessibleWarehouses = null, List<string>? BuildingsWithoutSites = null);
    private sealed record ActionRegistration(ActionPhase Phase, string Name, string[] Prefixes, string[] ExactIds, Func<WorldRuntime, ActionContext, bool>? Apply = null, ActionDefinition? Definition = null);
    private sealed record ChoiceRegistration(ChoiceGroup Group, string Name, Action<WorldRuntime, ChoiceContext> Add, Func<WorldRuntime, ChoiceContext, bool>? Enabled = null);
    private static readonly IReadOnlyList<ActionRegistration> ActionRegistrations = Array.AsReadOnly<ActionRegistration>(
    [
        new(ActionPhase.ProjectInput, "project_input", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.ProjectInputs.Definition),
        new(ActionPhase.FreshAdmission, "TownProjectProposal", [], [], Definition: TownProjectProposalDefinition),
        new(ActionPhase.FreshAdmission, "TownProjectDonation", [], [], Definition: TownProjectDonationDefinition),
        new(ActionPhase.FreshAdmission, "NonLandFiling", [], [], Definition: NonLandActions.Filing),
        new(ActionPhase.FreshAdmission, "NonLandEvidence", [], [], Definition: NonLandActions.Evidence),
        new(ActionPhase.FreshAdmission, "NonLandJudge", [], [], Definition: NonLandActions.Judge),
        new(ActionPhase.FreshAdmission, "NonLandAdjudication", [], [], Definition: NonLandActions.Adjudication),
        new(ActionPhase.FreshAdmission, "NonLandAgreement", [], [], Definition: NonLandActions.Agreement),
        new(ActionPhase.FreshAdmission, "NonLandWork", [], [], Definition: NonLandActions.Work),
        new(ActionPhase.FreshAdmission, "NonLandReportRelay", [], [], Definition: NonLandActions.ReportRelay),
        new(ActionPhase.FreshAdmission, "LandHearingFiling", [], [], Definition: LandHearingActions.Filing),
        new(ActionPhase.FreshAdmission, "LandHearingEvidence", [], [], Definition: LandHearingActions.Evidence),
        new(ActionPhase.FreshAdmission, "LandHearingJudge", [], [], Definition: LandHearingActions.Judge),
        new(ActionPhase.FreshAdmission, "LandHearingRuling", [], [], Definition: LandHearingActions.Ruling),
        new(ActionPhase.FreshAdmission, "LandHearingReopening", [], [], Definition: LandHearingActions.Reopening),
        new(ActionPhase.FreshAdmission, "LandHearingProperty", [], [], Definition: LandHearingActions.Property),
        new(ActionPhase.FreshAdmission, "TownLandClaim", [], [], Definition: LandRightsActions.TownLandClaim),
        new(ActionPhase.FreshAdmission, "HouseholdLandFiling", [], [], Definition: LandRightsActions.HouseholdLandFiling),
        new(ActionPhase.FreshAdmission, "HouseholdLandResponse", [], [], Definition: LandRightsActions.HouseholdLandResponse),
        new(ActionPhase.FreshAdmission, "LandTransferProposal", [], [], Definition: LandRightsActions.LandTransferProposal),
        new(ActionPhase.FreshAdmission, "LandTransferResponse", [], [], Definition: LandRightsActions.LandTransferResponse),
        new(ActionPhase.FreshAdmission, "LandSaleCollection", [], [], Definition: LandRightsActions.LandSaleCollection),
        new(ActionPhase.FreshAdmission, "LandSaleExchange", [], [], Definition: LandRightsActions.LandSaleExchange),
        new(ActionPhase.FreshAdmission, "TownAdmission", [], [], Definition: CivicAccessActions.TownAdmission),
        new(ActionPhase.FreshAdmission, "TownFounding", [], [], Definition: CivicAccessActions.TownFounding),
        new(ActionPhase.FreshAdmission, "CivicNotice", [], [], Definition: CivicAccessActions.CivicNotice),
        new(ActionPhase.FreshAdmission, "CivicVisit", [], [], Definition: CivicAccessActions.CivicVisit),
        new(ActionPhase.FreshAdmission, "MayoralBallot", [], [], Definition: GovernanceActions.MayoralBallot),
        new(ActionPhase.FreshAdmission, "GovernmentOffice", [], [], Definition: GovernanceActions.GovernmentOffice),
        new(ActionPhase.FreshAdmission, "GovernmentProposal", [], [], Definition: GovernanceActions.GovernmentProposal),
        new(ActionPhase.FreshAdmission, "TownPolicy", [], [], Definition: GovernanceActions.TownPolicy),
        new(ActionPhase.FreshAdmission, "TownLaw", [], [], Definition: GovernanceActions.TownLaw),
        new(ActionPhase.FreshAdmission, "CivicProposalVote", [], [], Definition: GovernanceActions.CivicProposalVote),
        new(ActionPhase.FreshAdmission, "council_consent", [], [], Definition: CouncilActions.Consent),
        new(ActionPhase.FreshAdmission, "council_ballot", [], [], Definition: CouncilActions.Ballot),
        new(ActionPhase.FreshAdmission, "market_occupancy", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.MarketActions.Occupancy),
        new(ActionPhase.FreshAdmission, "market_stock", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.MarketActions.Stock),
        new(ActionPhase.FreshAdmission, "market_exchange", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.MarketActions.Exchange),
        new(ActionPhase.FreshAdmission, "ornament", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.InventoryActions.Ornament),
        new(ActionPhase.FreshAdmission, "tool_request_consent", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Consent),
        new(ActionPhase.FreshAdmission, "animal_agreement", ["animal:agreement:"], [], Definition: AnimalAgreementDefinition),
        new(ActionPhase.FreshAdmission, "medical_consent", [MedicalConsentChoice.AllowPrefix, MedicalConsentChoice.RevokePrefix], [], Definition: MedicalConsentDefinition),
        new(ActionPhase.BeforeAge, "safe_idle_age_outcome", [], [WaitingChoice.Candidate], Definition: WaitingDefinition),
        new(ActionPhase.BeforeAge, "IsOrnamentCandidate", [], [], static (runtime, action) =>
    {
        return IsOrnamentCandidate(action.CandidateId);
    }),
        new(ActionPhase.BeforeAge, "tool_request_visit", [ToolRequestVisitChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Visit),
        new(ActionPhase.BeforeAge, "tool_request_work", [ToolRequestWorkChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Work),
        new(ActionPhase.BeforeAge, "tool_request_collect", [ToolRequestCollectionChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.ToolRequestActions.Collection),
        new(ActionPhase.BeforeAge, "ToolRequestPrefix", [ToolRequestPrefix], [], static (_, _) => true),
        RegisterConversationActions(),
        new(ActionPhase.BeforeAge, "TryApplyGuardianPlacementCandidate", [], [], Definition: GuardianTravelDefinition),
        new(ActionPhase.BeforeAge, "guardian_end:", ["guardian_end:"], [], Definition: DependentCareDefinition),
        new(ActionPhase.Ordinary, "safe_idle", [], [WaitingChoice.Candidate], Definition: WaitingDefinition),
        new(ActionPhase.Ordinary, "medical_supply", [], [MedicalSupplyChoice.Id], Definition: MedicalSupplyDefinition),
        new(ActionPhase.Ordinary, "medical_treatment", [MedicalTreatmentChoice.Prefix], [], Definition: MedicalTreatmentDefinition),
        new(ActionPhase.Ordinary, "medical_", ["medical_"], [], static (runtime, action) =>
    {
        runtime.ApplyMedicalCandidate(action.AgentId, action.State, action.CandidateId, action.Plan);
        return true;
    }),
        RegisterFieldActions(),
        new(ActionPhase.Ordinary, "child_", ["child_"], [], Definition: ChildDefinition),
        new(ActionPhase.Ordinary, "care", ["care:", "guardian_tend:"], [], Definition: PracticalCareDefinition),
        new(ActionPhase.Ordinary, "guardian_", ["guardian_"], [], Definition: DependentCareDefinition),
        new(ActionPhase.Ordinary, "parent_", ["parent_"], [], Definition: ParenthoodDefinition),
        new(ActionPhase.Ordinary, "partner_", ["partner_"], [], Definition: PartnershipDefinition),
        new(ActionPhase.Ordinary, "housing", [], [], Definition: HousingDefinition),
        new(ActionPhase.Ordinary, "departure", [], [], Definition: DepartureDefinition),
        new(ActionPhase.Ordinary, "replacement_care", [ReplacementCareChoice.Prefix], [], Definition: ReplacementCareDefinition),
        new(ActionPhase.Ordinary, "household_resume", [HouseholdResumeChoice.Prefix], [], Definition: HouseholdResumeDefinition),
        new(ActionPhase.Ordinary, "relocation", [], [RelocationChoice.Id], Definition: RelocationDefinition),
        new(ActionPhase.Ordinary, "household_", ["household_"], [], Definition: PersonalGoodsDefinition),
        new(ActionPhase.Ordinary, "learn:", ["learn:", "lesson_"], [], Definition: LearningDefinition),
        RegisterCivicPersonalActions(),
        new(ActionPhase.Ordinary, "IsTownProjectDonationCandidate", [], [], static (runtime, action) =>
    {
        return IsTownProjectDonationCandidate(action.CandidateId);
    }),
        new(ActionPhase.Ordinary, "TownProjectLoad", [], [], Definition: TownProjectLoadDefinition),
        new(ActionPhase.Ordinary, "TownProjectWork", [], [], Definition: TownProjectWorkDefinition),
        new(ActionPhase.Ordinary, "TownProjectRecovery", [], [], Definition: TownProjectRecoveryDefinition),
        new(ActionPhase.Ordinary, "ApplyTownProjectCandidate", [], [], static (runtime, action) =>
    {
        return IsTownProjectCandidate(action.CandidateId);
    }),
        new(ActionPhase.Ordinary, "IsMarketCandidate", [], [], static (runtime, action) =>
    {
        return IsMarketCandidate(action.CandidateId);
    }),
        RegisterFoodPolicyActions(),
        new(ActionPhase.Ordinary, "trade_", ["trade_"], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.MarketActions.Barter),
        new(ActionPhase.Ordinary, "shop_stock_store", [], [StoreStockChoice.Candidate], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.WorkshopActions.Store),
        new(ActionPhase.Ordinary, "shop_buy", [ShopBuyChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.MarketActions.ShopBuy),
        new(ActionPhase.Ordinary, "shop_", ["shop_"], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.MarketActions.ShopResponse),
        new(ActionPhase.Ordinary, "haul_household_stock", [], [new HouseholdStockRequest(HouseholdStockRequestKind.Haul).Encode()], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.HouseholdStockActions.Hauling),
        new(ActionPhase.Ordinary, "recover_household_delivery", [], [new HouseholdStockRequest(HouseholdStockRequestKind.RecoverDelivery).Encode()], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.HouseholdStockActions.Recovery),
        new(ActionPhase.Ordinary, "store_household_food", [], [new HouseholdStockRequest(HouseholdStockRequestKind.StoreFood).Encode()], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.HouseholdStockActions.Food),
        new(ActionPhase.Ordinary, "make_room_for_food", [], [FoodCargoChoice.Candidate], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.InventoryActions.FoodCargo),
        new(ActionPhase.Ordinary, "store_town_resources", [], [TownStockRequest.WarehouseCandidate], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.TownStockActions.Warehouse),
        new(ActionPhase.Ordinary, "TownSalvagePrefix", [TownSalvagePrefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.TownStockActions.Salvage),
        new(ActionPhase.Ordinary, "haul_farm_grain", [], [FarmStockActionPlan.Candidate(FarmStockActionKind.Grain)], Definition: FarmStockDefinition),
        new(ActionPhase.Ordinary, "haul_farm_flour", [], [FarmStockActionPlan.Candidate(FarmStockActionKind.Flour)], Definition: FarmStockDefinition),
        new(ActionPhase.Ordinary, "haul_smith_input", [], [SmithDeliveryActionPlan.Candidate(SmithDeliveryKind.Input)], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.WorkshopActions.Delivery),
        new(ActionPhase.Ordinary, "gather_smith_ore", [], [new SmithGatherChoice("iron_ore").Encode()], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.WorkshopActions.Gather),
        new(ActionPhase.Ordinary, "GatherBlacksmithInputPrefix", [SmithGatherChoice.InputPrefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.WorkshopActions.Gather),
        new(ActionPhase.Ordinary, "deliver_smith_ore", [], [SmithDeliveryActionPlan.Candidate(SmithDeliveryKind.Ore)], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.WorkshopActions.Delivery),
        new(ActionPhase.Ordinary, "collect_wooden_axe", [], [new ToolCollectionChoice("wooden_axe").Encode(), new ToolCollectionChoice("wooden_pickaxe").Encode(), new ToolCollectionChoice("wooden_hoe").Encode()], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.WorkshopActions.Collection),
        new(ActionPhase.Ordinary, "CollectToolPrefix", [ToolCollectionChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.WorkshopActions.Collection),
        new(ActionPhase.Ordinary, "boat_trip", [BoatTripActionChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.TransportActions.BoatTrip),
        new(ActionPhase.Ordinary, "ApplyBoatCandidate", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.TransportActions.BoatWaiting),
        new(ActionPhase.Ordinary, "animal_work", [], [], Definition: AnimalWorkDefinition),
        new(ActionPhase.Ordinary, "animal_product", [], [], Definition: AnimalProductDefinition),
        new(ActionPhase.Ordinary, "animal_travel", [], [], Definition: AnimalTravelDefinition),
        new(ActionPhase.Ordinary, "animal_supply", [], [], Definition: AnimalSupplyDefinition),
        new(ActionPhase.Ordinary, "animal_trip", [], [], Definition: AnimalTripDefinition),
        RegisterAnimalActions(),
        new(ActionPhase.Ordinary, "ApplyHandcartCandidate", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.TransportActions.Control),
        new(ActionPhase.Ordinary, "handcart_material", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.TransportActions.BuildMaterial),
        new(ActionPhase.Ordinary, "handcart_repair_input", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.TransportActions.RepairInput),
        new(ActionPhase.Ordinary, "handcart_pull", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.TransportActions.Pull),
        new(ActionPhase.Ordinary, "handcart_cargo", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.TransportActions.Cargo),
        new(ActionPhase.Ordinary, "handcart_repair", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.TransportActions.Repair),
        new(ActionPhase.Ordinary, "RepairToolPrefix", [ToolRepairChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.WorkshopActions.Repair),
        new(ActionPhase.Ordinary, "RareMiningPrefix", [RareMiningChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.WorkshopActions.Mining),
        new(ActionPhase.Ordinary, "KnowledgeWritePrefix", [KnowledgeWritePrefix, KnowledgeCopyPrefix], ["knowledge_continue"], Definition: KnowledgeWritingDefinition),
        new(ActionPhase.Ordinary, "KnowledgeReadPrefix", [KnowledgeReadPrefix], [], Definition: KnowledgeReadDefinition),
        new(ActionPhase.Ordinary, "ApplyKnowledgeStorageCandidate", [], [], Definition: KnowledgeStorageDefinition),
        new(ActionPhase.Ordinary, "KnowledgeSharePrefix", [KnowledgeSharePrefix], [], Definition: KnowledgeShareDefinition),
        new(ActionPhase.Ordinary, "SupplyWorkstationPrefix", [WorkstationSupplyChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.WorkshopActions.Supply),
        new(ActionPhase.Ordinary, "ReturnEmptyVesselPrefix", [VesselChoice.EmptyPrefix], [], Definition: HouseholdVesselDefinition(VesselActionKind.ReturnEmpty)),
        new(ActionPhase.Ordinary, "collect_water_jug", [], [new VesselChoice(VesselActionKind.CollectJug).Encode()], Definition: HouseholdVesselDefinition(VesselActionKind.CollectJug)),
        new(ActionPhase.Ordinary, "return_water_jug", [], [new VesselChoice(VesselActionKind.ReturnJug).Encode()], Definition: HouseholdVesselDefinition(VesselActionKind.ReturnJug)),
        new(ActionPhase.Ordinary, "store_food_in_pot", [], [new VesselChoice(VesselActionKind.StoreFood).Encode()], Definition: HouseholdVesselDefinition(VesselActionKind.StoreFood)),
        new(ActionPhase.Ordinary, "take_food_from_pot", [], [new VesselChoice(VesselActionKind.TakeFood).Encode()], Definition: HouseholdVesselDefinition(VesselActionKind.TakeFood)),
        new(ActionPhase.Ordinary, "FillWaterJugPrefix", [VesselChoice.FillPrefix], [], Definition: HouseholdVesselDefinition(VesselActionKind.FillJug)),
        new(ActionPhase.Ordinary, "GatherBuildingMaterialPrefix", [BuildingMaterialChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.ProjectMaterialActions.Building),
        new(ActionPhase.Ordinary, "ExpandBuildingPrefix", [ExpansionChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.ConstructionActions.Expansion),
        new(ActionPhase.Ordinary, "InviteHouseGuestPrefix", [InviteHouseGuestPrefix, global::ClankerWorld.Simulation.World.HouseGuestChoice.RevokePrefix], [], Definition: HouseGuestDefinition),
        new(ActionPhase.Ordinary, "recipe", [], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.ProductionActions.Recipe),
        new(ActionPhase.Ordinary, "build:", ["build:"], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.ConstructionActions.Building),
        new(ActionPhase.Ordinary, "assist:", [ProjectAssistanceChoice.Prefix], [], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.ProjectMaterialActions.Assistance),
        new(ActionPhase.Ordinary, "replant_tree", [], [TreeActionPlan.Candidate(TreeChoice.Replant)], Definition: TreeDefinition),
        new(ActionPhase.Ordinary, "plant_tree", [], [TreeActionPlan.Candidate(TreeChoice.Plant), TreeActionPlan.Candidate(TreeChoice.Orchard)], Definition: TreeDefinition),
        new(ActionPhase.Ordinary, "ExplorationGoalPrefix", [ExplorationGoalPrefix], [], Definition: ExplorationDefinition),
        new(ActionPhase.Ordinary, "explore", [], ["explore"], Definition: ExplorationDefinition),
        new(ActionPhase.Ordinary, "explore_return", [], ["explore_return"], Definition: ExplorationDefinition),
        new(ActionPhase.Ordinary, "wear_clothing", [], [PrivateEquipmentActionPlan.Candidate(PrivateEquipmentKind.Garment)], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.EquipmentActions.Garment),
        new(ActionPhase.Ordinary, "equip_carry_aid", [], [PrivateEquipmentActionPlan.Candidate(PrivateEquipmentKind.CarryAid)], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.EquipmentActions.CarryAid),
        new(ActionPhase.Ordinary, "repair_equipment", [], [EquipmentRepairChoice.Candidate], Definition: global::ClankerWorld.Simulation.World.WorldRuntime.EquipmentActions.Repair),
        new(ActionPhase.Ordinary, "tend_fire", [], [new WarmthChoice(WarmthChoiceKind.Fire).Encode()], Definition: FireDefinition),
        new(ActionPhase.Ordinary, "seek_warmth", [], [new WarmthChoice(WarmthChoiceKind.Protection).Encode()], Definition: WarmthDefinition),
        new(ActionPhase.Ordinary, "seek_food", [], [new FoodChoice(FoodActionKind.Seek).Encode()], Definition: FoodResourceDefinition),
        new(ActionPhase.Ordinary, "harvest_food", [], [new FoodChoice(FoodActionKind.Harvest).Encode()], Definition: FoodResourceDefinition),
        new(ActionPhase.Ordinary, "consume_food", [], [new FoodChoice(FoodActionKind.Consume).Encode()], Definition: FoodConsumptionDefinition),
        new(ActionPhase.Ordinary, "collect_shared_food", [], [new FoodChoice(FoodActionKind.CollectShared).Encode()], Definition: SharedFoodDefinition)
    ]);
    private static readonly IReadOnlyList<ChoiceRegistration> ChoiceRegistrations = Array.AsReadOnly<ChoiceRegistration>(
    [
        new(ChoiceGroup.Prelude, "food", static (runtime, context) => runtime.AddFoodCandidates(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.Prelude, "handcarts", static (runtime, context) => runtime.AddHandcartCandidates(context.Candidates, context.AgentId, context.State), static (runtime, context) => runtime.IsAdult(context.AgentId)),
        RegisterAnimalChoices(),
        new(ChoiceGroup.Prelude, "survival", static (runtime, context) => runtime.AddSurvivalCandidates(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.Prelude, "town_salvage", static (runtime, context) => runtime.townSystem.AddTownSalvageCandidates(runtime.civicWorld, context.Candidates, context.AgentId), null),
        new(ChoiceGroup.Prelude, "dependent_care", static (runtime, context) => runtime.AddDependentCareCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.Prelude, "guardian_placement", static (runtime, context) => runtime.AddGuardianPlacementChoice(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.Prelude, "medical", static (runtime, context) => runtime.AddMedicalCareCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.Adult, "AddKnowledgeCandidates", static (runtime, context) => runtime.AddKnowledgeCandidates(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.Adult, "AddFamilyCandidates", static (runtime, context) => runtime.AddFamilyCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.Adult, "AddHousingCandidates", static (runtime, context) => runtime.AddHousingCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.Adult, "AddParenthoodCandidates", static (runtime, context) => runtime.AddParenthoodCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.Adult, "AddRecoverHouseholdDeliveryCandidate", static (runtime, context) => runtime.AddRecoverHouseholdDeliveryCandidate(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.Adult, "AddUrgentFoodPotCandidate", static (runtime, context) => runtime.AddUrgentFoodPotCandidate(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.Adult, "AddShopCandidates", static (runtime, context) => runtime.AddShopCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.Adult, "AddToolMakingRequestCandidates", static (runtime, context) => runtime.AddToolMakingRequestCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.Child, "AddChildCandidates", static (runtime, context) => runtime.AddChildCandidates(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.Child, "AddChildNonLandCandidates", static (runtime, context) => runtime.townSystem.AddChildNonLandCandidates(runtime.civicWorld, context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddBuildCandidates", static (runtime, context) => CognitionSystem.AddBuildCandidates(runtime.CognitionCandidatesWorld,context.Candidates,context.Agent!,context.State,context.ReachableTools!,context.AccessibleWarehouses!,context.BuildingsWithoutSites!), null),
        new(ChoiceGroup.UnhurriedAdult, "AddBuildingExpansionCandidates", static (runtime, context) => runtime.AddBuildingExpansionCandidates(context.Candidates, context.AgentId, context.ReachableTools!, context.AccessibleWarehouses!), null),
        new(ChoiceGroup.UnhurriedAdult, "AddHouseGuestCandidates", static (runtime, context) => runtime.AddHouseGuestCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddHouseHaulCandidate", static (runtime, context) => runtime.AddHouseHaulCandidate(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.UnhurriedAdult, "AddWarehouseStockCandidate", static (runtime, context) => runtime.AddWarehouseStockCandidate(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.UnhurriedAdult, "AddFarmGrainCandidate", static (runtime, context) => runtime.AddFarmGrainCandidate(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddFarmFlourCandidate", static (runtime, context) => runtime.AddFarmFlourCandidate(context.Candidates, context.AgentId, context.State), null),
        RegisterFieldChoices(),
        new(ChoiceGroup.UnhurriedAdult, "AddBlacksmithStockCandidate", static (runtime, context) => runtime.AddBlacksmithStockCandidate(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.UnhurriedAdult, "AddBlacksmithOreCandidates", static (runtime, context) => runtime.AddBlacksmithOreCandidates(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.UnhurriedAdult, "AddWorkstationSupplyCandidate", static (runtime, context) => runtime.AddWorkstationSupplyCandidate(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddContainerCandidates", static (runtime, context) => runtime.AddContainerCandidates(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.UnhurriedAdult, "AddCraftToolCandidates", static (runtime, context) => runtime.AddCraftToolCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddRareMiningCandidates", static (runtime, context) => runtime.AddRareMiningCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddProjectAssistanceCandidates", static (runtime, context) => runtime.AddProjectAssistanceCandidates(context.Candidates, context.AgentId), null),
        RegisterTreePlantingChoices(),
        new(ChoiceGroup.UnhurriedAdult, "AddTradeCandidates", static (runtime, context) => runtime.AddTradeCandidates(context.Candidates, context.AgentId), null),
        RegisterFoodPolicyChoices(),
        RegisterTownCivicChoices(),
        new(ChoiceGroup.UnhurriedAdult, "AddTownProjectDonationCandidates", static (runtime, context) => runtime.AddTownProjectDonationCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddMarketCandidates", static (runtime, context) => runtime.AddMarketCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddBoatCandidates", static (runtime, context) => runtime.AddBoatCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddLearningCandidates", static (runtime, context) => runtime.AddLearningCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddExplorationCandidate", static (runtime, context) => runtime.AddExplorationCandidate(context.Candidates, context.AgentId, context.State), null),
        new(ChoiceGroup.UrgentAdult, "urgent_market", static (runtime, context) => runtime.AddMarketCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.Final, "town_projects", static (runtime, context) => runtime.AddTownProjectCandidates(context.Candidates, context.AgentId), null),
        RegisterConversationChoices(),
        new(ChoiceGroup.Final, "safe_idle", static (runtime, context) => context.Candidates.Add(runtime.WaitingCandidate(context.AgentId, "Continue safely without starting a new task.")), null)
    ]);
    private bool TryRegisteredAction(ActionPhase phase, ActionContext action)
    {
        foreach (var registration in ActionRegistrations)
        {
            if (registration.Phase != phase)
                continue;
            if (registration.Definition is { } codec && registration.Prefixes.Length == 0 && registration.ExactIds.Length == 0 &&
                !codec.Matches(action.CandidateId)) continue;
            if (registration.Prefixes.Length != 0 || registration.ExactIds.Length != 0)
            {
                if (!MatchesAction(registration, action.CandidateId))
                    continue;
            }

            if (registration.Definition is { } definition
                ? definition.Apply(this, action, phase) : registration.Apply!(this, action))
                return true;
        }

        return false;
    }

    private static bool MatchesAction(ActionRegistration registration, string candidateId)
    {
        foreach (var exact in registration.ExactIds)
            if (string.Equals(candidateId, exact, StringComparison.Ordinal))
                return true;
        foreach (var prefix in registration.Prefixes)
            if (candidateId.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        return false;
    }

    private void AddRegisteredChoices(ChoiceGroup group, ChoiceContext context)
    {
        foreach (var registration in ChoiceRegistrations)
            if (registration.Group == group && (registration.Enabled is null || registration.Enabled(this, context)))
                registration.Add(this, context);
    }

    private void AddGuardianPlacementChoice(List<CognitionCandidate> candidates, string agentId)
    {
        if (GuardianPlacementCandidate(agentId) is { } candidate)
            candidates.Add(candidate);
    }

    private void AddFoodCandidates(List<CognitionCandidate> candidates, string agentId, AgentState state)
    {
        var instruction = PendingInstructionFor(agentId);
        var instructionCandidate = instruction?.Task?.Action;
        var carriedFood = PreferredFood(agentId, agentId).FirstOrDefault();
        var hasFood = carriedFood is not null;
        if (hasFood && state.FullnessBasisPoints < ComfortableFullness)
        {
            AddFoodPlanCandidate(candidates, PlanFoodConsumption(agentId, carriedFood), "Eat one carried food item.", 0);
        }

        if (state.FullnessBasisPoints < ComfortableFullness) AddMilkCandidate(candidates, agentId);
        if (instructionCandidate == "consume_food" && hasFood && state.FullnessBasisPoints < ComfortableFullness && !candidates.Any(item => item.Id == "consume_food"))
        {
            AddFoodPlanCandidate(candidates, PlanFoodConsumption(agentId, carriedFood), "Follow the owner's food instruction.", 0);
        }

        var foodPriority = NeedsUrgentFood(state) ? 2 : state.FullnessBasisPoints < RoutineFoodSeekFullness ? 5 : 90;
        // An optional reserve remains selectable without outranking ordinary activities.
        var wantsFood = !hasFood && state.FullnessBasisPoints < NeedThresholds.FoodPickupFullness;
        var shouldGatherFood = wantsFood && FreeCarryCapacity(agentId) > 0;
        var foodSource = shouldGatherFood || instructionCandidate is "seek_food" or "harvest_food" ? AvailableFoodSource(agentId, state.Position) : null;
        var sharedFood = shouldGatherFood ? AvailableSharedFood(agentId) : null;
        if (sharedFood is not null && contentRegistry.ExportState().Packages.Any(package => package.Manifest.PackageId == StarterContent.PackageId && package.Lifecycle == ContentPackageLifecycle.Active))
        {
            AddFoodPlanCandidate(candidates, PlanSharedFood(agentId, sharedFood), "Collect one available household food serving from its store, then eat it.", foodPriority - 1, HouseholdId);
        }

        if (shouldGatherFood && foodSource is not null && IsWithinInteractionRange(state.Position, foodSource.Position, ResourceInteractionRange))
        {
            AddFoodPlanCandidate(candidates, PlanFoodResource(agentId, true, foodSource), "Gather several food servings from the nearby food source.", foodPriority, foodSource.Id);
        }
        else if (shouldGatherFood && foodSource is not null)
        {
            AddFoodPlanCandidate(candidates, PlanFoodResource(agentId, false, foodSource), "Travel within gathering range of an available food source.", foodPriority, foodSource.Id);
        }
        else if (wantsFood && sharedFood is null)
        {
            AddMakeRoomForFoodCandidate(candidates, agentId, state, foodPriority);
        }

        if (instructionCandidate == "seek_food" && foodSource is not null && !candidates.Any(item => item.Id == "seek_food"))
        {
            AddFoodPlanCandidate(candidates, PlanFoodResource(agentId, false, foodSource), "Follow the owner's travel instruction.", 0, foodSource.Id);
        }

        if (instructionCandidate == "harvest_food" && foodSource is not null && IsWithinInteractionRange(state.Position, foodSource.Position, ResourceInteractionRange) && !candidates.Any(item => item.Id == "harvest_food"))
        {
            AddFoodPlanCandidate(candidates, PlanFoodResource(agentId, true, foodSource), "Follow the owner's harvest instruction.", 0, foodSource.Id);
        }
    }
}
