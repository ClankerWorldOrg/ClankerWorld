using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

using static ClankerWorld.Simulation.Playtest.PrivateWorldRuntime.AnimalSharedRules;

namespace ClankerWorld.Simulation.Playtest;

internal sealed partial class AnimalSystem
{
    internal static bool IsAnimalOrder(string action) => action is "animal_care" or "animal_collect" or "animal_tame" or
        "animal_lead_home" or "animal_saddle" or "animal_mount" or "animal_dismount";

    internal OwnerInstructionOrder? ParseAnimalOrder(IAnimalWorld world, string text, string actor)
    {
        if (!world.AdultResident(actor)) return null;
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
                !(action == "tame" && animal.HouseholdId is null && world.Map.FootDistance(world.Inhabitants[actor].Position, animal.Position) <= 4)) return null;
            return new("animal_" + action, "queued", 1, 0, "animal_tasks", repeat) { TargetAnimalId = animal.Id };
        }
        return null;
    }

    internal CognitionCandidate? AnimalOrderCandidate(IAnimalWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetInhabitantId;
        var animal = Animal(instruction.Order!.TargetAnimalId!);
        if (!world.AdultResident(actor) || animal is not { DiedTick: null } ||
            instruction.Order.Action == "animal_care" && animal.CareUntilTick <= world.WorldTick && !HasAnimalCareOrderSupplies(world, actor, animal)) return null;
        return new("animal_order", "Follow the order for " + animal.Name + ", using its actual location, permissions and physical supplies.", 0, animal.Id);
    }

    // Admission can inspect replacement supplies before execution releases an unrelated retained trip.
    internal bool HasAnimalCareOrderSupplies(IAnimalWorld world, string actor, AnimalState animal) => MayCareForAnimal(world, actor, animal) &&
        (AnimalCareInputs(world, actor, animal, AnimalRules.Definition(animal.Species).DailyFeed,
            AnimalRules.Definition(animal.Species).DailyWater) is not null ||
         State.SupplyTrips.Any(trip => trip.ActorId == actor && trip.AnimalId == animal.Id && trip.Action == "care") ||
         AnimalSupplyChoices(world, actor, ignoreActiveTrip: true).Any(supply => supply.AnimalId == animal.Id && supply.Action == "care"));

    internal string AnimalOrderBlockedReason(IAnimalWorld world, OwnerQueuedInstruction instruction) =>
        instruction.Order is { Action: "animal_care", TargetAnimalId: { } id } && Animal(id) is { DiedTick: null } animal &&
        animal.CareUntilTick <= world.WorldTick && MayCareForAnimal(world, instruction.TargetInhabitantId, animal) &&
        !HasAnimalCareOrderSupplies(world, instruction.TargetInhabitantId, animal)
            ? "Waiting for safe feed and jug water to care for " + animal.Name + "."
            : "Waiting for the named animal, permission, a legal route, carry space and physical feed or jug water.";

    internal void ExecuteAnimalOrder(IAnimalWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetInhabitantId;
        var order = instruction.Order!;
        var animal = Animal(order.TargetAnimalId!);
        if (animal is not { DiedTick: null }) return;
        var action = order.Action[7..];
        if (State.SupplyTrips.FirstOrDefault(trip => trip.ActorId == actor) is { } trip &&
            (trip.AnimalId != animal.Id || trip.Action != action))
            FinishAnimalSupplyTrip(actor);
        var oldCare = animal.CareUntilTick;
        var oldProduct = animal.ReadyProductLotId;
        var choice = AnimalChoices(world, actor).FirstOrDefault(choice => choice.AnimalId == animal.Id && choice.Action == action);
        var previousPosition = world.Inhabitants[actor].Position;
        if (choice is not null) _ = ApplyAnimalCandidate(world, actor, choice.Id);
        if (CreditEffect()) return;
        if (world.Inhabitants[actor].Position != previousPosition) return;
        if (action is "care" or "collect" or "saddle")
        {
            if (State.SupplyTrips.FirstOrDefault(trip => trip.ActorId == actor && trip.AnimalId == animal.Id) is not null)
                _ = ApplyAnimalSupplyCandidate(world, actor, AnimalSupplyTripId(actor));
            else if (AnimalSupplyChoices(world, actor).FirstOrDefault(supply => supply.AnimalId == animal.Id && supply.Action == action) is { } supply)
                _ = ApplyAnimalSupplyCandidate(world, actor, supply.Id);
        }
        // A supply pickup can finish collection immediately at the animal.
        if (CreditEffect()) return;
        if (choice is null) world.SetOrderStatus(instruction, "blocked", "Waiting for this animal's permission, physical supplies, product or free yard place.");

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
            world.CreditOrderEffect(instruction, AnimalOrderReceiptId(instruction.InstructionId, animal.Id,
                (action == "care" ? animal.CareUntilTick.ToString(System.Globalization.CultureInfo.InvariantCulture) :
                 action == "collect" ? oldProduct : world.WorldTick.ToString(System.Globalization.CultureInfo.InvariantCulture))), 1);
            return true;
        }
    }

    internal void CancelAnimalSupplyForOrder(OwnerQueuedInstruction instruction)
    {
        if (instruction.Order is not { } order || !IsAnimalOrder(order.Action)) return;
        if (State.SupplyTrips.Any(trip => trip.ActorId == instruction.TargetInhabitantId &&
                trip.AnimalId == order.TargetAnimalId && trip.Action == order.Action[7..]))
            FinishAnimalSupplyTrip(instruction.TargetInhabitantId);
    }
}

