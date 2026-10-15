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

    private static TownProposal? PendingAdmission(TownRuntimeState town, string subject) => TownSystem.PendingAdmission(town, subject);
    private static TownProposal? AdmissionProposal(TownRuntimeState town, TownAdmissionRecord record) => TownSystem.AdmissionProposal(town, record);
    private TownAdmissionRecord? ApprovedAdmission(TownRuntimeState town, string subject) => TownSystem.ApprovedAdmission(civicWorld, town, subject);
    private bool HasPendingAdmission(string subject) => townSystem.HasPendingAdmission(civicWorld, subject);
    /// <summary>A Town where this adult has an undecided request or an approval to accept; it grants nothing yet.</summary>
    private bool HasOpenAdmission(string subject, string? townId) => townSystem.HasOpenAdmission(civicWorld, subject, townId);
    /// <summary>One open request of one's own at a time, in any Town; a resident of another Town may ask to move.</summary>
    private bool MayRequestOwnAdmission(string actor, TownRuntimeState town) => townSystem.MayRequestOwnAdmission(civicWorld, actor, town);
    /// <summary>A resident may ask the council to admit an unaffiliated adult; the adult must still accept.</summary>
    private bool MayBeSponsoredForAdmission(string nominee, TownRuntimeState town) => townSystem.MayBeSponsoredForAdmission(civicWorld, nominee, town);
    /// <summary>
    /// A refused or withdrawn request for the same newcomer waits one unpaused
    /// world day unless the council changed, matching the proposal rules, so
    /// the choice is not offered while it would only be refused.
    /// </summary>
    private bool AdmissionRetryWaits(TownRuntimeState town, string subject) => TownSystem.AdmissionRetryWaits(civicWorld, town, subject);
    /// <summary>
    /// An adult standing in a Town they do not belong to may walk to its notice
    /// place, and so may an adult with no Town from anywhere: Add Agent inside a
    /// border makes a resident, so they usually start outside. Either way the
    /// notice place must be reachable on foot. Walking registers nothing.
    /// </summary>
    private bool MayVisitAsNewcomer(string actor, TownRuntimeState town) => townSystem.MayVisitAsNewcomer(civicWorld, actor, town);
    /// <summary>Whether a tile close enough to read the Town's notices is reachable on foot.</summary>
    private bool CanWalkToCivicBoard(string actor, TownRuntimeState town) => TownSystem.CanWalkToCivicBoard(civicWorld, actor, town);
    private bool MayAcceptAdmission(string actor, TownRuntimeState town, TownAdmissionRecord record) => townSystem.MayAcceptAdmission(civicWorld, actor, town, record);
    private string AdmissionRequestText(string actor, TownRuntimeState town) => townSystem.AdmissionRequestText(civicWorld, actor, town);
    private void AddTownAdmissionCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => townSystem.AddTownAdmissionCandidates(civicWorld, candidates, actor, town);
    private void AcceptTownAdmission(string actor, string townId, string proposalId) => townSystem.AcceptTownAdmission(civicWorld, actor, townId, proposalId);
    /// <summary>
    /// Records each newly passed admission once and lapses approvals that no
    /// longer fit. Runs after every saved council decision and roster change,
    /// rereading the Towns after every change, so a delayed or duplicate
    /// approval cannot add a dead or already admitted person or leave anyone in
    /// two Towns, and every saved passed admission has its one outcome record.
    /// </summary>
    private void SettleTownAdmissions() => townSystem.SettleTownAdmissions(civicWorld);
    private bool SettleNextTownAdmission() => townSystem.SettleNextTownAdmission(civicWorld);
    private void ApplyTownAdmission(string townId, string proposalId, string subject, TownAdmissionRecord? approved) => townSystem.ApplyTownAdmission(civicWorld, townId, proposalId, subject, approved);
    /// <summary>
    /// A dependent joining their accepted guardian follows the guardian's recorded
    /// Town membership. The caller has already checked arrival and House capacity.
    /// This changes only rosters; it is not a separate adult admission proposal.
    /// </summary>
    private bool MoveDependentToGuardianTown(string child, string guardian) => townSystem.MoveDependentToGuardianTown(civicWorld, child, guardian);
    private void LapseTownAdmission(string townId, TownAdmissionRecord record, string reason) => townSystem.LapseTownAdmission(civicWorld, townId, record, reason);
    private void SetTownAdmission(string townId, TownAdmissionRecord record) => townSystem.SetTownAdmission(civicWorld, townId, record);
    /// <summary>The adult and the living dependents they are primary caregiver for who share their current Town (or none).</summary>
    private string[] TownCareGroup(string adult, string? fromTownId) => townSystem.TownCareGroup(civicWorld, adult, fromTownId);
    private string TownName(string townId) => townSystem.TownName(civicWorld, townId);
    /// <summary>What this agent's own model is told about their Town: actual membership, rights and the admission they know of.</summary>
    private string? TownMembershipNote(string actor) => townSystem.TownMembershipNote(civicWorld, actor);
    private static void ValidateTownAdmissions(IReadOnlyList<TownRuntimeState> savedTowns, SocietyCheckpoint society, int schemaVersion) => TownSystem.ValidateTownAdmissions(savedTowns, society, schemaVersion);
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
