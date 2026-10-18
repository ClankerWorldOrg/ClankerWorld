using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static TownLandClaimActionPlan? PlanTownLandClaim(ITownWorld world, string actor, TownLandClaimChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town || !world.Agents.TryGetValue(actor, out var person)) return null;
        return new(actor, choice, TownLandClaimRules.ClaimableNear(world.Map, town, world.TownLandTitles, person.Position, 6));
    }

    internal static TownLandClaimActionPlan BindTownLandClaim(TownLandClaimActionPlan plan, IReadOnlyList<CognitionLandTile>? tiles) =>
        plan.SelectedTiles is not null ? plan : plan with { SelectedTiles = tiles?.Select(tile => new GridPoint(tile.X, tile.Y)).ToArray() ?? [] };

    internal static ActionCheck CheckTownLandClaim(ITownWorld world, TownLandClaimActionPlan plan, TownRuntimeState? currentTown = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            (plan.SelectedTiles is { } selected ? !TownLandClaimRules.CanClaim(world.Map, town, selected, world.TownLandTitles) :
                TownLandClaimRules.ClaimableNear(world.Map, town, world.TownLandTitles, world.Agents[plan.Actor].Position, 1).Length == 0))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected plot is no longer connected unclaimed Town land");
        return new();
    }

    private static void OfferTownLandClaim(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town, string siteRecovery)
    {
        if (PlanTownLandClaim(world, actor, new(town.Id)) is not { } plan || !CheckTownLandClaim(world, plan).Allowed) return;
        var here = world.Agents[actor].Position;
        candidates.Add(new(plan.CandidateId, siteRecovery + $"Ask {town.Name}'s council to claim a connected plot of adjoining unclaimed land; include its exact coordinates in civic_land_tiles. " +
            FormattableString.Invariant($"You stand at ({here.X}, {here.Y}); unclaimed tiles beside the Town's land nearest you: {string.Join("; ", plan.OfferedTiles.Select(tile => FormattableString.Invariant($"({tile.X}, {tile.Y})")))}. ") +
            "Existing titles, household rights, buildings and goods stay with their holders.", 190)
        { ActionPlan = plan });
    }
}
