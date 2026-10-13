using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData("care", 2, true)]
    [InlineData("care", 1, true)]
    [InlineData("care", 2, false)]
    [InlineData("saddle", 4, true)]
    [InlineData("saddle", 3, true)]
    [InlineData("saddle", 4, false)]
    public async Task AnimalTaskPickupPreservesTheLoadedRouteToItsAnimal(string action, int stones, bool swim)
    {
        var (state, actor, home, yard) = CreateYard("cart-set-unfinished");
        using (var setup = WorldRuntime.Restore(state, _ => new AnimalChooser()))
        {
            Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
            state = setup.ExportState();
        }
        var source = swim ? new GridPoint(105, 49) : state.Map.FootNeighbors(yard.Position)
            .First(point => state.Map.IsBuildable(point) && state.Agents.All(person => person.Position != point));
        Assert.Equal(!swim, state.Map.IsReachableOnFoot(source, yard.Position));
        Assert.True(SwimmingRules.IsReachable(state.Map, source, yard.Position));
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryAuthority.AddLot(inventory, "animal-task-ballast", "stone", actor, stones);
        var rootId = action == "care" ? "animal-task-jug" : "animal-task-saddle";
        inventory = InventoryAuthority.AddLot(inventory, rootId, action == "care" ? "water_jug" : "saddle", home, 1,
            groundPosition: new(source.X, source.Y));
        if (action == "care")
        {
            inventory = InventoryAuthority.AddLot(inventory, "animal-task-water", "fresh_water", home, 2,
                containerLotId: rootId);
            inventory = InventoryAuthority.AddLot(inventory, "animal-task-feed", "grain", home, 2,
                storageBuildingId: yard.InstanceId);
        }
        var day = state.WorldSystems!.Config.TicksPerDay;
        var animal = new AnimalState("animal-task-target", "Moss", action == "care" ? "cow" : "horse", "female",
            -7L * day, yard.Position, WorldRuntime.AnimalSharedRules.AnimalHerdId(home), home, yard.InstanceId,
            CareUntilTick: action == "care" ? 0 : state.Society.Society.WorldTick + day);
        state = At(state, actor, source, inventory, [animal]) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = At(state, actor, source, inventory, [animal]).Agents.Select(person => person with
            { Survival = new(), Equipment = null }).ToArray(),
        };
        var blocked = swim && stones + (action == "care" ? 3 : 1) > SwimmingRules.MaximumCarriedUnits;
        var pickupId = WorldRuntime.AnimalSharedRules.AnimalSupplyId(rootId, yard.InstanceId, animal.Id, action);
        var tripId = WorldRuntime.AnimalSharedRules.AnimalSupplyTripId(actor);
        var actionId = WorldRuntime.AnimalSharedRules.AnimalActionId(action, animal.Id, null);
        var policy = new MarketRulesPolicy
        {
            Choose = (id, candidates) => id != actor ? null :
                candidates.FirstOrDefault(candidate => candidate.Id == pickupId) ??
                (blocked ? null : candidates.FirstOrDefault(candidate => candidate.Id == tripId) ??
                    candidates.FirstOrDefault(candidate => candidate.Id == actionId)),
        };
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), policy.CreateProvider);
        var finishedEvent = action == "care" ? "animal_cared" : "animal_saddled";
        if (blocked)
        {
            for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(policy.OfferedTo(actor), candidate => candidate.Id == pickupId);
            var root = world.Society.Inventory.GetLot(rootId);
            Assert.Equal((home, 1, (string?)null, new InventoryGroundPosition(source.X, source.Y)),
                (root.OwnerId, root.Quantity, root.CarrierId, root.GroundPosition));
            Assert.Empty(world.ExportState().AnimalWorld!.SupplyTrips);
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == finishedEvent);
        }
        else
        {
            await Until(world, () => world.ExportState().AnimalWorld!.SupplyTrips.Any(trip => trip.LotId == rootId));
            Assert.Contains(policy.OfferedTo(actor), candidate => candidate.Id == pickupId);
            var active = WorldRuntimeCodec.Encode(world.ExportState());
            using var resumed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(active), policy.CreateProvider);
            Assert.Equal(active, WorldRuntimeCodec.Encode(resumed.ExportState()));
            await Until(resumed, () => resumed.ExportState().Events.Any(item => item.Kind == finishedEvent));
            Assert.Single(resumed.ExportState().Events, item => item.Kind == finishedEvent);
            Assert.Equal(home, resumed.Society.Inventory.GetLot(rootId).OwnerId);
            Assert.Equal(stones, resumed.Society.Inventory.GetLot("animal-task-ballast").Quantity);
            if (action == "care")
            {
                Assert.Equal(0, resumed.Society.Inventory.Lots.Where(lot => lot.ItemKind == "grain" && lot.OwnerId == home)
                    .Sum(lot => lot.Quantity));
                Assert.Equal(0, resumed.Society.Inventory.Lots.Where(lot => lot.ItemKind == "fresh_water" && lot.OwnerId == home)
                    .Sum(lot => lot.Quantity));
                Assert.True(AnimalRules.HasCare(Assert.Single(resumed.Animals), resumed.WorldTick));
            }
            else Assert.Equal(rootId, Assert.Single(resumed.Animals).SaddleLotId);
            var completed = WorldRuntimeCodec.Encode(resumed.ExportState());
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(completed), _ => new AnimalChooser());
            Assert.Equal(completed, WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), policy.CreateProvider);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        Assert.Equal(stones, world.Society.Inventory.GetLot("animal-task-ballast").Quantity);
        world.Validate();
    }
}
