using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // Registration order is part of saved observation and event ordering.
    private enum ActionPhase
    {
        BeforeAge,
        Ordinary
    }

    private enum ChoiceGroup
    {
        Prelude,
        Adult,
        Child,
        UnhurriedAdult,
        UrgentAdult,
        Final
    }

    private readonly record struct ActionContext(string AgentId, AgentState State, string CandidateId, bool ReportIdle, ActionPlan? Plan = null);
    private readonly record struct ChoiceContext(List<CognitionCandidate> Candidates, string AgentId, AgentState State, bool HasConversation, SocietyAgent? Agent = null, Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>? ReachableTools = null, Lazy<PlacedBuilding[]>? AccessibleWarehouses = null, List<string>? BuildingsWithoutSites = null);
    private sealed record ActionRegistration(ActionPhase Phase, string Name, string[] Prefixes, string[] ExactIds, Func<WorldRuntime, ActionContext, bool>? Apply = null, ActionDefinition? Definition = null);
    private sealed record ChoiceRegistration(ChoiceGroup Group, string Name, Action<WorldRuntime, ChoiceContext> Add, Func<WorldRuntime, ChoiceContext, bool>? Enabled = null);
    private static readonly IReadOnlyList<ActionRegistration> ActionRegistrations = Array.AsReadOnly<ActionRegistration>(
    [
        new(ActionPhase.BeforeAge, "safe_idle_age_outcome", [], [WaitingChoice.Candidate], Definition: WaitingDefinition),
        new(ActionPhase.BeforeAge, "IsOrnamentCandidate", [], [], static (runtime, action) =>
    {
        return IsOrnamentCandidate(action.CandidateId);
    }),
        new(ActionPhase.BeforeAge, "ToolRequestPrefix", [ToolRequestPrefix], [], static (runtime, action) =>
    {
        runtime.ApplyToolMakingRequestCandidate(action.AgentId, action.State, action.CandidateId, action.ReportIdle, action.Plan);
        return true;
    }),
        RegisterConversationActions(),
        new(ActionPhase.BeforeAge, "TryApplyGuardianPlacementCandidate", [], [], static (runtime, action) =>
    {
        return runtime.TryApplyGuardianPlacementCandidate(action.AgentId, action.CandidateId, action.Plan);
    }),
        new(ActionPhase.BeforeAge, "guardian_end:", ["guardian_end:"], [], static (runtime, action) =>
    {
        if (!runtime.HasGuardianPlacementTask(action.AgentId))
            return false;
        runtime.ApplyDependentCareCandidate(action.AgentId, action.CandidateId, action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "safe_idle", [], [WaitingChoice.Candidate], Definition: WaitingDefinition),
        new(ActionPhase.Ordinary, "medical_", ["medical_"], [], static (runtime, action) =>
    {
        runtime.ApplyMedicalCandidate(action.AgentId, action.State, action.CandidateId, action.Plan);
        return true;
    }),
        RegisterFieldActions(),
        new(ActionPhase.Ordinary, "child_", ["child_"], [], Definition: ChildDefinition),
        new(ActionPhase.Ordinary, "guardian_", ["guardian_"], [], static (runtime, action) =>
    {
        runtime.ApplyDependentCareCandidate(action.AgentId, action.CandidateId, action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "parent_", ["parent_", "care:"], [], static (runtime, action) =>
    {
        runtime.ApplyParenthoodCandidate(action.AgentId, action.CandidateId, action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "partner_", ["partner_"], [], static (runtime, action) =>
    {
        runtime.ApplyFamilyCandidate(action.AgentId, action.CandidateId, action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "household_", ["household_"], [], static (runtime, action) =>
    {
        runtime.ApplyHousingCandidate(action.AgentId, action.CandidateId, action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "learn:", ["learn:", "lesson_"], [], static (runtime, action) =>
    {
        runtime.ApplyLearningCandidate(action.AgentId, action.CandidateId, action.Plan);
        return true;
    }),
        RegisterCivicPersonalActions(),
        new(ActionPhase.Ordinary, "IsTownProjectDonationCandidate", [], [], static (runtime, action) =>
    {
        return IsTownProjectDonationCandidate(action.CandidateId);
    }),
        new(ActionPhase.Ordinary, "ApplyTownProjectCandidate", [], [], static (runtime, action) =>
    {
        if (!(IsTownProjectCandidate(action.CandidateId)))
            return false;
        runtime.ApplyTownProjectCandidate(action.AgentId, action.State, action.CandidateId, action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "IsMarketCandidate", [], [], static (runtime, action) =>
    {
        return IsMarketCandidate(action.CandidateId);
    }),
        RegisterFoodPolicyActions(),
        new(ActionPhase.Ordinary, "trade_", ["trade_"], [], static (runtime, action) =>
    {
        runtime.ApplyTradeCandidate(action.AgentId, action.State, action.CandidateId, action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "shop_stock_store", [], [StoreStockChoice.Candidate], Definition: StoreStockDefinition),
        new(ActionPhase.Ordinary, "shop_", ["shop_"], [], static (runtime, action) =>
    {
        runtime.ApplyShopCandidate(action.AgentId, action.State, action.CandidateId, action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "haul_household_stock", [], [new HouseholdStockRequest(HouseholdStockRequestKind.Haul).Encode()], Definition: HouseholdHaulingDefinition),
        new(ActionPhase.Ordinary, "recover_household_delivery", [], [new HouseholdStockRequest(HouseholdStockRequestKind.RecoverDelivery).Encode()], Definition: HouseholdRecoveryDefinition),
        new(ActionPhase.Ordinary, "store_household_food", [], [new HouseholdStockRequest(HouseholdStockRequestKind.StoreFood).Encode()], Definition: HouseholdFoodStorageDefinition),
        new(ActionPhase.Ordinary, "make_room_for_food", [], ["make_room_for_food"], static (runtime, action) =>
    {
        runtime.ApplyFoodCargoPlan(action.Plan as FoodCargoActionPlan ?? runtime.PlanFoodCargo(action.AgentId));
        return true;
    }),
        new(ActionPhase.Ordinary, "store_town_resources", [], [TownStockRequest.WarehouseCandidate], Definition: WarehouseStockDefinition),
        new(ActionPhase.Ordinary, "TownSalvagePrefix", [TownSalvagePrefix], [], Definition: SalvageStockDefinition),
        new(ActionPhase.Ordinary, "haul_farm_grain", [], [FarmStockActionPlan.Candidate(FarmStockActionKind.Grain)], Definition: FarmStockDefinition),
        new(ActionPhase.Ordinary, "haul_farm_flour", [], [FarmStockActionPlan.Candidate(FarmStockActionKind.Flour)], Definition: FarmStockDefinition),
        new(ActionPhase.Ordinary, "haul_smith_input", [], [SmithDeliveryActionPlan.Candidate(SmithDeliveryKind.Input)], Definition: SmithDeliveryDefinition),
        new(ActionPhase.Ordinary, "gather_smith_ore", [], [new SmithGatherChoice("iron_ore").Encode()], Definition: SmithGatherDefinition),
        new(ActionPhase.Ordinary, "GatherBlacksmithInputPrefix", [SmithGatherChoice.InputPrefix], [], Definition: SmithGatherDefinition),
        new(ActionPhase.Ordinary, "deliver_smith_ore", [], [SmithDeliveryActionPlan.Candidate(SmithDeliveryKind.Ore)], Definition: SmithDeliveryDefinition),
        new(ActionPhase.Ordinary, "collect_wooden_axe", [], [new ToolCollectionChoice("wooden_axe").Encode(), new ToolCollectionChoice("wooden_pickaxe").Encode(), new ToolCollectionChoice("wooden_hoe").Encode()], Definition: ToolCollectionDefinition),
        new(ActionPhase.Ordinary, "CollectToolPrefix", [ToolCollectionChoice.Prefix], [], Definition: ToolCollectionDefinition),
        new(ActionPhase.Ordinary, "ApplyBoatCandidate", [], [], static (runtime, action) =>
    {
        return runtime.ApplyBoatCandidate(action.AgentId, action.CandidateId, action.Plan);
    }),
        RegisterAnimalActions(),
        new(ActionPhase.Ordinary, "ApplyHandcartCandidate", [], [], static (runtime, action) =>
    {
        return runtime.ApplyHandcartCandidate(action.AgentId, action.State, action.CandidateId, originalPlan: action.Plan);
    }),
        new(ActionPhase.Ordinary, "RepairToolPrefix", [ToolRepairChoice.Prefix], [], Definition: ToolRepairDefinition),
        new(ActionPhase.Ordinary, "RareMiningPrefix", [RareMiningChoice.Prefix], [], Definition: RareMiningDefinition),
        new(ActionPhase.Ordinary, "KnowledgeWritePrefix", [KnowledgeWritePrefix, KnowledgeCopyPrefix, KnowledgeReadPrefix], ["knowledge_continue"], static (runtime, action) =>
    {
        runtime.ApplyKnowledgeWritingCandidate(action.AgentId, action.State, action.CandidateId, action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "ApplyKnowledgeStorageCandidate", [], [], static (runtime, action) =>
    {
        return runtime.ApplyKnowledgeStorageCandidate(action.AgentId, action.State, action.CandidateId, action.Plan);
    }),
        new(ActionPhase.Ordinary, "KnowledgeSharePrefix", [KnowledgeSharePrefix], [], static (runtime, action) =>
    {
        runtime.ApplyKnowledgeShare(action.AgentId, action.State, action.CandidateId, action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "SupplyWorkstationPrefix", [WorkstationSupplyChoice.Prefix], [], Definition: WorkstationSupplyDefinition),
        new(ActionPhase.Ordinary, "ReturnEmptyVesselPrefix", [VesselChoice.EmptyPrefix], [], Definition: HouseholdVesselDefinition(VesselActionKind.ReturnEmpty)),
        new(ActionPhase.Ordinary, "collect_water_jug", [], [new VesselChoice(VesselActionKind.CollectJug).Encode()], Definition: HouseholdVesselDefinition(VesselActionKind.CollectJug)),
        new(ActionPhase.Ordinary, "return_water_jug", [], [new VesselChoice(VesselActionKind.ReturnJug).Encode()], Definition: HouseholdVesselDefinition(VesselActionKind.ReturnJug)),
        new(ActionPhase.Ordinary, "store_food_in_pot", [], [new VesselChoice(VesselActionKind.StoreFood).Encode()], Definition: HouseholdVesselDefinition(VesselActionKind.StoreFood)),
        new(ActionPhase.Ordinary, "take_food_from_pot", [], [new VesselChoice(VesselActionKind.TakeFood).Encode()], Definition: HouseholdVesselDefinition(VesselActionKind.TakeFood)),
        new(ActionPhase.Ordinary, "FillWaterJugPrefix", [VesselChoice.FillPrefix], [], Definition: HouseholdVesselDefinition(VesselActionKind.FillJug)),
        new(ActionPhase.Ordinary, "GatherBuildingMaterialPrefix", [GatherBuildingMaterialPrefix], [], static (runtime, action) =>
    {
        runtime.GatherBuildingMaterial(action.AgentId, action.State, action.CandidateId[GatherBuildingMaterialPrefix.Length..], action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "ExpandBuildingPrefix", [ExpandBuildingPrefix], [], static (runtime, action) =>
    {
        runtime.ApplyBuildingExpansionCandidate(action.AgentId, action.State, action.CandidateId[ExpandBuildingPrefix.Length..], originalPlan: action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "InviteHouseGuestPrefix", [InviteHouseGuestPrefix, RevokeHouseGuestPrefix], [], static (runtime, action) =>
    {
        runtime.ApplyHouseGuest(action.AgentId, action.CandidateId, action.Plan as HouseGuestActionPlan);
        return true;
    }),
        new(ActionPhase.Ordinary, "recipe", [], [], Definition: RecipeActionDefinition),
        new(ActionPhase.Ordinary, "build:", ["build:"], [], static (runtime, action) =>
    {
        runtime.BeginProject(action.AgentId, action.State, action.CandidateId, originalPlan: action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "assist:", ["assist:"], [], static (runtime, action) =>
    {
        runtime.AssistProject(action.AgentId, action.State, action.CandidateId[ProjectAssistanceChoice.Prefix.Length..], action.Plan);
        return true;
    }),
        new(ActionPhase.Ordinary, "replant_tree", [], [TreeActionPlan.Candidate(TreeChoice.Replant)], Definition: TreeDefinition),
        new(ActionPhase.Ordinary, "plant_tree", [], [TreeActionPlan.Candidate(TreeChoice.Plant), TreeActionPlan.Candidate(TreeChoice.Orchard)], Definition: TreeDefinition),
        new(ActionPhase.Ordinary, "ExplorationGoalPrefix", [ExplorationGoalPrefix], [], static (runtime, action) =>
    {
        runtime.ApplyExploration(action.Plan as ExplorationActionPlan ??
            (ExplorationChoice.Decode(action.CandidateId) is { } choice ? runtime.PlanExploration(action.AgentId, choice) : null));
        return true;
    }),
        new(ActionPhase.Ordinary, "explore", [], ["explore"], static (runtime, action) =>
    {
        runtime.ApplyExploration(action.Plan as ExplorationActionPlan ?? runtime.PlanExploration(action.AgentId, new(false)));
        return true;
    }),
        new(ActionPhase.Ordinary, "explore_return", [], ["explore_return"], static (runtime, action) =>
    {
        runtime.ApplyExploration(action.Plan as ExplorationActionPlan ?? runtime.PlanExploration(action.AgentId, new(true)));
        return true;
    }),
        new(ActionPhase.Ordinary, "wear_clothing", [], [PrivateEquipmentActionPlan.Candidate(PrivateEquipmentKind.Garment)], Definition: GarmentDefinition),
        new(ActionPhase.Ordinary, "equip_carry_aid", [], [PrivateEquipmentActionPlan.Candidate(PrivateEquipmentKind.CarryAid)], Definition: CarryAidDefinition),
        new(ActionPhase.Ordinary, "repair_equipment", [], [EquipmentRepairChoice.Candidate], Definition: EquipmentRepairDefinition),
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
        new(ChoiceGroup.Prelude, "town_salvage", static (runtime, context) => runtime.AddTownSalvageCandidates(context.Candidates, context.AgentId), null),
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
        new(ChoiceGroup.Child, "AddChildNonLandCandidates", static (runtime, context) => runtime.AddChildNonLandCandidates(context.Candidates, context.AgentId), null),
        new(ChoiceGroup.UnhurriedAdult, "AddBuildCandidates", static (runtime, context) => runtime.AddBuildCandidates(context.Candidates, context.Agent!, context.State, context.ReachableTools!, context.AccessibleWarehouses!, context.BuildingsWithoutSites!), null),
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
