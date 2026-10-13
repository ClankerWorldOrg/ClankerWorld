using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>Returns empty reusable household vessels from held workplaces to the House.</summary>
public sealed partial class WorldRuntime
{
    private const string ReturnEmptyVesselPrefix = "return_empty_vessel:";

    private InventoryLot? EmptyVesselToReturn(string actor, AgentState person,
        string? selectedLotId = null)
    {
        if (!IsAdult(actor) || CarriedHouseDelivery(actor) is not null || FreeCarryCapacity(actor) < 1 ||
            society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseForHousehold(householdId) is not { } house ||
            DestinationRoom(house.InstanceId) < 1)
            return null;

        var inventory = society.Checkpoint.Inventory;
        foreach (var vessel in FindGoods(new(GoodsUse.VesselFor, actor, GoodsOwners.One(householdId),
                     new GoodsKinds([InventoryContainerRules.StoragePot, InventoryContainerRules.WaterJug, InventoryContainerRules.Handcart])))
                     .Matches.Select(match => match.Lot).Where(lot =>
                     (selectedLotId is null || lot.Id == selectedLotId) &&
                     lot.GroundPosition is null &&
                     lot.StorageBuildingId is not null && lot.StorageBuildingId != house.InstanceId &&
                     ContainerContentsQuantity(inventory, lot.Id) == 0)
                     .OrderBy(lot => lot.Id, StringComparer.Ordinal))
        {
            // A household knows its own held workplace; foreign or unclaimed buildings grant no access.
            var source = worldSimulation.Buildings.SingleOrDefault(building =>
                building.InstanceId == vessel.StorageBuildingId && building.HouseholdId == householdId &&
                worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                    HouseholdBuildingKind(definition) is not null));
            if (source is not null &&
                (person.Position == source.Position || FindUnoccupiedRoute(actor, person.Position, source.Position, 0).Count > 0) &&
                PickupCarryCapacity(actor, vessel, house.Position) >= ContainerFamilyQuantity(inventory, vessel.Id))
                return vessel;
        }
        return null;
    }

    private void AddEmptyVesselReturnCandidate(List<CognitionCandidate> candidates, string actor,
        AgentState person)
    {
        if (EmptyVesselToReturn(actor, person) is not { } vessel) return;
        candidates.Add(new(ReturnEmptyVesselPrefix + vessel.Id,
            "Bring an empty household vessel back to the House for reuse.", 22, vessel.StorageBuildingId));
    }

    private void ReturnEmptyVessel(string actor, AgentState person, string lotId)
    {
        if (EmptyVesselToReturn(actor, person, lotId) is not { } vessel) return;
        var source = worldSimulation.Buildings.Single(building => building.InstanceId == vessel.StorageBuildingId);
        if (person.Position != source.Position)
        {
            MoveToward(actor, person, source.Position, "return_empty_vessel", 0);
            return;
        }
        var householdId = society.Checkpoint.GetAgent(actor).HouseholdId!;
        var house = HouseForHousehold(householdId)!;
        ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
            $"empty-vessel-pickup:{WorldTick}:{actor}", householdId, actor, vessel.Id, 1,
            "empty_vessel_picked_up", destinationDeliveryBuildingId: house.InstanceId, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        AppendEvent("empty_vessel_picked_up", $"{actor}:{vessel.Id}:{source.InstanceId}:{house.InstanceId}");
    }
}
