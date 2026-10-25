using System.Text.Json;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record ArchivedCivicNotice(TownCivicNotice Notice, IReadOnlyList<TownCivicReceipt> Knowledge);

public static partial class WorldHistory
{
    public const int RecentCouncilElectionLimit = 8;
    public const int RecentCouncilNoticeLimit = 32;
    public const int RecentLearnedNoticeLimit = 3;

    // The boat queue already keeps a recent terminal window plus exact live
    // dependencies. Use the same partition for other checkpoint histories.
    internal static T[] Retire<T>(IReadOnlyList<T> records, int recentLimit, Func<T, bool> required) =>
        records.Take(Math.Max(0, records.Count - recentLimit)).Where(record => !required(record)).ToArray();

    private static long ArchiveRecords<T>(string name, T[] records, long previous, List<ArchivedEventBatch> archived)
    {
        if (records.Length == 0) return previous;
        var through = checked(previous + records.Length);
        archived.Add(new(name, previous, through, JsonSerializer.SerializeToElement(records)));
        return through;
    }

    public static bool IsRecordStream(string name) =>
        name.StartsWith("council-elections:", StringComparison.Ordinal) ||
        name.StartsWith("council-notices:", StringComparison.Ordinal) || name == "production-jobs" || name == "repair-reservations";

