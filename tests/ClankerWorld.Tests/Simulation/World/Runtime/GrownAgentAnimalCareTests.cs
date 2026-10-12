using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    public async Task NativeDescendantCareKeepsFullActorAndPhysicalSupplyThroughHostReload(int generation, bool carried)
    {
        var state = WorldRuntimeCodec.Decode((await GatheringAdults.Value)[generation]);
        var actor = state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).Last().ChildId;
        Assert.Equal(generation, state.Society.Society.Births.Count);
        Assert.InRange(actor.Length, 1, DerivedId.MaximumLength);
        var home = state.Society.Society.GetAgent(actor).HouseholdId!;
        var position = state.Agents.Single(person => person.AgentId == actor).Position;
        var house = state.WorldSimulation!.Buildings.Single(building => building.HouseholdId == home &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
        // Explicit stock/animal preparation; the yard is built and paid for through the public action.
        // Native identities, ancestry, ages and positions remain unchanged.
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "native-care-wood", "wood", home, 8, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "native-care-rope", "rope", home, 2, storageBuildingId: house.InstanceId);
        var wood = inventory.Lots.Where(lot => lot.OwnerId == home && lot.ItemKind == "wood").Sum(lot => lot.Quantity);
        var rope = inventory.Lots.Where(lot => lot.OwnerId == home && lot.ItemKind == "rope").Sum(lot => lot.Quantity);
        using var setup = WorldRuntime.Restore(state with
        { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } });
        var yardDefinition = setup.WorldContent.Buildings.Single(definition => definition.Tags.Contains("animal-yard"));
        foreach (var tile in state.Map.Tiles.OrderBy(tile => state.Map.FootDistance(tile.Position, position)))
            if (setup.PlaceBuilding("native-care-yard", yardDefinition.CanonicalId, tile.Position, home).Applied) break;
        var yard = Assert.Single(setup.WorldSimulation.Buildings, building => building.InstanceId == "native-care-yard");
        Assert.Equal(wood - 8, setup.Society.Inventory.Lots.Where(lot => lot.OwnerId == home && lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.Equal(rope - 2, setup.Society.Inventory.Lots.Where(lot => lot.OwnerId == home && lot.ItemKind == "rope").Sum(lot => lot.Quantity));
        state = setup.ExportState();
        Assert.Equal(position, state.Agents.Single(person => person.AgentId == actor).Position);
        inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "native-care-feed", "grain", home, 6, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "native-care-jug", "water_jug", home, 1, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "native-care-water", "fresh_water", home, 2, storageBuildingId: house.InstanceId, containerLotId: "native-care-jug");
        if (carried)
        {
            inventory = InventoryAuthority.Relocate(inventory, "prepared-carried-feed", "native-care-feed", home, 1, carrierId: actor);
            inventory = InventoryAuthority.Relocate(inventory, "prepared-carried-jug", "native-care-jug", home, 1, carrierId: actor);
        }
        var chicken = new AnimalState("native-finch", "Finch", "chicken", "female",
            state.Society.Society.WorldTick - (long)AnimalRules.Definition("chicken").AdultDays * state.WorldSystems!.Config.TicksPerDay,
            yard.Position, "household:" + home, home, yard.InstanceId);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            AnimalWorld = new(true, [chicken], []),
        };
        var encoded = WorldRuntimeCodec.Encode(state);
        var choices = new NativeCareChoices(actor);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded), _ => choices);
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(world.ExportState()));
        // Reconcile orphaned life reviews through a real tick before capturing rollback state.
        world.Resume();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        world.Pause();
        using var host = new NativeGatheringHost(world);
        var initial = host.Saved();
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(initial), _ => new NativeCareChoices(actor));
        using var replayHost = new NativeGatheringHost(replay);
        world.Resume(); replay.Resume();
        var request = new OwnerInstructionRequest("native-care", "owner:test", actor, OwnerInstructionKind.Order, "care for Finch");
        var instruction = world.SubmitInstruction(request);
        Assert.Equal(instruction.InstructionId, replay.SubmitInstruction(request).InstructionId);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        var sawTrip = false;
        for (var tick = 0; tick < 60 && Order().Status != "finished"; tick++)
        {
            await host.Advance(); await replayHost.Advance();
            Assert.Equal(host.Saved(), replayHost.Saved());
            if (world.ExportState().AnimalWorld.SupplyTrips.FirstOrDefault(trip => trip.ActorId == actor) is not { } trip || sawTrip) continue;
            Assert.False(carried);
            sawTrip = true;
            var current = world.ExportState();
            Assert.Equal(actor, trip.ActorId);
            Assert.Equal(actor, current.Society.Society.Inventory.GetLot(trip.LotId).CarrierId);
            Assert.Equal((chicken.Id, "care", yard.InstanceId), (trip.AnimalId, trip.Action, trip.YardId));
            using var tripReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(host.Saved()), _ => new NativeCareChoices(actor));
            Assert.Equal(host.Saved(), WorldRuntimeCodec.Encode(tripReload.ExportState()));
            foreach (var invalid in new[] { trip with { ActorId = actor + "-missing" }, trip with { ActorId = actor + "\0" }, trip with { LotId = "missing" } })
                Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(current with
                { AnimalWorld = current.AnimalWorld with { SupplyTrips = [invalid] } }));
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(current with
            { AnimalWorld = current.AnimalWorld with { SupplyTrips = [trip, trip] } }));
        }
        Assert.Equal(!carried, sawTrip);
        Assert.Equal(("animal_care", "finished", 1), (Order().Action, Order().Status, Order().CompletedUnits));
        Assert.True(world.Animals.Single(animal => animal.Id == chicken.Id).CareUntilTick > world.WorldTick);
        Assert.Empty(world.ExportState().AnimalWorld.SupplyTrips);
        Assert.Equal(5, world.Society.Inventory.Lots.Where(lot => lot.Id.StartsWith("native-care-feed", StringComparison.Ordinal)).Sum(lot => lot.Quantity));
        Assert.Equal(1, world.Society.Inventory.GetLot("native-care-water").Quantity);
        Assert.Equal((home, actor), (world.Society.Inventory.GetLot("native-care-jug").OwnerId, world.Society.Inventory.GetLot("native-care-jug").CarrierId));
        var completed = host.Saved();
        using var strict = WorldRuntime.Restore(WorldRuntimeCodec.Decode(completed));
        Assert.Equal(completed, WorldRuntimeCodec.Encode(strict.ExportState()));
        var permitted = world.ExportState();
        var permission = chicken with { CareUntilTick = world.Animals.Single(animal => animal.Id == chicken.Id).CareUntilTick, CarePermissions = [actor] };
        if (!carried)
        {
            var permittedBytes = WorldRuntimeCodec.Encode(permitted with { AnimalWorld = permitted.AnimalWorld with { Animals = [permission] } });
            using var permissionReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(permittedBytes));
            Assert.Equal(permittedBytes, WorldRuntimeCodec.Encode(permissionReload.ExportState()));
        }
        foreach (var invalid in new[] { permission with { CarePermissions = [actor, actor] }, permission with { CarePermissions = [actor + "-missing"] },
                     permission with { CarePermissions = [actor + "\0"] }, permission with { Id = new string('x', 513) }, permission with { Name = new string('x', 513) } })
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(permitted with { AnimalWorld = permitted.AnimalWorld with { Animals = [invalid] } }));
        for (var tick = 0; tick < 3; tick++)
        {
            await host.Advance(); await replayHost.Advance();
            Assert.Equal(host.Saved(), replayHost.Saved());
        }
        Assert.Equal(1, Order().CompletedUnits);
        Assert.Equal(5, world.Society.Inventory.Lots.Where(lot => lot.Id.StartsWith("native-care-feed", StringComparison.Ordinal)).Sum(lot => lot.Quantity));

        OrderTask Order() => world.ExportState().Instructions!.Single(item => item.InstructionId == instruction.InstructionId).Task!;
    }

    private sealed class NativeCareChoices(string actor) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var choice = observation.AgentId == actor ? observation.Candidates.FirstOrDefault(item => item.Id == "animal_order") : null;
            choice ??= observation.Candidates.FirstOrDefault(item => item.Id == "safe_idle") ?? observation.Candidates.Single(item => item.Id == "identity_optional");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, 0,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, choice.Id, 1,
                new Dictionary<string, double> { [choice.Id] = 1 }));
        }
    }
}
