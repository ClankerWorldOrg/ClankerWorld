using BoatTripChoice = ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string BoatTripPrefix = BoatTripActionChoice.Prefix;

    private void AddBoatCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (TransportSystem.WaitingBoatRequest(this.BoatWorld, actor) is { } waiting)
        {
            if (TransportSystem.PlanBoatWaiting(BoatWorld, actor, new(BoatWaitingKind.Cancel, waiting.Id)) is { } cancel && CheckActionPlan(cancel).Allowed)
                candidates.Add(new(cancel.CandidateId, "Cancel your waiting boat trip and release unused reservations.", 90) { ActionPlan = cancel });
            if (TransportSystem.PlanBoatWaiting(BoatWorld, actor, new(BoatWaitingKind.Queue, waiting.Id)) is { } queue && agents[actor].Position != queue.Approach && CheckActionPlan(queue).Allowed)
                candidates.Add(new(queue.CandidateId, "Return to the Port's land approach to wait for your boat trip.", 80) { ActionPlan = queue });
            return;
        }
        foreach (var choice in TransportSystem.BoatTripChoices(this.BoatWorld, actor))
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
            if (TransportSystem.PlanBoatWaiting(BoatWorld, actor, choice) is { } plan) ApplyBoatWaitingPlan(plan);
            return true;
        }
        if (!candidateId.StartsWith(BoatTripPrefix, StringComparison.Ordinal)) return false;
        if (TransportSystem.BoatTripChoices(this.BoatWorld, actor).FirstOrDefault(choice => choice.Id == candidateId)?.Plan is { } planned) ApplyBoatTripPlan(planned);
        return true;
    }
}
