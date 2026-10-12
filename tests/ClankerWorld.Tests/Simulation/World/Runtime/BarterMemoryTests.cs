using System.Collections.Concurrent;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NativeBarterMemoriesReachEachTraderAndContinueAfterModelReload(bool nativeBorn, bool recallFirst)
    {
        // Genuine births/adulthood use the existing calendar, food and lifespan
        // fixture. Keep participant IDs, ages, positions, kinship and skills.
        var state = WorldRuntimeCodec.Decode(await LaterAdult.Value);
        var checkpoint = state.Society.Society;
        var births = checkpoint.Births.OrderBy(birth => birth.CommittedTick).ToArray();
        var first = nativeBorn ? births[2].ChildId : checkpoint.Agents.First(person =>
            person.Id.StartsWith("founder:", StringComparison.Ordinal) && person.HouseholdId == "household:camp-beta").Id;
        var second = nativeBorn ? births[1].ChildId : checkpoint.Agents.First(person => person.Id != first &&
            person.Id.StartsWith("founder:", StringComparison.Ordinal) && person.HouseholdId == "household:camp-beta").Id;
        Assert.InRange(first.Length, 1, DerivedId.MaximumLength);
        Assert.InRange(second.Length, 1, DerivedId.MaximumLength);
        var beforeTrust = new[] { (Owner: first, Subject: second), (Owner: second, Subject: first) }
            .ToDictionary(pair => pair.Owner, pair => state.Agents.Single(person => person.AgentId == pair.Owner)
                .SocialStanding?.SingleOrDefault(item => item.SubjectId == pair.Subject)?.Trust ?? 0);
        // Explicit personal stock isolates recall. This does not claim an ornament
        // manufacture/wear/gift journey. The actual offer, consent, reservations,
        // travel, transfer and memories all come from the runtime.
        var inventory = InventoryAuthority.AddLot(checkpoint.Inventory, "barter-recall-gold", OrnamentContent.GoldOrnament, first, 1);
        inventory = InventoryAuthority.AddLot(inventory, "barter-recall-clothing", "clothing", second, 2);
        state = state with
        {
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with { Society = checkpoint with { Inventory = inventory } }
        };
        var firstName = checkpoint.GetAgent(first).Name;
        var secondName = checkpoint.GetAgent(second).Name;
        var offering = new NativeBarterChoices(true, secondName);
        var accepting = new NativeBarterChoices(false, firstName);
        using var world = WorldRuntime.Restore(state,
            id => id == first ? offering : id == second ? accepting : new QuietProvider());
        world.Resume();
        using var host = new NativeConversationCheckpointHost(world);
        for (var tick = 0; tick < 100 && !world.Society.Inventory.Offers.Any(offer => offer.FirstPartyId == first &&
                 offer.SecondPartyId == second && offer.State == DirectBarterState.Settled); tick++)
            await AdvanceNativeBarter(world, host);
        var offer = Assert.Single(world.Society.Inventory.Offers, item => item.FirstPartyId == first && item.SecondPartyId == second);
        Assert.Equal(DirectBarterState.Settled, offer.State);
        Assert.Equal(new[] { first, second }.Order(StringComparer.Ordinal), offer.AcceptedBy.Order(StringComparer.Ordinal));
        Assert.Contains(offering.Choices, text => text.StartsWith("Offer " + secondName + " one gold_ornament for one clothing;", StringComparison.Ordinal));
        Assert.Contains(accepting.Choices, text => text == "Accept exchange: give one clothing, receive one gold_ornament.");
        Assert.Equal((second, 1), (world.Society.Inventory.GetLot("barter-recall-gold").OwnerId,
            world.Society.Inventory.GetLot("barter-recall-gold").Quantity));
        var payment = Assert.Single(world.Society.Inventory.Lots, lot => lot.OwnerId == first && lot.ProvenanceLotId == "barter-recall-clothing");
        Assert.Equal(1, payment.Quantity);
        Assert.True(PersonalEquipmentRules.IsCarried(payment, first));
        Assert.True(PersonalEquipmentRules.IsCarried(world.Society.Inventory.GetLot("barter-recall-gold"), second));
        Assert.Equal(1, world.Society.Inventory.GetLot("barter-recall-clothing").Quantity);
        var memories = world.Society.Memories.Where(memory => memory.Id.StartsWith("trust-memory:", StringComparison.Ordinal) &&
            (memory.OwnerId == first && memory.SubjectId == second || memory.OwnerId == second && memory.SubjectId == first)).ToArray();
        Assert.Equal(2, memories.Length);
        Assert.Equal(2, memories.Select(memory => memory.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(memories[0].SourceTick, memories[1].SourceTick);
        foreach (var memory in memories)
        {
            Assert.Equal(SocietyMemoryKind.Relationship, memory.Kind);
            Assert.Equal("public", memory.Visibility);
            Assert.Equal("Completed a mutually accepted exchange with " + checkpoint.GetAgent(memory.SubjectId).Name + ".", memory.Summary);
            if (!nativeBorn) Assert.Equal("trust-memory:" + memory.OwnerId + ":" + memory.SubjectId, memory.Id);
            Assert.Equal(beforeTrust[memory.OwnerId] + 1, world.Agents.Single(person => person.AgentId == memory.OwnerId)
                .SocialStanding!.Single(item => item.SubjectId == memory.SubjectId).Trust);
        }
        world.CancelPendingModelCalls();
        var bytes = host.Checkpoint();
        var nextFirst = new NativeBarterChoices(true, secondName);
        var nextSecond = new NativeBarterChoices(false, firstName);
        var replayFirst = new NativeBarterChoices(true, secondName);
        var replaySecond = new NativeBarterChoices(false, firstName);
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == first ? nextFirst : id == second ? nextSecond : new QuietProvider());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == first ? replayFirst : id == second ? replaySecond : new QuietProvider());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
        using var reloadHost = new NativeConversationCheckpointHost(reload);
        using var replayHost = new NativeConversationCheckpointHost(replay);
        var owner = recallFirst ? first : second;
        var recorder = recallFirst ? nextFirst : nextSecond;
        var source = Assert.Single(memories, item => item.OwnerId == owner);
        for (var tick = 0; tick < 30 && !recorder.Observations.Any(item => item.WorldTick > source.SourceTick); tick++)
        {
            await AdvanceNativeBarter(reload, reloadHost);
            await AdvanceNativeBarter(replay, replayHost);
            Assert.Equal(reloadHost.Saved(), replayHost.Saved());
        }
        var observation = recorder.Observations.First(item => item.WorldTick > source.SourceTick);
        observation.Validate();
        var recalled = Assert.Single(observation.RetrievedMemories ?? [], item => item.Id == source.Id);
        Assert.Equal(owner, recalled.OwnerId);
        Assert.Equal(source.Summary, recalled.Summary);
        Assert.Equal(source.SourceTick, recalled.SourceTick);
        Assert.Equal(source.Visibility, recalled.Visibility);
        Assert.DoesNotContain(observation.RetrievedMemories ?? [], item => item.Id == memories.Single(memory => memory.OwnerId != owner).Id);
        Assert.InRange(source.Id.Length, 1, 128);
        for (var tick = 0; tick < 2; tick++)
        {
            await AdvanceNativeBarter(reload, reloadHost);
            await AdvanceNativeBarter(replay, replayHost);
            Assert.Equal(reloadHost.Saved(), replayHost.Saved());
        }
        Assert.Single(reload.Society.Inventory.Offers, item => item.FirstPartyId == first && item.SecondPartyId == second);
        foreach (var memory in memories)
        {
            Assert.Equal(memory, Assert.Single(reload.Society.Memories, item => item.Id == memory.Id));
            Assert.Equal(beforeTrust[memory.OwnerId] + 1, reload.Agents.Single(person => person.AgentId == memory.OwnerId)
                .SocialStanding!.Single(item => item.SubjectId == memory.SubjectId).Trust);
        }
        if (nativeBorn)
            await RepeatNativeBarterWithoutReplacingMemory(reload.ExportState(), first, second, payment.Id, beforeTrust, recallFirst);
    }

    private static async Task RepeatNativeBarterWithoutReplacingMemory(WorldRuntimeState state,
        string first, string second, string paymentId, Dictionary<string, int> beforeTrust, bool legacyKeys)
    {
        var checkpoint = state.Society.Society;
        // These are the memories earned above, not fabricated experience. Only
        // rebind their keys to the old saved format for the compatibility row.
        var savedMemories = checkpoint.Memories.Select(memory => legacyKeys &&
            memory.Id.StartsWith("trust-memory:", StringComparison.Ordinal) &&
            (memory.OwnerId == first && memory.SubjectId == second || memory.OwnerId == second && memory.SubjectId == first)
                ? memory with { Id = "trust-memory:" + memory.OwnerId + ":" + memory.SubjectId } : memory)
            .OrderBy(memory => memory.Id, StringComparer.Ordinal).ToArray();
        var previous = savedMemories.Where(memory => memory.Id.StartsWith("trust-memory:", StringComparison.Ordinal) &&
            (memory.OwnerId == first && memory.SubjectId == second || memory.OwnerId == second && memory.SubjectId == first)).ToArray();
        Assert.Equal(2, previous.Length);
        // Explicit fixture transfers return the actual exchanged units so the
        // same pair can offer another barter; retain the settled offer history.
        var inventory = InventoryAuthority.Transfer(checkpoint.Inventory, "repeat-barter-gold", second, first,
            "barter-recall-gold", 1, "fixture_return");
        inventory = InventoryAuthority.Transfer(inventory, "repeat-barter-payment", first, second, paymentId, 1, "fixture_return");
        // Place the prior settled offer beyond its proposal cooldown. This
        // controlled history timing isolates memory compatibility, not cooldown.
        inventory = inventory with
        {
            Offers = inventory.Offers.Select(offer =>
            offer.FirstPartyId == first && offer.SecondPartyId == second
                ? offer with { ExpiryTick = checkpoint.WorldTick - 121 } : offer).ToArray()
        };
        state = state with { Society = state.Society with { Society = checkpoint with { Inventory = inventory, Memories = savedMemories } } };
        var offering = new NativeBarterChoices(true, checkpoint.GetAgent(second).Name);
        var accepting = new NativeBarterChoices(false, checkpoint.GetAgent(first).Name);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == first ? offering : id == second ? accepting : new QuietProvider());
        using var host = new NativeConversationCheckpointHost(world);
        for (var tick = 0; tick < 30 && world.Society.Inventory.Offers.Count(offer => offer.FirstPartyId == first &&
                 offer.SecondPartyId == second && offer.State == DirectBarterState.Settled) < 2; tick++)
            await AdvanceNativeBarter(world, host);
        Assert.Equal(2, world.Society.Inventory.Offers.Count(offer => offer.FirstPartyId == first &&
            offer.SecondPartyId == second && offer.State == DirectBarterState.Settled));
        var retained = world.Society.Memories.Where(memory => memory.Id.StartsWith("trust-memory:", StringComparison.Ordinal) &&
            (memory.OwnerId == first && memory.SubjectId == second || memory.OwnerId == second && memory.SubjectId == first)).ToArray();
        Assert.Equal(2, retained.Length);
        foreach (var memory in previous)
        {
            Assert.Equal(memory, Assert.Single(retained, item => item.Id == memory.Id));
            Assert.Equal(beforeTrust[memory.OwnerId] + 2, world.Agents.Single(person => person.AgentId == memory.OwnerId)
                .SocialStanding!.Single(item => item.SubjectId == memory.SubjectId).Trust);
        }
    }

    private static async Task AdvanceNativeBarter(WorldRuntime world, NativeConversationCheckpointHost host)
    {
        await AwaitConversationRequestsAsync(world);
        await host.Advance();
        await AwaitConversationRequestsAsync(world);
        var bytes = host.Saved();
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
    }

    private sealed class NativeBarterChoices(bool first, string otherName) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ConcurrentQueue<AgentObservation> Observations { get; } = new();
        public ConcurrentQueue<string> Choices { get; } = new();
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            Observations.Enqueue(observation);
            var choice = first
                ? observation.Candidates.FirstOrDefault(item => item.Description == "Meet the other trader and wait for their answer.") ??
                    observation.Candidates.FirstOrDefault(item => item.Description.StartsWith("Offer " + otherName + " one gold_ornament for one clothing;", StringComparison.Ordinal))
                : observation.Candidates.FirstOrDefault(item => item.Description == "Accept exchange: give one clothing, receive one gold_ornament.");
            choice ??= observation.Candidates.Single(item => item.Id == "safe_idle");
            Choices.Enqueue(choice.Description);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId,
                Kind, ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                choice.Id, 1, observation.Candidates.ToDictionary(item => item.Id, item => item.Id == choice.Id ? 1d : 0d)));
        }
    }
}
