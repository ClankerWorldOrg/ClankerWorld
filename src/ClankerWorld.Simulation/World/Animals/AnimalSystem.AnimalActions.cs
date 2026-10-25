using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

using static ClankerWorld.Simulation.World.WorldRuntime.AnimalSharedRules;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    internal sealed record AnimalChoice(string Action, string AnimalId, string? OtherId = null)
    {
        public string Id => AnimalActionId(Action, AnimalId, OtherId);
    }
    internal IEnumerable<AnimalChoice> AnimalChoices(IAnimalWorld world, string actor)
    {
        if (!world.IsAdult(actor)) yield break;
        var person = world.Agents[actor];
        foreach (var animal in State.Animals.Where(item => item.DiedTick is not null && item.HouseholdId is null))
            if (world.Map.FootDistance(person.Position, animal.Position) <= 4 && world.FreeCarryCapacity(actor) >= 1 &&
                WildAnimalHide(world, animal) is not null)
                yield return new("collect_hide", animal.Id);
        foreach (var animal in State.Animals.Where(item => item.DiedTick is null))
        {
            if (animal.LeaderId == actor) yield return new("lead_home", animal.Id);
            if (animal.RiderId == actor) { yield return new("dismount", animal.Id); continue; }
            if (animal.HouseholdId is null && MayLeadAnimal(world, actor, animal) &&
                MayApproachWildAnimal(world, actor, animal) &&
                world.HouseholdFor(actor) is { } household && AnimalYard(world, household) is { } yard &&
                HasAnimalTransferSpace(world, household, yard, animal) && animal.LeaderId is null &&
                (animal.TamingWork is null || animal.TamingWork.ActorId == actor))
                yield return new("tame", animal.Id);
            if (MayCareForAnimal(world, actor, animal) && animal.CareUntilTick <= world.WorldTick) yield return new("care", animal.Id);
            if (MayCareForAnimal(world, actor, animal) && animal.ReadyProductLotId is not null) yield return new("collect", animal.Id);
            if (AnimalHouseholdMember(world, actor, animal) && MayLeadAnimal(world, actor, animal) && animal.LeaderId is null && animal.RiderId is null &&
                AssignedAnimalYard(world, animal) is { } home && !YardTiles(world, home).Contains(animal.Position))
                yield return new("lead_home", animal.Id);
            if (animal.Species == "horse" && animal.SaddleLotId is null && AnimalHouseholdMember(world, actor, animal))
                yield return new("saddle", animal.Id);
            if (MayMountAnimal(world, actor, animal))
                yield return new("mount", animal.Id);
        }
    }
    private static bool MayApproachWildAnimal(IAnimalWorld world, string actor, AnimalState animal) =>
        world.Map.FootDistance(world.Agents[actor].Position, animal.Position) <= 4 ||
        world.PendingInstructionFor(actor)?.Task is { Action: "animal_tame" } order && order.TargetAnimalId == animal.Id;

    internal bool MayLeadAnimal(IAnimalWorld world, string actor, AnimalState animal) => world.AttachedHandcart(actor) is null && world.PassengerBoat(actor) is null &&
        !State.Animals.Any(other => other.RiderId == actor || other.Id != animal.Id && other.LeaderId == actor);
    internal void AddAnimalCandidates(IAnimalWorld world, List<CognitionCandidate> candidates, string actor)
    {
        foreach (var choice in AnimalChoices(world, actor))
        {
            var animal = Animal(choice.AnimalId)!;
            var workPlan = choice.Action is "care" or "tame" or "saddle" ? PlanAnimalWork(world, actor,
                new(choice.Action == "care" ? AnimalWorkKind.Care : choice.Action == "tame" ? AnimalWorkKind.Tame : AnimalWorkKind.Saddle, animal.Id)) : null;
            if (choice.Action is "care" or "tame" or "saddle" && (workPlan is null || !CheckAnimalWorkPlan(world, workPlan).Allowed)) continue;
            var productPlan = choice.Action is "collect" or "collect_hide" ? PlanAnimalProduct(world, actor,
                new(choice.Action == "collect" ? AnimalProductKind.Product : AnimalProductKind.Hide, animal.Id)) : null;
            if (choice.Action is "collect" or "collect_hide" && (productPlan is null || !CheckAnimalProductPlan(world, productPlan).Allowed)) continue;
            var travelPlan = choice.Action is "mount" or "dismount" or "lead_home" ? PlanAnimalTravel(world, actor,
                new(choice.Action == "mount" ? AnimalTravelKind.Mount : choice.Action == "dismount" ? AnimalTravelKind.Dismount : AnimalTravelKind.LeadHome, animal.Id)) : null;
            if (choice.Action is "mount" or "dismount" or "lead_home" && (travelPlan is null || !CheckAnimalTravelPlan(world, travelPlan).Allowed)) continue;
            var description = choice.Action switch
            {
                "tame" => $"Approach and tame {animal.Name} for your household with two feed and one jug water; lead it to the animal yard.",
                "care" => $"Bring physical feed and jug water to care for {animal.Name} for one day.",
                "collect" => $"Collect {AnimalRules.Definition(animal.Species).Product} from {animal.Name} into household goods.",
                "collect_hide" => $"Collect the unowned hide left by {animal.Name}'s natural death at its actual position.",
                "lead_home" => $"Lead {animal.Name} along a legal route to the household's animal yard.",
                "saddle" => $"Fit a real household saddle on {animal.Name}.",
                "mount" => $"Mount {animal.Name} with permission for faster travel and eight additional cargo units.",
                _ => $"Dismount {animal.Name}, leaving the horse and extra cargo at their actual position.",
            };
            candidates.Add(new(choice.Id, description, choice.Action == "lead_home" ? 13 : choice.Action == "care" ? 15 :
                choice.Action is "collect" or "collect_hide" ? 16 : 110, animal.Id)
            { ActionPlan = (ActionPlan?)productPlan ?? (ActionPlan?)workPlan ?? travelPlan });
        }
        AddAnimalSupplyCandidates(world, candidates, actor);
        AddAnimalPermissionAndTradeCandidates(world, candidates, actor);
    }
    internal bool ApplyAnimalCandidate(IAnimalWorld world, string actor, string candidateId, ActionPlan? originalPlan = null)
    {
        if (originalPlan is AnimalTravelActionPlan travelPlan)
        { ApplyAnimalTravelPlan(world, travelPlan); return true; }
        if (originalPlan is AnimalTripActionPlan tripPlan)
        { ApplyAnimalTripPlan(world, tripPlan); return true; }
        if (originalPlan is AnimalWorkActionPlan workPlan)
        { ApplyAnimalWorkPlan(world, workPlan); return true; }
        if (originalPlan is AnimalSupplyActionPlan supplyPlan)
        { ApplyAnimalSupplyPlan(world, supplyPlan); return true; }
        if (originalPlan is AnimalProductActionPlan captured)
        { ApplyAnimalProductPlan(world, captured); return true; }
        if (candidateId == "drink_milk")
        {
            if (PlanAnimalProduct(world, actor, new(AnimalProductKind.Milk)) is { } milk) ApplyAnimalProductPlan(world, milk);
            return true;
        }
        if (!candidateId.StartsWith("animal:", StringComparison.Ordinal)) return false;
        if (ApplyAnimalSupplyCandidate(world, actor, candidateId)) return true;
        var choice = AnimalChoices(world, actor).FirstOrDefault(item => item.Id == candidateId);
        if (choice is null) return true;
        if (choice.Action is "collect" or "collect_hide")
        {
            if (PlanAnimalProduct(world, actor, new(choice.Action == "collect" ? AnimalProductKind.Product : AnimalProductKind.Hide, choice.AnimalId)) is { } plan)
                ApplyAnimalProductPlan(world, plan);
            return true;
        }
        if (choice.Action is "care" or "tame" or "saddle")
        {
            if (PlanAnimalWork(world, actor, new(choice.Action == "care" ? AnimalWorkKind.Care : choice.Action == "tame" ? AnimalWorkKind.Tame : AnimalWorkKind.Saddle, choice.AnimalId)) is { } plan)
                ApplyAnimalWorkPlan(world, plan);
            return true;
        }
        if (PlanAnimalTravel(world, actor, new(choice.Action == "mount" ? AnimalTravelKind.Mount : choice.Action == "dismount" ? AnimalTravelKind.Dismount : AnimalTravelKind.LeadHome, choice.AnimalId)) is { } travel)
            ApplyAnimalTravelPlan(world, travel);
        return true;
    }
    internal static IEnumerable<InventoryLot> AnimalSuppliesAtHand(IAnimalWorld world, string actor, AnimalState animal)
    {
        var inventory = world.Society.Inventory;
        var owners = new GoodsOwners(animal.HouseholdId is { } home ? [actor, home] : [actor]);
        var kinds = new GoodsKinds(["grain", "wild_greens", "cultivated_greens", "saddle", InventoryContainerRules.FreshWater]);
        return world.FindGoods(new(GoodsUse.ConsumeAt, actor, owners, kinds)).Matches.Select(match => match.Lot)
            .Where(lot => AnimalSupplyIsAtHand(world, actor, animal, lot))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal);
    }
    private static bool AnimalSupplyIsAtHand(IAnimalWorld world, string actor, AnimalState animal, InventoryLot lot)
    {
        var inventory = world.Society.Inventory;
        return lot.DeliveryBuildingId is null &&
            (lot.OwnerId == actor && PersonalEquipmentRules.IsPhysicallyCarried(inventory, lot, actor) ||
             lot.OwnerId == animal.HouseholdId && MayCareForAnimal(world, actor, animal) &&
             (PersonalEquipmentRules.IsPhysicallyCarried(inventory, lot, actor) ||
              lot.CarrierId is null && animal.YardId is { } yardId &&
              world.Simulation.Buildings.FirstOrDefault(building => building.InstanceId == yardId) is { } yard &&
              (lot.StorageBuildingId == yardId && world.Agents[actor].Position == yard.Position ||
               lot.GroundPosition is { } ground && YardTiles(world, yard).Contains(new(ground.X, ground.Y)) &&
               world.IsWithinInteractionRange(world.Agents[actor].Position, new(ground.X, ground.Y), 1))));
    }

    internal static List<(InventoryLot Lot, int Quantity)>? AnimalCareInputs(IAnimalWorld world, string actor, AnimalState animal, int feed, int water)
    {
        var inventory = world.Society.Inventory;
        var lots = AnimalSuppliesAtHand(world, actor, animal).Where(lot =>
            world.Agents[actor].Position == animal.Position ||
            PersonalEquipmentRules.IsPhysicallyCarried(inventory, lot, actor)).ToArray();
        var result = new List<(InventoryLot, int)>();
        string? FoodHousehold(InventoryLot lot) => lot.OwnerId == animal.HouseholdId ? animal.HouseholdId : world.HouseholdFor(actor);
        bool Take(Func<InventoryLot, bool> predicate, int needed)
        {
            // Use grain before ready-to-eat servings, keeping family food when other feed fits.
            foreach (var lot in lots.Where(predicate).OrderBy(lot => IsEdibleFood(lot.ItemKind)))
            {
                var quantity = Math.Min(needed, world.AvailableLotQuantity(lot));
                var household = FoodHousehold(lot);
                var selected = result.Where(input => IsEdibleFood(input.Item1.ItemKind) &&
                    FoodHousehold(input.Item1) == household).Sum(input => input.Item2);
                // Care costs are small; a protected remainder need not hide a usable serving.
                while (quantity > 0 &&
                    (AnimalRules.IsFeed(lot.ItemKind) && !AnimalFeedMayBeSpent(world, actor, animal, lot, quantity) ||
                     IsEdibleFood(lot.ItemKind) && !AnimalFoodReserveRemaining(world, actor, selected + quantity, household)))
                    quantity--;
                if (quantity == 0) continue;
                result.Add((lot, quantity)); needed -= quantity;
                if (needed == 0) return true;
            }
            return needed == 0;
        }
        return Take(lot => AnimalRules.IsFeed(lot.ItemKind) && lot.ContainerLotId is null, feed) &&
            Take(lot => lot.ItemKind == InventoryContainerRules.FreshWater && lot.ContainerLotId is not null, water) &&
            result.Where(input => IsEdibleFood(input.Item1.ItemKind))
                .GroupBy(input => FoodHousehold(input.Item1))
                .All(group => AnimalFoodReserveRemaining(world, actor, group.Sum(input => input.Item2), group.Key)) ? result : null;
    }
    internal static bool AnimalFeedMayBeSpent(IAnimalWorld world, string actor, AnimalState animal, InventoryLot lot, int quantity)
    {
        if (lot.StorageBuildingId is not null && !world.CanRemoveWorkstationStock(world.Society.Inventory, lot, quantity)) return false;
        if (!IsEdibleFood(lot.ItemKind) || world.HouseholdFor(actor) is not { } household) return true;
        return AnimalFoodReserveRemaining(world, actor, quantity, lot.OwnerId == animal.HouseholdId ? animal.HouseholdId : null);
    }
    internal static bool AnimalFoodReserveRemaining(IAnimalWorld world, string actor, int quantity, string? stockHousehold = null)
    {
        var household = stockHousehold ?? world.HouseholdFor(actor);
        if (quantity == 0 || household is null) return true;
        var people = world.Society.Agents.Where(person => person.Status == SocietyAgentStatus.Active && person.HouseholdId == household)
            .Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var owners = people.Append(household).ToArray();
        var ready = world.HoldingsTotal(owners, world.EdibleFoodGoods);
        // General holdings exclude trip stock. Include this task's authorized
        // servings before subtracting its consumption, so they are counted once.
        var index = InventoryIndex.For(world.Society.Inventory);
        var tripHold = InventoryStore.CreateInventoryHold(InventoryHoldPurpose.SupplyTrip, actor);
        ready += world.FindGoods(new(GoodsUse.ConsumeAt, actor, new(owners), world.EdibleFoodGoods)).Matches
            .Where(match => match.RequiredHolds.Contains(tripHold) &&
                InventoryHoldRules.Allows(index, match.Lot, InventoryHoldRules.SupplyTripFor(actor)))
            .Sum(match => (long)match.Quantity);
        return ready - quantity >= people.Count * 2 + (world.Agents.Values.Any(person => people.Contains(person.AgentId) && ActiveParenthood(person.Parenthood)) ? 4 : 0);
    }
    internal static void ConsumeAnimalInputs(IAnimalWorld world, string actor, string purpose, List<(InventoryLot Lot, int Quantity)> inputs)
    {
        world.RunInventoryCommand(InventorySystem.ConsumeAnimalInputs(inputs, actor, world, purpose));
    }
    internal static InventoryLot? CarriedMilk(IAnimalWorld world, string actor) => world.FindGoods(new(GoodsUse.Eat, actor,
        new GoodsOwners(world.HouseholdFor(actor) is { } home ? [actor, home] : [actor]), GoodsKinds.One("milk"))).First?.Lot;

    internal static InventoryLot? WildAnimalHide(IAnimalWorld world, AnimalState animal) => animal.DiedTick is not null && animal.HouseholdId is null &&
        world.RecheckGoods(new(GoodsUse.AnimalHide, animal.Id, GoodsOwners.One(animal.Id), GoodsKinds.One("hide")), AnimalHideId(animal.Id)) is { Lot: var hide } &&
        hide.Quantity == 1 && hide.GroundPosition == new InventoryGroundPosition(animal.Position.X, animal.Position.Y) ? hide : null;

    private void CompleteAnimalCare(IAnimalWorld world, string actor, AnimalState animal, List<(InventoryLot Lot, int Quantity)> inputs, int careTicks)
    {
        ConsumeAnimalInputs(world, actor, "care:" + animal.Id, inputs);
        FinishAnimalSupplyTrip(world, actor);
        SetAnimal(animal with { CareUntilTick = world.WorldTick + careTicks });
        world.AppendEvent(new AnimalCaredEventPayload(actor, animal.Id), animal.Position);
    }

    private void CompleteAnimalTaming(IAnimalWorld world, AnimalWorkActionPlan plan, AnimalState animal, List<(InventoryLot Lot, int Quantity)> inputs)
    {
        if (plan.WorkTicks < AnimalRules.TamingWorkTicks)
        { SetAnimal(animal with { TamingWork = new(plan.Actor, plan.WorkTicks) }); return; }
        ConsumeAnimalInputs(world, plan.Actor, "tame:" + animal.Id, inputs);
        SetAnimal(animal with
        {
            HouseholdId = plan.ReceivingHouseholdId,
            YardId = plan.ReceivingYardId,
            HerdId = AnimalHerdId(plan.ReceivingHouseholdId!),
            CareUntilTick = 0,
            TamingWork = null,
            LeaderId = plan.Actor,
            LeadDestination = plan.ReceivingYardPosition,
            WildFedUntilTick = 0,
            WildWaterUntilTick = 0
        });
        world.AppendEvent(new AnimalTamedEventPayload(plan.Actor, animal.Id), animal.Position);
    }

    private static void CompleteMilkConsumption(IAnimalWorld world, string actor, InventoryLot milk, int quantity)
    {
        ConsumeAnimalInputs(world, actor, "drink_milk", [(milk, quantity)]);
        var person = world.Agents[actor];
        world.PhysicalAgents.RecordFullness(actor, person, Math.Min(10_000, person.FullnessBasisPoints + 3_000));
        world.AppendEvent(new MilkDrunkEventPayload(actor));
    }

    private void CompleteAnimalProductCollection(IAnimalWorld world, string actor, AnimalState animal, InventoryLot product)
    {
        ClearAnimalProduct(world, animal, discard: false);
        world.RunInventoryCommand(InventorySystem.CompleteAnimalProductCollection(world, product, actor));
        SetAnimal(animal with { ReadyProductLotId = null, ReadyProductReservationId = null });
        FinishAnimalSupplyTrip(world, actor);
        world.AppendEvent(new AnimalProductCollectedEventPayload(actor, animal.Id, product.ItemKind, product.Quantity), animal.Position);
    }

    private static void CompleteAnimalHideCollection(IAnimalWorld world, string actor, AnimalState animal, InventoryLot hide)
    {
        world.RunInventoryCommand(InventorySystem.CompleteAnimalHideCollection(actor, animal, world, hide));
        world.AppendEvent(new AnimalHideCollectedEventPayload(actor, animal.Id), animal.Position);
    }

    private void CompleteAnimalLead(IAnimalWorld world, AnimalTravelActionPlan plan, AnimalState animal)
    {
        if (plan.ReleaseLead)
        { SetAnimal(animal with { LeaderId = null, LeadDestination = null, YardId = plan.YardId }); return; }
        SetAnimal(animal with { LeaderId = plan.Actor, LeadDestination = plan.Destination });
        world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Destination!.Value, "lead_animal_home");
    }

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
        return new(actor, choice, animal.HouseholdId, animal.YardId, animal.Position, selected, preparation, dismount is null ? null : PlanAnimalDismount(world, actor, Animal(dismount)!),
            inputs is null && preparation is null, household, yard?.InstanceId, yard?.Position, animal.TamingWork,
            animal.TamingWork?.ActorId == actor ? animal.TamingWork.WorkTicks + 1 : 1, AnimalDayTicks(world),
            world.Reach(actor, animal.Position, ReachPurpose.AnimalWork, 0, 0, SwimmingPolicy.WhenCapable));
    }

    internal ActionCheck CheckAnimalWorkPlan(IAnimalWorld world, AnimalWorkActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capabilities = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ActionCapabilities);
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
        if (plan.Dismount is { } dismountPlan && CheckAnimalDismount(world, dismountPlan) is { Allowed: false } dismountCheck) return dismountCheck;
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
        ApplyCheckedAnimalWork(world, plan);
    }

    internal void ApplyCheckedAnimalWork(IAnimalWorld world, AnimalWorkActionPlan plan)
    {
        if (plan.SupplyPreparation is { } preparation) { ApplyAnimalSupplyPlan(world, preparation); return; }
        if (plan.Dismount is { } dismount) { CompleteAnimalDismount(world, dismount, "dismounted"); return; }
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
            animal.Position, animal.ReadyProductReservationId, dismount is null ? null : PlanAnimalDismount(world, actor, Animal(dismount)!),
            world.Reach(actor, animal.Position, ReachPurpose.AnimalProduct, 0, 0, SwimmingPolicy.WhenCapable));
    }

    internal ActionCheck CheckAnimalProductPlan(IAnimalWorld world, AnimalProductActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var milk = plan.Choice.Kind == AnimalProductKind.Milk;
        var capabilities = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ProductCapabilities(plan.Choice.Kind));
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
        if (plan.Dismount is { } dismountPlan && CheckAnimalDismount(world, dismountPlan) is { Allowed: false } dismountCheck) return dismountCheck;
        return plan.DismountAnimalId is not null || animal.LeaderId == plan.Actor || world.Agents[plan.Actor].Position == plan.Position ||
            world.Reach(plan.Actor, plan.Position, ReachPurpose.AnimalProduct, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected animal stock is out of reach now");
    }

    private void ApplyAnimalProductPlan(IAnimalWorld world, AnimalProductActionPlan plan)
    {
        if (CheckAnimalProductPlan(world, plan).Refusal is { } refusal) { world.RefuseChoice(plan, refusal); return; }
        ApplyCheckedAnimalProduct(world, plan);
    }

    internal void ApplyCheckedAnimalProduct(IAnimalWorld world, AnimalProductActionPlan plan)
    {
        var stock = world.Society.Inventory.GetLot(plan.StockId);
        if (plan.Choice.Kind == AnimalProductKind.Milk)
        {
            CompleteMilkConsumption(world, plan.Actor, stock, plan.Quantity);
            return;
        }
        var animal = Animal(plan.Choice.AnimalId!)!;
        if (plan.Dismount is { } dismount) { CompleteAnimalDismount(world, dismount, "dismounted"); return; }
        if (world.Agents[plan.Actor].Position != plan.Position && animal.LeaderId != plan.Actor)
        {
            world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Position,
                plan.Choice.Kind == AnimalProductKind.Product ? "animal_collect" : "animal_collect_hide");
            return;
        }
        if (plan.Choice.Kind == AnimalProductKind.Product) CompleteAnimalProductCollection(world, plan.Actor, animal, stock);
        else CompleteAnimalHideCollection(world, plan.Actor, animal, stock);
    }

    internal static ActorCapabilityPolicy ActionCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    internal static ActorCapabilityPolicy ProductCapabilities(AnimalProductKind kind) =>
        new(kind == AnimalProductKind.Milk ? ActionLifeStages.NonInfants : ActionLifeStages.Adults,
            AllowBoat: kind == AnimalProductKind.Milk, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    internal TPlan? PlanAnimalRequest<TPlan>(IAnimalWorld world, string actor, AnimalActionRequest request) where TPlan : ActionPlan
    {
        ActionPlan? plan;
        if (request.Family == AnimalActionFamily.Agreement)
            plan = AnimalAgreements(world, actor).FirstOrDefault(item => item.Id == request.CandidateId) is { } agreement
                ? PlanAnimalAgreement(world, actor, agreement) : null;
        else if (request.CandidateId == "drink_milk") plan = PlanAnimalProduct(world, actor, new(AnimalProductKind.Milk));
        else if (request.Family == AnimalActionFamily.Trip)
            plan = State.SupplyTrips.FirstOrDefault(item => item.ActorId == actor) is { } trip ? PlanAnimalTrip(world, actor, trip) : null;
        else if (request.Family == AnimalActionFamily.Supply)
            plan = AnimalSupplyChoices(world, actor).FirstOrDefault(item => item.Id == request.CandidateId) is { } supply
                ? PlanAnimalSupply(world, actor, supply) : null;
        else if (AnimalChoices(world, actor).FirstOrDefault(item => item.Id == request.CandidateId) is { } choice)
            plan = request.Family switch
            {
                AnimalActionFamily.Product => PlanAnimalProduct(world, actor, new(choice.Action == "collect" ? AnimalProductKind.Product : AnimalProductKind.Hide, choice.AnimalId)),
                AnimalActionFamily.Work => PlanAnimalWork(world, actor, new(choice.Action == "care" ? AnimalWorkKind.Care : choice.Action == "tame" ? AnimalWorkKind.Tame : AnimalWorkKind.Saddle, choice.AnimalId)),
                _ => PlanAnimalTravel(world, actor, new(choice.Action == "mount" ? AnimalTravelKind.Mount : choice.Action == "dismount" ? AnimalTravelKind.Dismount : AnimalTravelKind.LeadHome, choice.AnimalId)),
            };
        else plan = null;
        return plan as TPlan;
    }

    internal bool FinishMissingAnimalTrip(IAnimalWorld world, string actor)
    {
        if (State.SupplyTrips.Any(trip => trip.ActorId == actor)) FinishAnimalSupplyTrip(world, actor);
        return true;
    }

    internal static bool IsAnimalOrder(string action) => action is "animal_care" or "animal_collect" or "animal_tame" or
        "animal_lead_home" or "animal_saddle" or "animal_mount" or "animal_dismount";

    internal OrderTask? ParseAnimalOrder(IAnimalWorld world, string text, string actor)
    {
        if (!world.IsAdult(actor)) return null;
        var repeat = false;
        if (text.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase)) { repeat = true; text = text[7..]; }
        foreach (var (prefix, action) in new[] { ("care for ", "care"), ("collect from ", "collect"), ("tame ", "tame"),
                     ("lead home ", "lead_home"), ("saddle ", "saddle"), ("mount ", "mount"), ("dismount ", "dismount") })
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || repeat && action is not ("care" or "collect")) continue;
            var name = text[prefix.Length..].Trim();
            var targets = State.Animals.Where(animal => animal.DiedTick is null && animal.Id == name).ToArray();
            if (targets.Length == 0) targets = State.Animals.Where(animal => animal.DiedTick is null &&
                string.Equals(animal.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (targets.Length != 1) return null;
            var animal = targets[0];
            if (!AnimalHouseholdMember(world, actor, animal) && !MayCareForAnimal(world, actor, animal) && !MayRideAnimal(world, actor, animal) &&
                !(action == "tame" && animal.HouseholdId is null && world.Map.FootDistance(world.Agents[actor].Position, animal.Position) <= 4)) return null;
            return OrderStore.CreateOrderTask((("animal_" + action)) switch
            {
                "animal_care" => OrderStore.CreateAnimalCareOrderDefinition(TargetAnimalId: animal.Id!),
                "animal_collect" => OrderStore.CreateAnimalCollectOrderDefinition(TargetAnimalId: animal.Id!),
                "animal_tame" => OrderStore.CreateAnimalTameOrderDefinition(TargetAnimalId: animal.Id!),
                "animal_lead_home" => OrderStore.CreateAnimalLeadHomeOrderDefinition(TargetAnimalId: animal.Id!),
                "animal_saddle" => OrderStore.CreateAnimalSaddleOrderDefinition(TargetAnimalId: animal.Id!),
                "animal_mount" => OrderStore.CreateAnimalMountOrderDefinition(TargetAnimalId: animal.Id!),
                "animal_dismount" => OrderStore.CreateAnimalDismountOrderDefinition(TargetAnimalId: animal.Id!),
                _ => throw new InvalidOperationException("The parser returned an unsupported order kind."),
            }, "queued", 1, 0, "animal_tasks", repeat);
        }
        return null;
    }

    internal CognitionCandidate? AnimalOrderCandidate(IAnimalWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var animal = Animal(instruction.Task!.TargetAnimalId!);
        if (!world.IsAdult(actor) || animal is not { DiedTick: null } ||
            instruction.Task.Action == "animal_care" && animal.CareUntilTick <= world.WorldTick && !HasAnimalCareOrderSupplies(world, actor, animal)) return null;
        return new("animal_order", "Follow the order for " + animal.Name + ", using its actual location, permissions and physical supplies.", 0, animal.Id);
    }

    // Admission can inspect replacement supplies before execution releases an unrelated retained trip.
    internal bool HasAnimalCareOrderSupplies(IAnimalWorld world, string actor, AnimalState animal) => MayCareForAnimal(world, actor, animal) &&
        (AnimalCareInputs(world, actor, animal, AnimalRules.Definition(animal.Species).DailyFeed,
            AnimalRules.Definition(animal.Species).DailyWater) is not null ||
         State.SupplyTrips.Any(trip => trip.ActorId == actor && trip.AnimalId == animal.Id && trip.Action == "care") ||
         AnimalSupplyChoices(world, actor, ignoreActiveTrip: true).Any(supply => supply.AnimalId == animal.Id && supply.Action == "care"));

    internal string AnimalOrderBlockedReason(IAnimalWorld world, OwnerQueuedInstruction instruction) =>
        instruction.Task is { Action: "animal_care", TargetAnimalId: { } id } && Animal(id) is { DiedTick: null } animal &&
        animal.CareUntilTick <= world.WorldTick && MayCareForAnimal(world, instruction.TargetAgentId, animal) &&
        !HasAnimalCareOrderSupplies(world, instruction.TargetAgentId, animal)
            ? "Waiting for safe feed and jug water to care for " + animal.Name + "."
            : "Waiting for the animal, permission, an open route, carrying space, and feed or jug water.";

    internal void ExecuteAnimalOrder(IAnimalWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        var animal = Animal(order.TargetAnimalId!);
        if (animal is not { DiedTick: null }) return;
        var action = order.Action[7..];
        if (State.SupplyTrips.FirstOrDefault(trip => trip.ActorId == actor) is { } trip &&
            (trip.AnimalId != animal.Id || trip.Action != action))
            FinishAnimalSupplyTrip(world, actor);
        var oldCare = animal.CareUntilTick;
        var oldProduct = animal.ReadyProductLotId;
        var choice = AnimalChoices(world, actor).FirstOrDefault(choice => choice.AnimalId == animal.Id && choice.Action == action);
        var previousPosition = world.Agents[actor].Position;
        if (choice is not null) _ = ApplyAnimalCandidate(world, actor, choice.Id);
        if (CreditEffect()) return;
        if (world.Agents[actor].Position != previousPosition) return;
        if (action is "care" or "collect" or "saddle")
        {
            if (State.SupplyTrips.FirstOrDefault(trip => trip.ActorId == actor && trip.AnimalId == animal.Id) is not null)
                _ = ApplyAnimalSupplyCandidate(world, actor, AnimalSupplyTripId(actor));
            else if (AnimalSupplyChoices(world, actor).FirstOrDefault(supply => supply.AnimalId == animal.Id && supply.Action == action) is { } supply)
                _ = ApplyAnimalSupplyCandidate(world, actor, supply.Id);
        }
        // A supply pickup can finish collection immediately at the animal.
        if (CreditEffect()) return;
        if (choice is null) world.SetOrderStatus(instruction, OrderStatus.Blocked, "Waiting for permission to handle this animal, its supplies, a ready product or a free yard place.");

        bool CreditEffect()
        {
            animal = Animal(animal.Id)!;
            var finished = action switch
            {
                "care" => MayCareForAnimal(world, actor, animal) && animal.CareUntilTick > oldCare,
                "collect" => oldProduct is not null && animal.ReadyProductLotId is null && MayCareForAnimal(world, actor, animal),
                "tame" => AnimalHouseholdMember(world, actor, animal),
                "lead_home" => AnimalHouseholdMember(world, actor, animal) && AssignedAnimalYard(world, animal) is { } yard &&
                    YardTiles(world, yard).Contains(animal.Position) && animal.LeaderId is null,
                "saddle" => AnimalHouseholdMember(world, actor, animal) && animal.SaddleLotId is not null,
                "mount" => animal.RiderId == actor,
                _ => animal.RiderId is null,
            };
            if (!finished) return false;
            world.CreditOrderEffect(instruction, Identity.Parse(AnimalOrderReceiptId(instruction.InstructionId, animal.Id,
                (action == "care" ? animal.CareUntilTick.ToString(System.Globalization.CultureInfo.InvariantCulture) :
                 action == "collect" ? oldProduct : world.WorldTick.ToString(System.Globalization.CultureInfo.InvariantCulture)))), 1);
            return true;
        }
    }

    internal void CancelAnimalSupplyForOrder(IAnimalWorld world, OwnerQueuedInstruction instruction)
    {
        if (instruction.Task is not { } order || !IsAnimalOrder(order.Action)) return;
        if (State.SupplyTrips.Any(trip => trip.ActorId == instruction.TargetAgentId &&
                trip.AnimalId == order.TargetAnimalId && trip.Action == order.Action[7..]))
            FinishAnimalSupplyTrip(world, instruction.TargetAgentId);
    }
}
