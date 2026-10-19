using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// A household plans only the buildings it needs for itself: one of each
/// household kind it does not hold, and only once the materials are in hand.
/// Buildings the Town shares wait for governance and are never offered here.
/// </summary>
public sealed partial class WorldRuntime
{
    private const string GatherBuildingMaterialPrefix = BuildingMaterialChoice.Prefix;
    // Provisional balance: tune the shared offer/admission limit here.
    private const int ResidentsPerSharedShop = 8;

    private void AddHouseholdBuildingPlans(List<CognitionCandidate> candidates, SocietyAgent agent,
        bool needsWarmth, string householdId, List<string> buildingsWithoutSites)
    {
        TownLayoutContext? sharedLayout = null;
        foreach (var definition in PlannableHouseholdBuildings(householdId, agent.Id)
                     .Where(building => !needsWarmth || building.Tags.Any(tag => tag is "shelter" or "warmth" or "cooking")))
        {
            // The identity reads the same actor, design and reservations for this
            // synchronous query; compute it once rather than for every building.
            var instanceId = BuildInstanceId(agent.Id, definition);
            if (worldSimulation.Buildings.Any(item => item.InstanceId == instanceId) ||
                !HouseholdHasMaterialsInHand(householdId, definition.BuildCosts) ||
                !WorkBuildingHasInputs(agent.Id, householdId, definition))
                continue;

            var layout = HouseholdBuildingKind(definition) == "silo"
                ? CreateTownLayoutContext(agent.Id, building: definition)
                : sharedLayout ??= CreateTownLayoutContext(agent.Id);
            var sites = TownLayoutService.RankConstructionSites(layout, definition);
            if (sites.Count == 0) buildingsWithoutSites.Add(definition.DisplayName);
            for (var rank = 0; rank < sites.Count; rank++)
            {
                var site = sites[rank];
                var description = string.Join(" ", site.Reasons.Select(reason => reason.Description));
                var plan = PlanConstruction(agent.Id, definition, site.Position, instanceId);
                if (CheckConstruction(plan).Allowed) candidates.Add(new CognitionCandidate(
                    plan.CandidateId,
                    $"Plan {definition.DisplayName} at ({site.Position.X}, {site.Position.Y}): {description}",
                    20 + rank + HouseholdBuildingKinds.PlanOrder(HouseholdBuildingKind(definition)),
                    $"build-site:{site.Position.X},{site.Position.Y}")
                { ActionPlan = plan });
            }
        }

        AddBuildingMaterialCandidate(candidates, agent.Id, householdId);
    }

    /// <summary>Active building designs this household may plan now, in a stable order.</summary>
    private IEnumerable<BuildingDefinition> PlannableHouseholdBuildings(string householdId, string actor) => worldContent.Buildings
        .Where(definition => !RetiredBuildings.Contains(definition) &&
            HouseholdBuildingKind(definition) is { } kind && HouseholdMayPlan(householdId, kind, actor))
        .OrderBy(definition => HouseholdBuildingKinds.PlanOrder(HouseholdBuildingKind(definition)))
        .ThenBy(definition => definition.CanonicalId, StringComparer.Ordinal);

    private bool HouseholdMayPlan(string householdId, string kind, string actor) =>
        HouseholdBuildingWithTag(householdId, kind) is null &&
        TownHasRoomForSharedShop(actor, kind) &&
        (kind != "silo" || FarmhouseForHousehold(householdId) is not null) &&
        (kind != AnimalContent.YardTag || HouseForHousehold(householdId) is not null) &&
        !HouseholdBuildingProjectInProgress(householdId, kind, actor);

