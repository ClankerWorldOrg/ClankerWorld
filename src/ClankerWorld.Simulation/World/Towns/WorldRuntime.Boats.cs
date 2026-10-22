using BoatTripChoice = ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string BoatTripPrefix = BoatTripActionChoice.Prefix;

    private BoatState? PassengerBoat(string actor) =>
        TransportSystem.PassengerBoat(BoatWorld, actor);
    private BoatTripRequest? WaitingBoatRequest(string actor) =>
        TransportSystem.WaitingBoatRequest(BoatWorld, actor);

    private bool ReadyToBoard(string actor) =>
        TransportSystem.ReadyToBoard(BoatWorld, actor);

    private bool CanUseTownBoat(string townId, string actor) =>
        TransportSystem.CanUseTownBoat(BoatWorld, townId, actor);

    private GridPoint? AvailablePortLanding(PlacedBuilding port, string passenger) =>
        TransportSystem.AvailablePortLanding(BoatWorld, port, passenger);

    private IEnumerable<BoatTripChoice> BoatTripChoices(string actor) =>
        TransportSystem.BoatTripChoices(BoatWorld, actor);

    private void AddBoatCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (WaitingBoatRequest(actor) is { } waiting)
        {
            if (PlanBoatWaiting(actor, new(BoatWaitingKind.Cancel, waiting.Id)) is { } cancel && CheckActionPlan(cancel).Allowed)
                candidates.Add(new(cancel.CandidateId, "Cancel your waiting boat trip and release unused reservations.", 90) { ActionPlan = cancel });
            if (PlanBoatWaiting(actor, new(BoatWaitingKind.Queue, waiting.Id)) is { } queue && agents[actor].Position != queue.Approach && CheckActionPlan(queue).Allowed)
                candidates.Add(new(queue.CandidateId, "Return to the Port's land approach to wait for your boat trip.", 80) { ActionPlan = queue });
            return;
        }
        foreach (var choice in BoatTripChoices(actor))
            candidates.Add(new(choice.Id,
                FormattableString.Invariant($"Request a communal boat trip to the Port at ({choice.Destination.Position.X}, {choice.Destination.Position.Y}), carrying your goods; wait at the departure Port's land approach."),
                80, choice.Destination.InstanceId)
            { ActionPlan = choice.Plan });
    }

    private bool ApplyBoatCandidate(string actor, string candidateId, ActionPlan? originalPlan = null)
    {
        if (originalPlan is BoatTripActionPlan trip) { ApplyBoatTripPlan(trip); return true; }
        if (originalPlan is BoatWaitingActionPlan waiting) { ApplyBoatWaitingPlan(waiting); return true; }
        if (BoatWaitingChoice.Decode(candidateId) is { } choice)
        {
            if (PlanBoatWaiting(actor, choice) is { } plan) ApplyBoatWaitingPlan(plan);
            return true;
        }
        if (!candidateId.StartsWith(BoatTripPrefix, StringComparison.Ordinal)) return false;
        if (BoatTripChoices(actor).FirstOrDefault(choice => choice.Id == candidateId)?.Plan is { } planned) ApplyBoatTripPlan(planned);
        return true;
    }

    private BoatTripRequest QueueBoatTrip(string actor, BoatTripChoice choice, string? orderInstructionId = null) =>
        TransportSystem.QueueBoatTrip(BoatWorld, actor, choice, orderInstructionId);

    private void ProcessBoatQueue() =>
        TransportSystem.ProcessBoatQueue(BoatWorld);

    private void ProcessBoatTransport(long tick) =>
        TransportSystem.ProcessBoatTransport(BoatWorld, tick);

    private string? RecordBoatDeath(string actor) =>
        TransportSystem.RecordBoatDeath(BoatWorld, actor);

    private BoatState MoveBoatGroundCargo(BoatState boat, GridPoint? destination = null, bool preserveCustody = false) =>
        TransportSystem.MoveBoatGroundCargo(BoatWorld, boat, destination, preserveCustody);
}
