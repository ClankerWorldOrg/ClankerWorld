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
    internal static void CreatePhysicalState(IEnvironmentAgentWorld world)
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
            ("founder-mira", "practical", "make the household self-sufficient"),
            ("founder-rowan", "patient", "build something lasting"),
            ("founder-ilya", "observant", "teach and preserve memory"),
        };
        for (var index = 0; index < profiles.Length; index++)
        {
            var profile = profiles[index];
            world.PhysicalAgents.PlaceStartingAgent(profile.Id, startingPositions[index], profile.Personality, profile.Aspiration);
        }
    }

    internal static void DrainNeeds(IEnvironmentAgentWorld world)
    {
        foreach (var state in world.Agents.Values.ToArray())
        {
            world.PhysicalAgents.RecordFullness(state.AgentId, state, Math.Max(0, state.FullnessBasisPoints - 4));
        }
    }

    internal static void RemoveDeadPhysicalState(IEnvironmentAgentWorld world)
    {
        var activeIds = world.Society.Agents
            .Where(item => item.Status == SocietyAgentStatus.Active)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in world.Agents.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            if (world.PendingInstructionFor(id) is { Task.Action: "construct_building" or "expand_building" } buildingOrder)
            {
                world.CancelConstructionForOrder(buildingOrder);
                world.CancelExpansionForOrder(buildingOrder);
                world.SetOrderStatus(buildingOrder, "blocked", "The ordered building worker is no longer alive.");
            }
            if (world.PendingInstructionFor(id) is { Task.Action: "produce_item" } productionOrder)
            {
                world.CancelProductionForOrder(productionOrder);
                world.SetOrderStatus(productionOrder, "blocked", "The ordered production worker is no longer alive.");
            }
            if (world.Society.Inventory.Lots.Any(lot => lot.CarrierId == id))
            {
                var position = world.Agents[id].Position;
                world.ApplyInventoryTransition(inventory => InventoryAuthority.DropCarrierGoods(inventory, id,
                    new InventoryGroundPosition(position.X, position.Y), holdAccess: inventory.Lots.Where(lot => lot.CarrierId == id)
                        .SelectMany(lot => lot.Holds ?? []).Distinct().ToArray()));
            }
            var deceased = world.Society.GetAgent(id);
            var deathTick = deceased.DeathTick ?? throw new InvalidDataException("A removed agent has no committed death.");
            // Society escrows ownership at death. Goods that had no separate
            // custodian or storage were carried by their owner; keep their
            // physical position at the deceased's tile after that owner is gone.
            var estate = world.Society.Estates.SingleOrDefault(item => item.DeceasedId == id);
            if (estate is not null)
            {
                var position = world.Agents[id].Position;
                foreach (var lot in world.Society.Inventory.Lots.Where(item => item.OwnerId == estate.Id &&
                             item.ContainerLotId is null && item.CarrierId is null &&
                             item.StorageBuildingId is null && item.GroundPosition is null).ToArray())
                    world.ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(inventory,
                        $"death:{id}:{lot.Id}", lot.Id, estate.Id, lot.Quantity,
                        groundPosition: new InventoryGroundPosition(position.X, position.Y), holdAccess: lot.Holds));
            }
            foreach (var moment in (world.Agents[id].IdentityMoments ?? [])
                         .Where(item => item.Outcome is "waiting" or "requested").ToArray())
                world.FinishIdentityMoment(id, moment.Kind, "interrupted");
            world.PhysicalAgents.RecordDeath(id, new DeceasedAgentState(
                id, deathTick, world.Society.AgeAt(deceased, deathTick),
                AgentStore.PrepareForDeath(world.Agents[id], null, null, null, world.Agents[id].Equipment is { } equipment
                        ? equipment with { OrnamentLotId = null } : null), world.TownForResident(id))
            { BoatIdAtDeath = world.RecordBoatDeath(id) });
            world.PhysicalAgents.RemovePhysicalPresence(id);
            world.RemoveTownResident(id);
            world.AppendEvent("agent_removed", id);
        }
    }
}
