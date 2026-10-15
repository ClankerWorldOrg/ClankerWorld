using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    private void OfferTownSettlement(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        bool resettle, string text)
    {
        var previous = TownForResident(world, actor);
        var plan = new TownFoundingActionPlan(actor, new(town.Id, resettle), town, world.Agents[actor].Position,
            previous, TownCareGroup(world, actor, previous));
        if (CheckTownFoundingPlan(world, plan).Allowed)
            candidates.Add(new(plan.CandidateId, text, 170) { ActionPlan = plan });
    }

    internal TownFoundingActionPlan? PlanTownFounding(ITownWorld world, string actor, TownFoundingChoice choice)
    {
        if (!world.Agents.TryGetValue(actor, out var person)) return null;
        var town = choice.Resettle ? world.Towns.SingleOrDefault(item => item.Id == choice.TownId) : TownFoundingLayout(world, actor);
        if (town is null || town.Id != choice.TownId) return null;
        var previous = TownForResident(world, actor);
        return new(actor, choice, town, person.Position, previous, TownCareGroup(world, actor, previous));
    }

    internal ActionCheck CheckTownFoundingPlan(ITownWorld world, TownFoundingActionPlan plan)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (world.Agents[plan.Actor].Position != plan.Site || TownForResident(world, plan.Actor) != plan.PreviousTown ||
            !TownCareGroup(world, plan.Actor, plan.PreviousTown).SequenceEqual(plan.CareGroup))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected site, affiliation or dependent group has changed");
        if (plan.Choice.Resettle)
            return world.Towns.SingleOrDefault(item => item.Id == plan.Town.Id) is { } current && MayResettleTown(world, plan.Actor, current) &&
                current.OriginSite == plan.Town.OriginSite && current.BorderTiles.SequenceEqual(plan.Town.BorderTiles)
                    ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected abandoned Town is no longer available to resettle");
        return SelectedFoundingLayoutAvailable(world, plan.Actor, plan.Town)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected founding plot is no longer available");
    }

    private bool SelectedFoundingLayoutAvailable(ITownWorld world, string actor, TownRuntimeState layout)
    {
        if (founderSetup is not { Started: true } || !world.IsAdult(actor) || !world.ReadyForBriefInteraction(actor) ||
            layout.OriginSite is not { } site || world.Agents[actor].Position != site || !world.Map.IsBuildable(site) ||
            towns.Any(town => town.Id == layout.Id || town.BorderTiles.Any(layout.BorderTiles.Contains)) ||
            townLandTitles.Any(title => title.Tiles.Any(layout.BorderTiles.Contains))) return false;
        var household = world.Society.GetAgent(actor).HouseholdId;
        var assigned = layout.AssignedBuildingIds.ToHashSet(StringComparer.Ordinal);
        var core = new HashSet<GridPoint> { site };
        var found = new HashSet<string>(StringComparer.Ordinal);
        var warehouses = 0;
        foreach (var building in world.WorldSimulation.Buildings)
        {
            var footprint = TownFoundingBuildingTiles(world, building).ToArray();
            if (!assigned.Contains(building.InstanceId) && !footprint.Any(layout.BorderTiles.Contains)) continue;
            if (!assigned.Contains(building.InstanceId) || building.TownId is not null || household is null || building.HouseholdId != household)
                return false;
            found.Add(building.InstanceId);
            core.UnionWith(footprint);
            if (world.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("warehouse", StringComparer.Ordinal))
                warehouses++;
        }
        return warehouses <= 1 && found.SetEquals(assigned) && TownBorderRules.Around(world.Map, core).SequenceEqual(layout.BorderTiles) &&
            !world.Fields.Any(field => layout.BorderTiles.Contains(field.Position) && field.HouseholdId != household);
    }
}
