using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
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
        var position = inhabitants[actor].Position;
        // Try the next permitted store when the preferred one cannot accept
        // this actual load. The planner retains whole-vessel and partial-grain rules.
        return FarmStorageFor(householdId, choice.Resource.ItemKind, requestedBuildingId, destination =>
            PlanFarmStockHaul(actor, destination.InstanceId, choice, maximumQuantity) is not null &&
            (IsWithinInteractionRange(position, source, range) ||
             FindUnoccupiedRoute(actor, position, source, range).Count > 0) &&
            FindUnoccupiedRoute(actor, source, destination.Position, 0).Count > 0);
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
            !inhabitants.TryGetValue(actor, out var person))
            return null;
        var inputTarget = worldContent.Recipes.Where(recipe =>
                recipe.WorkstationBuildingId == farmhouse.DefinitionId && !recipe.IsCrop &&
                NeedsRecipeOutput(recipe, householdId))
            .SelectMany(recipe => recipe.Inputs).Where(input => input.ResourceId == FarmFieldRules.Grain)
            .Select(input => input.Amount).DefaultIfEmpty(0).Max() * SupplyBatches;
        var inventory = society.Checkpoint.Inventory;
        var supplied = inventory.Lots.Where(lot => lot.OwnerId == householdId &&
                lot.StorageBuildingId == farmhouse.InstanceId && lot.ItemKind == FarmFieldRules.Grain)
            .Sum(AvailableLotQuantity);
        var incoming = inventory.Lots.Where(lot => lot.DeliveryBuildingId == farmhouse.InstanceId &&
                lot.ItemKind == FarmFieldRules.Grain).Sum(AvailableLotQuantity);
        var missing = inputTarget - supplied - incoming;
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
        var householdId = society.Checkpoint.GetInhabitant(actor).HouseholdId;
        if (!AdultResident(actor) || householdId is null || CarriedHouseDelivery(actor) is not null ||
            FarmhouseForHousehold(householdId) is null ||
            FarmGrainForDelivery(householdId, actor) is not { } grain ||
            FarmStorageForHaul(householdId, actor, grain) is not { } farmhouse)
            return;
        candidates.Add(new CognitionCandidate("haul_farm_grain",
            "Carry household crops and seeds from their actual location to farm storage.", 24, farmhouse.InstanceId));
    }

    private void HaulFarmGrain(string actor, PlaytestInhabitantState state)
    {
        var householdId = society.Checkpoint.GetInhabitant(actor).HouseholdId;
        if (!AdultResident(actor) || householdId is null || CarriedHouseDelivery(actor) is not null ||
            FarmhouseForHousehold(householdId) is null ||
            FarmGrainForDelivery(householdId, actor) is not { } grain ||
            FarmStorageForHaul(householdId, actor, grain) is not { } farmhouse)
            return;
        var source = HouseholdStockPosition(grain.Carrier);
        var range = HouseholdStockInteractionRange(grain.Carrier);
        if (!IsWithinInteractionRange(state.Position, source, range))
        {
            MoveToward(actor, state, source, "farm_grain", range);
            return;
        }
        if (PlanFarmStockHaul(actor, farmhouse.InstanceId, grain) is not { } plan)
            return;
        ApplyFarmStockHaul(actor, householdId, farmhouse.InstanceId, plan,
            $"farm-grain-pickup:{WorldTick}:{actor}", "farm_grain_picked_up");
    }

    private void AddFarmFlourCandidate(List<CognitionCandidate> candidates, string actor,
        PlaytestInhabitantState state)
    {
        var householdId = society.Checkpoint.GetInhabitant(actor).HouseholdId;
        if (!AdultResident(actor) || householdId is null || CarriedHouseDelivery(actor) is not null ||
            FarmhouseForHousehold(householdId) is not { } farmhouse ||
            HouseForHousehold(householdId) is not { } house ||
            FarmFlourForHouse(householdId, farmhouse.InstanceId, actor, house.InstanceId) is not { } flour ||
            PlanFarmStockHaul(actor, house.InstanceId, flour) is null)
            return;
        if ((!IsWithinInteractionRange(state.Position, farmhouse.Position, 0) &&
             FindUnoccupiedRoute(actor, state.Position, farmhouse.Position, 0).Count == 0) ||
            FindUnoccupiedRoute(actor, farmhouse.Position, house.Position, 0).Count == 0)
            return;
        candidates.Add(new CognitionCandidate("haul_farm_flour",
            "Carry household flour from its Farmhouse to its House.", 25, house.InstanceId));
    }

    private void HaulFarmFlour(string actor, PlaytestInhabitantState state)
    {
        var householdId = society.Checkpoint.GetInhabitant(actor).HouseholdId;
        if (!AdultResident(actor) || householdId is null || CarriedHouseDelivery(actor) is not null ||
            FarmhouseForHousehold(householdId) is not { } farmhouse ||
            HouseForHousehold(householdId) is not { } house ||
            FarmFlourForHouse(householdId, farmhouse.InstanceId, actor, house.InstanceId) is not { } flour)
            return;
        if (state.Position != farmhouse.Position)
        {
            MoveToward(actor, state, farmhouse.Position, "farm_flour", 0);
            return;
        }
        if (PlanFarmStockHaul(actor, house.InstanceId, flour) is not { } plan)
            return;
        ApplyFarmStockHaul(actor, householdId, house.InstanceId, plan,
            $"farm-flour-pickup:{WorldTick}:{actor}", "farm_flour_picked_up");
    }

    private void ApplyFarmStockHaul(string actor, string householdId, string destinationId,
        FarmStockHaulPlan plan, string operationId, string purpose)
    {
        ApplyInventoryTransition(inventory => plan.MoveContainerFamily
            ? InventoryFixture.Transfer(inventory, operationId, householdId, actor,
                plan.Carrier.Id, plan.TransferQuantity, purpose, destinationDeliveryBuildingId: destinationId)
            : plan.Resource.ContainerLotId == plan.Carrier.Id
                ? InventoryFixture.TakeFromContainer(inventory, operationId, householdId, actor,
                    plan.Carrier.Id, plan.Resource.Id, plan.TransferQuantity,
                    destinationDeliveryBuildingId: destinationId)
                : InventoryFixture.Transfer(inventory, operationId, householdId, actor,
                    plan.Resource.Id, plan.TransferQuantity, purpose, destinationDeliveryBuildingId: destinationId));
        AppendEvent(purpose, $"{actor}:{plan.Resource.Id}:{plan.ResourceQuantity}:{destinationId}");
    }
}
