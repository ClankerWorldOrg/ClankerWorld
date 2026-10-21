using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal const int ExplorationMemoryLimit = 256;

    internal static bool ValidExplorationGoal(ExplorationGoal? goal) => goal is null ||
        (goal.Kind == "resource" && (OrderTaskParser.IsMaterialKind(goal.Target) || goal.Target is "food" or "berries" or "wild_greens" or "fruit") ||
         goal.Kind == "terrain" && goal.Target is "Forest" or "Mountain") &&
        (goal.OrderInstructionId is null || BoundedText.IsValid(goal.OrderInstructionId, DerivedId.MaximumLength, allowWhitespaceOnly: true));

    internal static void ValidateLocalExploration(ExplorationState exploration)
    {
        if (!ValidExplorationGoal(exploration.Goal) || exploration.VisitedTiles is null || exploration.OutingPath is null ||
            exploration.Goal is not null && exploration.OutingPath.Count == 0 || exploration.VisitedTiles.Count > ExplorationMemoryLimit ||
            exploration.OutingPath.Distinct().Count() != exploration.OutingPath.Count ||
            (exploration.OutingDiscoveries?.Count ?? 0) > ExplorationMemoryLimit || exploration.LastOutingTick < 0 ||
            (exploration.OutingDiscoveries ?? []).Distinct().Count() != (exploration.OutingDiscoveries?.Count ?? 0))
            throw new InvalidDataException("The saved local exploration record is invalid.");
    }

    internal static void ValidateLocalDepartures(string actor, IReadOnlyList<HouseholdDeparture> departures)
    {
        foreach (var departure in departures)
            if (departure?.SharedProject is { } plan) ProductionSystem.ValidateLocalAgentProject(plan);
        if (departures.Count == 0 || departures.Any(item => item is null || item.Tick < 0 ||
            item.Cause is not ("voluntary" or "displaced") || item.AllowancePortions is < 0 or > 2 ||
            item.CareGroup is null || !item.CareGroup.Contains(actor, StringComparer.Ordinal) ||
            item.CareGroup.Distinct(StringComparer.Ordinal).Count() != item.CareGroup.Count || item.AllowanceLotIds is null ||
            item.AllowanceLotIds.Count > item.AllowancePortions ||
            item.AllowanceLotIds.Distinct(StringComparer.Ordinal).Count() != item.AllowanceLotIds.Count || item.AllowanceLotIds.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException("The saved household departure record is invalid.");
    }
}
