using System.Text.Json;

namespace ClankerWorld.Simulation.World;

public sealed record BoatTripActionChoice(string TownId, string OriginPortId, string DestinationPortId)
{
    public const string Prefix = "boat_trip:";
    public string Encode() => DerivedId.Hash(JsonSerializer.Serialize(new[] { TownId, OriginPortId, DestinationPortId }), Prefix);
}

public enum BoatWaitingKind { Cancel, Queue }

public sealed record BoatWaitingChoice(BoatWaitingKind Kind, string RequestId)
{
    public const string CancelPrefix = "boat_cancel:";
    public const string QueuePrefix = "boat_queue:";
    public string Encode() => (Kind == BoatWaitingKind.Cancel ? CancelPrefix : QueuePrefix) + RequestId;
    public static BoatWaitingChoice? Decode(string value) => value.StartsWith(CancelPrefix, StringComparison.Ordinal) ? new(BoatWaitingKind.Cancel, value[CancelPrefix.Length..]) :
        value.StartsWith(QueuePrefix, StringComparison.Ordinal) ? new(BoatWaitingKind.Queue, value[QueuePrefix.Length..]) : null;
}

public sealed record BoatTripActionPlan(string ActorId, BoatTripActionChoice Choice, GridPoint OriginPosition,
    GridPoint Approach, GridPoint DestinationPosition, string ProofBoatId, GridPoint ProofBoatPosition,
    IReadOnlyList<GridPoint> ApproachRoute, IReadOnlyList<GridPoint> WaterRoute) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "request the boat trip";
}

public sealed record BoatWaitingActionPlan(string ActorId, BoatWaitingChoice Choice, BoatTripRequest Request,
    GridPoint? Approach, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind == BoatWaitingKind.Cancel ? "cancel the waiting boat trip" : "return to the departure Port";
}
