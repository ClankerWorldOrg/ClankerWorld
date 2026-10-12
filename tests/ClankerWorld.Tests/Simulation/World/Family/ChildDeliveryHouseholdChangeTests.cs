using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class ChildDeliveryHouseholdChangeTests
{
    [Fact]
    public async Task GuardianCheckpointWaitsForHeldIdentityInputBeforeContinuing()
    {
        var scenario = GuardianPlacementTestFixture.Prepared(1, 0);
        var choices = new DeliveryChoices(scenario.Children[0], scenario.Guardians[0], guardian: true)
        {
            IdentityResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        using var world = WorldRuntime.Restore(scenario.State, _ => choices);
        using var host = new CheckpointHost(world, choices);
        var replayChoices = choices.Copy();
        using var replay = WorldRuntime.Restore(scenario.State, _ => replayChoices);
        using var replayHost = new CheckpointHost(replay, replayChoices);
        var step = ReachIdentityCheckpoint(host);
        try
        {
            await choices.IdentityEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.ThrowsAsync<TimeoutException>(() => step.WaitAsync(TimeSpan.FromSeconds(1)));
            await ReachIdentityCheckpoint(replayHost);
            var requested = world.Agents.SelectMany(person => (person.IdentityMoments ?? [])
                .Where(moment => moment.Outcome == "requested").Select(moment => (person.AgentId, moment.Kind))).ToArray();
            Assert.NotEmpty(requested);
            choices.IdentityResponseGate.SetResult();
            await step.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            await host.Step();
            await replayHost.Step();
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_identity_moment_kept");
            Assert.All(requested, item => Assert.Equal("kept", world.Agents.Single(person => person.AgentId == item.AgentId)
                .IdentityMoments!.Single(moment => moment.Kind == item.Kind).Outcome));
        }
        finally
        {
            choices.IdentityResponseGate.TrySetResult();
            await step.WaitAsync(TimeSpan.FromSeconds(30));
        }

        static async Task ReachIdentityCheckpoint(CheckpointHost checkpointHost)
        {
            for (var tick = 0; tick < 20; tick++)
            {
                await checkpointHost.Step();
                if (checkpointHost.World.Agents.SelectMany(person => person.IdentityMoments ?? [])
                    .Any(moment => moment.Outcome == "requested")) return;
            }
            Assert.Fail("The native host never requested an identity input.");
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task CaregiverDeparturePreservesChildCargoAndNativeCheckpoints(bool leave, bool deliverFirst)
    {
        var (state, child, house, source) = await ChildHouseholdHelpingTests.Prepared("wood");
        var household = state.Society.Society.GetAgent(child).HouseholdId!;
        var caregiver = state.Society.Society.GetAgent(child).PrimaryCaregiverId!;
        state = AddStock(state, child, household, house, source.Position);
        var choices = new DeliveryChoices(child, caregiver);
        using var world = WorldRuntime.Restore(state, _ => choices);
        using var host = new CheckpointHost(world, choices);
        await host.Until(() => world.Society.Inventory.GetLot("child-moving-wood").DeliveryBuildingId == house);
        Assert.Equal(child, world.Society.Inventory.GetLot("child-moving-wood").OwnerId);
        Assert.Contains((child, "child_carry:child-moving-wood"), choices.Selected);
        if (deliverFirst)
        {
            choices.Deliver = true;
            Refresh(world, child);
            await host.Until(() => world.ExportState().Events.Any(item => item.Kind == "child_delivered_household"));
        }
        choices.ChangeHousehold = leave;
        choices.Deliver = false;
        Refresh(world, child);
        Refresh(world, caregiver);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var replayChoices = choices.Copy();
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => replayChoices);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var replayHost = new CheckpointHost(replay, replayChoices);
        using var continuing = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => choices);
        using var continuingHost = new CheckpointHost(continuing, choices);
        await ContinuePair(continuingHost, replayHost, () => !leave || continuing.Society.GetAgent(child).HouseholdId is null);
        Assert.Equal(leave ? null : household, continuing.Society.GetAgent(child).HouseholdId);
        Assert.Equal(leave ? null : household, continuing.Society.GetAgent(caregiver).HouseholdId);
        if (leave) Assert.Contains((caregiver, "household_leave"), choices.Selected);
        AssertCargo(continuing, child, household, house, deliverFirst);
        Assert.Equal(leave ? 1 : 0, continuing.ExportState().Events.Count(item => item.Kind == "household_left"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task GuardianEscortPreservesFormerHouseholdCargoAndNativeCheckpoints(bool pickup, bool deliverFirst)
    {
        var scenario = GuardianPlacementTestFixture.Prepared(1, 0);
        var child = scenario.Children[0];
        var caregiver = scenario.Guardians[0];
        var state = scenario.State;
        var society = state.Society.Society;
        // Retain the real birth and recorded parental deaths, controlling only school age and pickup arrival.
        var age = Assert.IsType<SocietyDayLifecycle>(society.Config.DayLifecycle).ChildStartDay;
        var born = society.LifeTickAt(society.WorldTick) - age * society.Config.TicksPerLifecycleAge;
        society = society with
        {
            Agents = society.Agents.Select(person => person.Id == child ? person with
            {
                BirthTick = born,
                BirthLifeTick = society.LifeClock is null ? null : born,
                AgeBand = SocietyAgeBand.Child,
                LastLifecycleYearChecked = age,
            } : person).ToArray(),
        };
        state = state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with { Society = society },
            Agents = state.Agents.Select(person => person.AgentId == child
                ? person with { Position = scenario.SourceHouse.Position } : person).ToArray(),
        };
        state = AddStock(state, child, GuardianPlacementTestFixture.SourceHousehold,
            scenario.SourceHouse.InstanceId, scenario.SourceHouse.Position);
        var choices = new DeliveryChoices(child, caregiver, guardian: true) { Pickup = pickup };
        using var world = WorldRuntime.Restore(state, _ => choices);
        using var host = new CheckpointHost(world, choices);
        if (pickup)
        {
            await host.Until(() => world.Society.Inventory.GetLot("child-moving-wood").DeliveryBuildingId == scenario.SourceHouse.InstanceId);
            Assert.Contains((child, "child_carry:child-moving-wood"), choices.Selected);
        }
        if (deliverFirst)
        {
            choices.Deliver = true;
            Refresh(world, child);
            await host.Until(() => world.ExportState().Events.Any(item => item.Kind == "child_delivered_household"));
        }
        choices.ChangeHousehold = true;
        choices.Deliver = false;
        Refresh(world, child);
        Refresh(world, caregiver);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var replayChoices = choices.Copy();
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => replayChoices);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var replayHost = new CheckpointHost(replay, replayChoices, refuseArrival: true);
        using var continuing = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => choices);
        using var continuingHost = new CheckpointHost(continuing, choices, refuseArrival: true);
        await ContinuePair(continuingHost, replayHost, () => GuardianPlacementTestFixture.Placed(continuing, scenario, child));
        Assert.True(continuingHost.RefusedArrival);
        Assert.True(replayHost.RefusedArrival);
        Assert.Equal(scenario.House.Position, continuing.Agents.Single(person => person.AgentId == child).Position);
        Assert.Equal(GuardianPlacementTestFixture.DestinationHousehold, continuing.Society.GetAgent(child).HouseholdId);
        Assert.Equal(scenario.DestinationTownId, GuardianPlacementTestFixture.TownOf(continuing, child));
        Assert.Contains((caregiver, "guardian_accept:" + child), choices.Selected);
        Assert.Contains((caregiver, "guardian_relocate:" + child), choices.Selected);
        if (pickup) AssertCargo(continuing, child, GuardianPlacementTestFixture.SourceHousehold, scenario.SourceHouse.InstanceId, deliverFirst);
        else
        {
            var stock = continuing.Society.Inventory.GetLot("child-moving-wood");
            Assert.Equal((GuardianPlacementTestFixture.SourceHousehold, 2, new InventoryGroundPosition(scenario.SourceHouse.Position.X, scenario.SourceHouse.Position.Y)),
                (stock.OwnerId, stock.Quantity, stock.GroundPosition));
            Assert.DoesNotContain(continuing.ExportState().Events, item => item.Kind == "child_collected_household" || item.Kind == "child_delivered_household");
        }
        Assert.Single(continuing.ExportState().Events, item => item.Kind == "guardian_placement_completed");
    }

    private static WorldRuntimeState AddStock(WorldRuntimeState state, string child, string household, string house, GridPoint point)
    {
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "child-moving-wood", "wood", household, 2,
            groundPosition: new(point.X, point.Y));
        inventory = InventoryAuthority.AddLot(inventory, "child-personal-stone", "stone", child, 1, storageBuildingId: house);
        inventory = InventoryAuthority.AddLot(inventory, "stationary-household-wood", "wood", household, 3, storageBuildingId: house);
        return state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
    }

    private static void AssertCargo(WorldRuntime world, string child, string household, string house, bool delivered)
    {
        var lot = world.Society.Inventory.GetLot("child-moving-wood");
        Assert.Equal((household, 2, delivered ? house : null, delivered ? null : child, (string?)null, (InventoryGroundPosition?)null),
            (lot.OwnerId, lot.Quantity, lot.StorageBuildingId, lot.CarrierId, lot.DeliveryBuildingId, lot.GroundPosition));
        Assert.Equal(delivered ? 1 : 0, world.ExportState().Events.Count(item => item.Kind == "child_delivered_household"));
        Assert.Equal((child, 1, house), (world.Society.Inventory.GetLot("child-personal-stone").OwnerId,
            world.Society.Inventory.GetLot("child-personal-stone").Quantity, world.Society.Inventory.GetLot("child-personal-stone").StorageBuildingId));
        Assert.Equal((household, 3, house), (world.Society.Inventory.GetLot("stationary-household-wood").OwnerId,
            world.Society.Inventory.GetLot("stationary-household-wood").Quantity, world.Society.Inventory.GetLot("stationary-household-wood").StorageBuildingId));
    }

    private static void Refresh(WorldRuntime world, string actor) => world.SubmitInstruction(new(
        "child-house-change:" + world.WorldTick + ":" + Array.IndexOf(world.Agents.Select(person => person.AgentId).ToArray(), actor), "owner:test", actor, OwnerInstructionKind.Suggestion, "Consider the next household task."));

    private static async Task ContinuePair(CheckpointHost host, CheckpointHost replay, Func<bool> completed)
    {
        for (var tick = 0; tick < 80 && !completed(); tick++)
        {
            await host.Step();
            await replay.Step();
            Assert.Equal(WorldRuntimeCodec.Encode(host.World.ExportState()), WorldRuntimeCodec.Encode(replay.World.ExportState()));
        }
        Assert.True(completed());
        for (var tick = 0; tick < 3; tick++)
        {
            await host.Step();
            await replay.Step();
            Assert.Equal(WorldRuntimeCodec.Encode(host.World.ExportState()), WorldRuntimeCodec.Encode(replay.World.ExportState()));
        }
    }

    private sealed class CheckpointHost : IDisposable
    {
        private static readonly string[] PendingInputQueues = ["pendingModelCalls", "pendingIdentityMoments", "pendingWills", "pendingConversationTurns"];
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("child-house-change-");
        private readonly WorldStateFile file;
        private readonly WorldRuntimeService service;
        private readonly DeliveryChoices choices;
        private readonly bool refuseArrival;
        public bool RefusedArrival { get; private set; }
        public WorldRuntime World { get; }
        public CheckpointHost(WorldRuntime world, DeliveryChoices provider, bool refuseArrival = false)
        {
            World = world;
            choices = provider;
            this.refuseArrival = refuseArrival;
            file = new(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            world.Resume();
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("test-owner");
            service = new(world, file, presence);
        }
        public async Task Step()
        {
            if (refuseArrival && !RefusedArrival && World.Agents.Any(person =>
                    person.GuardianPlacement is { Stage: "escorting", HousePosition: { } home } placement &&
                    person.TravelCooldownTicks == 0 && World.ExportState().Map.FootDistance(person.Position, home) == 1 &&
                    World.Agents.Single(guardian => guardian.AgentId == placement.CaregiverId).Position == home))
            {
                var before = WorldRuntimeCodec.Encode(World.ExportState());
                Assert.False((await World.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
                Assert.Equal(before, WorldRuntimeCodec.Encode(World.ExportState()));
                RefusedArrival = true;
            }
            Assert.True(await service.TryAdvanceOnceAsync(), $"Native host halted at tick {World.WorldTick}.");
            // Replies are sampled by the next tick. Match every external input before either continuation advances.
            var tasks = PendingInputQueues.SelectMany(name => ((IDictionary)typeof(WorldRuntime)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(World)!).Values.Cast<object>())
                .Select(item => (Task)item.GetType().GetProperty("Task")!.GetValue(item)!);
            await Task.WhenAll(tasks)
                .WaitAsync(TimeSpan.FromSeconds(30));
            var bytes = File.ReadAllBytes(file.Path);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(World.ExportState()));
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => choices.Copy());
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        public async Task Until(Func<bool> completed)
        {
            for (var tick = 0; tick < 30 && !completed(); tick++) await Step();
            Assert.True(completed());
        }
        public void Dispose()
        {
            service.Dispose();
            directory.Delete(true);
        }
    }

    private sealed class DeliveryChoices(string child, string caregiver, bool guardian = false) : IDecisionProvider
    {
        public TaskCompletionSource? IdentityResponseGate { get; init; }
        public TaskCompletionSource IdentityEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Pickup { get; init; } = true;
        public bool Deliver { get; set; }
        public bool ChangeHousehold { get; set; }
        public ConcurrentQueue<(string Actor, string Candidate)> Selected { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public DeliveryChoices Copy() => new(child, caregiver, guardian) { Pickup = Pickup, Deliver = Deliver, ChangeHousehold = ChangeHousehold };
        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            request.Validate();
            var observation = request.Observation;
            if (observation.IdentityMoment is not null && IdentityResponseGate is not null)
            {
                IdentityEntered.TrySetResult();
                await IdentityResponseGate.Task.WaitAsync(cancellationToken);
            }
            string[] wanted = observation.AgentId == child
                ? Deliver ? ["child_carry:"] : Pickup && !ChangeHousehold ? ["child_carry:child-moving-wood"] : []
                : observation.AgentId == caregiver && ChangeHousehold
                    ? guardian ? ["guardian_accept:" + child, "guardian_relocate:" + child] : ["household_leave"] : [];
            var candidate = wanted.Select(prefix => observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(prefix, StringComparison.Ordinal)))
                .FirstOrDefault(item => item is not null) ?? observation.Candidates.Single(item => item.Id == "safe_idle");
            Selected.Enqueue((observation.AgentId, candidate.Id));
            return new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, candidate.Id, 1,
                new Dictionary<string, double> { [candidate.Id] = 1 },
                ChosenName: observation.NeedsName ? observation.Self!.Name : null,
                ChosenPersonality: observation.NeedsPersonality ? "patient" : null,
                ChosenAspiration: observation.NeedsAspiration ? "help at home" : null);
        }
    }
}
