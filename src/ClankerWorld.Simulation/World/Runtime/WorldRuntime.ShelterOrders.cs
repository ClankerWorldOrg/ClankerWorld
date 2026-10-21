using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool IsShelterOrder(string action) => OrderSystem.IsShelterOrder(action);

    private bool ShelterBuildingKnown(string actor, AgentState person, PlacedBuilding building) =>
        OrderSystem.ShelterBuildingKnown(ShelterOrdersWorld, actor, person, building);

    private bool ShelterBuildingCovers(string actor, PlacedBuilding building, GridPoint point) =>
        OrderSystem.ShelterBuildingCovers(ShelterOrdersWorld, actor, building, point);

    private PlacedBuilding? BoundShelterBuilding(string actor, OrderTask order) =>
        OrderSystem.BoundShelterBuilding(ShelterOrdersWorld, actor, order);

    private bool ShelterBindingIsAvailable(string actor, OrderTask order) =>
        OrderSystem.ShelterBindingIsAvailable(ShelterOrdersWorld, actor, order);

    private OwnerShelterBinding? SelectShelterBinding(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.SelectShelterBinding(ShelterOrdersWorld, instruction, person);

    private bool CanInspectShelterTarget(OrderTask order, string actor, AgentState person) =>
        OrderSystem.CanInspectShelterTarget(ShelterOrdersWorld, order, actor, person);

    private MapResource? KnownOrderFirewoodSource(string actor, AgentState person,
        GridPoint? returnTo = null) =>
        OrderSystem.KnownOrderFirewoodSource(ShelterOrdersWorld, actor, person, returnTo);

    private bool CanObtainFirewood(string actor, AgentState person, GridPoint? returnTo = null) =>
        OrderSystem.CanObtainFirewood(ShelterOrdersWorld, actor, person, returnTo);

    private CognitionCandidate? ShelterOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ShelterOrderCandidateFor(ShelterOrdersWorld, instruction, person);

    private void ExecuteShelterOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteShelterOrderStep(ShelterOrdersWorld, instruction, person);

    private string ShelterOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ShelterOrderBlockedReason(ShelterOrdersWorld, instruction, person);

    private static string ShelterOrderReceipt(string instructionId, OrderTask order) =>
        DerivedId.Hash(JsonSerializer.Serialize(
            new { instructionId, order.Action, order.ShelterBinding, order.ShelterCompletion }),
            order.Action == "tend_fire" ? "shelter:fire:" : "shelter:arrival:");

    private static void ValidateShelterOrderBindings(
        WorldContentSimulationState? simulation, DeclarativeWorldContentState content, SocietyCheckpoint society,
        IEnumerable<OwnerQueuedInstruction> instructions, long worldTick)
    {
        var fuelReceipts = new HashSet<string>(StringComparer.Ordinal);
        var householdIds = society.Households.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var instruction in instructions.Where(item => item.Task is { } order && IsShelterOrder(order.Action)))
        {
            var order = instruction.Task!;
            if (!IsValidShelterOrderShape(order, instruction, worldTick))
                throw new InvalidDataException("A shelter order has an invalid saved shape.");
            if (order.ShelterBinding is { } binding)
            {
                if (binding.Kind is not ("natural" or "building") || binding.Position.X < 0 || binding.Position.Y < 0 ||
                    order.TargetPosition is { } requested && requested != binding.Position)
                    throw new InvalidDataException("A shelter order has an invalid destination binding.");
                if (binding.Kind == "natural")
                {
                    if (order.Action != "seek_shelter" || order.TargetBuildingKind is not null || binding.BuildingInstanceId is not null ||
                        binding.DefinitionId is not null || binding.OwnerId is not null || binding.BuildingPosition is not null || binding.BuildingPlacedTick is not null)
                        throw new InvalidDataException("Natural cover cannot carry a building or fire binding.");
                }
                else
                {
                    var definition = content.Buildings.FirstOrDefault(item => item.CanonicalId == binding.DefinitionId);
                    if (simulation is null || !IdentityRules.Reference(binding.BuildingInstanceId, int.MaxValue, requireTrimmed: true) || definition is null ||
                        binding.BuildingPosition is not { X: >= 0, Y: >= 0 } || binding.BuildingPlacedTick is not { } placed || placed < 0 || placed > worldTick ||
                        binding.OwnerId is { } owner && !householdIds.Contains(owner) ||
                        order.TargetBuildingKind == "house" && (!definition.Tags.Contains("house", StringComparer.Ordinal) || binding.OwnerId is null) ||
                        !(order.Action == "seek_shelter" ? definition.Tags.Any(tag => tag is "shelter" or TownHallContent.HallTag) :
                            definition.Tags.Any(tag => tag is "cooking" or "warmth")))
                        throw new InvalidDataException("A shelter order has an invalid building binding.");
                }
            }
            if (order.ShelterCompletion is not { } completion)
            {
                if (order.CompletedUnits != 0 || order.LastEffectId is not null)
                    throw new InvalidDataException("Shelter progress requires an actual completion receipt.");
                continue;
            }
            if (order.ShelterBinding is not { } completedBinding || order.Status != "finished" || order.CompletedUnits != 1 ||
                completion.WorldTick < instruction.SubmittedTick || completion.WorldTick > worldTick ||
                completedBinding.BuildingPlacedTick > completion.WorldTick || completion.Position != completedBinding.Position ||
                order.LastEffectId != ShelterOrderReceipt(instruction.InstructionId, order))
                throw new InvalidDataException("A shelter completion does not match its bound owner order.");
            if (order.Action == "seek_shelter")
            {
                if (completion.FuelReservationId is not null)
                    throw new InvalidDataException("A shelter arrival cannot claim a fire payment.");
                continue;
            }
            var reservation = society.Inventory.Reservations.FirstOrDefault(item => item.Id == completion.FuelReservationId);
            if (reservation is null || !fuelReceipts.Add(reservation.Id) || reservation.OwnerId != instruction.TargetAgentId ||
                reservation.Quantity != 1 || reservation.Purpose != "heating_fuel" || reservation.State != InventoryReservationState.Completed ||
                reservation.ExpiryTick != completion.WorldTick ||
                society.Inventory.Lots.FirstOrDefault(item => item.Id == reservation.LotId) is { ItemKind: not "wood" })
                throw new InvalidDataException("A fire order lacks its unique completed wood payment.");
        }
    }
}
