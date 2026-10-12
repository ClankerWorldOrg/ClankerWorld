using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

public sealed partial class ShopTradeTests
{
    [Theory]
    [InlineData(48)]
    [InlineData(49)]
    [InlineData(512)]
    public async Task ImportedResourceIsInheritedThroughItsOfferedItemKey(int length)
    {
        var (state, heir, deceased, shopId) = CreateShopState("store");
        var kind = new string('x', length);
        state = await ContentIdFixture.Produce(state, shopId, deceased, kind);
        var produced = Assert.Single(state.Society.Society.Inventory.Lots, lot => lot.ItemKind == kind);
        // Controlled transfer of the actual produced unit, then arrange age for a native natural death.
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "mod-will-gift",
            produced.OwnerId, deceased, produced.Id, 1, "gift");
        state = WithInventory(state, inventory);
        var checkpoint = state.Society.Society;
        var lastDay = Assert.IsType<SocietyDayLifecycle>(checkpoint.Config.DayLifecycle).MaximumDay;
        var birth = checkpoint.LifeTickAt(checkpoint.WorldTick + 1) - lastDay * checkpoint.Config.TicksPerLifecycleAge;
        checkpoint = checkpoint with
        {
            Agents = checkpoint.Agents.Select(person => person.Id == deceased ? person with
            {
                BirthTick = checkpoint.LifeClock is null ? birth : person.BirthTick,
                BirthLifeTick = checkpoint.LifeClock is null ? null : birth,
                AgeBand = SocietyAgeBand.Elder,
                LastLifecycleYearChecked = lastDay - 1,
            } : person).ToArray(),
        };
        var provider = new ModWillProvider(heir);
        using var world = WorldRuntime.Restore(state with { Society = state.Society with { Society = checkpoint } },
            actor => actor == deceased ? provider : new ContentIdFixture.Idle());
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        if (world.Society.Estates.Single(item => item.DeceasedId == deceased).WillStatus == "pending")
            await provider.Called.Task.WaitAsync(TimeSpan.FromSeconds(10));
        for (var tick = 0; tick < 12 && !world.Society.Estates.Any(estate => estate.DeceasedId == deceased && estate.WillStatus == "accepted"); tick++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        var estate = Assert.Single(world.Society.Estates, item => item.DeceasedId == deceased);
        Assert.Equal("accepted", estate.WillStatus);
        Assert.Equal("Keep tending the orchard.", estate.FinalWords);
        Assert.Equal(1, provider.Calls);
        Assert.All(provider.Items!, item => Assert.InRange(item.Kind.Length, 1, 48));
        Assert.Contains(estate.FrozenLots!, lot => lot.LotId == produced.Id && lot.ItemKind == kind);
        Assert.Contains(estate.WillBequests!, bequest => bequest.LotId == produced.Id && bequest.HeirId == heir && bequest.Quantity == 1);
        world.Pause();
        var saved = ContentIdFixture.Saved(world);
        // Only shorten the real estate's settlement clock; its native frozen lots and admitted bequests stay intact.
        saved = saved with
        {
            Society = saved.Society with
            {
                Society = saved.Society.Society with
                { Estates = saved.Society.Society.Estates.Select(item => item.Id == estate.Id ? item with { ExpiryTick = world.WorldTick + 1 } : item).ToArray() }
            }
        };
        saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(saved));
        using var continuing = WorldRuntime.Restore(saved, _ => new ContentIdFixture.Idle(DecisionProviderKind.Deterministic));
        using var reloaded = WorldRuntime.Restore(saved, _ => new ContentIdFixture.Idle(DecisionProviderKind.Deterministic));
        continuing.Resume();
        reloaded.Resume();
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await continuing.AdvanceOneTickAsync()).Advanced);
            Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(continuing.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
        }
        Assert.Contains(reloaded.Society.Inventory.Lots, lot => lot.ItemKind == kind && lot.OwnerId == heir && lot.ProvenanceLotId == produced.Id);
    }

    private sealed class ModWillProvider(string heirId) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public int Calls { get; private set; }
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<CognitionWillItem>? Items { get; private set; }
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            request.Observation.Validate();
            var context = Assert.IsType<CognitionWillContext>(request.Observation.Will);
            Items = context.Items;
            Calls++;
            Called.TrySetResult();
            var heir = Assert.Single(context.Heirs, item => item.Key == "will:heir:" + heirId).Key;
            // The production lot is the only x-prefixed good. Reply with its offered key, never its kind or lot ID.
            var item = Assert.Single(context.Items, item => item.Kind.StartsWith('x'));
            var choice = new CognitionWillChoice([heir], CognitionWillContext.ItemSplit,
                new Dictionary<string, string> { [item.Key] = heir }, "Keep tending the orchard.");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, CognitionWillContext.HeirsCandidateId, 1,
                new Dictionary<string, double> { [CognitionWillContext.HeirsCandidateId] = 1 }, Will: choice));
        }
    }
}
