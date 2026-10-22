using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// Town admission (#602). A Town's resident list stays the one record of
/// membership; this applies each passed admission proposal from that Town's
/// current council exactly once. The newcomer must have asked, or accept an
/// approval someone else requested, and their dependent children move with
/// them. Household, House and position are separate and never change here.
/// </summary>
public sealed partial class WorldRuntime
{
    internal const string AdmissionApproved = "approved";
    internal const string AdmissionAdmitted = "admitted";
    internal const string AdmissionLapsed = "lapsed";
    internal static readonly IReadOnlyList<string> AdmissionLapseReasons =
        ["unavailable", "already_resident", "affiliation_changed", "joined_elsewhere", "acceptance_expired"];
}

/// <summary>
/// Plain words for an agent's recorded Town membership, its rights and any
/// admission in progress, shared by the agent's own model context and the
/// owner's agent card. Membership comes only from the Town resident lists.
/// </summary>
public static class TownMembershipText
{
    public const int MaximumLength = 256;

    public static IReadOnlySet<string> TownsWithWarehouse(WorldContentSimulationState simulation, DeclarativeWorldContentState content)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(content);
        var warehouses = content.Buildings.Where(definition => definition.Tags.Contains("warehouse", StringComparer.Ordinal))
            .Select(definition => definition.CanonicalId).ToHashSet(StringComparer.Ordinal);
        return simulation.Buildings.Where(building => building.TownId is not null && warehouses.Contains(building.DefinitionId))
            .Select(building => building.TownId!).ToHashSet(StringComparer.Ordinal);
    }

    /// <param name="knows">Whether this reader learned a Town notice of the given kind and subject; the owner sees all,
    /// an agent only what they read or were told.</param>
    /// <param name="calendarOffsetTicks">The world's saved clock offset, so day numbers match its calendar.</param>
    public static string? Describe(IReadOnlyList<TownRuntimeState> towns, SocietyCheckpoint society, string agentId,
        int ticksPerDay, IReadOnlySet<string> townsWithWarehouse, Func<TownRuntimeState, string, string, bool>? knows = null,
        int calendarOffsetTicks = 0)
    {
        ArgumentNullException.ThrowIfNull(towns);
        ArgumentNullException.ThrowIfNull(society);
        ArgumentNullException.ThrowIfNull(townsWithWarehouse);
        var person = society.Agents.FirstOrDefault(item => item.Id == agentId);
        if (person is not { Status: SocietyAgentStatus.Active }) return null;
        knows ??= (_, _, _) => true;
        var adult = LifeStageRules.IsAdult(person.AgeBand);
        var home = towns.SingleOrDefault(town => town.ResidentIds.Contains(agentId, StringComparer.Ordinal));
        var council = home?.Council?.Form switch
        {
            "representative" => "vote in its council elections",
            "leader" => "vote in its mayoral elections",
            _ => "sit and vote on its council",
        };
        var text = home is null
            ? adult ? "Town: none · no council vote; can ask a Town's council to join, or resettle an abandoned Town"
                : "Town: none · follows their primary caregiver's Town"
            : !adult ? person.PrimaryCaregiverId is { } caregiver && home.ResidentIds.Contains(caregiver, StringComparer.Ordinal)
                ? $"Town: resident of {home.Name} with their primary caregiver · council rights begin at adulthood"
                : $"Town: resident of {home.Name} · council rights begin at adulthood"
            : townsWithWarehouse.Contains(home.Id)
                ? $"Town: resident of {home.Name} · may {council} and collect its Warehouse stock in person, housed or not"
                : $"Town: resident of {home.Name} · may {council}, housed or not; it has no Warehouse yet";
        if (adult && AdmissionStatus(towns, agentId, home?.Id, society.WorldTick, ticksPerDay, calendarOffsetTicks, knows) is { } status)
        {
            if (text.Length + status.Length + 3 > MaximumLength && AcceptanceDeadline(towns, society, agentId, ticksPerDay, knows) is not null)
            {
                var prefix = home is null ? "Town: none" : $"Town: resident of {home.Name}";
                var budget = Math.Max(0, MaximumLength - status.Length - 3);
                text = prefix.Length <= budget ? prefix :
                    prefix[..Math.Max(0, budget - 1)].TrimEnd() + (budget > 0 ? "…" : string.Empty);
            }
            text += " · " + status;
        }
        return text.Length > MaximumLength ? text[..MaximumLength] : text;
    }

    /// <summary>The exact deadline for the open approval described to this reader, if any.</summary>
    public static long? AcceptanceDeadline(IReadOnlyList<TownRuntimeState> towns, SocietyCheckpoint society,
        string agentId, int ticksPerDay, Func<TownRuntimeState, string, string, bool>? knows = null)
    {
        ArgumentNullException.ThrowIfNull(towns);
        ArgumentNullException.ThrowIfNull(society);
        if (!society.Agents.Any(person => person.Id == agentId && person.Status == SocietyAgentStatus.Active &&
                LifeStageRules.IsAdult(person.AgeBand))) return null;
        knows ??= (_, _, _) => true;
        return KnownPendingAdmission(towns, agentId, knows) is not null ? null :
            KnownOpenApproval(towns, agentId, society.WorldTick, ticksPerDay, knows)?.Deadline;
    }

    private static (TownRuntimeState Town, TownProposal Proposal)? KnownPendingAdmission(
        IReadOnlyList<TownRuntimeState> towns, string agentId, Func<TownRuntimeState, string, string, bool> knows)
    {
        foreach (var town in towns.OrderBy(item => item.Id, StringComparer.Ordinal))
            if (town.Council?.Proposals.FirstOrDefault(proposal => proposal.Kind == "admission" &&
                    proposal.SubjectId == agentId && proposal.Status == "pending") is { } pending && knows(town, "proposal", pending.Id))
                return (town, pending);
        return null;
    }

    private static (TownRuntimeState Town, long Deadline)? KnownOpenApproval(IReadOnlyList<TownRuntimeState> towns,
        string agentId, long worldTick, int ticksPerDay, Func<TownRuntimeState, string, string, bool> knows)
    {
        foreach (var town in towns.OrderBy(item => item.Id, StringComparer.Ordinal))
            if (town.Admissions?.LastOrDefault(record => record.SubjectId == agentId &&
                    record.Status == WorldRuntime.AdmissionApproved) is { } approved && knows(town, "result", approved.ProposalId) &&
                town.Council?.Proposals.SingleOrDefault(proposal => proposal.Id == approved.ProposalId) is { } proposal &&
                TownAdmissionDeadlineRules.IsOpen(proposal, worldTick, ticksPerDay) &&
                TownAdmissionDeadlineRules.AcceptanceDeadline(proposal, ticksPerDay) is { } deadline)
                return (town, deadline);
        return null;
    }

    private static string? AdmissionStatus(IReadOnlyList<TownRuntimeState> towns, string agentId, string? homeId,
        long worldTick, int ticksPerDay, int calendarOffsetTicks, Func<TownRuntimeState, string, string, bool> knows)
    {
        var day = Math.Max(1, ticksPerDay);
        // Calendar days, as WorldCalendarRules counts them: the offset moves midnight, not elapsed time.
        string Day(long tick) => "world day " +
            (tick / day + (tick % day + calendarOffsetTicks) / day + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (KnownPendingAdmission(towns, agentId, knows) is { } pending)
            return $"asked to join {pending.Town.Name}; its council decides by {Day(pending.Proposal.DeadlineTick)}, and nothing changes until then";
        if (KnownOpenApproval(towns, agentId, worldTick, day, knows) is { } approved)
            return $"{approved.Town.Name}'s council agreed to the request to join; accept before {TownAdmissionDeadlineRules.DescribeWindow(approved.Deadline, worldTick, day, calendarOffsetTicks)}";
        foreach (var town in towns.OrderBy(item => item.Id, StringComparer.Ordinal))
            if (town.Id != homeId && town.Admissions?.LastOrDefault(record => record.SubjectId == agentId) is { } record &&
                (record.Status == WorldRuntime.AdmissionApproved || record.Status == WorldRuntime.AdmissionLapsed && record.Reason == "acceptance_expired") &&
                knows(town, "result", record.ProposalId) && TownAdmissionDeadlineRules.IsExpired(
                    town.Council?.Proposals.SingleOrDefault(proposal => proposal.Id == record.ProposalId), worldTick, day))
                return $"{town.Name}'s approval to join expired before it was accepted; can ask again";
        var closed = towns.Where(town => town.Id != homeId).SelectMany(town => (town.Council?.Proposals ?? [])
                .Where(proposal => proposal.Kind == "admission" && proposal.SubjectId == agentId &&
                    proposal.Status is "rejected" or "cancelled" && worldTick - proposal.SettledTick < day &&
                    // A refusal is posted as the proposal's result; a cancellation as the next council notice.
                    (proposal.Status == "rejected" ? knows(town, "result", proposal.Id)
                        : knows(town, "council", "council:" + (proposal.CouncilRevision + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))))
                .Select(proposal => (Town: town, Proposal: proposal)))
            .OrderByDescending(item => item.Proposal.SettledTick).FirstOrDefault();
        return closed.Proposal switch
        {
            { Status: "rejected" } proposal => $"{closed.Town.Name}'s council turned down the request to join on {Day(proposal.SettledTick ?? 0)}",
            { Status: "cancelled" } => $"the request to join {closed.Town.Name} closed when its council changed; can ask again",
            _ => null,
        };
    }
}