    /// <summary>Placed shops and other residents' live construction plans hold a Town slot.</summary>
    private bool TownHasRoomForSharedShop(string actor, string? kind)
    {
        if (kind is not ("store" or "tailor" or "restaurant" or "clinic" or "blacksmith"))
            return true;
        if (TownForResident(actor) is not { } townId)
            return false;
        var town = towns.Single(item => item.Id == townId);
        var limit = (town.ResidentIds.Count + ResidentsPerSharedShop - 1) / ResidentsPerSharedShop;
        var placed = worldSimulation.Buildings.Count(building => building.TownId == townId &&
            worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                HouseholdBuildingKind(definition) == kind));
        var planned = agents.Values.Count(person => person.AgentId != actor &&
            TownForResident(person.AgentId) == townId &&
            person.Project is { Stage: not ("completed" or "cancelled") } project &&
            TownConstructionCandidateIds.TryParse(project.CandidateId, out var selection) && selection.IsBuilding &&
            worldContent.Buildings.Any(definition => definition.CanonicalId == selection.DefinitionId &&
                HouseholdBuildingKind(definition) == kind));
        return placed + planned < limit;
    }

    /// <summary>Whether a member already has a live plan for this kind, so the household plans at most one.</summary>
    private bool HouseholdBuildingProjectInProgress(string householdId, string? kind = null,
        string? retryingActor = null) =>
        agents.Values.Any(person =>
            person.Project is { } project && project.Stage is not ("completed" or "cancelled") &&
            TownConstructionCandidateIds.TryParse(project.CandidateId, out var selection) && selection.IsBuilding &&
            society.Checkpoint.GetAgent(person.AgentId).HouseholdId == householdId &&
            worldContent.Buildings.FirstOrDefault(definition => definition.CanonicalId == selection.DefinitionId) is { } planned &&
            HouseholdBuildingKind(planned) is { } plannedKind && (kind is null || plannedKind == kind) &&
            !(person.AgentId == retryingActor &&
                (project.RequiresFreshChoice || project.Stage == "blocked" &&
                    WorldTick - project.LastTransitionTick >= BlockedProjectRetryDelayTicks)));

    /// <summary>Usable materials the household or its members own, wherever they store them.</summary>
    private bool HouseholdHasMaterialsInHand(string householdId, IReadOnlyList<ContentQuantity> costs) =>
        costs.All(cost => HouseholdMaterialInHand(householdId, cost.ResourceId) >= cost.Amount);

    private long HouseholdMaterialInHand(string householdId, string itemKind) =>
        HoldingsTotal(agents.Keys.Where(actor => HouseholdFor(actor) == householdId).Prepend(householdId),
            GoodsKinds.One(itemKind));

    /// <summary>
    /// The first material missing for the first building the household still
    /// needs, when it can be gathered from a reachable source. One such offer
    /// at a time keeps the choice list short.
    /// </summary>
    private (BuildingDefinition Building, ContentQuantity Material, MapResource Source)? NeededBuildingMaterial(string actor, string householdId)
    {
        if (HouseholdBuildingProjectInProgress(householdId, retryingActor: actor))
            return null;
        foreach (var definition in PlannableHouseholdBuildings(householdId, actor))
        {
            var missing = definition.BuildCosts.FirstOrDefault(cost =>
                HouseholdMaterialInHand(householdId, cost.ResourceId) < cost.Amount);
            if (missing.Amount == 0 || !WorkBuildingHasInputs(actor, householdId, definition))
                continue;
            if (MaterialSource(missing.ResourceId, actor, HouseForHousehold(householdId)?.Position ?? SharedStoragePosition,
                HouseForHousehold(householdId) is null ? ResourceInteractionRange : 0, useHarvestBonus: false) is { } source)
                return (definition, missing, source);
        }
        return null;
    }

    private void AddBuildingMaterialCandidate(List<CognitionCandidate> candidates, string actor, string householdId)
    {
        var plan = PlanBuildingMaterial(actor, householdId);
        if (plan is null || !CheckBuildingMaterial(plan).Allowed) return;
        var load = plan.Harvest.Quantity > 4 ? "using its faster whole load" : "as a smaller whole load that fits your carrying space";
        candidates.Add(new CognitionCandidate(plan.CandidateId,
            $"Gather {plan.Material.ResourceId} {load} so the household has what it needs to build its own {plan.Building.DisplayName}.",
            34)
        { ActionPlan = plan });
    }

    /// <summary>Use the faster harvest only when its complete output fits the current carry space.</summary>
    private bool UseHarvestBonusForBuildingMaterial(string actor, string itemKind, MapResource source,
        GridPoint? returnTo = null, int returnRange = 0)
    {
        var plan = ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus: true);
        return plan is { ToolLotId: not null, Quantity: > 4 } &&
            FreeCarryCapacity(actor) >= checked(plan.Quantity + plan.TreeSeedQuantity) &&
            (returnTo is null || CanReturnWithHarvest(actor, itemKind, source, returnTo.Value, returnRange));
    }

    /// <summary>
    /// Store a spare generic tool at home so preparation materials have room
    /// to travel. Building work uses an available hammer or proceeds by hand.
    /// </summary>
    private InventoryLot? BuildingPreparationToolToStore(string actor, string householdId, string? sourceLotId = null)
    {
        if (agents[actor].Project is { Stage: not ("completed" or "cancelled") } ||
            HouseForHousehold(householdId) is not { } house || DestinationRoom(house.InstanceId) == 0 ||
            NeededBuildingMaterial(actor, householdId) is not { } need ||
            MaterialSource(need.Material.ResourceId, actor, HouseForHousehold(householdId)?.Position ?? SharedStoragePosition,
                HouseForHousehold(householdId) is null ? ResourceInteractionRange : 0, useHarvestBonus: false) is not { } source)
            return null;

        var free = FreeCarryCapacity(actor);
        var required = ProjectMaterialCarryUnits(actor, need.Material.ResourceId, source, useHarvestBonus: false);
        if (free >= required)
            return null;

        var tool = SpendingGoods(actor, GoodsUse.ConsumeAt, [actor, householdId], "tool").Select(match => match.Lot).Where(lot =>
                (lot.OwnerId == actor || lot.OwnerId == householdId) &&
                PersonalEquipmentRules.IsCarried(lot, actor) && lot.DeliveryBuildingId is null &&
                lot.ContainerLotId is null &&
                lot.ItemKind == "tool" && AvailableLotQuantity(lot) > 0 && (sourceLotId is null || lot.Id == sourceLotId))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();
        return tool is not null && free + Math.Min(1, AvailableLotQuantity(tool)) >= required ? tool : null;
    }

    private void GatherBuildingMaterial(string actor, AgentState state, string itemKind, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as BuildingMaterialActionPlan;
        if (plan is null && HouseholdFor(actor) is { } household)
            plan = PlanBuildingMaterial(actor, household);
        if (plan is not null && plan.Choice.ItemKind == itemKind) ApplyBuildingMaterial(plan);
    }
}
