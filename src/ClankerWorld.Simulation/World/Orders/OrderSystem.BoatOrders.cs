using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static BoatTripRequest? BoundBoatRequest(IBoatOrdersWorld world, OwnerQueuedInstruction instruction) => instruction.Task?.BoatTravel?.RequestId is { } id
            ? world.BoatTransport.Requests.FirstOrDefault(request => request.Id == id)
            : null;

    internal static CognitionCandidate? BoatOrderCandidate(IBoatOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        if (instruction.Task?.BoatTravel is not { } binding || world.Port(binding.DestinationPortId) is not { } destination ||
            !world.PortIsLegal(destination) || world.PassengerBoat(actor) is not null) return null;
        var waiting = world.WaitingBoatRequest(actor);
        if (waiting is not null && waiting.Id == binding.RequestId)
        {
            if (world.Port(waiting.OriginPortId) is not { } origin || world.Agents[actor].Position == origin.Entrance ||
                !world.CanWalkForTownProject(actor, world.Agents[actor].Position, origin.Entrance!.Value)) return null;
        }
        else if (!world.ReadyToBoard(actor) || waiting is null &&
                 !world.BoatTripChoices(actor).Any(choice => choice.Destination.InstanceId == binding.DestinationPortId)) return null;
        return new("boat_order", "Travel to the named Port using a real communal boat, its normal queue and safe landing rules.",
            0, binding.DestinationPortId);
    }


    internal static void ExecuteBoatOrder(IBoatOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var binding = instruction.Task!.BoatTravel!;
        if (world.WaitingBoatRequest(actor) is { } waiting)
        {
            if (waiting.Id == binding.RequestId)
            {
                _ = world.ApplyBoatCandidate(actor, IdentityRules.PrefixedReference("boat_queue:", waiting.Id));
                return;
            }
            // An owner order replaces an ordinary waiting plan, never an underway journey.
            _ = world.ApplyBoatCandidate(actor, IdentityRules.PrefixedReference("boat_cancel:", waiting.Id));
        }
        var choice = world.BoatTripChoices(actor).FirstOrDefault(choice => choice.Destination.InstanceId == binding.DestinationPortId);
        if (choice is null)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.BoatOrderBlockedReason(instruction));
            return;
        }
        if (world.Agents[actor].Position != choice.Origin.Entrance)
        {
            world.MoveToward(actor, world.Agents[actor], choice.Origin.Entrance!.Value, "boat_departure", 0);
            return;
        }
        var request = world.QueueBoatTrip(actor, choice, instruction.InstructionId);
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        world.Orders.RecordBoatOrder(current.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, current.Task!.UpdateTypedDefinition<TravelByBoatOrderDefinition>(task =>
            OrderStore.PrepareExecuteBoatOrderTravelByBoatOrderDefinition(task, OrderStore.PrepareExecuteBoatOrderOwnerBoatTravelBinding(binding, request.Id)))
));
    }


    internal static string BoatOrderBlockedReason(IBoatOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var binding = instruction.Task!.BoatTravel!;
        if (world.PassengerBoat(actor) is not null) return "The passenger must finish their current boat journey before starting another trip.";
        if (world.Port(binding.DestinationPortId) is not { } destination) return "The requested Port is no longer available.";
        if (!world.PortIsLegal(destination)) return "The destination Port or its land approach is blocked.";
        if (world.BoundBoatRequest(instruction) is { Status: "waiting" } request)
        {
            if (!world.CanUseTownBoat(request.BoatTownId, actor)) return "Waiting for permission to use the departure Town's boat.";
            if (world.Port(request.OriginPortId) is not { } origin || !world.PortIsLegal(origin)) return "The departure Port or its land approach is blocked.";
            if (world.Agents[actor].Position != origin.Entrance) return "Waiting for an open walking route to the departure Port's land approach.";
            if (!world.ReadyToBoard(actor)) return "Only an unaccompanied adult with no cart, animal or unfinished work can board.";
            if (world.AvailablePortLanding(destination, actor) is null) return "Waiting for a free landing tile at the destination Port.";
            if (!world.FreePortDocks(destination).Any()) return "Waiting for dock space at the destination Port.";
            var boats = world.BoatTransport.Boats.Where(boat => boat.TownId == request.BoatTownId &&
                boat.DockedPortId == origin.InstanceId && boat.Journey is null).ToArray();
            if (boats.Length == 0) return "Waiting for a communal boat at the departure Port.";
            return "Waiting for an open water route and the next usable place in the boat queue.";
        }
        if (!world.ReadyToBoard(actor)) return "Only an unaccompanied adult with no cart, animal or unfinished work can board.";
        var departures = world.Simulation.Buildings.Where(port => port.InstanceId != destination.InstanceId &&
            world.Port(port.InstanceId) is not null && world.PortIsLegal(port)).ToArray();
        var reachable = departures.Where(port => world.CanWalkForTownProject(actor, world.Agents[actor].Position, port.Entrance!.Value)).ToArray();
        if (reachable.Length == 0) return "Waiting for an open walking route to a usable departure Port.";
        var available = world.BoatTransport.Boats.Where(boat => boat.Journey is null &&
            reachable.Any(port => port.InstanceId == boat.DockedPortId)).ToArray();
        if (available.Length == 0) return "Waiting for a communal boat at a reachable departure Port.";
        if (!available.Any(boat => world.CanUseTownBoat(boat.TownId, actor))) return "Waiting for permission to use the departure Town's boat.";
        return "Waiting for an open water route to the requested Port.";
    }


    internal static void CancelBoatTravelForOrder(IBoatOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (world.BoundBoatRequest(instruction) is { Status: "waiting" } request)
        {
            world.CancelBoatRequest(request, world.WorldTick);
            world.AppendEvent(new BoatTripCancelledEventPayload(request.PassengerId, request.Id, "order_cancelled"));
        }
    }


    internal static OwnerQueuedInstruction? ActiveBoatOrder(IBoatOrdersWorld world, BoatTripRequest request) => request.OrderInstructionId is { } id
            ? world.Orders.Instructions.Values.FirstOrDefault(instruction => instruction.InstructionId == id &&
                instruction.Task is { Action: "travel_by_boat", BoatTravel: { } binding } order &&
                world.IsActiveOrder(order.Status) && binding.RequestId == request.Id)
            : null;

    internal static void SetBoatOrderTravelStatus(IBoatOrdersWorld world, BoatTripRequest request, OrderStatus status, string? reason)
    {
        if (world.ActiveBoatOrder(request) is { } instruction) world.SetOrderStatus(instruction, status, reason);
    }


    internal static void CreditBoatOrderArrival(IBoatOrdersWorld world, BoatTripRequest request)
    {
        if (world.ActiveBoatOrder(request) is { } instruction && instruction.Task!.BoatTravel!.DestinationPortId == request.DestinationPortId)
            world.CreditOrderEffect(instruction, Identity.Parse(world.BoatOrderArrivalEffectId(request.Id)), 1);
    }

}
