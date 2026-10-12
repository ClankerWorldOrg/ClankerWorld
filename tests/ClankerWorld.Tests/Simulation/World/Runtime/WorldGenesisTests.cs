using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldGenesisTests
{
    [Theory]
    [InlineData(WorldStartPace.Legacy)]
    [InlineData(WorldStartPace.PlacedFounders)]
    [InlineData(WorldStartPace.FounderSetup)]
    public async Task NewWorldsStartWithoutMoneyOrASeededTheftFineAndReplayThatState(WorldStartPace pace)
    {
        Func<string, IDecisionProvider> providers = _ => new ActionCoverageRecorder(chooseIdle: true);
        using var world = pace == WorldStartPace.FounderSetup
            ? NormalPathWorld.CreateGenerated("no-starting-money", providers)
            : new WorldRuntime("no-starting-money", providers, startPace: pace);
        world.Validate();
        AssertNoStartingMoneyOrFine(world);
        if (world.Society.IsPaused) world.Resume();

        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), providers);
        restored.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        AssertNoStartingMoneyOrFine(restored);

        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()),
                WorldRuntimeCodec.Encode(restored.ExportState()));
            AssertNoStartingMoneyOrFine(world);
        }
    }

    [Fact]
    public async Task CurrentFormatLoadPreservesSavedCurrencyAndFactionLawInsteadOfApplyingGenesis()
    {
        Func<string, IDecisionProvider> providers = _ => new ActionCoverageRecorder(chooseIdle: true);
        using var world = new WorldRuntime("saved-money", providers);
        var state = world.ExportState();
        var systems = state.WorldSystems!;
        var saved = state with
        {
            WorldSystems = systems with
            {
                Currency = new CurrencyState(
                    [new CurrencyDefinition("copper", "Copper", "cp")],
                    [new CurrencyAccount("saved-wallet", state.Society.Society.Households[0].Id, "copper", 37)], []),
                Factions = systems.Factions with
                {
                    Laws = [new LawRule("saved-law", systems.Factions.Factions[0].Id,
                        LawActionKind.Theft, LawSeverity.Major, 500, 12)],
                },
            },
        };
        var bytes = WorldRuntimeCodec.Encode(saved);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), providers);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(37, restored.WorldSystems.Currency.GetAccount("saved-wallet").BalanceMinorUnits);
            Assert.Equal(12, Assert.Single(restored.WorldSystems.Factions.Laws).FineMinorUnits);
        }
    }

    private static void AssertNoStartingMoneyOrFine(WorldRuntime world)
    {
        Assert.Empty(world.WorldSystems.Currency.Currencies);
        Assert.Empty(world.WorldSystems.Currency.Accounts);
        Assert.Empty(world.WorldSystems.Currency.Transfers);
        Assert.Empty(world.WorldSystems.Factions.Laws);
        Assert.Empty(world.WorldSystems.Factions.Violations);
    }
}
