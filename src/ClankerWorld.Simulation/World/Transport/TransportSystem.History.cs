using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal void RetireOrderLinks(IReadOnlySet<string> retired)
    {
        var next = Boats with
        {
            Requests = Boats.Requests.Select(request => retired.Contains(request.OrderInstructionId ?? "")
                ? request with { OrderInstructionId = null } : request).ToArray(),
        };
        if (SystemInvariantChecks.Enabled) ValidateLocalBoats(next);
        Boats = next;
    }

    internal static (BoatTransportState Transport, BoatTripRequest[] Retired) PrepareBoatHistory(WorldRuntimeState state)
    {
        var transport = state.BoatTransport;
        var closed = transport.Requests.Where(request => request.SettledTick is not null).ToArray();
        // An order's latest native request remains authority for arrival or
        // recovery. Older retries and ordinary requests can still be archived.
        var boundRequests = (state.Instructions ?? [])
            .Where(instruction => instruction.Task?.Action == "travel_by_boat")
            .Select(instruction => instruction.Task!.BoatTravel?.RequestId)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        var retired = WorldHistory.Retire(closed, WorldHistory.RecentBoatRequestLimit, request => boundRequests.Contains(request.Id));
        if (retired.Length != 0)
        {
            var retiredIds = retired.Select(request => request.Id).ToHashSet(StringComparer.Ordinal);
            transport = transport with
            {
                Requests = transport.Requests.Where(request => !retiredIds.Contains(request.Id)).ToArray(),
                RetiredRequestRanges = MergeRetiredRanges(transport.RetiredRequestRanges, retired),
            };
        }
        return (transport, retired);
    }

    private static List<RetiredBoatRequestRange> MergeRetiredRanges(
        IReadOnlyList<RetiredBoatRequestRange> previous, IReadOnlyList<BoatTripRequest> retired)
    {
        // A low-sequence trip can remain active while later trips finish. Keep
        // those gaps explicit rather than treating a high-water mark as authority.
        var ranges = new List<RetiredBoatRequestRange>();
        foreach (var range in previous.Concat(retired.Select(request =>
                     new RetiredBoatRequestRange(request.Sequence, request.Sequence))).OrderBy(range => range.FirstSequence))
        {
            if (ranges.Count > 0 && ranges[^1].LastSequence < long.MaxValue &&
                range.FirstSequence == ranges[^1].LastSequence + 1)
                ranges[^1] = ranges[^1] with { LastSequence = range.LastSequence };
            else ranges.Add(range);
        }
        return ranges;
    }

}
