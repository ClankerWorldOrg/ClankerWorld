using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
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
                (world.Map.FootDistance(person.Position, animal.Position) <= 4 ||
                 world.PendingInstructionFor(actor)?.Task is { Action: "animal_tame" } order && order.TargetAnimalId == animal.Id) &&
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
            if (MayRideAnimal(world, actor, animal) && animal.SaddleLotId is not null && animal.RiderId is null && animal.LeaderId is null &&
                AnimalRules.HasCare(animal, world.WorldTick) && AnimalRules.IsAdult(animal, world.WorldTick, AnimalDayTicks(world)) &&
                world.AttachedHandcart(actor) is null && !State.Animals.Any(item => item.RiderId == actor || item.LeaderId == actor))
                yield return new("mount", animal.Id);
        }
    }
    internal bool MayLeadAnimal(IAnimalWorld world, string actor, AnimalState animal) => world.AttachedHandcart(actor) is null && world.PassengerBoat(actor) is null &&
        !State.Animals.Any(other => other.RiderId == actor || other.Id != animal.Id && other.LeaderId == actor);
    internal void AddAnimalCandidates(IAnimalWorld world, List<CognitionCandidate> candidates, string actor)
    {
        foreach (var choice in AnimalChoices(world, actor))
        {
            var animal = Animal(choice.AnimalId)!;
            if (choice.Action == "care" && AnimalCareInputs(world, actor, animal, AnimalRules.Definition(animal.Species).DailyFeed,
                    AnimalRules.Definition(animal.Species).DailyWater) is null &&
                !AnimalSupplyChoices(world, actor).Any(supply => supply.AnimalId == animal.Id && supply.Action == "care")) continue;
            var productPlan = choice.Action is "collect" or "collect_hide" ? PlanAnimalProduct(world, actor,
                new(choice.Action == "collect" ? AnimalProductKind.Product : AnimalProductKind.Hide, animal.Id)) : null;
            if (choice.Action is "collect" or "collect_hide" && (productPlan is null || !CheckAnimalProductPlan(world, productPlan).Allowed)) continue;
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
            { ActionPlan = productPlan });
        }
        AddAnimalSupplyCandidates(world, candidates, actor);
        AddAnimalPermissionAndTradeCandidates(world, candidates, actor);
    }
    internal bool ApplyAnimalCandidate(IAnimalWorld world, string actor, string candidateId, ActionPlan? originalPlan = null)
    {
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
        var animal = Animal(choice.AnimalId)!;
        var person = world.Agents[actor];
        if (choice.Action is "care" or "saddle")
        {
            var needsSupply = choice.Action switch
            {
                "care" => AnimalCareInputs(world, actor, animal, AnimalRules.Definition(animal.Species).DailyFeed,
                    AnimalRules.Definition(animal.Species).DailyWater) is null,
                _ => !AnimalSuppliesAtHand(world, actor, animal).Any(lot => lot.ItemKind == "saddle" && lot.OwnerId == animal.HouseholdId),
            };
            if (needsSupply && AnimalSupplyChoices(world, actor).FirstOrDefault(supply => supply.AnimalId == animal.Id && supply.Action == choice.Action) is { } supply)
            { _ = ApplyAnimalSupplyCandidate(world, actor, supply.Id); return true; }
            if (choice.Action == "care" && needsSupply) return true;
        }
        if (choice.Action == "dismount") { EndAnimalRide(world, animal, "dismounted"); return true; }
        if (choice.Action == "care" && State.Animals.FirstOrDefault(other =>
                other.RiderId == actor && other.Id != animal.Id) is { } ridden)
        {
            EndAnimalRide(world, ridden, "dismounted");
            return true;
        }
        if (choice.Action is "collect" or "saddle" && State.Animals.FirstOrDefault(other =>
                other.RiderId == actor && other.Id != animal.Id) is { } riddenForWork)
        {
            EndAnimalRide(world, riddenForWork, "dismounted");
            return true;
        }
        if (person.Position != animal.Position && animal.LeaderId != actor)
        {
            world.MoveToward(actor, person, animal.Position, "animal_" + choice.Action);
            return true;
        }
        switch (choice.Action)
        {
            case "tame":
                if (world.HouseholdFor(actor) is not { } household || AnimalYard(world, household) is not { } yard || !HasAnimalTransferSpace(world, household, yard, animal)) break;
                if (AnimalCareInputs(world, actor, animal, 2, 1) is not { } tamingInputs) break;
                var work = animal.TamingWork?.ActorId == actor ? animal.TamingWork.WorkTicks + 1 : 1;
                if (work < AnimalRules.TamingWorkTicks) { SetAnimal(animal with { TamingWork = new(actor, work) }); break; }
                ConsumeAnimalInputs(world, actor, "tame:" + animal.Id, tamingInputs);
                SetAnimal(animal with
                {
                    HouseholdId = household,
                    YardId = yard.InstanceId,
                    HerdId = AnimalHerdId(household),
                    CareUntilTick = 0,
                    TamingWork = null,
                    LeaderId = actor,
                    LeadDestination = yard.Position,
                    WildFedUntilTick = 0,
                    WildWaterUntilTick = 0
                });
                world.AppendEvent("animal_tamed", actor + ":" + animal.Id, animal.Position);
                break;
            case "care":
                var definition = AnimalRules.Definition(animal.Species);
                if (AnimalCareInputs(world, actor, animal, definition.DailyFeed, definition.DailyWater) is not { } inputs) break;
                ConsumeAnimalInputs(world, actor, "care:" + animal.Id, inputs);
                FinishAnimalSupplyTrip(world, actor);
                SetAnimal(animal with { CareUntilTick = world.WorldTick + AnimalDayTicks(world) });
                world.AppendEvent("animal_cared", actor + ":" + animal.Id, animal.Position);
                break;
            case "lead_home": LeadAnimalHome(world, actor, animal); break;
            case "saddle": SaddleAnimal(world, actor, animal); break;
            case "mount": SetAnimal(animal with { RiderId = actor }); world.AppendEvent("horse_mounted", actor + ":" + animal.Id, animal.Position); break;
        }
        return true;
    }
    internal static IEnumerable<InventoryLot> AnimalSuppliesAtHand(IAnimalWorld world, string actor, AnimalState animal)
    {
        var inventory = world.Society.Inventory;
        var owners = new GoodsOwners(animal.HouseholdId is { } home ? [actor, home] : [actor]);
        var kinds = new GoodsKinds(["grain", "wild_greens", "cultivated_greens", "saddle", InventoryContainerRules.FreshWater]);
        return world.FindGoods(new(GoodsUse.ConsumeAt, actor, owners, kinds)).Matches.Select(match => match.Lot)
            .Where(lot => lot.DeliveryBuildingId is null &&
            (lot.OwnerId == actor && PersonalEquipmentRules.IsPhysicallyCarried(inventory, lot, actor) ||
             lot.OwnerId == animal.HouseholdId && MayCareForAnimal(world, actor, animal) &&
             (PersonalEquipmentRules.IsPhysicallyCarried(inventory, lot, actor) ||
              lot.CarrierId is null && animal.YardId is { } yardId &&
              world.Simulation.Buildings.FirstOrDefault(building => building.InstanceId == yardId) is { } yard &&
              (lot.StorageBuildingId == yardId && world.Agents[actor].Position == yard.Position ||
               lot.GroundPosition is { } ground && YardTiles(world, yard).Contains(new(ground.X, ground.Y)) &&
               world.IsWithinInteractionRange(world.Agents[actor].Position, new(ground.X, ground.Y), 1)))))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal);
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
        var ready = world.HoldingsTotal(people.Append(household), world.EdibleFoodGoods);
        return ready - quantity >= people.Count * 2 + (world.Agents.Values.Any(person => people.Contains(person.AgentId) && ActiveParenthood(person.Parenthood)) ? 4 : 0);
    }
    internal static void ConsumeAnimalInputs(IAnimalWorld world, string actor, string purpose, List<(InventoryLot Lot, int Quantity)> inputs)
    {
        world.ApplyInventoryTransition(inventory =>
        {
            var next = inventory;
            for (var index = 0; index < inputs.Count; index++)
            {
                var (lot, quantity) = inputs[index];
                var id = AnimalInputReservationId(actor, world.WorldTick, world.NextEventId, purpose, index);
                next = InventoryAuthority.ConsumeReservation(InventoryAuthority.Reserve(next, id, lot.OwnerId, lot.Id,
                    quantity, purpose, world.WorldTick, holdAccess: InventoryHoldRules.SupplyTripFor(actor)), id);
            }
            return next;
        });
    }
    internal static InventoryLot? CarriedMilk(IAnimalWorld world, string actor) => world.FindGoods(new(GoodsUse.Eat, actor,
        new GoodsOwners(world.HouseholdFor(actor) is { } home ? [actor, home] : [actor]), GoodsKinds.One("milk"))).First?.Lot;

    internal static InventoryLot? WildAnimalHide(IAnimalWorld world, AnimalState animal) => animal.DiedTick is not null && animal.HouseholdId is null &&
        world.RecheckGoods(new(GoodsUse.AnimalHide, animal.Id, GoodsOwners.One(animal.Id), GoodsKinds.One("hide")), AnimalHideId(animal.Id)) is { Lot: var hide } &&
        hide.Quantity == 1 && hide.GroundPosition == new InventoryGroundPosition(animal.Position.X, animal.Position.Y) ? hide : null;

    internal void CollectAnimalProduct(IAnimalWorld world, string actor, AnimalState animal)
    {
        if (PlanAnimalProduct(world, actor, new(AnimalProductKind.Product, animal.Id)) is { } plan && CheckAnimalProductPlan(world, plan).Allowed)
            CompleteAnimalProductCollection(world, actor, animal, world.Society.Inventory.GetLot(plan.StockId));
    }

    private void CompleteAnimalProductCollection(IAnimalWorld world, string actor, AnimalState animal, InventoryLot product)
    {
        ClearAnimalProduct(world, animal, discard: false);
        world.ApplyInventoryTransition(current => InventoryAuthority.Relocate(current, "animal-collect-" + world.WorldTick + "-" + world.NextEventId,
            product.Id, product.OwnerId, product.Quantity, carrierId: actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        SetAnimal(animal with { ReadyProductLotId = null, ReadyProductReservationId = null });
        FinishAnimalSupplyTrip(world, actor);
        world.AppendEvent("animal_product_collected", actor + ":" + animal.Id + ":" + product.ItemKind + ":" + product.Quantity, animal.Position);
    }

    private static void CompleteAnimalHideCollection(IAnimalWorld world, string actor, AnimalState animal, InventoryLot hide)
    {
        world.ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(InventoryAuthority.Transfer(inventory,
            AnimalHideTransferId(actor, animal.Id, world.WorldTick, false), animal.Id, actor,
            hide.Id, 1, "wild-animal-hide", destinationGroundPosition: hide.GroundPosition, holdAccess: InventoryHoldRules.BorrowedBy(actor)),
            AnimalHideTransferId(actor, animal.Id, world.WorldTick, true), hide.Id, actor, 1, carrierId: actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        world.AppendEvent("animal_hide_collected", actor + ":" + animal.Id, animal.Position);
    }

    internal void LeadAnimalHome(IAnimalWorld world, string actor, AnimalState animal)
    {
        if (!AnimalHouseholdMember(world, actor, animal) || AssignedAnimalYard(world, animal) is not { } yard ||
            animal.RiderId is not null || animal.LeaderId is not (null) && animal.LeaderId != actor) return;
        if (YardTiles(world, yard).Contains(animal.Position))
        {
            SetAnimal(animal with { LeaderId = null, LeadDestination = null, YardId = yard.InstanceId });
            return;
        }
        var destination = FreeAnimalYardTile(world, yard, animal.Id);
        if (destination is null) return;
        SetAnimal(animal with { LeaderId = actor, LeadDestination = destination });
        world.MoveToward(actor, world.Agents[actor], destination.Value, "lead_animal_home");
    }
}

