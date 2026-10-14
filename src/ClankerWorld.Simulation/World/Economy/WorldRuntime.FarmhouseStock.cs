using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed record FarmStockChoice(InventoryLot Carrier, InventoryLot Resource,
        int? ResourceQuantityLimit = null);

    private sealed record FarmStockHaulPlan(
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

    private void HaulFarmGrain(string actor, AgentState state) => ApplyFarmStockAction(PlanFarmStockAction(actor, FarmStockActionKind.Grain));

    private void AddFarmFlourCandidate(List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        if (FarmStockDefinition.Offer(this, actor, FarmStockActionKind.Flour) is { } plan)
            candidates.Add(new CognitionCandidate(FarmStockDefinition.Encode(FarmStockActionKind.Flour),
                "Carry household flour from its Farmhouse to its House.", 25, plan.DestinationId)
            { ActionPlan = plan });
    }

    private void HaulFarmFlour(string actor, AgentState state) => ApplyFarmStockAction(PlanFarmStockAction(actor, FarmStockActionKind.Flour));

    private void ApplyFarmStockHaul(string actor, string householdId, string destinationId,
        FarmStockHaulPlan plan, string operationId, string purpose)
    {
        ApplyInventoryTransition(inventory => plan.MoveContainerFamily
            ? InventoryAuthority.Transfer(inventory, operationId, householdId, actor,
                plan.Carrier.Id, plan.TransferQuantity, purpose, destinationDeliveryBuildingId: destinationId, holdAccess: InventoryHoldRules.BorrowedBy(actor))
            : plan.Resource.ContainerLotId == plan.Carrier.Id
                ? InventoryAuthority.TakeFromContainer(inventory, operationId, householdId, actor,
                    plan.Carrier.Id, plan.Resource.Id, plan.TransferQuantity,
                    destinationDeliveryBuildingId: destinationId, holdAccess: InventoryHoldRules.BorrowedBy(actor))
                : InventoryAuthority.Transfer(inventory, operationId, householdId, actor,
                    plan.Resource.Id, plan.TransferQuantity, purpose, destinationDeliveryBuildingId: destinationId, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        AppendEvent(purpose, $"{actor}:{plan.Resource.Id}:{plan.ResourceQuantity}:{destinationId}");
    }
}
