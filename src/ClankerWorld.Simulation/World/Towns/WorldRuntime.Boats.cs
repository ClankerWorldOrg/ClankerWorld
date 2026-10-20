using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const int BoatStepTicks = 3; // Provisional travel speed.
    private const string BoatTripPrefix = BoatTripActionChoice.Prefix;
    private sealed record BoatTripChoice(string Id, string TownId, PlacedBuilding Origin, PlacedBuilding Destination, BoatTripActionPlan? Plan = null);

    private BoatState? PassengerBoat(string actor) => boatTransport.Boats.FirstOrDefault(boat => boat.Journey?.PassengerId == actor);
    private BoatTripRequest? WaitingBoatRequest(string actor) => boatTransport.Requests.FirstOrDefault(request =>
        request.PassengerId == actor && request.Status == "waiting");

    private bool ReadyToBoard(string actor) => IsAdult(actor) && PassengerBoat(actor) is null &&
        !animalWorld.Animals.Any(animal => animal.RiderId == actor || animal.LeaderId == actor) &&
        ConversationFor(actor) is null && AttachedHandcart(actor) is null && !HasGuardianPlacementTask(actor) &&
        !agents.Values.Any(person => person.GuardianPlacement?.CaregiverId == actor) && MovingCareGroup(actor).Count == 1 &&
        agents[actor].Project is not { Stage: not ("completed" or "cancelled") } &&
        agents[actor].Equipment?.Repair is null && agents[actor].MedicalTreatment is null &&
        !fields.Any(field => field.Work?.WorkerId == actor) &&
        !IsActiveTownProjectDeliveryForActor(actor);

    private bool IsActiveTownProjectDeliveryForActor(string actor) => society.Checkpoint.Inventory.Lots.Any(lot =>
        lot.CarrierId == actor && IsActiveTownProjectDelivery(lot.Id));

    private bool CanUseTownBoat(string townId, string actor) => towns.SingleOrDefault(town => town.Id == townId) is { } town &&
        (town.ResidentIds.Contains(actor, StringComparer.Ordinal) || TownBoatAccessRules.Allows(town, actor, WorldTick));

    private GridPoint? AvailablePortLanding(PlacedBuilding port, string passenger) =>
        PortGeometryFor(port).LandTiles.Where(point => !agents.Values.Any(person =>
            person.AgentId != passenger && person.Position == point)).Cast<GridPoint?>().FirstOrDefault();

    private IReadOnlyList<GridPoint> BoatWaterRoute(GridPoint origin, GridPoint dock, string? boatId = null) =>
        PortNavigationRules.WaterRoute(map, origin, [dock], PortObstacles()
            .Concat(boatTransport.Boats.Where(boat => boat.Id != boatId).SelectMany(boat => boat.Journey is { } journey ? new[] { boat.Position, journey.ReservedDock } : new[] { boat.Position })).ToHashSet());

    private IEnumerable<BoatTripChoice> BoatTripChoices(string actor)
    {
        if (!ReadyToBoard(actor) || WaitingBoatRequest(actor) is not null) yield break;
        foreach (var origin in worldSimulation.Buildings.Where(port => Port(port.InstanceId) is not null && PortIsLegal(port))
                     .OrderBy(port => port.InstanceId, StringComparer.Ordinal))
        {
            var approach = origin.Entrance!.Value;
            if (Reach(actor, approach, ReachPurpose.BoatDeparture, 0, 0, SwimmingPolicy.WhenCapable).Count == 0) continue;
            foreach (var townId in boatTransport.Boats.Where(boat => boat.DockedPortId == origin.InstanceId)
                         .Select(boat => boat.TownId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                if (!CanUseTownBoat(townId, actor)) continue;
                foreach (var destination in worldSimulation.Buildings.Where(port => port.InstanceId != origin.InstanceId &&
                             Port(port.InstanceId) is not null && PortIsLegal(port)).OrderBy(port => port.InstanceId, StringComparer.Ordinal))
                {
                    if (PlanBoatTrip(actor, townId, origin, destination) is { } plan && CheckActionPlan(plan).Allowed)
                        yield return new(plan.CandidateId, townId, origin, destination, plan);
                }
            }
        }
    }

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

    private BoatTripRequest QueueBoatTrip(string actor, BoatTripChoice choice, string? orderInstructionId = null)
    {
        var sequence = checked(boatTransport.Sequence + 1);
        boatTransport = boatTransport with { Sequence = sequence };
        var queuedRequest = new BoatTripRequest("boat-request:" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            sequence, actor, choice.TownId, choice.Origin.InstanceId, choice.Destination.InstanceId, WorldTick)
        { OrderInstructionId = orderInstructionId };
        SetBoatRequest(queuedRequest);
        AppendEvent(new BoatTripRequestedEventPayload(actor, queuedRequest.Id, choice.Origin.InstanceId, choice.Destination.InstanceId));
        return queuedRequest;
    }

    private void SetBoatRequest(BoatTripRequest request) => boatTransport = boatTransport with
    {
        Requests = boatTransport.Requests.Where(item => item.Id != request.Id).Append(request)
            .OrderBy(item => item.Sequence).ToArray(),
    };

    private void ProcessBoatQueue()
    {
        foreach (var request in boatTransport.Requests.Where(request => request.Status == "waiting").OrderBy(request => request.Sequence).ToArray())
        {
            var origin = Port(request.OriginPortId);
            var destination = Port(request.DestinationPortId);
            if (!agents.ContainsKey(request.PassengerId) || !CanUseTownBoat(request.BoatTownId, request.PassengerId) || origin is null || destination is null)
            {
                SetBoatRequest(request with { Status = "cancelled", SettledTick = WorldTick });
                AppendEvent(new BoatTripCancelledEventPayload(request.PassengerId, request.Id, "departure_unavailable"));
                continue;
            }
            if (!ReadyToBoard(request.PassengerId) || agents[request.PassengerId].Position != origin.Entrance ||
                !PortIsLegal(origin) || !PortIsLegal(destination) || AvailablePortLanding(destination, request.PassengerId) is null) continue;
            foreach (var boat in boatTransport.Boats.Where(boat => boat.TownId == request.BoatTownId &&
                         boat.DockedPortId == origin.InstanceId && boat.Journey is null).OrderBy(boat => boat.Id, StringComparer.Ordinal))
            {
                var route = FreePortDocks(destination).Select(dock => BoatWaterRoute(boat.Position, dock, boat.Id))
                    .FirstOrDefault(path => path.Count > 0);
                if (route is null) continue;
                var dock = route[^1];
                SetBoat(boat with
                {
                    DockedPortId = null,
                    Journey = new(request.Id, request.PassengerId, origin.InstanceId, destination.InstanceId, dock,
                        route, 0, WorldTick, checked(WorldTick + BoatStepTicks)),
                });
                SetBoatRequest(request with { Status = "underway", BoatId = boat.Id });
                SetBoatOrderTravelStatus(request, OrderStatus.Doing, null);
                agentStore.RecordMovement(request.PassengerId, agents[request.PassengerId], boat.Position, 0, 0);
                AppendEvent(new BoatDepartedEventPayload(request.PassengerId, boat.Id, origin.InstanceId, destination.InstanceId), boat.Position);
                break;
            }
        }
    }

    private void ProcessBoatTransport(long tick)
    {
        foreach (var original in boatTransport.Boats.Where(boat => boat.Journey is not null).ToArray())
        {
            var boat = MoveBoatGroundCargo(original);
            var journey = boat.Journey!;
            if (tick < journey.NextMoveTick) continue;
            var target = Port(journey.Returning ? journey.OriginPortId : journey.DestinationPortId);
            if (target is null || !PortIsLegal(target)) { WaitOrRecoverBoat(boat, tick); continue; }
            if (journey.PathIndex + 1 < journey.WaterPath.Count)
            {
                var next = journey.WaterPath[journey.PathIndex + 1];
                if (PortObstacles().Contains(next) || boatTransport.Boats.Any(other => other.Id != boat.Id && (other.Position == next || other.Journey?.ReservedDock == next)))
                {
                    var revised = BoatWaterRoute(boat.Position, journey.ReservedDock, boat.Id);
                    if (revised.Count == 0) { WaitOrRecoverBoat(boat, tick); continue; }
                    journey = journey with { WaterPath = revised, PathIndex = 0 };
                    next = revised.Count > 1 ? revised[1] : boat.Position;
                }
                boat = boat with
                {
                    Position = next,
                    Journey = journey with
                    {
                        PathIndex = Math.Min(journey.PathIndex + 1, journey.WaterPath.Count - 1),
                        NextMoveTick = checked(tick + BoatStepTicks),
                        WaitingSinceTick = null,
                    }
                };
                SetBoat(boat);
                if (agents.TryGetValue(journey.PassengerId, out var passenger))
                    agentStore.RecordPosition(journey.PassengerId, passenger, next);
                boat = MoveBoatGroundCargo(boat);
                journey = boat.Journey!;
                if (journey.PathIndex + 1 < journey.WaterPath.Count) continue;
            }
            var landing = AvailablePortLanding(target, journey.PassengerId);
            if (landing is null) { WaitOrRecoverBoat(boat, tick); continue; }
            if (agents.TryGetValue(journey.PassengerId, out var arriving))
                agentStore.RecordMovement(journey.PassengerId, arriving, landing.Value, 0, 0);
            MoveBoatGroundCargo(boat, landing.Value);
            SetBoat(boat with { DockedPortId = target.InstanceId, Journey = null, GroundCargoLotIds = null });
            var request = boatTransport.Requests.Single(request => request.Id == journey.RequestId);
            SetBoatRequest(request with { Status = journey.Returning ? "returned" : "arrived", SettledTick = tick });
            if (!journey.Returning) CreditBoatOrderArrival(request);
            else SetBoatOrderTravelStatus(request, OrderStatus.Blocked, "The boat returned to departure; the requested destination has not been reached.");
            AppendEvent(journey.Returning ? new BoatReturnedEventPayload(journey.PassengerId, boat.Id, target.InstanceId) : new BoatArrivedEventPayload(journey.PassengerId, boat.Id, target.InstanceId), landing.Value);
        }
        ProcessBoatQueue();
    }

    private void WaitOrRecoverBoat(BoatState boat, long tick)
    {
        var journey = boat.Journey!;
        var since = journey.WaitingSinceTick ?? tick;
        SetBoat(boat with { Journey = journey with { WaitingSinceTick = since, NextMoveTick = checked(tick + BoatStepTicks) } });
        var request = boatTransport.Requests.Single(request => request.Id == journey.RequestId);
        SetBoatOrderTravelStatus(request, OrderStatus.Blocked, "The boat is waiting for a safe landing; it may return to departure with its passenger and goods.");
        if (journey.WaitingSinceTick is null) AppendEvent(new BoatWaitingEventPayload(journey.PassengerId, boat.Id), boat.Position);
        if (!journey.Returning && tick - since < CivicDay) return;
        var recovery = Port(journey.Returning ? journey.DestinationPortId : journey.OriginPortId);
        if (recovery is null || !PortIsLegal(recovery) || AvailablePortLanding(recovery, journey.PassengerId) is null) return;
        var route = FreePortDocks(recovery, boat.Id).Select(dock => BoatWaterRoute(boat.Position, dock, boat.Id))
            .FirstOrDefault(path => path.Count > 0);
        if (route is null) return;
        var dock = route[^1];
        SetBoat(boat with
        {
            Journey = journey with
            {
                Returning = !journey.Returning,
                ReservedDock = dock,
                WaterPath = route,
                PathIndex = 0,
                WaitingSinceTick = null,
                NextMoveTick = checked(tick + BoatStepTicks),
            }
        });
        AppendEvent(new BoatRecoveryStartedEventPayload(journey.PassengerId, boat.Id, recovery.InstanceId), boat.Position);
    }

    private string? RecordBoatDeath(string actor)
    {
        if (PassengerBoat(actor) is not { } boat) return null;
        var ids = society.Checkpoint.Inventory.Lots.Where(lot => lot.ContainerLotId is null && lot.Quantity > 0 &&
            lot.GroundPosition == new InventoryGroundPosition(boat.Position.X, boat.Position.Y))
            .Select(lot => lot.Id).Order(StringComparer.Ordinal).ToArray();
        SetBoat(boat with { GroundCargoLotIds = ids });
        return boat.Id;
    }

    private BoatState MoveBoatGroundCargo(BoatState boat, GridPoint? destination = null, bool preserveCustody = false)
    {
        if (boat.GroundCargoLotIds is not { Count: > 0 } roots) return boat;
        var remaining = society.Checkpoint.Inventory.Lots.Where(lot => lot.ContainerLotId is null && lot.Quantity > 0 &&
            (roots.Contains(lot.Id, StringComparer.Ordinal) || lot.ProvenanceLotId is { } source && roots.Contains(source, StringComparer.Ordinal)) &&
            (preserveCustody || lot.GroundPosition is not null)).Select(lot => lot.Id).Order(StringComparer.Ordinal).ToArray();
        var position = destination ?? boat.Position;
        ApplyInventoryTransition(inventory => InventoryAuthority.MoveGroundCargo(inventory, remaining,
            new(position.X, position.Y), preserveCustody, holdAccess: remaining.SelectMany(id =>
                InventoryHoldRules.Family(InventoryIndex.For(inventory), inventory.GetLot(id))).Distinct().ToArray()));
        boat = boat with { GroundCargoLotIds = remaining };
        SetBoat(boat);
        return boat;
    }
}
