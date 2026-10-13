using System.Text.Json;

namespace ClankerWorld.Simulation.World;

public sealed record ArchivedCivicNotice(TownCivicNotice Notice, IReadOnlyList<TownCivicReceipt> Knowledge);

public static partial class WorldHistory
{
    public const int RecentCouncilElectionLimit = 8;
    public const int RecentCouncilNoticeLimit = 32;
    public const int RecentLearnedNoticeLimit = 3;

    // The boat queue already keeps a recent terminal window plus exact live
    // dependencies. Use the same partition for other checkpoint histories.
    private static T[] Retire<T>(IReadOnlyList<T> records, int recentLimit, Func<T, bool> required) =>
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
        name.StartsWith("council-notices:", StringComparison.Ordinal) || name == "repair-reservations";

    public static IReadOnlyDictionary<string, long> RequiredRecordCounts(WorldRuntimeState state)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var town in state.Towns ?? [])
        {
            if (town.Council is not { } council) continue;
            if (council.ArchivedElectionCount > 0) counts.Add("council-elections:" + town.Id, council.ArchivedElectionCount);
            if (council.ArchivedNoticeCount > 0) counts.Add("council-notices:" + town.Id, council.ArchivedNoticeCount);
        }
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
}
