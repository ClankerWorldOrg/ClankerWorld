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
    private sealed record ActionRegistration(ActionPhase Phase, string Name, string[] Prefixes, string[] ExactIds, Func<WorldRuntime, ActionContext, bool> Apply);
    private sealed record ChoiceRegistration(ChoiceGroup Group, string Name, Action<WorldRuntime, ChoiceContext> Add, Func<WorldRuntime, ChoiceContext, bool>? Enabled = null);
    private static readonly IReadOnlyList<ActionRegistration> ActionRegistrations = Array.AsReadOnly<ActionRegistration>(
    [
        new(ActionPhase.BeforeAge, "IsOrnamentCandidate", [], [], static (runtime, action) =>
    {
        return IsOrnamentCandidate(action.CandidateId);
    }),
        new(ActionPhase.BeforeAge, "ToolRequestPrefix", [ToolRequestPrefix], [], static (runtime, action) =>
    {
        runtime.ApplyToolMakingRequestCandidate(action.AgentId, action.State, action.CandidateId, action.ReportIdle);
        return true;
    }),
        RegisterConversationActions(),
        new(ActionPhase.BeforeAge, "TryApplyGuardianPlacementCandidate", [], [], static (runtime, action) =>
    {
        return runtime.TryApplyGuardianPlacementCandidate(action.AgentId, action.CandidateId);
    }),
        new(ActionPhase.BeforeAge, "guardian_end:", ["guardian_end:"], [], static (runtime, action) =>
    {
        if (!runtime.HasGuardianPlacementTask(action.AgentId))
            return false;
        runtime.ApplyDependentCareCandidate(action.AgentId, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "medical_", ["medical_"], [], static (runtime, action) =>
    {
        runtime.ApplyMedicalCandidate(action.AgentId, action.State, action.CandidateId);
        return true;
    }),
        RegisterFieldActions(),
        new(ActionPhase.Ordinary, "child_", ["child_"], [], static (runtime, action) =>
    {
        runtime.ApplyChildPlan(action.Plan as ChildActionPlan ?? runtime.PlanChildCandidate(action.AgentId, action.CandidateId));
        return true;
    }),
        new(ActionPhase.Ordinary, "guardian_", ["guardian_"], [], static (runtime, action) =>
    {
        runtime.ApplyDependentCareCandidate(action.AgentId, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "parent_", ["parent_", "care:"], [], static (runtime, action) =>
    {
        runtime.ApplyParenthoodCandidate(action.AgentId, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "partner_", ["partner_"], [], static (runtime, action) =>
    {
        runtime.ApplyFamilyCandidate(action.AgentId, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "household_", ["household_"], [], static (runtime, action) =>
    {
        runtime.ApplyHousingCandidate(action.AgentId, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "learn:", ["learn:", "lesson_"], [], static (runtime, action) =>
    {
        runtime.ApplyLearningCandidate(action.AgentId, action.CandidateId);
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
        runtime.ApplyTownProjectCandidate(action.AgentId, action.State, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "IsMarketCandidate", [], [], static (runtime, action) =>
    {
        return IsMarketCandidate(action.CandidateId);
    }),
        RegisterFoodPolicyActions(),
        new(ActionPhase.Ordinary, "trade_", ["trade_"], [], static (runtime, action) =>
    {
        runtime.ApplyTradeCandidate(action.AgentId, action.State, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "shop_", ["shop_"], [], static (runtime, action) =>
    {
        runtime.ApplyShopCandidate(action.AgentId, action.State, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "haul_household_stock", [], ["haul_household_stock"], static (runtime, action) =>
    {
        runtime.ApplyHouseholdStockPlan(action.Plan as HouseholdStockActionPlan ?? runtime.PlanHouseholdStock(action.AgentId, action.CandidateId));
        return true;
    }),
        new(ActionPhase.Ordinary, "recover_household_delivery", [], ["recover_household_delivery"], static (runtime, action) =>
    {
        runtime.ApplyHouseholdStockPlan(action.Plan as HouseholdStockActionPlan ?? runtime.PlanHouseholdStock(action.AgentId, action.CandidateId));
        return true;
    }),
        new(ActionPhase.Ordinary, "store_household_food", [], ["store_household_food"], static (runtime, action) =>
    {
        runtime.ApplyHouseholdStockPlan(action.Plan as HouseholdStockActionPlan ?? runtime.PlanHouseholdStock(action.AgentId, action.CandidateId));
        return true;
    }),
        new(ActionPhase.Ordinary, "make_room_for_food", [], ["make_room_for_food"], static (runtime, action) =>
    {
        runtime.ApplyFoodCargoPlan(action.Plan as FoodCargoActionPlan ?? runtime.PlanFoodCargo(action.AgentId));
        return true;
    }),
        new(ActionPhase.Ordinary, "store_town_resources", [], ["store_town_resources"], static (runtime, action) =>
    {
        runtime.ApplyTownStockPlan(action.Plan as TownStockActionPlan ?? runtime.PlanWarehouseSurplus(action.AgentId));
        return true;
    }),
        new(ActionPhase.Ordinary, "TownSalvagePrefix", [TownSalvagePrefix], [], static (runtime, action) =>
    {
        runtime.ApplyTownStockPlan(action.Plan as TownStockActionPlan ?? runtime.townSystem.PlanTownSalvage(runtime.civicWorld, action.AgentId, action.CandidateId));
        return true;
    }),
        new(ActionPhase.Ordinary, "haul_farm_grain", [], ["haul_farm_grain"], static (runtime, action) =>
    {
        runtime.ApplyFarmStockAction(action.Plan as FarmStockActionPlan ?? runtime.PlanFarmStockAction(action.AgentId, FarmStockActionKind.Grain));
        return true;
    }),
        new(ActionPhase.Ordinary, "haul_farm_flour", [], ["haul_farm_flour"], static (runtime, action) =>
    {
        runtime.ApplyFarmStockAction(action.Plan as FarmStockActionPlan ?? runtime.PlanFarmStockAction(action.AgentId, FarmStockActionKind.Flour));
        return true;
    }),
        new(ActionPhase.Ordinary, "haul_smith_input", [], ["haul_smith_input"], static (runtime, action) =>
    {
        runtime.HaulBlacksmithInput(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "gather_smith_ore", [], ["gather_smith_ore"], static (runtime, action) =>
    {
        runtime.GatherBlacksmithOre(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "GatherBlacksmithInputPrefix", [GatherBlacksmithInputPrefix], [], static (runtime, action) =>
    {
        runtime.GatherBlacksmithInput(action.AgentId, action.State, action.CandidateId[GatherBlacksmithInputPrefix.Length..]);
        return true;
    }),
        new(ActionPhase.Ordinary, "deliver_smith_ore", [], ["deliver_smith_ore"], static (runtime, action) =>
    {
        runtime.DeliverBlacksmithOre(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "collect_wooden_axe", [], ["collect_wooden_axe", "collect_wooden_pickaxe", "collect_wooden_hoe"], static (runtime, action) =>
    {
        var kind = action.CandidateId == "collect_wooden_axe" ? "wooden_axe" : action.CandidateId == "collect_wooden_hoe" ? FarmFieldRules.Hoe : "wooden_pickaxe";
        if (runtime.MayCollectToolFamily(action.AgentId, ToolProgressionRules.Find(kind)!.Family))
            runtime.CollectEquipment(action.AgentId, action.State, kind);
        return true;
    }),
        new(ActionPhase.Ordinary, "CollectToolPrefix", [CollectToolPrefix], [], static (runtime, action) =>
    {
        var kind = action.CandidateId[CollectToolPrefix.Length..];
        if (ToolProgressionRules.Find(kind)is { } tool && runtime.MayCollectToolFamily(action.AgentId, tool.Family))
            runtime.CollectEquipment(action.AgentId, action.State, kind);
        return true;
    }),
        new(ActionPhase.Ordinary, "ApplyBoatCandidate", [], [], static (runtime, action) =>
    {
        return runtime.ApplyBoatCandidate(action.AgentId, action.CandidateId);
    }),
        RegisterAnimalActions(),
        new(ActionPhase.Ordinary, "ApplyHandcartCandidate", [], [], static (runtime, action) =>
    {
        return runtime.ApplyHandcartCandidate(action.AgentId, action.State, action.CandidateId);
    }),
        new(ActionPhase.Ordinary, "RepairToolPrefix", [RepairToolPrefix], [], static (runtime, action) =>
    {
        runtime.RepairTool(action.AgentId, action.State, action.CandidateId[RepairToolPrefix.Length..]);
        return true;
    }),
        new(ActionPhase.Ordinary, "RareMiningPrefix", [RareMiningPrefix], [], static (runtime, action) =>
    {
        runtime.GatherRareMaterial(action.AgentId, action.State, action.CandidateId[RareMiningPrefix.Length..]);
        return true;
    }),
        new(ActionPhase.Ordinary, "KnowledgeWritePrefix", [KnowledgeWritePrefix, KnowledgeCopyPrefix, KnowledgeReadPrefix], ["knowledge_continue"], static (runtime, action) =>
    {
        runtime.ApplyKnowledgeWritingCandidate(action.AgentId, action.State, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "ApplyKnowledgeStorageCandidate", [], [], static (runtime, action) =>
    {
        return runtime.ApplyKnowledgeStorageCandidate(action.AgentId, action.State, action.CandidateId);
    }),
        new(ActionPhase.Ordinary, "KnowledgeSharePrefix", [KnowledgeSharePrefix], [], static (runtime, action) =>
    {
        runtime.ApplyKnowledgeShare(action.AgentId, action.State, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "SupplyWorkstationPrefix", [SupplyWorkstationPrefix], [], static (runtime, action) =>
    {
        runtime.ApplyWorkstationSupplyPlan(action.Plan as WorkstationSupplyActionPlan ??
            runtime.PlanWorkstationSupplyCandidate(action.AgentId, action.CandidateId));
        return true;
    }),
        new(ActionPhase.Ordinary, "ReturnEmptyVesselPrefix", [ReturnEmptyVesselPrefix], [], static (runtime, action) =>
    {
        runtime.ReturnEmptyVessel(action.AgentId, action.State, action.CandidateId[ReturnEmptyVesselPrefix.Length..]);
        return true;
    }),
        new(ActionPhase.Ordinary, "collect_water_jug", [], ["collect_water_jug"], static (runtime, action) =>
    {
        runtime.CollectWaterJug(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "return_water_jug", [], ["return_water_jug"], static (runtime, action) =>
    {
        runtime.ReturnWaterJug(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "store_food_in_pot", [], ["store_food_in_pot"], static (runtime, action) =>
    {
        runtime.StoreFoodInPot(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "take_food_from_pot", [], ["take_food_from_pot"], static (runtime, action) =>
    {
        runtime.TakeFoodFromPot(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "FillWaterJugPrefix", [FillWaterJugPrefix], [], static (runtime, action) =>
    {
        runtime.FillWaterJug(action.AgentId, action.State, action.CandidateId[FillWaterJugPrefix.Length..]);
        return true;
    }),
        new(ActionPhase.Ordinary, "GatherBuildingMaterialPrefix", [GatherBuildingMaterialPrefix], [], static (runtime, action) =>
    {
        runtime.GatherBuildingMaterial(action.AgentId, action.State, action.CandidateId[GatherBuildingMaterialPrefix.Length..]);
        return true;
    }),
        new(ActionPhase.Ordinary, "ExpandBuildingPrefix", [ExpandBuildingPrefix], [], static (runtime, action) =>
    {
        runtime.ApplyBuildingExpansionCandidate(action.AgentId, action.State, action.CandidateId[ExpandBuildingPrefix.Length..]);
        return true;
    }),
        new(ActionPhase.Ordinary, "InviteHouseGuestPrefix", [InviteHouseGuestPrefix, RevokeHouseGuestPrefix], [], static (runtime, action) =>
    {
        var invited = action.CandidateId.StartsWith(InviteHouseGuestPrefix, StringComparison.Ordinal);
        var household = runtime.society.Checkpoint.GetAgent(action.AgentId).HouseholdId;
        if (household is not null && runtime.HouseForHousehold(household)is { } house)
            runtime.SetHouseGuestInvitationCore(action.AgentId, house.InstanceId, action.CandidateId[(invited ? InviteHouseGuestPrefix.Length : RevokeHouseGuestPrefix.Length)..], invited);
        return true;
    }),
        new(ActionPhase.Ordinary, "build:", ["build:"], [], static (runtime, action) =>
    {
        runtime.BeginProject(action.AgentId, action.State, action.CandidateId);
        return true;
    }),
        new(ActionPhase.Ordinary, "assist:", ["assist:"], [], static (runtime, action) =>
    {
        runtime.AssistProject(action.AgentId, action.State, action.CandidateId[7..]);
        return true;
    }),
        new(ActionPhase.Ordinary, "replant_tree", [], ["replant_tree"], static (runtime, action) =>
    {
        runtime.ReplantTree(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "plant_tree", [], ["plant_tree", "plant_orchard"], static (runtime, action) =>
    {
        runtime.PlantTreeNearby(action.AgentId, action.State, action.CandidateId == "plant_orchard");
        return true;
    }),
        new(ActionPhase.Ordinary, "ExplorationGoalPrefix", [ExplorationGoalPrefix], [], static (runtime, action) =>
    {
        var parts = action.CandidateId[ExplorationGoalPrefix.Length..].Split(':');
        if (parts.Length == 2 && ValidExplorationGoal(new(parts[0], parts[1])))
            runtime.Explore(action.AgentId, action.State, new(parts[0], parts[1]));
        return true;
    }),
        new(ActionPhase.Ordinary, "explore", [], ["explore"], static (runtime, action) =>
    {
        runtime.Explore(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "explore_return", [], ["explore_return"], static (runtime, action) =>
    {
        runtime.ChooseExplorationReturn(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "wear_clothing", [], ["wear_clothing"], static (runtime, action) =>
    {
        runtime.EquipPrivateItem(action.AgentId, action.State, carryAid: false);
        return true;
    }),
        new(ActionPhase.Ordinary, "equip_carry_aid", [], ["equip_carry_aid"], static (runtime, action) =>
    {
        runtime.EquipPrivateItem(action.AgentId, action.State, carryAid: true);
        return true;
    }),
        new(ActionPhase.Ordinary, "repair_equipment", [], ["repair_equipment"], static (runtime, action) =>
    {
        runtime.RepairEquipment(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "tend_fire", [], ["tend_fire"], static (runtime, action) =>
    {
        runtime.TendFire(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "seek_warmth", [], ["seek_warmth"], static (runtime, action) =>
    {
        runtime.SeekWarmth(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "seek_food", [], ["seek_food"], static (runtime, action) =>
    {
        if (runtime.AvailableFoodSource(action.AgentId, action.State.Position)is { } foodSource)
            runtime.MoveToward(action.AgentId, action.State, foodSource.Position, "food", ResourceInteractionRange);
        return true;
    }),
        new(ActionPhase.Ordinary, "harvest_food", [], ["harvest_food"], static (runtime, action) =>
    {
        runtime.HarvestFood(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "consume_food", [], ["consume_food"], static (runtime, action) =>
    {
        runtime.ConsumeFood(action.AgentId, action.State);
        return true;
    }),
        new(ActionPhase.Ordinary, "collect_shared_food", [], ["collect_shared_food"], static (runtime, action) =>
    {
        runtime.CollectSharedFood(action.AgentId, action.State);
        return true;
    })
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
        new(ChoiceGroup.Final, "safe_idle", static (runtime, context) => context.Candidates.Add(new CognitionCandidate("safe_idle", "Continue safely without starting a new task.", 100)), null)
    ]);
    private bool TryRegisteredAction(ActionPhase phase, ActionContext action)
    {
        foreach (var registration in ActionRegistrations)
        {
            if (registration.Phase != phase)
                continue;
            if (registration.Prefixes.Length != 0 || registration.ExactIds.Length != 0)
            {
                if (!MatchesAction(registration, action.CandidateId))
                    continue;
            }

            if (registration.Apply(this, action))
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
        var hasFood = PreferredFood(agentId, agentId).Any();
        if (hasFood && state.FullnessBasisPoints < ComfortableFullness)
        {
            candidates.Add(new CognitionCandidate("consume_food", "Eat one carried food item.", 0));
        }

        if (CarriedMilk(agentId) is not null && state.FullnessBasisPoints < ComfortableFullness)
            candidates.Add(new("drink_milk", "Drink one portion of your carried milk.", 0));
        if (instructionCandidate == "consume_food" && hasFood && state.FullnessBasisPoints < ComfortableFullness && !candidates.Any(item => item.Id == "consume_food"))
        {
            candidates.Add(new CognitionCandidate("consume_food", "Follow the owner's food instruction.", 0));
        }

        var foodPriority = NeedsUrgentFood(state) ? 2 : state.FullnessBasisPoints < RoutineFoodSeekFullness ? 5 : 90;
        // An optional reserve remains selectable without outranking ordinary activities.
        var wantsFood = !hasFood && state.FullnessBasisPoints < 7_000;
        var shouldGatherFood = wantsFood && FreeCarryCapacity(agentId) > 0;
        var foodSource = shouldGatherFood || instructionCandidate is "seek_food" or "harvest_food" ? AvailableFoodSource(agentId, state.Position) : null;
        var sharedFood = shouldGatherFood ? AvailableSharedFood(agentId) : null;
        if (sharedFood is not null && contentRegistry.ExportState().Packages.Any(package => package.Manifest.PackageId == StarterContent.PackageId && package.Lifecycle == ContentPackageLifecycle.Active))
        {
            candidates.Add(new CognitionCandidate("collect_shared_food", "Collect one available household food serving from its store, then eat it.", foodPriority - 1, HouseholdId));
        }

        if (shouldGatherFood && foodSource is not null && IsWithinInteractionRange(state.Position, foodSource.Position, ResourceInteractionRange))
        {
            candidates.Add(new CognitionCandidate("harvest_food", "Gather several food servings from the nearby food source.", foodPriority, foodSource.Id));
        }
        else if (shouldGatherFood && foodSource is not null)
        {
            candidates.Add(new CognitionCandidate("seek_food", "Travel within gathering range of an available food source.", foodPriority, foodSource.Id));
        }
        else if (wantsFood && sharedFood is null)
        {
            AddMakeRoomForFoodCandidate(candidates, agentId, state, foodPriority);
        }

        if (instructionCandidate == "seek_food" && foodSource is not null && !candidates.Any(item => item.Id == "seek_food"))
        {
            candidates.Add(new CognitionCandidate("seek_food", "Follow the owner's travel instruction.", 0, foodSource.Id));
        }

        if (instructionCandidate == "harvest_food" && foodSource is not null && IsWithinInteractionRange(state.Position, foodSource.Position, ResourceInteractionRange) && !candidates.Any(item => item.Id == "harvest_food"))
        {
            candidates.Add(new CognitionCandidate("harvest_food", "Follow the owner's harvest instruction.", 0, foodSource.Id));
        }
    }
}
