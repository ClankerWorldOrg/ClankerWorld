using OwnerInstructionReceipt = ClankerWorld.Simulation.World.OwnerInstructionReceipt;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class BrokenVesselProductionTests
{
    private const string Household = "household:first";
    private const string House = "first-town-house-a";
    private static readonly Lazy<Task<byte[]>> Generated = new(async () =>
    {
        using var world = NormalPathWorld.CreateGenerated("broken-vessel-cooking", _ => new Choices());
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        return WorldRuntimeCodec.Encode(world.ExportState());
    });

    [Theory]
    [InlineData("pot", true)]
    [InlineData("jug", true)]
    [InlineData("pot_removed", true)]
    [InlineData("jug_removed", true)]
    [InlineData("healthy", false)]
    [InlineData("pot", false)]
    public async Task BreadUsesHealthyIngredientsWhileLeavingBrokenVesselFamiliesUntouched(string damaged, bool alternative)
    {
        var state = WorldRuntimeCodec.Decode(await Generated.Value);
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == Household).Id;
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == House);
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => !(lot.OwnerId == Household && lot.StorageBuildingId == House &&
                lot.ItemKind is "flour" or "fresh_water" or "wood")).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "a-input-pot", InventoryContainerRules.StoragePot, Household, 1, storageBuildingId: House);
        inventory = InventoryAuthority.AddLot(inventory, "a-input-flour", "flour", Household, 2,
            storageBuildingId: House, containerLotId: "a-input-pot");
        inventory = InventoryAuthority.AddLot(inventory, "a-input-jug", "water_jug", Household, 1, storageBuildingId: House);
        inventory = InventoryAuthority.AddLot(inventory, "a-input-water", "fresh_water", Household, 1,
            storageBuildingId: House, containerLotId: "a-input-jug");
        inventory = InventoryAuthority.AddLot(inventory, "bread-wood", "wood", Household, 1, storageBuildingId: House);
        if (alternative && damaged.StartsWith("pot", StringComparison.Ordinal))
            inventory = InventoryAuthority.AddLot(inventory, "z-healthy-flour", "flour", Household, 2, storageBuildingId: House);
        if (alternative && damaged.StartsWith("jug", StringComparison.Ordinal))
        {
            inventory = InventoryAuthority.AddLot(inventory, "z-healthy-jug", "water_jug", Household, 1, storageBuildingId: House);
            inventory = InventoryAuthority.AddLot(inventory, "z-healthy-water", "fresh_water", Household, 1,
                storageBuildingId: House, containerLotId: "z-healthy-jug");
        }
        var brokenId = damaged.StartsWith("pot", StringComparison.Ordinal) ? "a-input-pot" : "a-input-jug";
        if (damaged != "healthy") inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => !damaged.EndsWith("_removed", StringComparison.Ordinal) ||
                    lot.Id != brokenId && lot.ContainerLotId != brokenId)
                .Select(lot => lot.Id == brokenId ? lot with { ConditionBasisPoints = 0 } : lot).ToArray(),
        };
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? house.Position : person.Position,
                FullnessBasisPoints = 10_000,
                LastDecisionContext = null,
            }).ToArray(),
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => new Choices());
        var receipt = world.SubmitInstruction(new("healthy-bread", "owner:test", actor, OwnerInstructionKind.Order, "make house bread"));
        var beforeTick = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(beforeTick, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = damaged == "pot" && alternative
            ? WorldRuntime.Restore(WorldRuntimeCodec.Decode(beforeTick), _ => new Choices()) : null;
        for (var tick = 0; tick < 65 && Order(world, receipt).Status != "finished"; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            if (replay is not null)
            {
                Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
                Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            }
        }
        var expected = alternative || damaged == "healthy" ? 2 : 0;
        Assert.Equal(expected, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "bread" && lot.OwnerId == Household).Sum(lot => lot.Quantity));
        Assert.Equal(expected > 0 ? "finished" : "blocked", Order(world, receipt).Status);
        if (expected > 0) Assert.Equal(WorldProductionJobState.Completed, Assert.Single(world.WorldSimulation.ProductionJobs).State);
        foreach (var original in inventory.Lots.Where(lot => lot.Id == brokenId || lot.ContainerLotId == brokenId))
            if (damaged != "healthy")
            {
                var kept = world.Society.Inventory.GetLot(original.Id);
                Assert.Equal((original.OwnerId, original.Quantity, original.ConditionBasisPoints, original.ContainerLotId, original.StorageBuildingId),
                    (kept.OwnerId, kept.Quantity, kept.ConditionBasisPoints, kept.ContainerLotId, kept.StorageBuildingId));
                Assert.DoesNotContain(world.Society.Inventory.Reservations, reservation => reservation.LotId == original.Id);
            }
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "build_rejected");
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new Choices());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        world.Validate();
    }

    private static OrderTask Order(WorldRuntime world, OwnerInstructionReceipt receipt) =>
        world.ExportState().Instructions!.Single(instruction => instruction.InstructionId == receipt.InstructionId).Task!;

    private sealed class Choices : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            request.Validate();
            var selected = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == "produce_item")?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
