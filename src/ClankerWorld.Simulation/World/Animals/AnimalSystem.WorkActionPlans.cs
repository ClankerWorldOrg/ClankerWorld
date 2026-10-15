using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    private AnimalWorkActionPlan? PlanAnimalWork(IAnimalWorld world, string actor, AnimalWorkSelection choice)
    {
        if (Animal(choice.AnimalId) is not { } animal) return null;
        var person = world.Agents[actor];
        var definition = AnimalRules.Definition(animal.Species);
        List<(InventoryLot Lot, int Quantity)>? inputs;
        if (choice.Kind == AnimalWorkKind.Saddle)
            inputs = AnimalSuppliesAtHand(world, actor, animal).FirstOrDefault(lot => lot.ItemKind == "saddle" &&
                lot.OwnerId == animal.HouseholdId && lot.ContainerLotId is null) is { } saddle ? [(saddle, 1)] : null;
        else inputs = AnimalCareInputs(world, actor, animal, choice.Kind == AnimalWorkKind.Tame ? 2 : definition.DailyFeed,
            choice.Kind == AnimalWorkKind.Tame ? 1 : definition.DailyWater);
        var preparation = inputs is null && choice.Kind != AnimalWorkKind.Tame
            ? AnimalSupplyChoices(world, actor).Where(item => item.AnimalId == animal.Id && item.Action == choice.Action)
                .Select(item => PlanAnimalSupply(world, actor, item)).FirstOrDefault(plan => CheckAnimalSupplyPlan(world, plan).Allowed) : null;
        if (inputs is null && preparation is null && (choice.Kind == AnimalWorkKind.Care || person.Position == animal.Position)) return null;
        var household = choice.Kind == AnimalWorkKind.Tame ? world.HouseholdFor(actor) : null;
        var yard = household is not null ? AnimalYard(world, household) : null;
        if (choice.Kind == AnimalWorkKind.Tame && yard is null) return null;
        var index = InventoryIndex.For(world.Society.Inventory);
        var selected = Array.AsReadOnly((inputs ?? []).Select(input =>
        {
            var root = index.Root(input.Lot);
            var carried = PersonalEquipmentRules.IsPhysicallyCarried(world.Society.Inventory, input.Lot, actor);
            return new PlannedAnimalInput(input.Lot.Id, input.Lot.OwnerId, input.Lot.ItemKind, input.Quantity,
                root.Id, carried, carried ? null : world.HouseholdStockPosition(root));
        }).ToArray());
        var dismount = preparation is null && choice.Kind is AnimalWorkKind.Care or AnimalWorkKind.Saddle
            ? State.Animals.FirstOrDefault(other => other.Id != animal.Id && other.RiderId == actor)?.Id : null;
        return new(actor, choice, animal.HouseholdId, animal.YardId, animal.Position, selected, preparation, dismount,
            inputs is null && preparation is null, household, yard?.InstanceId, yard?.Position, animal.TamingWork,
            animal.TamingWork?.ActorId == actor ? animal.TamingWork.WorkTicks + 1 : 1, AnimalDayTicks(world),
            world.Reach(actor, animal.Position, ReachPurpose.AnimalWork, 0, 0, SwimmingPolicy.WhenCapable));
    }

    internal ActionCheck CheckAnimalWorkPlan(IAnimalWorld world, AnimalWorkActionPlan plan)
    {
        var capabilities = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capabilities.Allowed) return capabilities;
        var animal = Animal(plan.Choice.AnimalId);
        if (animal is null || animal.DiedTick is not null || animal.Position != plan.Position || animal.HouseholdId != plan.AnimalHouseholdId || animal.YardId != plan.AnimalYardId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal has changed");
        if (plan.Choice.Kind == AnimalWorkKind.Care && (!MayCareForAnimal(world, plan.Actor, animal) || AnimalRules.HasCare(animal, world.WorldTick)) ||
            plan.Choice.Kind == AnimalWorkKind.Saddle && (!AnimalHouseholdMember(world, plan.Actor, animal) || animal.Species != "horse" || animal.SaddleLotId is not null))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal no longer needs this work from them");
        if (plan.Choice.Kind == AnimalWorkKind.Tame)
        {
            if (!MayApproachWildAnimal(world, plan.Actor, animal))
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected wild animal is too far away now");
            var yard = world.Simulation.Buildings.FirstOrDefault(item => item.InstanceId == plan.ReceivingYardId);
            if (animal.HouseholdId is not null || animal.LeaderId is not null || !MayLeadAnimal(world, plan.Actor, animal) ||
                world.HouseholdFor(plan.Actor) != plan.ReceivingHouseholdId || yard is null || yard.HouseholdId != plan.ReceivingHouseholdId ||
                yard.Position != plan.ReceivingYardPosition || !HasAnimalTransferSpace(world, plan.ReceivingHouseholdId!, yard, animal) ||
                animal.TamingWork != plan.InitialTamingWork || animal.TamingWork is { } work && work.ActorId != plan.Actor)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal or taming place is no longer available");
        }
        if (plan.SupplyPreparation is { } preparation) return CheckAnimalSupplyPlan(world, preparation);
        if (plan.Choice.Kind is AnimalWorkKind.Care or AnimalWorkKind.Saddle &&
            State.Animals.FirstOrDefault(other => other.Id != animal.Id && other.RiderId == plan.Actor)?.Id != plan.DismountAnimalId)
            return ActionCheck.Refuse(ActionRefusalReason.Mounted, "the selected mount has changed");
        var index = InventoryIndex.For(world.Society.Inventory);
        foreach (var input in plan.Inputs)
        {
            if (world.RecheckGoods(new(GoodsUse.ConsumeAt, plan.Actor, GoodsOwners.One(input.OwnerId), GoodsKinds.One(input.ItemKind)), input.StockId) is not { } selected ||
                selected.Root.Id != input.RootId || !AnimalSupplyIsAtHand(world, plan.Actor, animal, selected.Lot) ||
                PersonalEquipmentRules.IsPhysicallyCarried(world.Society.Inventory, selected.Lot, plan.Actor) != input.Carried ||
                !input.Carried && (world.HouseholdStockPosition(selected.Root) != input.Position || world.Agents[plan.Actor].Position != plan.Position) ||
                !InventoryRules.CanReserve(index, selected.Lot, input.OwnerId, input.Quantity, InventoryHoldRules.SupplyTripFor(plan.Actor)) ||
                AnimalRules.IsFeed(input.ItemKind) && !AnimalFeedMayBeSpent(world, plan.Actor, animal, selected.Lot, input.Quantity))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "a selected animal-work input is no longer available");
        }
        if (plan.Inputs.Where(input => WorldRuntime.AnimalSharedRules.IsEdibleFood(input.ItemKind))
            .GroupBy(input => input.OwnerId == animal.HouseholdId ? animal.HouseholdId : world.HouseholdFor(plan.Actor))
            .Any(group => !AnimalFoodReserveRemaining(world, plan.Actor, group.Sum(input => input.Quantity), group.Key)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected feed is needed for household meals");
        if (plan.ApproachOnly && world.Agents[plan.Actor].Position == plan.Position)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the planned approach still needs work inputs");
        return plan.DismountAnimalId is not null || animal.LeaderId == plan.Actor || world.Agents[plan.Actor].Position == plan.Position ||
            world.Reach(plan.Actor, plan.Position, ReachPurpose.AnimalWork, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected animal is out of reach now");
    }

    private void ApplyAnimalWorkPlan(IAnimalWorld world, AnimalWorkActionPlan plan)
    {
        if (CheckAnimalWorkPlan(world, plan).Refusal is { } refusal) { world.RefuseChoice(plan, refusal); return; }
        if (plan.SupplyPreparation is { } preparation) { ApplyAnimalSupplyPlan(world, preparation); return; }
        if (plan.DismountAnimalId is { } mount) { EndAnimalRide(world, Animal(mount)!, "dismounted"); return; }
        var animal = Animal(plan.Choice.AnimalId)!;
        if (world.Agents[plan.Actor].Position != plan.Position && animal.LeaderId != plan.Actor)
        { world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Position, "animal_" + plan.Choice.Action); return; }
        var inputs = plan.Inputs.Select(input => (world.Society.Inventory.GetLot(input.StockId), input.Quantity)).ToList();
        switch (plan.Choice.Kind)
        {
            case AnimalWorkKind.Care: CompleteAnimalCare(world, plan.Actor, animal, inputs, plan.CareTicks); break;
            case AnimalWorkKind.Tame: CompleteAnimalTaming(world, plan, animal, inputs); break;
            case AnimalWorkKind.Saddle: CompleteAnimalSaddling(world, plan.Actor, animal, inputs[0].Item1); break;
        }
    }
}
