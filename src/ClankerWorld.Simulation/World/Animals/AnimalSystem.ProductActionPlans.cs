using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

using static ClankerWorld.Simulation.World.WorldRuntime.AnimalSharedRules;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    internal void AddMilkCandidate(IAnimalWorld world, List<CognitionCandidate> candidates, string actor)
    {
        if (PlanAnimalProduct(world, actor, new(AnimalProductKind.Milk)) is { } plan && CheckAnimalProductPlan(world, plan).Allowed)
            candidates.Add(new(plan.CandidateId, "Drink one portion of your carried milk.", 0) { ActionPlan = plan });
    }

    private AnimalProductActionPlan? PlanAnimalProduct(IAnimalWorld world, string actor, AnimalProductChoice choice)
    {
        var index = InventoryIndex.For(world.Society.Inventory);
        if (choice.Kind == AnimalProductKind.Milk)
            return CarriedMilk(world, actor) is { } milk ? new(actor, choice, milk.Id, milk.OwnerId, milk.ItemKind,
                index.Root(milk).Id, 1, world.Agents[actor].Position, null, null, Array.AsReadOnly<GridPoint>([])) : null;
        if (choice.AnimalId is not { } animalId || Animal(animalId) is not { } animal) return null;
        var lot = choice.Kind == AnimalProductKind.Hide ? WildAnimalHide(world, animal) :
            animal.ReadyProductLotId is { } product && animal.ReadyProductReservationId is { } reservation
                ? world.RecheckGoods(new(GoodsUse.AnimalProduct, actor, GoodsOwners.One(animal.HouseholdId!),
                    GoodsKinds.One(AnimalRules.Definition(animal.Species).Product!), ReservationId: reservation), product)?.Lot : null;
        if (lot is null) return null;
        var dismount = choice.Kind == AnimalProductKind.Product
            ? State.Animals.FirstOrDefault(other => other.RiderId == actor && other.Id != animalId)?.Id : null;
        return new(actor, choice, lot.Id, lot.OwnerId, lot.ItemKind, index.Root(lot).Id, lot.Quantity,
            animal.Position, animal.ReadyProductReservationId, dismount,
            world.Reach(actor, animal.Position, ReachPurpose.AnimalProduct, 0, 0, SwimmingPolicy.WhenCapable));
    }

    internal ActionCheck CheckAnimalProductPlan(IAnimalWorld world, AnimalProductActionPlan plan)
    {
        var milk = plan.Choice.Kind == AnimalProductKind.Milk;
        var capabilities = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(milk ? ActionLifeStages.NonInfants : ActionLifeStages.Adults, AllowBoat: milk,
                AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capabilities.Allowed) return capabilities;
        var index = InventoryIndex.For(world.Society.Inventory);
        if (milk)
        {
            var owners = new GoodsOwners(world.HouseholdFor(plan.Actor) is { } home ? [plan.Actor, home] : [plan.Actor]);
            return owners.Ids.Contains(plan.OwnerId) && world.RecheckGoods(new(GoodsUse.Eat, plan.Actor,
                GoodsOwners.One(plan.OwnerId), GoodsKinds.One("milk")), plan.StockId) is { } stock && stock.Root.Id == plan.RootId &&
                InventoryRules.CanReserve(index, stock.Lot, plan.OwnerId, plan.Quantity, InventoryHoldRules.SupplyTripFor(plan.Actor))
                    ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected carried milk is no longer available");
        }
        var animal = Animal(plan.Choice.AnimalId!);
        if (animal is null || animal.Position != plan.Position ||
            (plan.Choice.Kind == AnimalProductKind.Product ? !MayCareForAnimal(world, plan.Actor, animal) : animal.DiedTick is null || animal.HouseholdId is not null))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal is no longer available to them");
        var request = new GoodsRequest(plan.Choice.Kind == AnimalProductKind.Product ? GoodsUse.AnimalProduct : GoodsUse.AnimalHide,
            plan.Actor, GoodsOwners.One(plan.OwnerId), GoodsKinds.One(plan.ItemKind), ReservationId: plan.ReservationId);
        if (world.RecheckGoods(request, plan.StockId) is not { } selected || selected.Root.Id != plan.RootId || selected.Lot.Quantity != plan.Quantity ||
            selected.Lot.GroundPosition != new InventoryGroundPosition(plan.Position.X, plan.Position.Y))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected animal stock is no longer available");
        if (plan.Choice.Kind == AnimalProductKind.Product && (animal.ReadyProductLotId != plan.StockId || animal.ReadyProductReservationId != plan.ReservationId ||
            animal.HouseholdId != plan.OwnerId || !index.ActiveReservationsFor(plan.StockId).Any(claim => claim.Id == plan.ReservationId && claim.Purpose == "animal-product:" + animal.Id)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected animal product is no longer ready");
        if (plan.Choice.Kind == AnimalProductKind.Hide && (plan.StockId != AnimalHideId(animal.Id) || plan.OwnerId != animal.Id || plan.Quantity != 1))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected hide is no longer available");
        if (world.FreeCarryCapacity(plan.Actor) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected animal stock");
        if (plan.Choice.Kind == AnimalProductKind.Product &&
            State.Animals.FirstOrDefault(other => other.Id != animal.Id && other.RiderId == plan.Actor)?.Id != plan.DismountAnimalId)
            return ActionCheck.Refuse(ActionRefusalReason.Mounted, "the selected mount has changed");
        return plan.DismountAnimalId is not null || animal.LeaderId == plan.Actor || world.Agents[plan.Actor].Position == plan.Position ||
            world.Reach(plan.Actor, plan.Position, ReachPurpose.AnimalProduct, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected animal stock is out of reach now");
    }

    private void ApplyAnimalProductPlan(IAnimalWorld world, AnimalProductActionPlan plan)
    {
        if (CheckAnimalProductPlan(world, plan).Refusal is { } refusal) { world.RefuseChoice(plan, refusal); return; }
        var stock = world.Society.Inventory.GetLot(plan.StockId);
        if (plan.Choice.Kind == AnimalProductKind.Milk)
        {
            ConsumeAnimalInputs(world, plan.Actor, "drink_milk", [(stock, plan.Quantity)]);
            var person = world.Agents[plan.Actor];
            world.SetAgent(plan.Actor, person with { FullnessBasisPoints = Math.Min(10_000, person.FullnessBasisPoints + 3_000) });
            world.AppendEvent("milk_drunk", plan.Actor);
            return;
        }
        var animal = Animal(plan.Choice.AnimalId!)!;
        if (plan.DismountAnimalId is { } mount) { EndAnimalRide(world, Animal(mount)!, "dismounted"); return; }
        if (world.Agents[plan.Actor].Position != plan.Position && animal.LeaderId != plan.Actor)
        {
            world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Position,
                plan.Choice.Kind == AnimalProductKind.Product ? "animal_collect" : "animal_collect_hide");
            return;
        }
        if (plan.Choice.Kind == AnimalProductKind.Product) CompleteAnimalProductCollection(world, plan.Actor, animal, stock);
        else CompleteAnimalHideCollection(world, plan.Actor, animal, stock);
    }
}
