using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static int DeliveryResourceQuantity(IDeliveryOrderSelectionWorld world, InventoryLot carrier, string kind) =>
        (carrier.ItemKind == kind ? carrier.Quantity : 0) +
        (InventoryContainerRules.IsContainer(carrier.ItemKind)
            ? world.Society.Inventory.Lots.Where(lot => lot.ContainerLotId == carrier.Id && lot.ItemKind == kind)
                .Sum(lot => lot.Quantity) : 0);

    internal static bool DeliveryDestinationMatches(IDeliveryOrderSelectionWorld world, OrderTask order, PlacedBuilding building) =>
        world.Content.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
            definition.Tags.Contains(order.TargetBuildingKind!, StringComparer.Ordinal)) &&
        (order.TargetPosition is null || building.Position == order.TargetPosition) &&
        (order.TargetStorageBuildingId is null || building.InstanceId == order.TargetStorageBuildingId &&
            building.Position == order.TargetStoragePosition &&
            (order.DeliveryPurpose == "town_surplus" ? building.TownId : building.HouseholdId) == order.TargetStorageOwnerId);

    internal static DeliveryOrderPlan? DeliveryPlanFor(IDeliveryOrderSelectionWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (!world.IsAdult(actor) || !world.ReadyForBriefInteraction(actor) || world.DeliveryOrderRemaining(order) <= 0) return null;
        var boundLot = order.DeliveryLotId is { } boundId
            ? world.Society.Inventory.Lots.SingleOrDefault(lot => lot.Id == boundId) : null;
        if (order.DeliveryLotId is not null && (boundLot is null || boundLot.OwnerId != actor ||
            !PersonalEquipmentRules.IsCarried(boundLot, actor))) return null;
        if (boundLot?.DeliveryBuildingId is not null)
            return world.CarriedDeliveryOrderPlan(actor, order) is { } boundShipment &&
                world.CanReachLeg(actor, person.Position, boundShipment.Destination.Position, ReachPurpose.GoodsDelivery, 0, 0, SwimmingPolicy.WhenCapable) ? boundShipment : null;
        var otherPromise = order.DeliveryLotId is null && world.CarriedHouseDelivery(actor) is not null;
        if (otherPromise && world.CarriedDeliveryOrderPlan(actor, order) is { } carried)
            return world.CanReachLeg(actor, person.Position, carried.Destination.Position, ReachPurpose.GoodsDelivery, 0, 0, SwimmingPolicy.WhenCapable) ? carried : null;
        var maximum = Math.Min(world.DeliveryOrderRemaining(order), order.DeliveryQuantity ?? int.MaxValue);
        var plan = order.DeliveryPurpose switch
        {
            "household_stock" or "household_food" => world.HouseholdDeliveryOrderPlan(actor, person, order, maximum),
            "workstation_input" => world.GetBlacksmithOrderPlan(actor, person, order, maximum) ??
                world.GetWorkstationOrderPlan(actor, person, order, maximum),
            "town_surplus" => world.GetTownOrderPlan(actor, person, order, maximum),
            "store_stock" => world.GetStoreOrderPlan(actor, person, order, maximum),
            _ => null,
        };
        if (plan is null || otherPromise && !plan.DirectDelivery ||
            order.DeliveryRoute is not null && order.DeliveryRoute != plan.Route ||
            order.DeliveryLotId is not null && (plan.Carrier.Id != order.DeliveryLotId ||
                plan.ResourceQuantity != order.DeliveryQuantity)) return null;
        var source = plan.DirectDelivery ? person.Position : world.HouseholdStockPosition(plan.Carrier);
        var range = plan.DirectDelivery ? 0 : world.HouseholdStockInteractionRange(plan.Carrier);
        return world.CanReachLeg(actor, person.Position, source, ReachPurpose.GoodsDelivery, range, 0, SwimmingPolicy.WhenCapable) && world.CanReachLeg(actor, source, plan.Destination.Position, ReachPurpose.GoodsDelivery, 0, 0, SwimmingPolicy.WhenCapable)
            ? plan : null;
    }

    internal static (PlacedBuilding Building, string Route, MapResource Source)? DeliveryGatherFor(IDeliveryOrderSelectionWorld world,
        OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task!;
        var actor = instruction.TargetAgentId;
        if (order.DeliveryPurpose != "workstation_input" || order.DeliveryLotId is not null ||
            world.CarriedHouseDelivery(actor) is not null || !world.IsAdult(actor) || !world.ReadyForBriefInteraction(actor)) return null;
        if (order.DeliveryRoute is null or "blacksmith_input" && world.HouseholdFor(actor) is { } householdId &&
            world.BlacksmithForHousehold(householdId) is { } smith && DeliveryDestinationMatches(world, order, smith) &&
            world.BlacksmithInputToGather(householdId, smith.InstanceId, actor, order.TargetItemKind) is { } missing &&
            world.FreeCarryCapacity(actor) >= world.ProjectMaterialCarryUnits(actor, missing.ItemKind, missing.Source) &&
            world.CanReachLeg(actor, person.Position, missing.Source.Position, ReachPurpose.GoodsDelivery, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable))
            return (smith, "blacksmith_input", missing.Source);
        if (order.DeliveryRoute is null or "blacksmith_input" && order.TargetItemKind == "iron_ore" &&
            world.HouseholdFor(actor) is { } oreHousehold && world.BlacksmithForHousehold(oreHousehold) is { } oreSmith &&
            DeliveryDestinationMatches(world, order, oreSmith) &&
            world.DestinationRoom(oreSmith.InstanceId) > 0 &&
            world.BlacksmithOreStocked(oreHousehold, oreSmith.InstanceId) < world.BlacksmithInputTarget(oreSmith.InstanceId, "iron_ore") &&
            world.PersonalSmithOre(actor) is null && !world.BlacksmithHasDeliverableInput(oreHousehold, oreSmith, actor, "iron_ore") &&
            world.MaterialSource("iron_ore", actor, oreSmith.Position) is { } oreSource &&
            world.FreeCarryCapacity(actor) >= world.ProjectMaterialCarryUnits(actor, "iron_ore", oreSource) &&
            world.CanReachLeg(actor, person.Position, oreSource.Position, ReachPurpose.GoodsDelivery, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable))
            return (oreSmith, "blacksmith_input", oreSource);
        if (order.DeliveryRoute is not (null or "workstation_input")) return null;
        var need = world.WorkstationSupplyNeeds(actor).FirstOrDefault(need => need.ItemKind == order.TargetItemKind &&
            DeliveryDestinationMatches(world, order, need.Building) && need.Source is not null &&
            world.CanReachLeg(actor, person.Position, need.Source.Position, ReachPurpose.GoodsDelivery, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
        return need?.Source is { } source ? (need.Building, "workstation_input", source) : null;
    }
}
