using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    private const int BoatStepTicks = 3; // Provisional travel speed.
    internal sealed record BoatTripChoice(string Id, string TownId, PlacedBuilding Origin, PlacedBuilding Destination, BoatTripActionPlan? Plan = null);

    internal static BoatState? PassengerBoat(IBoatWorld world, string actor) => world.Transport.Boats.Boats.FirstOrDefault(boat => boat.Journey?.PassengerId == actor);

    internal static BoatTripRequest? WaitingBoatRequest(IBoatWorld world, string actor) => world.Transport.Boats.Requests.FirstOrDefault(request =>
        request.PassengerId == actor && request.Status == "waiting");

    internal static bool ReadyToBoard(IBoatWorld world, string actor) => world.IsAdult(actor) && PassengerBoat(world, actor) is null &&
        !world.Animals.Any(animal => animal.RiderId == actor || animal.LeaderId == actor) &&
        world.ConversationFor(actor) is null && world.AttachedHandcart(actor) is null && !world.HasGuardianPlacementTask(actor) &&
        !world.Agents.Values.Any(person => person.GuardianPlacement?.CaregiverId == actor) && world.MovingCareGroup(actor).Count == 1 &&
        world.Agents[actor].Project is not { Stage: not ("completed" or "cancelled") } &&
        world.Agents[actor].Equipment?.Repair is null && world.Agents[actor].MedicalTreatment is null &&
        !world.Fields.Any(field => field.Work?.WorkerId == actor) &&
        !IsActiveTownProjectDeliveryForActor(world, actor);

    internal static bool IsActiveTownProjectDeliveryForActor(IBoatWorld world, string actor) => world.Inventory.Lots.Any(lot =>
        lot.CarrierId == actor && world.IsActiveTownProjectDelivery(lot.Id));

    internal static bool CanUseTownBoat(IBoatWorld world, string townId, string actor) => world.Towns.SingleOrDefault(town => town.Id == townId) is { } town &&
        (town.ResidentIds.Contains(actor, StringComparer.Ordinal) || TownBoatAccessRules.Allows(town, actor, world.WorldTick));

    internal static GridPoint? AvailablePortLanding(IBoatWorld world, PlacedBuilding port, string passenger) =>
        world.PortGeometryFor(port).LandTiles.Where(point => !world.Agents.Values.Any(person =>
            person.AgentId != passenger && person.Position == point)).Cast<GridPoint?>().FirstOrDefault();

    internal static IReadOnlyList<GridPoint> BoatWaterRoute(IBoatWorld world, GridPoint origin, GridPoint dock, string? boatId = null) =>
        PortNavigationRules.WaterRoute(world.Map, origin, [dock], world.PortObstacles()
            .Concat(world.Transport.Boats.Boats.Where(boat => boat.Id != boatId).SelectMany(boat => boat.Journey is { } journey ? new[] { boat.Position, journey.ReservedDock } : new[] { boat.Position })).ToHashSet());

    internal static IEnumerable<BoatTripChoice> BoatTripChoices(IBoatWorld world, string actor)
    {
        if (!ReadyToBoard(world, actor) || WaitingBoatRequest(world, actor) is not null) yield break;
        foreach (var origin in world.Buildings.Where(port => world.Port(port.InstanceId) is not null && world.PortIsLegal(port))
                     .OrderBy(port => port.InstanceId, StringComparer.Ordinal))
        {
            var approach = origin.Entrance!.Value;
            if (world.Reach(actor, approach, ReachPurpose.BoatDeparture, 0, 0, SwimmingPolicy.WhenCapable).Count == 0) continue;
            foreach (var townId in world.Transport.Boats.Boats.Where(boat => boat.DockedPortId == origin.InstanceId)
                         .Select(boat => boat.TownId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                if (!CanUseTownBoat(world, townId, actor)) continue;
                foreach (var destination in world.Buildings.Where(port => port.InstanceId != origin.InstanceId &&
                             world.Port(port.InstanceId) is not null && world.PortIsLegal(port)).OrderBy(port => port.InstanceId, StringComparer.Ordinal))
                {
                    if (PlanBoatTrip(world, actor, townId, origin, destination) is { } plan && world.CheckActionPlan(plan).Allowed)
                        yield return new(plan.CandidateId, townId, origin, destination, plan);
                }
            }
        }
    }

    internal static BoatTripRequest QueueBoatTrip(IBoatWorld world, string actor, BoatTripChoice choice, string? orderInstructionId = null)
    {
        var sequence = world.Transport.BeginBoatRequestSequence();
        var queuedRequest = new BoatTripRequest(DerivedId.Numbered("boat-request:", sequence),
            sequence, actor, choice.TownId, choice.Origin.InstanceId, choice.Destination.InstanceId, world.WorldTick)
        { OrderInstructionId = orderInstructionId };
        world.Transport.RecordBoatRequest(queuedRequest);
        world.AppendEvent(new BoatTripRequestedEventPayload(actor, queuedRequest.Id, choice.Origin.InstanceId, choice.Destination.InstanceId));
        return queuedRequest;
    }

    internal static void ProcessBoatQueue(IBoatWorld world)
    {
        foreach (var request in world.Transport.Boats.Requests.Where(request => request.Status == "waiting").OrderBy(request => request.Sequence).ToArray())
        {
            var origin = world.Port(request.OriginPortId);
            var destination = world.Port(request.DestinationPortId);
            if (!world.Agents.ContainsKey(request.PassengerId) || !CanUseTownBoat(world, request.BoatTownId, request.PassengerId) || origin is null || destination is null)
            {
                world.Transport.RecordBoatRequest(request with { Status = "cancelled", SettledTick = world.WorldTick });
                world.AppendEvent(new BoatTripCancelledEventPayload(request.PassengerId, request.Id, "departure_unavailable"));
                continue;
            }
            if (!ReadyToBoard(world, request.PassengerId) || world.Agents[request.PassengerId].Position != origin.Entrance ||
                !world.PortIsLegal(origin) || !world.PortIsLegal(destination) || AvailablePortLanding(world, destination, request.PassengerId) is null) continue;
            foreach (var boat in world.Transport.Boats.Boats.Where(boat => boat.TownId == request.BoatTownId &&
                         boat.DockedPortId == origin.InstanceId && boat.Journey is null).OrderBy(boat => boat.Id, StringComparer.Ordinal))
            {
                var route = world.FreePortDocks(destination).Select(dock => BoatWaterRoute(world, boat.Position, dock, boat.Id))
                    .FirstOrDefault(path => path.Count > 0);
                if (route is null) continue;
                var dock = route[^1];
                world.Transport.RecordBoat(boat with
                {
                    DockedPortId = null,
                    Journey = new(request.Id, request.PassengerId, origin.InstanceId, destination.InstanceId, dock,
                        route, 0, world.WorldTick, checked(world.WorldTick + BoatStepTicks)),
                });
                world.Transport.RecordBoatRequest(request with { Status = "underway", BoatId = boat.Id });
                world.SetBoatOrderTravelStatus(request, OrderStatus.Doing, null);
                world.PhysicalAgents.RecordMovement(request.PassengerId, world.Agents[request.PassengerId], boat.Position, 0, 0);
                world.AppendEvent(new BoatDepartedEventPayload(request.PassengerId, boat.Id, origin.InstanceId, destination.InstanceId), boat.Position);
                break;
            }
        }
    }

    internal static void ProcessBoatTransport(IBoatWorld world, long tick)
    {
        foreach (var original in world.Transport.Boats.Boats.Where(boat => boat.Journey is not null).ToArray())
        {
            var boat = MoveBoatGroundCargo(world, original);
            var journey = boat.Journey!;
            if (tick < journey.NextMoveTick) continue;
            var target = world.Port(journey.Returning ? journey.OriginPortId : journey.DestinationPortId);
            if (target is null || !world.PortIsLegal(target)) { WaitOrRecoverBoat(world, boat, tick); continue; }
            if (journey.PathIndex + 1 < journey.WaterPath.Count)
            {
                var next = journey.WaterPath[journey.PathIndex + 1];
                if (world.PortObstacles().Contains(next) || world.Transport.Boats.Boats.Any(other => other.Id != boat.Id && (other.Position == next || other.Journey?.ReservedDock == next)))
                {
                    var revised = BoatWaterRoute(world, boat.Position, journey.ReservedDock, boat.Id);
                    if (revised.Count == 0) { WaitOrRecoverBoat(world, boat, tick); continue; }
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
                world.Transport.RecordBoat(boat);
                if (world.Agents.TryGetValue(journey.PassengerId, out var passenger))
                    world.PhysicalAgents.RecordPosition(journey.PassengerId, passenger, next);
                boat = MoveBoatGroundCargo(world, boat);
                journey = boat.Journey!;
                if (journey.PathIndex + 1 < journey.WaterPath.Count) continue;
            }
            var landing = AvailablePortLanding(world, target, journey.PassengerId);
            if (landing is null) { WaitOrRecoverBoat(world, boat, tick); continue; }
            if (world.Agents.TryGetValue(journey.PassengerId, out var arriving))
                world.PhysicalAgents.RecordMovement(journey.PassengerId, arriving, landing.Value, 0, 0);
            MoveBoatGroundCargo(world, boat, landing.Value);
            world.Transport.RecordBoat(boat with { DockedPortId = target.InstanceId, Journey = null, GroundCargoLotIds = null });
            var request = world.Transport.Boats.Requests.Single(request => request.Id == journey.RequestId);
            world.Transport.RecordBoatRequest(request with { Status = journey.Returning ? "returned" : "arrived", SettledTick = tick });
            if (!journey.Returning) world.CreditBoatOrderArrival(request);
            else world.SetBoatOrderTravelStatus(request, OrderStatus.Blocked, "The boat returned to departure; the requested destination has not been reached.");
            world.AppendEvent(journey.Returning ? new BoatReturnedEventPayload(journey.PassengerId, boat.Id, target.InstanceId) : new BoatArrivedEventPayload(journey.PassengerId, boat.Id, target.InstanceId), landing.Value);
        }
        ProcessBoatQueue(world);
    }

    internal static void WaitOrRecoverBoat(IBoatWorld world, BoatState boat, long tick)
    {
        var journey = boat.Journey!;
        var since = journey.WaitingSinceTick ?? tick;
        world.Transport.RecordBoat(boat with { Journey = journey with { WaitingSinceTick = since, NextMoveTick = checked(tick + BoatStepTicks) } });
        var request = world.Transport.Boats.Requests.Single(request => request.Id == journey.RequestId);
        world.SetBoatOrderTravelStatus(request, OrderStatus.Blocked, "The boat is waiting for a safe landing; it may return to departure with its passenger and goods.");
        if (journey.WaitingSinceTick is null) world.AppendEvent(new BoatWaitingEventPayload(journey.PassengerId, boat.Id), boat.Position);
        if (!journey.Returning && tick - since < world.CivicDay) return;
        var recovery = world.Port(journey.Returning ? journey.DestinationPortId : journey.OriginPortId);
        if (recovery is null || !world.PortIsLegal(recovery) || AvailablePortLanding(world, recovery, journey.PassengerId) is null) return;
        var route = world.FreePortDocks(recovery, boat.Id).Select(dock => BoatWaterRoute(world, boat.Position, dock, boat.Id))
            .FirstOrDefault(path => path.Count > 0);
        if (route is null) return;
        var dock = route[^1];
        world.Transport.RecordBoat(boat with
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
        world.AppendEvent(new BoatRecoveryStartedEventPayload(journey.PassengerId, boat.Id, recovery.InstanceId), boat.Position);
    }

    internal static string? RecordBoatDeath(IBoatWorld world, string actor)
    {
        if (PassengerBoat(world, actor) is not { } boat) return null;
        var ids = world.Inventory.Lots.Where(lot => lot.ContainerLotId is null && lot.Quantity > 0 &&
            lot.GroundPosition == new InventoryGroundPosition(boat.Position.X, boat.Position.Y))
            .Select(lot => lot.Id).Order(StringComparer.Ordinal).ToArray();
        world.Transport.RecordBoat(boat with { GroundCargoLotIds = ids });
        return boat.Id;
    }

    internal static BoatState MoveBoatGroundCargo(IBoatWorld world, BoatState boat, GridPoint? destination = null, bool preserveCustody = false)
    {
        if (boat.GroundCargoLotIds is not { Count: > 0 } roots) return boat;
        var remaining = world.Inventory.Lots.Where(lot => lot.ContainerLotId is null && lot.Quantity > 0 &&
            (roots.Contains(lot.Id, StringComparer.Ordinal) || lot.ProvenanceLotId is { } source && roots.Contains(source, StringComparer.Ordinal)) &&
            (preserveCustody || lot.GroundPosition is not null)).Select(lot => lot.Id).Order(StringComparer.Ordinal).ToArray();
        var position = destination ?? boat.Position;
        world.MoveGroundCargo(remaining, position, preserveCustody);
        boat = boat with { GroundCargoLotIds = remaining };
        world.Transport.RecordBoat(boat);
        return boat;
    }

    internal static BoatTripActionPlan? PlanBoatTrip(IBoatWorld world, string actor, string townId, PlacedBuilding origin, PlacedBuilding destination)
    {
        var approach = origin.Entrance!.Value;
        foreach (var boat in world.Transport.Boats.Boats.Where(boat => boat.TownId == townId && boat.DockedPortId == origin.InstanceId && boat.Journey is null))
        {
            // This proves geography only. The queue later assigns a free boat and dock.
            var water = world.ReachBoatGeography(boat, destination);
            if (water.Count == 0) continue;
            return new(actor, new(townId, origin.InstanceId, destination.InstanceId), origin.Position, approach,
                destination.Position, boat.Id, boat.Position, world.Reach(actor, approach, ReachPurpose.BoatDeparture, 0, 0,
                    SwimmingPolicy.WhenCapable), water);
        }
        return null;
    }

    internal static BoatWaitingActionPlan? PlanBoatWaiting(IBoatWorld world, string actor, BoatWaitingChoice choice)
    {
        if (WaitingBoatRequest(world, actor) is not { } request || request.Id != choice.RequestId) return null;
        var approach = choice.Kind == BoatWaitingKind.Queue ? world.Port(request.OriginPortId)?.Entrance : null;
        if (choice.Kind == BoatWaitingKind.Queue && approach is null) return null;
        return new(actor, choice, request, approach, approach is { } point
            ? world.Reach(actor, point, ReachPurpose.BoatDeparture, 0, 0, SwimmingPolicy.WhenCapable) : Array.AsReadOnly<GridPoint>([]));
    }

    internal static ActionCheck CheckBoatTripPlan(IBoatWorld world, BoatTripActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.BoatTripCapabilities);
        if (!capability.Allowed) return capability;
        var origin = world.Port(plan.Choice.OriginPortId);
        var destination = world.Port(plan.Choice.DestinationPortId);
        var boat = world.Transport.Boats.Boats.FirstOrDefault(item => item.Id == plan.ProofBoatId);
        if (!ReadyToBoard(world, plan.Actor) || WaitingBoatRequest(world, plan.Actor) is not null || !CanUseTownBoat(world, plan.Choice.TownId, plan.Actor) ||
            origin is null || destination is null || !world.PortIsLegal(origin) || !world.PortIsLegal(destination) || origin.Position != plan.OriginPosition ||
            origin.Entrance != plan.Approach || destination.Position != plan.DestinationPosition || boat is null ||
            boat.TownId != plan.Choice.TownId || boat.DockedPortId != origin.InstanceId || boat.Journey is not null || boat.Position != plan.ProofBoatPosition)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected departure, boat access or trip is no longer available");
        if (world.ReachBoatGeography(boat, destination).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected destination is no longer reachable by boat");
        return world.Agents[plan.Actor].Position == plan.Approach || world.Reach(plan.Actor, plan.Approach, ReachPurpose.BoatDeparture, 0, 0,
            SwimmingPolicy.WhenCapable).Count > 0 ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected departure Port is out of reach now");
    }

    internal static ActionCheck CheckBoatWaitingPlan(IBoatWorld world, BoatWaitingActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.TransportCapabilities);
        if (!capability.Allowed) return capability;
        if (WaitingBoatRequest(world, plan.Actor) != plan.Request)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected waiting boat request has changed");
        if (plan.Choice.Kind == BoatWaitingKind.Cancel) return new();
        if (world.Port(plan.Request.OriginPortId)?.Entrance != plan.Approach || plan.Approach is null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected departure Port has changed");
        // Returning to a blocked departure remains a native movement wait.
        return new();
    }

    internal static void ApplyCheckedBoatTrip(IBoatWorld world, BoatTripActionPlan plan)
    {
        if (world.Agents[plan.Actor].Position != plan.Approach) { world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Approach, "boat_departure"); return; }
        QueueBoatTrip(world, plan.Actor, new(plan.CandidateId, plan.Choice.TownId, world.Port(plan.Choice.OriginPortId)!, world.Port(plan.Choice.DestinationPortId)!, plan));
    }

    internal static void ApplyCheckedBoatWaiting(IBoatWorld world, BoatWaitingActionPlan plan)
    {
        if (plan.Choice.Kind == BoatWaitingKind.Cancel) world.Transport.RecordBoatRequest(plan.Request with { Status = "cancelled", SettledTick = world.WorldTick });
        else world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Approach!.Value, "boat_queue");
    }

    internal static void CompletePaidBoatProject(IBoatWorld world, string townId, TownProject project)
    {
        var port = world.Port(project.Plan.BoatPortId!)!;
        var dock = world.FreePortDock(port) ?? throw new InvalidOperationException("The completed boat has no reserved launch space.");
        var id = TownProjectRules.BuildingId(project.Id);
        world.Transport.RecordBoat(new(id, townId, project.Id, dock, port.InstanceId));
        world.SetTownProject(townId, project with
        {
            Stage = "completed",
            CompletedBoatId = id,
            Blocker = null,
            LastTransitionTick = world.WorldTick,
        });
        world.AppendEvent(new CommunalBoatLaunchedEventPayload(null, townId, id, port.InstanceId, project.Plan.Name), dock);
    }

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


    internal static PlacedBuilding? Port(IBoatWorld world, string id) => world.Buildings.FirstOrDefault(building =>
        building.InstanceId == id && world.Definitions.Any(definition =>
            definition.CanonicalId == building.DefinitionId && PortNavigationRules.IsPort(definition)));

    internal static PortGeometry PortGeometryFor(IBoatWorld world, PlacedBuilding port) => PortNavigationRules.Geometry(world.Map,
        world.Definitions.Single(definition => definition.CanonicalId == port.DefinitionId), port.Position);

    internal static HashSet<GridPoint> PortObstacles(IBoatWorld world, string? exceptProjectId = null, string? exceptPortId = null, bool protectDocks = false)
    {
        var ground = world.Ground;
        return GroundOccupancy.Combine(ground.Resources, ground.MapObjects, ground.Fields, ground.RoadsAndBridgeEntrances,
            protectDocks ? ground.ProtectedBuildingTiles(exceptPortId) : ground.BuildingFootprintsExcept(exceptPortId),
            ground.ExpansionSites, ground.ProtectedProjectSites(exceptProjectId));
    }

    internal static IEnumerable<GridPoint> FreePortDocks(IBoatWorld world, PlacedBuilding port, string? exceptBoatId = null) =>
        PortGeometryFor(world, port).DockingTiles.Where(dock => !world.Transport.Boats.Boats.Any(boat => boat.Id != exceptBoatId &&
            (boat.Position == dock || boat.Journey?.ReservedDock == dock)));

    internal static GridPoint? FreePortDock(IBoatWorld world, PlacedBuilding port, string? exceptBoatId = null) =>
        FreePortDocks(world, port, exceptBoatId).Cast<GridPoint?>().FirstOrDefault();

    internal static bool HasCommunalBoatPort(IBoatWorld world, string townId) =>
        world.Buildings.Any(building => building.TownId == townId && Port(world, building.InstanceId) is not null);

    internal static IReadOnlyList<GridPoint> ReachBoatGeography(IBoatWorld world, BoatState boat, PlacedBuilding destination) =>
        PortNavigationRules.WaterRoute(world.Map, boat.Position, PortGeometryFor(world, destination).DockingTiles, PortObstacles(world));
}
