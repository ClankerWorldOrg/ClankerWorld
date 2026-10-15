using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal sealed partial class ConstructionSystem
{
    internal static ConstructionActionPlan PlanConstruction(IConstructionPlanningWorld world, string actor, BuildingDefinition definition, GridPoint site, string? selectedInstance = null)
    {
        var household = world.HouseholdFor(actor);
        var candidate = TownConstructionCandidateIds.Building(definition.CanonicalId, site);
        var instruction = world.Agents[actor].Project?.CandidateId == candidate ? world.ConstructionInstructionForProject(actor, world.Agents[actor].Project) : null;
        var owner = world.BuildingConstructionOwner(actor, definition);
        return new(actor, definition, site, selectedInstance ?? instruction?.Task!.ConstructionInstanceId ?? world.BuildInstanceId(actor, definition),
            household, world.IsHouseholdBuilding(definition) ? household : null, world.TownForResident(actor), owner,
            instruction?.InstructionId, PlanConstructionInputs(world, definition.BuildCosts, owner),
            world.Reach(actor, site, ReachPurpose.Construction, 0, 0, SwimmingPolicy.WhenCapable));
    }

    internal static ConstructionMaterialInput[]? PlanConstructionInputs(IConstructionPlanningWorld world, IReadOnlyList<ContentQuantity> costs, string owner)
    {
        var selected = new List<ConstructionMaterialInput>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < costs.Count; index++)
        {
            var cost = costs[index];
            var remaining = cost.Amount;
            foreach (var match in world.ConstructionGoods(world.Society.Inventory,
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

    internal static ActorCapabilityPolicy ConstructionCapabilities(BuildingDefinition definition) =>
        new(ActionLifeStages.Adults, AllowUrgentFood: false,
            AllowUrgentWarmth: definition.Tags.Any(tag => tag is "shelter" or "warmth" or "cooking"), AllowActiveOrder: true);

    internal ActionCheck CheckConstruction(IConstructionPlanningWorld world, ConstructionActionPlan plan, bool checkDestination = true, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? ConstructionCapabilities(plan.Definition));
        if (!capability.Allowed) return capability;
        if (world.HouseholdFor(plan.Actor) != plan.ActorHouseholdId || world.TownForResident(plan.Actor) != plan.TownId ||
            !world.Content.Buildings.Any(definition => definition.CanonicalId == plan.Definition.CanonicalId && definition.PayloadDigest == plan.Definition.PayloadDigest) ||
            world.IsHouseholdBuilding(plan.Definition) && plan.HouseholdId is null ||
            State.Buildings.Any(building => building.InstanceId == plan.InstanceId) || world.BuildingIdentityIsReserved(plan.InstanceId, plan.InstructionId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected building design, identity or ownership has changed");
        if (world.HouseholdBuildingKind(plan.Definition) is { } kind && plan.HouseholdId is { } household &&
            (world.HouseholdBuildingWithTag(household, kind) is not null || kind == "silo" && world.FarmhouseForHousehold(household) is null ||
                world.HouseholdBuildingProjectInProgress(household, kind, plan.Actor) &&
                !(world.Agents[plan.Actor].Project is { } own && own.CandidateId == plan.CandidateId)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household building is already held or planned");
        if (plan.Definition.Tags.Contains("warehouse", StringComparer.Ordinal) &&
            (plan.TownId is null || State.Buildings.Any(building => building.TownId == plan.TownId &&
                world.Content.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId && definition.Tags.Contains("warehouse", StringComparer.Ordinal)))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Town already has a Warehouse or is unavailable");
        if (checkDestination && !TownLayoutService.TryEvaluateConstructionSite(world.CreateTownLayoutContext(plan.Actor, plan.Site, plan.Definition), plan.Definition, plan.Site, out _))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected building footprint is no longer legal");
        var inputs = CheckConstructionInputs(world, plan.MaterialOwnerId, plan.Inputs);
        if (!inputs.Allowed) return inputs;
        return !checkDestination || world.Agents[plan.Actor].Position == plan.Site || world.Reach(plan.Actor, plan.Site, ReachPurpose.Construction, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected construction site is out of reach");
    }

    internal static ActionCheck CheckConstructionInputs(IConstructionPlanningWorld world, string owner, IReadOnlyList<ConstructionMaterialInput>? inputs) =>
        inputs is not null && inputs.GroupBy(input => (input.LotId, input.ItemKind)).Any(group =>
            world.ConstructionGoods(world.Society.Inventory, new(GoodsUse.Holdings, null, GoodsOwners.One(owner), GoodsKinds.One(group.Key.ItemKind)))
                .FirstOrDefault(match => match.Lot.Id == group.Key.LotId) is not { } match || match.Quantity < group.Sum(input => input.Quantity))
            ? ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected construction material lots are unavailable") : new();

}
