using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class FarmIllnessWorkTests
{
    [Theory]
    [InlineData(FarmWorkKind.Till)]
    [InlineData(FarmWorkKind.Plant)]
    [InlineData(FarmWorkKind.Harvest)]
    public async Task SevereIllnessSlowsActualFieldStrokesAndWearWithoutLosingWorkAcrossReload(FarmWorkKind kind)
    {
        var setup = await PreparedWork(kind);
        using var healthy = FarmFieldTests.Restore(WithIllness(setup.State, setup.Actor, 0));
        using var ill = FarmFieldTests.Restore(WithIllness(setup.State, setup.Actor, 9_000));
        Start(healthy, setup, kind);
        Start(ill, setup, kind);
        var initialSeedReservationId = Assert.Single(ill.Fields).Work!.SeedReservationId;
        var toolId = kind == FarmWorkKind.Harvest ? "illness-sickle" : "carried-hoe";
        var originalCondition = ill.Society.Inventory.GetLot(toolId).ConditionBasisPoints;

        var checkpointTick = kind == FarmWorkKind.Plant ? 6 : 4;
        for (var tick = 0; tick < checkpointTick; tick++)
        {
            var before = ill.ExportState();
            var previousWork = Assert.Single(before.Fields!).Work!;
            var previousCondition = ill.Society.Inventory.GetLot(toolId).ConditionBasisPoints;
            Assert.True((await healthy.AdvanceOneTickAsync()).Advanced);
            Assert.True((await ill.AdvanceOneTickAsync()).Advanced);
            var person = ill.Agents.Single(person => person.AgentId == setup.Actor);
            Assert.InRange(person.Survival!.IllnessBasisPoints, 7_500, 10_000);
            if (!IllnessRules.AllowsWork(setup.Actor, ill.WorldTick, person.Survival.IllnessBasisPoints))
            {
                Assert.Equal(previousWork, Assert.Single(ill.Fields).Work);
                Assert.Equal(previousCondition, ill.Society.Inventory.GetLot(toolId).ConditionBasisPoints);
            }
        }

        Assert.Null(Assert.Single(healthy.Fields).Work);
        var delayed = Assert.Single(ill.Fields);
        Assert.NotNull(delayed.Work);
        Assert.True(delayed.Work.RemainingTicks > 0);
        if (kind == FarmWorkKind.Plant)
        {
            Assert.Equal(FarmWorkKind.Plant, delayed.Work.Kind);
            Assert.InRange(delayed.Work.RemainingTicks, 1, 3);
            Assert.Equal(initialSeedReservationId, delayed.Work.SeedReservationId);
            Assert.Equal(originalCondition, ill.Society.Inventory.GetLot(toolId).ConditionBasisPoints);
            Assert.Equal(2, ill.Society.Inventory.GetLot("illness-planting").Quantity);
            Assert.Equal(InventoryReservationState.Reserved,
                ill.Society.Inventory.GetReservation(initialSeedReservationId!).State);
        }
        else
        {
            Assert.True(ill.Society.Inventory.GetLot(toolId).ConditionBasisPoints >
                healthy.Society.Inventory.GetLot(toolId).ConditionBasisPoints);
        }

        var bytes = WorldRuntimeCodec.Encode(ill.ExportState());
        using var reloaded = FarmFieldTests.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        for (var tick = checkpointTick; tick < 16; tick++)
        {
            Assert.True((await ill.AdvanceOneTickAsync()).Advanced);
            Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(ill.ExportState()),
                WorldRuntimeCodec.Encode(reloaded.ExportState()));
        }
        Assert.Null(Assert.Single(reloaded.Fields).Work);
        Assert.Contains(reloaded.ExportState().Events, item => item.Kind == CompletionEvent(kind) &&
            item.Detail.StartsWith(setup.Actor + ":", StringComparison.Ordinal));
        Assert.Equal(healthy.Society.Inventory.GetLot(toolId).ConditionBasisPoints,
            reloaded.Society.Inventory.GetLot(toolId).ConditionBasisPoints);
        if (kind == FarmWorkKind.Plant)
        {
            Assert.Equal(1, reloaded.Society.Inventory.GetLot("illness-planting").Quantity);
            var receipt = Assert.Single(reloaded.Society.Inventory.Reservations,
                item => item.Purpose == "field_planting" && item.LotId == "illness-planting");
            Assert.Equal(initialSeedReservationId, receipt.Id);
            Assert.Equal((setup.Actor, "illness-planting", 1, InventoryReservationState.Completed),
                (receipt.OwnerId, receipt.LotId, receipt.Quantity, receipt.State));
            Assert.Single(reloaded.Society.Inventory.Events,
                item => item.Kind == "reservation_consumed" && item.Detail == receipt.Id);
            Assert.Single(reloaded.ExportState().Events, item => item.Kind == "field_planted" &&
                item.Detail.StartsWith(setup.Actor + ":", StringComparison.Ordinal));
            using var restored = Reload(reloaded);
            Assert.Equal(WorldRuntimeCodec.Encode(reloaded.ExportState()),
                WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        if (kind == FarmWorkKind.Harvest)
        {
            var field = Assert.Single(reloaded.Fields);
            Assert.Equal((FarmFieldStage.Harvested, 1), (field.Stage, field.Cycle));
            Assert.Contains(reloaded.Society.Inventory.Lots, lot => lot.ItemKind == FarmFieldRules.Grain &&
                lot.OwnerId == setup.Household && lot.GroundPosition == new InventoryGroundPosition(setup.Point.X, setup.Point.Y));
            var planting = reloaded.Society.Inventory.GetReservation(field.ReplantingReservationId!);
            Assert.Equal((setup.Household, 1, InventoryReservationState.Reserved),
                (planting.OwnerId, planting.Quantity, planting.State));
        }
    }

    private sealed record Setup(WorldRuntimeState State, string Actor, string Household, GridPoint Point);

    private static async Task<Setup> PreparedWork(FarmWorkKind kind)
    {
        var (state, actor, household, point) = kind == FarmWorkKind.Harvest
            ? await FarmFieldTests.ReadyFarmer("illness-field-" + kind)
            : FarmFieldTests.PreparedFarmer("illness-field-" + kind);
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Clear);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "illness-planting", FarmFieldRules.GrainSeed, actor, 2);
        inventory = InventoryAuthority.AddLot(inventory, "illness-sickle", "wooden_sickle", actor, 1);
        using var world = FarmFieldTests.Restore(FarmFieldTests.WithInventory(state, inventory));
        if (kind == FarmWorkKind.Plant)
        {
            Assert.True(world.StartFieldWork(actor, point, FarmWorkKind.Till).Accepted);
            await Advance(world, 4);
        }
        return new(world.ExportState(), actor, household, point);
    }

    private static WorldRuntimeState WithIllness(WorldRuntimeState state, string actor, int illness) => state with
    {
        Survival = state.Survival ?? new SurvivalState(0, []),
        Agents = state.Agents.Select(person => person.AgentId == actor ? person with
        {
            Survival = (person.Survival ?? new SurvivalCondition()) with { WarmthBasisPoints = 10_000, IllnessBasisPoints = illness },
        } : person).ToArray(),
    };

    private static void Start(WorldRuntime world, Setup setup, FarmWorkKind kind) => Assert.True(
        world.StartFieldWork(setup.Actor, setup.Point, kind,
            kind == FarmWorkKind.Plant ? FarmFieldRules.Grain : null,
            kind == FarmWorkKind.Plant ? "illness-planting" : null).Accepted);

    private static string CompletionEvent(FarmWorkKind kind) => kind switch
    {
        FarmWorkKind.Till => "field_prepared",
        FarmWorkKind.Plant => "field_planted",
        _ => "field_harvested",
    };

    private static async Task Advance(WorldRuntime world, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        world.Validate();
    }

    private static WorldRuntime Reload(WorldRuntime world) =>
        FarmFieldTests.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
}
