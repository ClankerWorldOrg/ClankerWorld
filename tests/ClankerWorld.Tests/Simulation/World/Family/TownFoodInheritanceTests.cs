using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class TownFoodInheritanceTests
{
    [Theory]
    [InlineData("eggs", null, true, false)]
    [InlineData("milk", null, true, false)]
    [InlineData("milk", "storage_pot", true, false)]
    [InlineData("cooked_eggs", null, true, false)]
    [InlineData("milk_porridge", "storage_pot", true, false)]
    [InlineData("rich_meal", "storage_pot", true, false)]
    [InlineData("berry_porridge", "storage_pot", true, false)]
    [InlineData("fresh_water", "water_jug", false, false)]
    [InlineData("wood", null, false, false)]
    [InlineData("cooked_eggs", null, true, true)]
    public async Task NativeTownBequestsKeepFoodWithHouseholdBeneficiariesAndVessels(string kind, string? vesselKind, bool household, bool holdWill)
    {
        const string deceased = NormalPathWorld.Founder1;
        var will = new PostDeathWillTests.WillProvider(CognitionWillContext.HeirsCandidateId, observation =>
            new CognitionWillChoice([observation.Will!.Heirs.Single(heir => heir.Key.StartsWith("will:town:", StringComparison.Ordinal)).Key],
                CognitionWillContext.EqualSplit), hold: holdWill, ignoreCancellation: holdWill);
        IDecisionProvider Provider(string id) => id == deceased ? will : new DeterministicDecisionProvider();
        using var generated = NormalPathWorld.CreateGenerated("new-food-town-will-audit", Provider);
        var state = generated.ExportState();
        var society = state.Society.Society;
        var lastDay = Assert.IsType<SocietyDayLifecycle>(society.Config.DayLifecycle).MaximumDay;
        var birth = society.LifeTickAt(society.WorldTick + 1) - lastDay * society.Config.TicksPerLifecycleAge;
        var inventory = society.Inventory with { Lots = society.Inventory.Lots.Where(lot => lot.OwnerId != deceased).ToArray() };
        if (vesselKind is not null) inventory = InventoryAuthority.AddLot(inventory, "will-vessel", vesselKind, deceased, 1);
        inventory = InventoryAuthority.AddLot(inventory, "will-content", kind, deceased, 2,
            containerLotId: vesselKind is not null ? "will-vessel" : null);
        society = society with
        {
            Inventory = inventory,
            Agents = society.Agents.Select(person => person.Id == deceased ? person with
            {
                BirthTick = society.LifeClock is null ? birth : person.BirthTick,
                BirthLifeTick = society.LifeClock is null ? null : birth,
                AgeBand = SocietyAgeBand.Elder,
                LastLifecycleYearChecked = lastDay - 1
            } : person).ToArray()
        };
        state = state with
        {
            Society = state.Society with { Society = society },
            Agents = state.Agents.Select(person => person.AgentId == deceased
                ? person with { Equipment = null } : person).ToArray()
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), Provider);
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        var estate = Assert.Single(world.Society.Estates);
        // Wait at the death checkpoint; polling with ticks can expire the will.
        var pendingWill = NativeWillFixture.PendingTask(world);
        var frozen = WorldRuntimeCodec.Encode(world.ExportState());
        var waiting = pendingWill;
        if (holdWill)
        {
            try
            {
                await ModelWorkHarness.WaitForSignalAsync(will.Called.Task);
                Assert.False(waiting.IsCompleted);
            }
            finally { will.ReleaseHeldResponse(); }
        }
        await ModelWorkHarness.WaitForSignalAsync(waiting);
        Assert.Equal(frozen, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Equal("pending", world.Society.GetEstate(estate.Id).WillStatus);
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        estate = world.Society.GetEstate(estate.Id);
        Assert.Equal("accepted", estate.WillStatus);
        Assert.Equal(TownBorderRules.FirstTownId, Assert.Single(estate.WillHeirIds!));
        Assert.All(estate.WillBequests!, bequest => Assert.Equal(TownBorderRules.FirstTownId, bequest.HeirId));
        Assert.Single(will.Observations);
        var saved = world.ExportState();
        // Bound only escrow duration after native death and personal will acceptance.
        // All stock, beneficiary selection and settlement remain native.
        saved = saved with
        {
            Society = saved.Society with
            {
                Society = saved.Society.Society with
                { Estates = saved.Society.Society.Estates.Select(item => item with { ExpiryTick = saved.Society.Society.WorldTick + 1 }).ToArray() }
            }
        };
        var bytes = WorldRuntimeCodec.Encode(saved);
        using var settling = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Provider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Provider);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(settling.ExportState()));
        Assert.False((await settling.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(settling.ExportState()));
        Assert.True((await settling.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(settling.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.True(settling.Society.GetEstate(estate.Id).Settled);
        var contents = settling.Society.Inventory.Lots.Where(lot => lot.Id == "will-content" || lot.ProvenanceLotId == "will-content").ToArray();
        Assert.Equal(2, contents.Sum(lot => lot.Quantity));
        Assert.All(contents, lot =>
        {
            Assert.Equal(kind, lot.ItemKind);
            Assert.True(lot.FreshnessBasisPoints > 0);
            if (household) Assert.Contains(lot.OwnerId, estate.BeneficiaryIds);
            else Assert.Equal(TownBorderRules.FirstTownId, lot.OwnerId);
            Assert.Equal(household ? null : "first-town-warehouse", lot.StorageBuildingId);
            Assert.Null(lot.CarrierId);
        });
        if (vesselKind is not null)
        {
            var vessel = settling.Society.Inventory.GetLot("will-vessel");
            Assert.Equal((vesselKind, 1), (vessel.ItemKind, vessel.Quantity));
            Assert.Equal(household ? null : "first-town-warehouse", vessel.StorageBuildingId);
            Assert.All(contents, lot => Assert.Equal((vessel.OwnerId, vessel.Id), (lot.OwnerId, lot.ContainerLotId)));
        }
        settling.Validate();
        replay.Validate();
        var final = WorldRuntimeCodec.Encode(settling.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final), Provider);
        Assert.Equal(final, WorldRuntimeCodec.Encode(restored.ExportState()));
        if (household)
        {
            var invalid = settling.ExportState();
            var stock = invalid.Society.Society.Inventory;
            stock = stock with
            {
                Lots = stock.Lots.Select(lot => contents.Any(content => content.Id == lot.Id) || lot.Id == "will-vessel"
                ? lot with { OwnerId = TownBorderRules.FirstTownId, StorageBuildingId = "first-town-warehouse", GroundPosition = null }
                : lot).ToArray()
            };
            invalid = invalid with { Society = invalid.Society with { Society = invalid.Society.Society with { Inventory = stock } } };
            Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(invalid, Provider));
        }
    }
}