    public static IReadOnlyDictionary<string, long> RequiredRecordCounts(WorldRuntimeState state)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var town in state.Towns ?? [])
        {
            if (town.Council is not { } council) continue;
            if (council.ArchivedElectionCount > 0) counts.Add("council-elections:" + town.Id, council.ArchivedElectionCount);
            if (council.ArchivedNoticeCount > 0) counts.Add("council-notices:" + town.Id, council.ArchivedNoticeCount);
        }
        if (state.WorldSimulation is { ArchivedProductionJobCount: > 0 } simulation)
            counts.Add("production-jobs", simulation.ArchivedProductionJobCount);
        if (state.Society.Society.Inventory.ArchivedRepairReservationCount > 0)
            counts.Add("repair-reservations", state.Society.Society.Inventory.ArchivedRepairReservationCount);
        return counts;
    }

    private static TownRuntimeState[] CompactCouncils(IReadOnlyList<TownRuntimeState> towns, List<ArchivedEventBatch> archived) =>
        towns.Select(town =>
        {
            if (town.Council is not { } council) return town;
            var bound = (town.Government?.Changes ?? []).Select(change => change.ForcedCouncilElectionId)
                .OfType<string>().ToHashSet(StringComparer.Ordinal);
            var retired = Retire(council.ElectionHistory, RecentCouncilElectionLimit, election => bound.Contains(election.Id));
            var retiredIds = retired.Select(election => election.Id).ToHashSet(StringComparer.Ordinal);
            var elections = council.ElectionHistory.Where(election => !retiredIds.Contains(election.Id)).ToArray();
            var retainedSubjects = elections.Select(election => election.Id).ToHashSet(StringComparer.Ordinal);
            if (council.Election is { } live) retainedSubjects.Add(live.Id);
            var notices = council.Notices.ToDictionary(notice => notice.Id, StringComparer.Ordinal);
            // Keep the exact sources visible in each actor's civic prompt, along
            // with every receipt for those notices so relays still reach a read.
            var learned = council.Knowledge.GroupBy(receipt => receipt.AgentId, StringComparer.Ordinal)
                .SelectMany(actor => actor.OrderByDescending(receipt => receipt.LearnedTick)
                    .ThenByDescending(receipt => notices[receipt.NoticeId].PostedTick).Take(RecentLearnedNoticeLimit))
                .Select(receipt => receipt.NoticeId).ToHashSet(StringComparer.Ordinal);
            var removedNotices = Retire(council.Notices, RecentCouncilNoticeLimit, notice =>
                !DerivedId.MatchesNumbered(notice.SubjectId, town.Id + ":election:", council.Sequence) ||
                retainedSubjects.Contains(notice.SubjectId) || learned.Contains(notice.Id));
            var removedIds = removedNotices.Select(notice => notice.Id).ToHashSet(StringComparer.Ordinal);
            var receipts = council.Knowledge.ToLookup(receipt => receipt.NoticeId, StringComparer.Ordinal);
            return town with
            {
                Council = council with
                {
                    ElectionHistory = elections,
                    Notices = council.Notices.Where(notice => !removedIds.Contains(notice.Id)).ToArray(),
                    Knowledge = council.Knowledge.Where(receipt => !removedIds.Contains(receipt.NoticeId)).ToArray(),
                    ArchivedElectionCount = ArchiveRecords("council-elections:" + town.Id, retired, council.ArchivedElectionCount, archived),
                    ArchivedNoticeCount = ArchiveRecords("council-notices:" + town.Id,
                        removedNotices.Select(notice => new ArchivedCivicNotice(notice, receipts[notice.Id].ToArray())).ToArray(),
                        council.ArchivedNoticeCount, archived),
                },
            };
        }).ToArray();

    public const int RecentProductionJobLimit = 32;

    private static WorldProductionJob[] ClosedProductionJobs(WorldContentSimulationState simulation) =>
        simulation.ProductionJobs.Where(job => job.State is WorldProductionJobState.Completed or WorldProductionJobState.Cancelled)
            .OrderBy(job => job.StartedTick).ThenBy(job => job.JobId, StringComparer.Ordinal).ToArray();

    /// <summary>Only current work and the recent terminal window belong in each owner response.</summary>
    public static WorldProductionJob[] ProductionJobsForInspection(WorldContentSimulationState simulation)
    {
        var recent = ClosedProductionJobs(simulation).TakeLast(RecentProductionJobLimit)
            .Select(job => job.JobId).ToHashSet(StringComparer.Ordinal);
        return simulation.ProductionJobs.Where(job => job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused ||
            recent.Contains(job.JobId)).ToArray();
    }

    private static WorldContentSimulationState CompactProduction(WorldRuntimeState state, List<ArchivedEventBatch> archived)
    {
        var simulation = state.WorldSimulation!;
        var knowledge = state.Knowledge!;
        var required = knowledge.Recipes.Concat(knowledge.Artifacts.SelectMany(artifact => artifact.Recipes))
            .Concat(knowledge.WritingProjects.SelectMany(project => project.Recipes))
            .Select(recipe => recipe.SourceProductionJobId).ToHashSet(StringComparer.Ordinal);
        required.UnionWith(state.Agents.Concat((state.DeceasedAgents ?? []).Select(person => person.LastPhysical))
            .Where(person => person.Project is { Stage: not ("completed" or "cancelled") })
            .Select(person => person.Project!.JobId).OfType<string>());
        required.UnionWith((state.ToolMakingRequests ?? []).Select(request => request.JobId).OfType<string>());
        var instructions = state.Instructions ?? [];
        required.UnionWith(instructions.Select(instruction => instruction.Task?.ProductionJobId).OfType<string>());
        // Saved order progress counts every real completed batch, not just its
        // latest job. Order retention owns when these links can be retired.
        var orderIds = instructions.Select(instruction => instruction.InstructionId).ToHashSet(StringComparer.Ordinal);
        required.UnionWith(simulation.ProductionJobs.Where(job => job.OrderInstructionId is { } id && orderIds.Contains(id))
            .Select(job => job.JobId));
        required.UnionWith(GoodsInventoryQuery.FindFoodProductionSources(state.Society.Society.Inventory, simulation.ProductionJobs)
            .Select(job => job.JobId));
        var retired = Retire(ClosedProductionJobs(simulation), RecentProductionJobLimit, job => required.Contains(job.JobId));
        if (retired.Length == 0) return simulation;
        var retiredIds = retired.Select(job => job.JobId).ToHashSet(StringComparer.Ordinal);
        return ProductionSystem.ArchiveJobs(simulation, retiredIds,
            ArchiveRecords("production-jobs", retired, simulation.ArchivedProductionJobCount, archived));
    }

    public const int RecentRepairReservationLimit = 32;

    private static InventoryCheckpoint CompactRepairReservations(WorldRuntimeState state, List<ArchivedEventBatch> archived)
    {
        var inventory = state.Society.Society.Inventory;
        // Other consumption purposes have their own authority and retention.
        // Active claims never enter the retirement candidates.
        var closed = inventory.Reservations.Where(item => item.Purpose == "equipment_repair" &&
                item.State is InventoryReservationState.Completed or InventoryReservationState.Released)
            .OrderBy(item => item.ExpiryTick).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var required = (state.Towns ?? []).SelectMany(town => town.NonLandHearings.NativeReceipts)
            .SelectMany(receipt => receipt.RepairInputs).Select(input => input.Reservation.Id.Value).ToHashSet(StringComparer.Ordinal);
        required.UnionWith(WorldRuntime.RequiredCartRepairReservations(state.Instructions ?? [], inventory.WorldTick));
        var retired = Retire(closed, RecentRepairReservationLimit, item => required.Contains(item.Id));
        if (retired.Length == 0) return inventory;
        var retiredIds = retired.Select(item => item.Id.Value).ToHashSet(StringComparer.Ordinal);
        return InventoryStore.PrepareArchivedRepairReservations(inventory,
            inventory.Reservations.Where(item => !retiredIds.Contains(item.Id)).ToArray(),
            ArchiveRecords("repair-reservations", retired, inventory.ArchivedRepairReservationCount, archived));
    }
}
