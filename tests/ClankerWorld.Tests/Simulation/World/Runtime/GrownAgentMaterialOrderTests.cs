using System.Collections.Concurrent;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    private static readonly Lazy<Task<IReadOnlyDictionary<int, byte[]>>> GatheringAdults = new(async () =>
    {
        var adults = new Dictionary<int, byte[]>();
        await CreateLaterAdultAsync(5, (generation, bytes) =>
        {
            if (generation >= 4) adults.Add(generation, bytes);
        });
        return adults;
    });

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public async Task NativeDescendantGatheringKeepsFullHarvestIdentityAndBoundedHostReceipts(int generation)
    {
        // The shared accelerated fixture performs actual parenting, births and adult growth.
        // Keep its native positions, inventory, resources, ancestry and complete identities.
        var state = WorldRuntimeCodec.Decode((await GatheringAdults.Value)[generation]);
        Assert.Equal(generation, state.Society.Society.Births.Count);
        var actor = state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).Last().ChildId;
        Assert.InRange(actor.Length, 1, DerivedId.MaximumLength);
        var choices = new NativeGatheringChoices();
        using var world = WorldRuntime.Restore(state, _ => choices);
        // A generated checkpoint can retain requested life reviews with no live task.
        // Reconcile them through a normal tick before checking prepared-tick rollback.
        world.Resume();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        world.Pause();
        using var host = new NativeGatheringHost(world);
        // Capture after pausing has cancelled live calls, then resume the original and its reload.
        var initial = host.Saved();
        var replayChoices = new NativeGatheringChoices();
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(initial), _ => replayChoices);
        Assert.Equal(initial, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var replayHost = new NativeGatheringHost(replay);
        var initialQuantity = world.WorldSystems.Ecology.GetResource("settlement-fiber").Quantity;
        var initialLots = world.Society.Inventory.Lots.Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        world.Resume();
        replay.Resume();
        var request = new OwnerInstructionRequest("native-descendant-gather", "owner:test", actor,
            OwnerInstructionKind.Order, "gather fiber from settlement-fiber");
        var submitted = world.SubmitInstruction(request);
        Assert.Equal(submitted.InstructionId, replay.SubmitInstruction(request).InstructionId);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var step = 0; step < 30 && Order().Status != "finished"; step++)
        {
            await host.Advance();
            await replayHost.Advance();
            Assert.Equal(host.Saved(), replayHost.Saved());
        }
        var order = Order();
        Assert.Equal(("gather_material", "finished", 1, "harvests"),
            (order.Action, order.Status, order.CompletedUnits, order.ProgressUnit));
        var harvested = Assert.Single(world.Society.Inventory.Lots, lot => !initialLots.Contains(lot.Id) && lot.ItemKind == "fiber");
        Assert.Equal(actor, harvested.OwnerId);
        Assert.EndsWith(":" + actor, harvested.Id, StringComparison.Ordinal);
        Assert.Equal(initialQuantity - 1, world.WorldSystems.Ecology.GetResource("settlement-fiber").Quantity);
        Assert.InRange(order.LastEffectId!.Length, 1, DerivedId.MaximumLength);
        Assert.Equal(DerivedId.Create("gather:" + harvested.Id, "gather:material:sha256:"), order.LastEffectId);
        Assert.Contains(choices.Selected, item => item.Actor == actor && item.Instruction == submitted.InstructionId &&
            item.Choice is "inspect_material_site" or "gather_material");
        Assert.Contains(replayChoices.Selected, item => item.Actor == actor && item.Instruction == submitted.InstructionId &&
            item.Choice is "inspect_material_site" or "gather_material");
        Assert.Single(world.ExportState().Events, item => item.Kind == "material_gathered" &&
            item.Detail.StartsWith(actor + ":fiber:", StringComparison.Ordinal));
        var completed = host.Saved();
        using var completedReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(completed));
        Assert.Equal(completed, WorldRuntimeCodec.Encode(completedReload.ExportState()));
        foreach (var invalid in new OrderSaveChange[]
{
    new OrderSaveChange(order, new Dictionary<string, object?> { ["LastEffectId"] = "gather:material:" + new string('x', 513) }),
    new OrderSaveChange(order, new Dictionary<string, object?> { ["LastEffectId"] = "gather:material:\0" }),
    new OrderSaveChange(order, new Dictionary<string, object?> { ["TargetMaterialKind"] = "cloth" }),
    new OrderSaveChange(order, new Dictionary<string, object?> { ["CompletedUnits"] = 0 }),
    new OrderSaveChange(order, new Dictionary<string, object?> { ["ProgressUnit"] = "food_items" })
})
            Assert.Throws<InvalidDataException>(() => OrderSaveMutation.Decode(world.ExportState(), invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.SubmitInstruction(request with
        { IdempotencyKey = new string('x', OwnerQueuedInstruction.MaximumIdentifierLength + 1) }));
        for (var step = 0; step < 3; step++)
        {
            await host.Advance();
            await replayHost.Advance();
            Assert.Equal(host.Saved(), replayHost.Saved());
        }
        Assert.Equal(order, Order());
        Assert.Equal(harvested.Quantity, world.Society.Inventory.GetLot(harvested.Id).Quantity);
        Assert.Equal(initialQuantity - 1, world.WorldSystems.Ecology.GetResource("settlement-fiber").Quantity);
        Assert.Single(world.ExportState().Events, item => item.Kind == "material_gathered" &&
            item.Detail.StartsWith(actor + ":fiber:", StringComparison.Ordinal));

        OrderTask Order() => world.ExportState().Instructions!.Single(item => item.InstructionId == submitted.InstructionId).Task!;
    }

    private sealed class NativeGatheringChoices : IDecisionProvider
    {
        public ConcurrentQueue<(string Actor, string? Instruction, string Choice)> Selected { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var choice = observation.Candidates.FirstOrDefault(item => item.Id is "inspect_material_site" or "gather_material")
                ?? observation.Candidates.FirstOrDefault(item => item.Id == "safe_idle")
                ?? observation.Candidates.Single(item => item.Id == "identity_optional");
            Selected.Enqueue((observation.AgentId, observation.OperativeOrderInstructionId, choice.Id));
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, 0,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, choice.Id, 1,
                new Dictionary<string, double> { [choice.Id] = 1 }));
        }
    }

    private sealed class NativeGatheringHost : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("native-descendant-gather-");
        private readonly WorldRuntime world;
        private readonly WorldStateFile file;
        private readonly WorldRuntimeService service;
        private readonly RecordingLogger<WorldRuntimeService> logger = new();
        public NativeGatheringHost(WorldRuntime world)
        {
            this.world = world;
            file = new(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("test-owner");
            service = new(world, file, presence, logger);
        }
        public byte[] Saved() => File.ReadAllBytes(file.Path);
        public async Task Advance()
        {
            await WaitForRequests(world);
            var field = typeof(WorldRuntime).GetField("pendingIdentityMoments",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var pending = (System.Collections.IDictionary)field.GetValue(world)!;
            await Task.WhenAll(pending.Values.Cast<object>().Select(item => (Task)item.GetType().GetProperty("Task")!.GetValue(item)!))
                .WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(await service.TryAdvanceOnceAsync(), string.Join("\n", logger.Messages.TakeLast(8)));
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), Saved());
        }
        public void Dispose() { service.Dispose(); directory.Delete(recursive: true); }
    }
}
