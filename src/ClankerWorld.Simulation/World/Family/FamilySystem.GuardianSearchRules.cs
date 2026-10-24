using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    /// <summary>
    /// The search widens: each stage adds a new group and keeps asking the earlier ones, so a relative
    /// who missed their first day can still accept later.
    /// </summary>
    internal static string[] GuardianAdultsForStage(
        SocietyCheckpoint checkpoint,
        IReadOnlyList<TownRuntimeState> towns,
        string child,
        string stage)
    {
        var stages = stage switch
        {
            "relatives" => new[] { "relatives" },
            "household" => ["relatives", "household"],
            "town" => ["relatives", "household", "town"],
            _ => throw new InvalidDataException("The saved guardian search stage is invalid."),
        };
        return stages.SelectMany(item => NewGuardianAdultsForStage(checkpoint, towns, child, item))
            .Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>The adults a stage adds to the search; nobody belongs to more than one stage.</summary>
    internal static string[] NewGuardianAdultsForStage(
        SocietyCheckpoint checkpoint,
        IReadOnlyList<TownRuntimeState> towns,
        string child,
        string stage)
    {
        var relatives = LivingAdultRelatives(checkpoint, child).ToHashSet(StringComparer.Ordinal);
        var householdId = checkpoint.GetAgent(child).HouseholdId;
        var householdAdults = householdId is null ? [] : checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
                LifeStageRules.IsAdult(person.AgeBand) &&
                person.HouseholdId == householdId)
            .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
        return stage switch
        {
            "relatives" => relatives.Order(StringComparer.Ordinal).ToArray(),
            "household" => householdAdults.Where(id => !relatives.Contains(id)).ToArray(),
            "town" => TownAdultsFor(checkpoint, towns, child).Where(id => !relatives.Contains(id) &&
                !householdAdults.Contains(id, StringComparer.Ordinal)).ToArray(),
            _ => throw new InvalidDataException("The saved guardian search stage is invalid."),
        };
    }

    internal static string[] TownAdultsFor(SocietyCheckpoint checkpoint, IReadOnlyList<TownRuntimeState> towns, string child)
    {
        var townId = towns.SingleOrDefault(item => item.ResidentIds.Contains(child, StringComparer.Ordinal))?.Id;
        if (townId is null)
            return [];
        return towns.Single(town => town.Id == townId).ResidentIds
            .Where(id => id != child && checkpoint.Agents.Any(person => person.Id == id &&
                person.Status == SocietyAgentStatus.Active &&
                LifeStageRules.IsAdult(person.AgeBand)))
            .Order(StringComparer.Ordinal).ToArray();
    }

    internal static string[] LivingAdultRelatives(SocietyCheckpoint checkpoint, string child)
    {
        var parentage = checkpoint.Relationships.Where(edge =>
                edge.Type == SocietyRelationshipType.BiologicalParentage &&
                edge.State is not (SocietyRelationshipState.Proposed or SocietyRelationshipState.Rejected))
            .ToArray();
        var parents = parentage.Where(edge => edge.TargetId == child)
            .Select(edge => edge.ProposerId).ToHashSet(StringComparer.Ordinal);
        var grandparents = parentage.Where(edge => parents.Contains(edge.TargetId))
            .Select(edge => edge.ProposerId).ToHashSet(StringComparer.Ordinal);
        var adultSiblings = parentage.Where(edge => parents.Contains(edge.ProposerId))
            .Select(edge => edge.TargetId);
        var auntsAndUncles = parentage.Where(edge => grandparents.Contains(edge.ProposerId))
            .Select(edge => edge.TargetId).Where(id => !parents.Contains(id));
        return parents.Concat(grandparents).Concat(adultSiblings).Concat(auntsAndUncles)
            .Where(id => id != child && checkpoint.Agents.Any(person => person.Id == id &&
                person.Status == SocietyAgentStatus.Active &&
                LifeStageRules.IsAdult(person.AgeBand)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
}
