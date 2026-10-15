using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static HandcartBuildMaterialActionPlan? PlanHandcartBuildMaterial(IHandcartWorld world, string actor, HandcartBuildMaterialChoice choice,
        RecipeDefinition? selected = null)
    {
        var recipe = selected ?? HandcartToBuild(world, actor);
        if (recipe is null || world.HouseholdFor(actor) is not { } home || world.HouseholdBuildingWithTag(home, "blacksmith") is not { } smith)
            return null;
        var input = recipe.Inputs.FirstOrDefault(item => item.ResourceId == choice.ItemKind);
        if (input.Amount <= 0 || world.SharedItem(choice.ItemKind, actor, smith.Position) is not { } stock ||
            world.RecheckGoods(world.SharedCollectionRequest(actor, stock.OwnerId, choice.ItemKind), stock.Id) is not { } match) return null;
        var pickup = new PlannedEquipmentPickup(stock.Id, stock.OwnerId, stock.ItemKind,
            world.HouseholdStockPosition(stock), world.HouseholdStockInteractionRange(stock));
        var quantity = Math.Min(input.Amount - CarriedMaterialQuantity(world, actor, input.ResourceId),
            Math.Min(match.Quantity, world.PickupCarryCapacity(actor, stock, smith.Position)));
        return new(actor, choice, home, recipe.CanonicalId, input.Amount, smith.InstanceId, smith.Position, pickup,
            GoodsPlanFamily.Capture(world.Inventory, stock), quantity,
            world.Reach(actor, pickup.Position, ReachPurpose.HandcartBuildMaterial, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    internal static ActionCheck CheckHandcartBuildMaterialPlan(IHandcartWorld world, HandcartBuildMaterialActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.TransportCapabilities);
        if (!capability.Allowed) return capability;
        var recipe = world.Recipes.FirstOrDefault(item => item.CanonicalId == plan.RecipeId);
        var smith = world.Buildings.FirstOrDefault(item => item.InstanceId == plan.SmithId);
        if (world.HouseholdFor(plan.Actor) != plan.HouseholdId || recipe is null || smith is null || smith.HouseholdId != plan.HouseholdId ||
            smith.Position != plan.Destination || smith.DefinitionId != recipe.WorkstationBuildingId ||
            recipe.Inputs.FirstOrDefault(input => input.ResourceId == plan.Choice.ItemKind).Amount != plan.InputAmount ||
            !MayBuildHandcart(world, plan.Actor, plan.HouseholdId, recipe))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected handcart recipe or building plan is no longer available");
        if (plan.Quantity <= 0 || plan.InputAmount - CarriedMaterialQuantity(world, plan.Actor, plan.Choice.ItemKind) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they no longer need the planned material quantity");
        var pickup = plan.Pickup;
        if (world.RecheckGoods(world.SharedCollectionRequest(plan.Actor, pickup.OwnerId, pickup.ItemKind), pickup.StockId) is not { } match ||
            match.Quantity < plan.Quantity || !GoodsPlanFamily.Matches(world.Inventory, match.Lot, plan.Family) ||
            world.HouseholdStockPosition(match.Lot) != pickup.Position || world.HouseholdStockInteractionRange(match.Lot) != pickup.InteractionRange)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected building material is no longer available");
        if (world.PickupCarryCapacity(plan.Actor, match.Lot, plan.Destination) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "they cannot carry the planned materials back to the Blacksmith");
        return world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, pickup.Position, pickup.InteractionRange) ||
            world.Reach(plan.Actor, pickup.Position, ReachPurpose.HandcartBuildMaterial, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected building material is out of reach now");
    }

    internal static void ApplyCheckedHandcartBuildMaterial(IHandcartWorld world, HandcartBuildMaterialActionPlan plan)
    {
        var pickup = plan.Pickup;
        if (!world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, pickup.Position, pickup.InteractionRange))
        { world.MoveToward(plan.Actor, world.Agents[plan.Actor], pickup.Position, "handcart_material", pickup.InteractionRange); return; }
        CompleteHandcartBuildMaterial(world, plan);
    }
}
