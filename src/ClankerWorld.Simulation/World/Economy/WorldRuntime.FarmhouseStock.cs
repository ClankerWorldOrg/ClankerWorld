using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static void ValidateFarmFields(FarmFieldState[] fields, WorldMap map, string seed,
        SocietyCheckpoint society, WorldContentSimulationState simulation, DeclarativeWorldContentState content,
        IReadOnlyList<GridPoint> roads) =>
        FarmingSystem.ValidateFarmFields(fields, map, seed, society, simulation, content, roads);
    private static ActorCapabilityPolicy FarmStockPolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private FarmStockActionPlan? PlanFarmStockAction(string actor, FarmStockActionKind kind, ActorCapabilityPolicy? policy = null)
    {
        var candidateId = FarmStockActionPlan.Candidate(kind);
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), candidateId, policy ?? FarmStockPolicy).Allowed ||
            HouseholdFor(actor) is not { } household || CarriedHouseDelivery(actor) is not null ||
            FarmhouseForHousehold(household) is not { } farmhouse)
            return null;
        FarmStockChoice? choice;
        PlacedBuilding? destination;
        if (kind == FarmStockActionKind.Grain)
        {
            choice = FarmGrainForDelivery(household, actor);
            destination = choice is null ? null : FarmStorageForHaul(household, actor, choice);
        }
        else
        {
            destination = HouseForHousehold(household);
            choice = destination is null ? null : FarmFlourForHouse(household, farmhouse.InstanceId, actor, destination.InstanceId);
        }
        if (choice is null || destination is null || PlanFarmStockHaul(actor, destination.InstanceId, choice) is not { } haul)
            return null;
        var source = HouseholdStockPosition(choice.Carrier);
        var range = HouseholdStockInteractionRange(choice.Carrier);
        var route = Reach(actor, source, ReachPurpose.FarmStockPickup, range, 0, SwimmingPolicy.WhenCapable);
        var returnRoute = ReachLeg(actor, source, destination.Position, ReachPurpose.FarmStockDelivery, 0, 0, SwimmingPolicy.WhenCapable);
        if (!IsWithinInteractionRange(agents[actor].Position, source, range) && route.Count == 0 || returnRoute.Count == 0)
            return null;
        var inventory = society.Checkpoint.Inventory;
        return new(actor, kind, household, choice.Carrier.Id, choice.Resource.Id, choice.Resource.ItemKind,
            haul.TransferQuantity, haul.ResourceQuantity,
            haul.MoveContainerFamily ? ContainerFamilyQuantity(inventory, choice.Carrier.Id) : haul.TransferQuantity,
            haul.MoveContainerFamily, destination.InstanceId, destination.Position, source, range,
            choice.Carrier.StorageBuildingId, choice.ResourceQuantityLimit,
            haul.MoveContainerFamily ? GoodsPlanFamily.Capture(inventory, choice.Carrier) : null, route, returnRoute);
    }

    private ActionCheck CheckFarmStockAction(FarmStockActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? FarmStockPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || FarmhouseForHousehold(plan.HouseholdId) is not { } farmhouse ||
            CarriedHouseDelivery(plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or delivery work has changed");
        var destination = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == plan.DestinationId);
        if (destination is null || destination.HouseholdId != plan.HouseholdId || destination.Position != plan.Destination ||
            (plan.Kind == FarmStockActionKind.Flour && HouseForHousehold(plan.HouseholdId)?.InstanceId != plan.DestinationId ||
             plan.Kind == FarmStockActionKind.Grain && destination.InstanceId != farmhouse.InstanceId &&
                HouseholdBuildingWithTag(plan.HouseholdId, "silo")?.InstanceId != plan.DestinationId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected farm storage has changed");
        var inventory = society.Checkpoint.Inventory;
        var request = DeliveryGoodsRequest(plan.Actor, plan.HouseholdId, plan.ItemKind, plan.DestinationId, GoodsDeliveryPurpose.FarmStock);
        if (RecheckGoods(request, plan.ResourceId) is not { } match || match.Root.Id != plan.CarrierId ||
            !plan.MoveContainerFamily && match.Quantity < plan.ResourceQuantity || match.Root.CarrierId is not null ||
            match.Root.StorageBuildingId != plan.SourceBuildingId || HouseholdStockPosition(match.Root) != plan.Source ||
            HouseholdStockInteractionRange(match.Root) != plan.SourceRange ||
            !GoodsPlanFamily.Matches(inventory, match.Root, plan.Family) ||
            InventoryContainerRules.IsContainer(match.Root.ItemKind) && HasActiveContainerReservation(inventory, match.Root.Id) ||
            plan.MoveContainerFamily && UnusableDeliveryStock(inventory, match.Root))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected farm load is no longer available");
        if (plan.Kind == FarmStockActionKind.Flour && plan.SourceBuildingId != farmhouse.InstanceId ||
            plan.ResourceQuantityLimit is not null && FarmhouseGrainDeficit(plan.HouseholdId, farmhouse) < plan.ResourceQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the Farmhouse no longer needs this load");
        if (PickupCarryCapacity(plan.Actor, match.Root, plan.Destination) < plan.PhysicalQuantity ||
            DestinationRoom(plan.DestinationId) < plan.PhysicalQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected load no longer fits");
        if (!IsWithinInteractionRange(agents[plan.Actor].Position, plan.Source, plan.SourceRange) &&
            Reach(plan.Actor, plan.Source, ReachPurpose.FarmStockPickup, plan.SourceRange, 0, SwimmingPolicy.WhenCapable).Count == 0 ||
            ReachLeg(plan.Actor, plan.Source, plan.Destination, ReachPurpose.FarmStockDelivery, 0, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected farm trip is out of reach now");
        return new();
    }

    private int FarmhouseGrainDeficit(string household, PlacedBuilding farmhouse) => worldContent.Recipes
        .Where(recipe => recipe.WorkstationBuildingId == farmhouse.DefinitionId && !recipe.IsCrop && NeedsRecipeOutput(recipe, household))
        .SelectMany(recipe => recipe.Inputs).Where(input => input.ResourceId == FarmFieldRules.Grain)
        .Select(input => input.Amount).DefaultIfEmpty(0).Max() * SupplyBatches -
        FarmSuppliedQuantity(household, farmhouse.InstanceId) - FarmIncomingQuantity(farmhouse.InstanceId);

    private void ApplyCheckedFarmStockAction(FarmStockActionPlan plan)
    {
        var state = agents[plan.Actor];
        if (!IsWithinInteractionRange(state.Position, plan.Source, plan.SourceRange))
        {
            MoveToward(plan.Actor, state, plan.Source, plan.Kind == FarmStockActionKind.Grain ? "farm_grain" : "farm_flour", plan.SourceRange);
            return;
        }
        var inventory = society.Checkpoint.Inventory;
        var haul = new FarmStockHaulPlan(inventory.GetLot(plan.CarrierId), inventory.GetLot(plan.ResourceId),
            plan.TransferQuantity, plan.ResourceQuantity, plan.MoveContainerFamily);
        ApplyFarmStockHaul(plan.Actor, plan.HouseholdId, plan.DestinationId, haul,
            plan.Kind == FarmStockActionKind.Grain ? DerivedId.Create($"farm-grain-pickup:{WorldTick}:{plan.Actor}") : DerivedId.Create($"farm-flour-pickup:{WorldTick}:{plan.Actor}"),
            plan.Kind == FarmStockActionKind.Grain ? "farm_grain_picked_up" : "farm_flour_picked_up");
    }
    internal sealed record FarmStockChoice(InventoryLot Carrier, InventoryLot Resource,
        int? ResourceQuantityLimit = null);

    internal sealed record FarmStockHaulPlan(
        InventoryLot Carrier,
        InventoryLot Resource,
        int TransferQuantity,
        int ResourceQuantity,
        bool MoveContainerFamily);

    private PlacedBuilding? FarmhouseForHousehold(string householdId) =>
        HouseholdBuildingWithTag(householdId, "farmhouse");

    private PlacedBuilding? FarmStorageFor(string householdId, string kind, string? requestedBuildingId = null,
        Func<PlacedBuilding, bool>? fitsHaul = null)
    {
        var farmhouse = FarmhouseForHousehold(householdId);
        var silo = HouseholdBuildingWithTag(householdId, "silo");
        var choices = kind == FarmFieldRules.Grain ? new[] { farmhouse, silo } : new[] { silo, farmhouse };
        return choices.FirstOrDefault(building => building is not null &&
            (requestedBuildingId is null || building.InstanceId == requestedBuildingId) && DestinationRoom(building.InstanceId) > 0 &&
            (fitsHaul is null || fitsHaul(building)));
    }

    private PlacedBuilding? FarmStorageForHaul(string householdId, string actor, FarmStockChoice choice,
        string? requestedBuildingId = null, int maximumQuantity = int.MaxValue)
    {
        var source = HouseholdStockPosition(choice.Carrier);
        var range = HouseholdStockInteractionRange(choice.Carrier);
        var position = agents[actor].Position;
        // Try the next permitted store when the preferred one cannot accept
        // this actual load. The planner retains whole-vessel and partial-grain rules.
        return FarmStorageFor(householdId, choice.Resource.ItemKind, requestedBuildingId, destination =>
            PlanFarmStockHaul(actor, destination.InstanceId, choice, maximumQuantity) is not null &&
            (IsWithinInteractionRange(position, source, range) ||
             Reach(actor, source, ReachPurpose.FarmStockPickup, range, 0, SwimmingPolicy.WhenCapable).Count > 0) &&
            ReachLeg(actor, source, destination.Position, ReachPurpose.FarmStockDelivery, 0, 0, SwimmingPolicy.WhenCapable).Count > 0);
    }

    /// <summary>
    /// Loose household farm stock, or a loose vessel holding it, whose load
    /// the actor can carry to farm storage. Stock already stored in a House
    /// or Farmhouse stays where it is. Stored Silo grain can supply the
    /// Farmhouse's actual milling deficit without undoing the flour haul.
    /// </summary>
    private FarmStockChoice? FarmGrainForDelivery(string householdId, string actor, string? itemKind = null,
        string? destinationId = null, int maximumQuantity = int.MaxValue)
    {
        var kinds = InventoryIndex.For(society.Checkpoint.Inventory).OwnedBy(householdId)
            .Select(lot => lot.ItemKind).Where(kind => FarmFieldRules.IsFarmStock(kind) &&
                (itemKind is null || itemKind == kind)).Distinct(StringComparer.Ordinal).ToArray();
        if (kinds.Length > 0)
        {
            var request = new GoodsRequest(GoodsUse.Holdings, actor, GoodsOwners.One(householdId), new GoodsKinds(kinds));
            foreach (var match in FindGoods(request).Matches.OrderBy(match => match.Root.GroundPosition is not null ? 0 : 1)
                         .ThenBy(match => match.Root.Id, StringComparer.Ordinal).ThenBy(match => match.Lot.Id, StringComparer.Ordinal))
            {
                if (match.Root.CarrierId is not null || match.Root.StorageBuildingId is not null) continue;
                var choice = new FarmStockChoice(match.Root, match.Lot);
                if (FarmStorageForHaul(householdId, actor, choice, destinationId, maximumQuantity) is not null)
                    return choice;
            }
        }
        return SiloGrainForFarmhouse(householdId, actor, itemKind, destinationId);
    }

    private FarmStockChoice? SiloGrainForFarmhouse(string householdId, string actor, string? itemKind, string? destinationId)
    {
        if (itemKind is not null && itemKind != FarmFieldRules.Grain ||
            FarmhouseForHousehold(householdId) is not { } farmhouse ||
            destinationId is not null && destinationId != farmhouse.InstanceId ||
            !agents.TryGetValue(actor, out var person))
            return null;
        var inventory = society.Checkpoint.Inventory;
        var missing = FarmhouseGrainDeficit(householdId, farmhouse);
        if (missing <= 0 || DestinationRoom(inventory, farmhouse.InstanceId) <= 0)
            return null;
        var silos = worldSimulation.Buildings.Where(building => building.HouseholdId == householdId && building.InstanceId != farmhouse.InstanceId &&
                worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                    definition.Tags.Contains("silo")))
            .Select(building => building.InstanceId).ToHashSet(StringComparer.Ordinal);
        var request = DeliveryGoodsRequest(actor, householdId, FarmFieldRules.Grain, farmhouse.InstanceId,
            GoodsDeliveryPurpose.FarmStock);
        foreach (var match in FindGoods(request).Matches.OrderBy(match => match.Root.Id, StringComparer.Ordinal)
                     .ThenBy(match => match.Lot.Id, StringComparer.Ordinal))
        {
            if (match.Root.CarrierId is not null || match.Root.StorageBuildingId is not { } storage || !silos.Contains(storage))
                continue;
            var choice = new FarmStockChoice(match.Root, match.Lot, missing);
            if (PlanFarmStockHaul(actor, farmhouse.InstanceId, choice) is not null) return choice;
        }
        return null;
    }

    private FarmStockChoice? FarmFlourForHouse(string householdId, string farmhouseId, string actor, string houseId,
        int maximumQuantity = int.MaxValue)
    {
        var request = DeliveryGoodsRequest(actor, householdId, "flour", houseId, GoodsDeliveryPurpose.FarmStock);
        foreach (var match in FindGoods(request).Matches.OrderBy(match => match.Root.Id, StringComparer.Ordinal)
                     .ThenBy(match => match.Lot.Id, StringComparer.Ordinal))
        {
            if (match.Root.StorageBuildingId != farmhouseId) continue;
            var choice = new FarmStockChoice(match.Root, match.Lot);
            if (PlanFarmStockHaul(actor, houseId, choice, maximumQuantity) is not null) return choice;
        }
        return null;
    }

    private FarmStockHaulPlan? PlanFarmStockHaul(string actor, string destinationId, FarmStockChoice choice,
        int maximumQuantity = int.MaxValue)
    {
        var inventory = society.Checkpoint.Inventory;
        var request = DeliveryGoodsRequest(actor, choice.Resource.OwnerId, choice.Resource.ItemKind, destinationId,
            GoodsDeliveryPurpose.FarmStock);
        if (RecheckGoods(request, choice.Resource.Id) is null) return null;
        var destination = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == destinationId);
        var capacity = destination is null ? 0 : Math.Min(HouseHaulLoadQuantity,
            Math.Min(PickupCarryCapacity(actor, choice.Carrier, destination.Position), DestinationRoom(inventory, destinationId)));
        if (capacity <= 0)
            return null;

        if (!InventoryContainerRules.IsContainer(choice.Carrier.ItemKind))
        {
            var quantity = Math.Min(maximumQuantity, Math.Min(capacity, Math.Min(AvailableLotQuantity(choice.Resource),
                choice.ResourceQuantityLimit ?? int.MaxValue)));
            return quantity > 0
                ? new FarmStockHaulPlan(choice.Carrier, choice.Resource, quantity, quantity, MoveContainerFamily: false)
                : null;
        }

        if (HasActiveContainerReservation(inventory, choice.Carrier.Id))
            return null;
        var familyQuantity = ContainerFamilyQuantity(inventory, choice.Carrier.Id);
        var resourceQuantity = DeliveryResourceQuantity(choice.Carrier, choice.Resource.ItemKind);
        if (familyQuantity <= capacity && resourceQuantity <= maximumQuantity && choice.ResourceQuantityLimit is null &&
            !UnusableDeliveryStock(inventory, choice.Carrier))
            return new FarmStockHaulPlan(choice.Carrier, choice.Resource, 1,
                resourceQuantity, MoveContainerFamily: true);

        // Only grain and flour have an approved partial-vessel haul. Other
        // oversized families stay intact at their current location.
        if (choice.Resource.ContainerLotId != choice.Carrier.Id || choice.Carrier.ConditionBasisPoints == 0 ||
            choice.Resource.ItemKind is not (FarmFieldRules.Grain or "flour"))
            return null;
        var takenQuantity = Math.Min(maximumQuantity, Math.Min(capacity, Math.Min(AvailableLotQuantity(choice.Resource),
            choice.ResourceQuantityLimit ?? int.MaxValue)));
        return takenQuantity > 0
            ? new FarmStockHaulPlan(choice.Carrier, choice.Resource, takenQuantity, takenQuantity,
                MoveContainerFamily: false)
            : null;
    }

    private void AddFarmGrainCandidate(List<CognitionCandidate> candidates, string actor)
    {
        if (FarmStockDefinition.Offer(this, actor, FarmStockActionKind.Grain) is { } plan)
            candidates.Add(new CognitionCandidate(FarmStockDefinition.Encode(FarmStockActionKind.Grain),
                "Carry household crops and seeds from their actual location to farm storage.", 24, plan.DestinationId)
            { ActionPlan = plan });
    }

    private void AddFarmFlourCandidate(List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        if (FarmStockDefinition.Offer(this, actor, FarmStockActionKind.Flour) is { } plan)
            candidates.Add(new CognitionCandidate(FarmStockDefinition.Encode(FarmStockActionKind.Flour),
                "Carry household flour from its Farmhouse to its House.", 25, plan.DestinationId)
            { ActionPlan = plan });
    }

    private void ApplyFarmStockHaul(string actor, string householdId, string destinationId,
        FarmStockHaulPlan plan, string operationId, string purpose)
    {
        RunInventoryCommand(InventorySystem.ApplyFarmStockHaul(plan, operationId, householdId, actor, purpose, destinationId));
        AppendEvent(WorldEvents.FarmStock(purpose, actor, plan.Resource.Id, plan.ResourceQuantity, destinationId));
    }

    public IReadOnlyList<FarmFieldState> Fields => fields.ToArray();

    public FarmWorkResult StartFieldWork(string workerId, GridPoint position, FarmWorkKind kind,
        string? crop = null, string? seedLotId = null)
    {
        gate.Wait();
        try { return this.farmingSystem.StartFieldWorkCore(this.farmingWorld, workerId, position, kind, crop, seedLotId, null); }
        finally { gate.Release(); }
    }
}
