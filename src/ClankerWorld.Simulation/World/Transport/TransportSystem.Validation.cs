namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static void ValidateLocalHitches(IEnumerable<HandcartHitch> source)
    {
        var hitches = source.ToArray();
        if (hitches.Any(item => item is null || string.IsNullOrWhiteSpace(item.CartLotId) || string.IsNullOrWhiteSpace(item.PullerId)) ||
            hitches.Select(item => item.CartLotId).Distinct(StringComparer.Ordinal).Count() != hitches.Length ||
            hitches.Select(item => item.PullerId).Distinct(StringComparer.Ordinal).Count() != hitches.Length)
            throw new InvalidDataException("Cart attachments must be distinct for both carts and pullers.");
    }

    internal static void ValidateLocalBridges(IEnumerable<BridgeState> source)
    {
        var bridges = source.ToArray();
        if (bridges.Any(item => item is null || string.IsNullOrWhiteSpace(item.Id) || item.BuiltTick < 0 ||
                item.Entrances is not { Count: 2 } || item.Span is not { Count: > 0 }) ||
            bridges.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != bridges.Length)
            throw new InvalidDataException("Bridge records have invalid local geometry or identity.");
    }

    internal static void ValidateLocalTraffic(BridgeTrafficState traffic)
    {
        if (traffic.InProgress is null || traffic.Completed is null ||
            traffic.InProgress.Any(item => item is null || string.IsNullOrWhiteSpace(item.AgentId) || string.IsNullOrWhiteSpace(item.CrossingId)) ||
            traffic.Completed.Any(item => item is null || string.IsNullOrWhiteSpace(item.AgentId) || string.IsNullOrWhiteSpace(item.CrossingId) || item.Tick < 0))
            throw new InvalidDataException("Bridge traffic has invalid local evidence.");
    }

    internal static void ValidateLocalBoats(BoatTransportState transport)
    {
        if (transport.Sequence < 0 || transport.Boats is null || transport.Requests is null || transport.RetiredRequestRanges is null ||
            transport.Boats.Any(boat => boat is null || string.IsNullOrWhiteSpace(boat.Id) || string.IsNullOrWhiteSpace(boat.ProjectId) || string.IsNullOrWhiteSpace(boat.TownId)) ||
            transport.Boats.Select(boat => boat.Id).Distinct(StringComparer.Ordinal).Count() != transport.Boats.Count ||
            transport.Boats.Select(boat => boat.ProjectId).Distinct(StringComparer.Ordinal).Count() != transport.Boats.Count ||
            transport.Boats.Select(boat => boat.Position).Distinct().Count() != transport.Boats.Count ||
            !transport.Boats.Select(boat => boat.Id).SequenceEqual(transport.Boats.Select(boat => boat.Id).Order(StringComparer.Ordinal)) ||
            transport.Requests.Any(request => request is null || request.Sequence < 1 || request.Sequence > transport.Sequence ||
                request.Id != "boat-request:" + request.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                string.IsNullOrWhiteSpace(request.PassengerId) || string.IsNullOrWhiteSpace(request.BoatTownId) ||
                string.IsNullOrWhiteSpace(request.OriginPortId) || string.IsNullOrWhiteSpace(request.DestinationPortId) ||
                request.OriginPortId == request.DestinationPortId || request.RequestedTick < 0 ||
                request.Status is not ("waiting" or "underway" or "arrived" or "returned" or "cancelled") ||
                (request.Status is "arrived" or "returned" or "cancelled") != (request.SettledTick is not null) ||
                request.SettledTick is { } settled && settled < request.RequestedTick) ||
            transport.Requests.Select(request => request.Sequence).Distinct().Count() != transport.Requests.Count ||
            !transport.Requests.Select(request => request.Sequence).SequenceEqual(transport.Requests.Select(request => request.Sequence).Order()))
            throw new InvalidDataException("Boat assets and trip queues have invalid local state.");
        foreach (var boat in transport.Boats)
            if (boat.Journey is { } journey && (journey.WaterPath is not { Count: > 0 } ||
                journey.PathIndex < 0 || journey.PathIndex >= journey.WaterPath.Count || journey.StartedTick < 0 ||
                journey.NextMoveTick < journey.StartedTick || journey.WaitingSinceTick is { } since && since < journey.StartedTick))
                throw new InvalidDataException("Boat journeys have invalid local progress.");
    }
}
