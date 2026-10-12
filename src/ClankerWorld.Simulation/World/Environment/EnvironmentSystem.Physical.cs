using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class EnvironmentSystem
{
    internal static void CreatePhysicalState(IEnvironmentResidentWorld world)
    {
        var startingPositions = new[]
        {
            new GridPoint(0, 0),
            new GridPoint(1, 1),
            new GridPoint(2, 1),
            new GridPoint(3, 1),
        };
        var profiles = new (string Id, string Personality, string Aspiration)[]
        {
            ("founder-scout", "curious", "map the nearby world"),
            ("founder-mira", "practical", "make the camp self-sufficient"),
            ("founder-rowan", "patient", "build something lasting"),
            ("founder-ilya", "observant", "teach and preserve memory"),
        };
        for (var index = 0; index < profiles.Length; index++)
        {
            var profile = profiles[index];
            world.AddInhabitant(
                profile.Id,
                new InhabitantState(
                    profile.Id,
                    startingPositions[index],
                    6_500,
                    0,
                    profile.Personality,
                    profile.Aspiration));
        }
    }

    internal static void DrainNeeds(IEnvironmentResidentWorld world)
    {
        foreach (var state in world.Inhabitants.Values.ToArray())
        {
            world.SetInhabitant(state.InhabitantId, state with
            {
                HungerBasisPoints = Math.Max(0, state.HungerBasisPoints - 4),
            });
        }
    }

    internal static void RemoveDeadPhysicalState(IEnvironmentResidentWorld world)
    {
        var activeIds = world.Society.Inhabitants
            .Where(item => item.Status == SocietyInhabitantStatus.Active)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in world.Inhabitants.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            if (world.PendingInstructionFor(id) is { Order.Action: "construct_building" or "expand_building" } buildingOrder)
            {
                world.CancelConstructionForOrder(buildingOrder);
                world.CancelExpansionForOrder(buildingOrder);
                world.SetOrderStatus(buildingOrder, "blocked", "The ordered building worker is no longer alive.");
            }
            if (world.PendingInstructionFor(id) is { Order.Action: "produce_item" } productionOrder)
            {
                world.CancelProductionForOrder(productionOrder);
                world.SetOrderStatus(productionOrder, "blocked", "The ordered production worker is no longer alive.");
            }
            if (world.Society.Inventory.Lots.Any(lot => lot.CarrierId == id))
            {
                var position = world.Inhabitants[id].Position;
                world.ApplyInventoryTransition(inventory => InventoryAuthority.DropCarrierGoods(inventory, id,
                    new InventoryGroundPosition(position.X, position.Y)));
            }
            var deceased = world.Society.GetInhabitant(id);
            var deathTick = deceased.DeathTick ?? throw new InvalidDataException("A removed inhabitant has no committed death.");
            // Society escrows ownership at death. Goods that had no separate
            // custodian or storage were carried by their owner; keep their
            // physical position at the deceased's tile after that owner is gone.
            var estate = world.Society.Estates.SingleOrDefault(item => item.DeceasedId == id);
            if (estate is not null)
            {
                var position = world.Inhabitants[id].Position;
                foreach (var lot in world.Society.Inventory.Lots.Where(item => item.OwnerId == estate.Id &&
                             item.ContainerLotId is null && item.CarrierId is null &&
                             item.StorageBuildingId is null && item.GroundPosition is null).ToArray())
                    world.ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(inventory,
                        $"death:{id}:{lot.Id}", lot.Id, estate.Id, lot.Quantity,
                        groundPosition: new InventoryGroundPosition(position.X, position.Y)));
            }
            foreach (var moment in (world.Inhabitants[id].IdentityMoments ?? [])
                         .Where(item => item.Outcome is "waiting" or "requested").ToArray())
                world.FinishIdentityMoment(id, moment.Kind, "interrupted");
            world.AddDeceasedInhabitant(id, new DeceasedInhabitantState(
                id, deathTick, world.Society.AgeAt(deceased, deathTick),
                world.Inhabitants[id] with
                {
                    MedicalTreatment = null,
                    MedicalSupplyTrip = null,
                    GuardianPlacement = null,
                    Equipment = world.Inhabitants[id].Equipment is { } equipment
                        ? equipment with { OrnamentLotId = null } : null,
                }, world.TownForResident(id))
            { BoatIdAtDeath = world.RecordBoatDeath(id) });
            world.RemoveInhabitant(id);
            world.RemoveTownResident(id);
            world.AppendEvent("inhabitant_removed", id);
        }
    }
}
