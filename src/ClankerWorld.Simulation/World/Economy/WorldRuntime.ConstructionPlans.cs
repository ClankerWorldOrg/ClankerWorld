using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private readonly Dictionary<string, (long StartedTick, ConstructionActionPlan Plan)> constructionProjectPlans = new(StringComparer.Ordinal);

    private void CopyConstructionProjectPlansFrom(WorldRuntime source)
    {
        if (ReferenceEquals(this, source)) return;
        constructionProjectPlans.Clear();
        foreach (var (actor, retained) in source.constructionProjectPlans)
            if (agents.TryGetValue(actor, out var person) && person.Project is { Stage: not ("completed" or "cancelled") } project &&
                project.StartedTick == retained.StartedTick && project.CandidateId == retained.Plan.CandidateId &&
                project.OrderInstructionId == retained.Plan.InstructionId)
                constructionProjectPlans.Add(actor, retained);
    }

    private ConstructionActionPlan PlanConstruction(string actor, BuildingDefinition definition, GridPoint site, string? selectedInstance = null)
    {
        var household = HouseholdFor(actor);
        var candidate = TownConstructionCandidateIds.Building(definition.CanonicalId, site);
        var instruction = agents[actor].Project?.CandidateId == candidate ? ConstructionInstructionForProject(actor, agents[actor].Project) : null;
        var owner = BuildingConstructionOwner(actor, definition);
        return new(actor, definition, site, selectedInstance ?? instruction?.Task!.ConstructionInstanceId ?? BuildInstanceId(actor, definition),
            household, definition.Tags.Any(IsHouseholdBuildingTag) ? household : null, TownForResident(actor), owner,
            instruction?.InstructionId, PlanConstructionInputs(definition.BuildCosts, owner),
            Reach(actor, site, ReachPurpose.Construction, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private ConstructionMaterialInput[]? PlanConstructionInputs(IReadOnlyList<ContentQuantity> costs, string owner)
    {
        var selected = new List<ConstructionMaterialInput>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < costs.Count; index++)
        {
            var cost = costs[index];
            var remaining = cost.Amount;
            foreach (var match in ProductionGoods(society.Checkpoint.Inventory,
                         new(GoodsUse.Holdings, null, GoodsOwners.One(owner), GoodsKinds.One(cost.ResourceId))))
            {
                var quantity = Math.Min(remaining, Math.Max(0, match.Quantity - used.GetValueOrDefault(match.Lot.Id)));
                if (quantity == 0) continue;
                selected.Add(new(index, match.Lot.Id, cost.ResourceId, quantity));
                used[match.Lot.Id] = used.GetValueOrDefault(match.Lot.Id) + quantity;
                remaining -= quantity;
                if (remaining == 0) break;
            }
            if (remaining > 0) return null;
        }
        return selected.ToArray();
    }

    private static ActorCapabilityPolicy ConstructionCapabilities(BuildingDefinition definition) =>
        new(ActionLifeStages.Adults, AllowUrgentFood: false,
            AllowUrgentWarmth: definition.Tags.Any(tag => tag is "shelter" or "warmth" or "cooking"), AllowActiveOrder: true);

    private ActionCheck CheckConstruction(ConstructionActionPlan plan, bool checkDestination = true, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? ConstructionCapabilities(plan.Definition));
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.ActorHouseholdId || TownForResident(plan.Actor) != plan.TownId ||
            !worldContent.Buildings.Any(definition => definition.CanonicalId == plan.Definition.CanonicalId && definition.PayloadDigest == plan.Definition.PayloadDigest) ||
            plan.Definition.Tags.Any(IsHouseholdBuildingTag) && plan.HouseholdId is null ||
            worldSimulation.Buildings.Any(building => building.InstanceId == plan.InstanceId) || BuildingIdentityIsReserved(plan.InstanceId, plan.InstructionId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected building design, identity or ownership has changed");
        if (HouseholdBuildingKind(plan.Definition) is { } kind && plan.HouseholdId is { } household &&
            (HouseholdBuildingWithTag(household, kind) is not null || kind == "silo" && FarmhouseForHousehold(household) is null ||
                HouseholdBuildingProjectInProgress(household, kind, plan.Actor) &&
                !(agents[plan.Actor].Project is { } own && own.CandidateId == plan.CandidateId)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household building is already held or planned");
        if (plan.Definition.Tags.Contains("warehouse", StringComparer.Ordinal) &&
            (plan.TownId is null || worldSimulation.Buildings.Any(building => building.TownId == plan.TownId &&
                worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId && definition.Tags.Contains("warehouse", StringComparer.Ordinal)))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Town already has a Warehouse or is unavailable");
        if (checkDestination && !TownLayoutService.TryEvaluateConstructionSite(CreateTownLayoutContext(plan.Actor, plan.Site, plan.Definition), plan.Definition, plan.Site, out _))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected building footprint is no longer legal");
        var inputs = CheckConstructionInputs(plan.MaterialOwnerId, plan.Inputs);
        if (!inputs.Allowed) return inputs;
        return !checkDestination || agents[plan.Actor].Position == plan.Site || Reach(plan.Actor, plan.Site, ReachPurpose.Construction, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected construction site is out of reach");
    }

    private ActionCheck CheckConstructionInputs(string owner, IReadOnlyList<ConstructionMaterialInput>? inputs) =>
        inputs is not null && inputs.GroupBy(input => (input.LotId, input.ItemKind)).Any(group =>
            ProductionGoods(society.Checkpoint.Inventory, new(GoodsUse.Holdings, null, GoodsOwners.One(owner), GoodsKinds.One(group.Key.ItemKind)))
                .FirstOrDefault(match => match.Lot.Id == group.Key.LotId) is not { } match || match.Quantity < group.Sum(input => input.Quantity))
            ? ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected construction material lots are unavailable") : new();

    private ConstructionActionPlan? RetainedConstruction(string actor, AgentProject project) =>
        constructionProjectPlans.TryGetValue(actor, out var retained) && retained.StartedTick == project.StartedTick &&
        retained.Plan.CandidateId == project.CandidateId && retained.Plan.InstructionId == project.OrderInstructionId ? retained.Plan : null;
}
