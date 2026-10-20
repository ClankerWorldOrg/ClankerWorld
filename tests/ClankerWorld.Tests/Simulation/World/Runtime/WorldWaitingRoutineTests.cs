using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class WorldRuntimeTests
{
    [Fact]
    public async Task APendingPersonalReplyKeepsItsPreviouslyChosenTripWithoutCommittingARejectedTick()
    {
        var provider = new WaitingRoutineProvider("seek_food", holdFromCall: 2);
        using var setup = CreateCancellationTravelWorld(new CancellationPlanningProvider());
        using var world = RestoreWaitingWorld(setup.ExportState(), HarvestInstructionActor, provider);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal("seek_food", world.ExportState().Society.Cognition.Runtimes.Single(item => item.AgentId == HarvestInstructionActor).CurrentIntention!.CandidateId);
        world.SubmitInstruction(new("waiting-trip-suggestion", "owner:test", HarvestInstructionActor,
            OwnerInstructionKind.Suggestion, "Think about your next task."));
        var dispatch = await world.TickModelWorkAsync();
        Assert.True(dispatch.Advanced);
        Assert.Contains(dispatch.Events, item => item.Kind == "hosted_decision_started" && item.Detail == HarvestInstructionActor);
        // Native dispatch and held provider entry are controlled by the scheduler.
        await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.TickModelWorkAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        var position = CancellationActorPosition(world.ExportState());
        for (var tick = 0; tick < 5; tick++)
        {
            var step = await world.TickModelWorkAsync();
            Assert.True(step.Advanced);
            Assert.DoesNotContain(step.Decisions, item => item.AgentId == HarvestInstructionActor);
        }
        Assert.NotEqual(position, CancellationActorPosition(world.ExportState()));
        Assert.Equal(2, provider.Requests.Count);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "hosted_decision_completed");
        world.Pause();
        var paused = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.TickModelWorkAsync()).Advanced);
        Assert.Equal(paused, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(paused));
        Assert.Equal(paused, WorldRuntimeCodec.Encode(restored.ExportState()));
        using var replay = RestoreWaitingWorld(WorldRuntimeCodec.Decode(paused), HarvestInstructionActor,
            new WaitingRoutineProvider("seek_food"));
        using var duplicate = RestoreWaitingWorld(WorldRuntimeCodec.Decode(paused), HarvestInstructionActor,
            new WaitingRoutineProvider("seek_food"));
        replay.Resume();
        duplicate.Resume();
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await replay.TickModelWorkAsync()).Advanced);
            Assert.True((await duplicate.TickModelWorkAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(replay.ExportState()), WorldRuntimeCodec.Encode(duplicate.ExportState()));
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task APendingPersonalReplyFeedsOnlyANearbyDependentAndMakesNoNewCareAgreement(bool nearby, bool ordered)
    {
        var (state, actor, household, point) = FarmFieldTests.PreparedFarmer("waiting-dependent");
        var checkpoint = ChosenBirthNameTestFixture.NameParent(state.Society.Society, actor);
        var partner = checkpoint.Agents.First(person => person.HouseholdId == household && person.Id != actor).Id;
        checkpoint = SocietyRules.ProposeRelationship(checkpoint, new("waiting-care-parents", 1,
            SocietyRelationshipType.Partnership, actor, partner, checkpoint.WorldTick)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, "waiting-care-parents", 1, partner).Checkpoint;
        var food = checkpoint.Inventory.Lots.First(lot => lot.OwnerId == household && lot.ItemKind == "food" && lot.Quantity >= 4);
        var birth = SocietyRules.CommitBirth(checkpoint, new("waiting-care-birth", 1, actor, partner,
            household, [actor, partner], [actor, partner], food.Id, 4, checkpoint.WorldTick,
            ChildName: ChosenBirthNameTestFixture.ChildName(checkpoint, actor, "Ari"), PrimaryCaregiverId: actor));
        var child = Assert.IsType<string>(birth.CreatedId);
        checkpoint = birth.Checkpoint with
        {
            Inventory = InventoryAuthority.AddLot(birth.Checkpoint.Inventory,
            "waiting-child-serving", "berries", actor, 1)
        };
        var childPosition = state.Map.Tiles.Select(tile => tile.Position).First(tile => state.Map.IsBuildable(tile) &&
            !state.Agents.Any(person => person.Position == tile) &&
            (nearby ? state.Map.FootDistance(point, tile) == 1 : state.Map.FootDistance(point, tile) is >= 8 and <= 10));
        state = state with
        {
            Society = state.Society with { Society = checkpoint },
            Survival = new SurvivalState(0, []),
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = 9_000,
                Survival = new SurvivalCondition(10_000),
                LastDecisionContext = null
            }).Append(
                new AgentState(child, childPosition, 1_000, 0, "curious", "grow",
                    Survival: new SurvivalCondition(10_000))).OrderBy(person => person.AgentId, StringComparer.Ordinal).ToArray(),
            Towns = state.Towns!.Select(town => town.ResidentIds.Contains(actor) ? town with
            { ResidentIds = town.ResidentIds.Append(child).Order(StringComparer.Ordinal).ToArray() } : town).ToArray(),
        };
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Clear);
        var provider = new WaitingRoutineProvider("safe_idle");
        using var world = RestoreWaitingWorld(state, actor, provider);
        if (ordered) world.SubmitInstruction(new("waiting-care-order", "owner:test", actor,
            OwnerInstructionKind.Order, "gather food"));
        var dispatch = await world.TickModelWorkAsync();
        Assert.True(dispatch.Advanced);
        Assert.Contains(dispatch.Events, item => item.Kind == "hosted_decision_started" && item.Detail == actor);
        await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
        var relationships = world.Society.Relationships.ToArray();
        var position = world.Agents.Single(person => person.AgentId == actor).Position;
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.TickModelWorkAsync()).Advanced);
        Assert.Equal(relationships, world.Society.Relationships);
        if (nearby)
        {
            Assert.Contains(world.ExportState().Events, item => item.Kind == "child_cared_for" && item.Detail == child);
            Assert.True(world.Agents.Single(person => person.AgentId == child).FullnessBasisPoints > 2_000);
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "waiting-child-serving");
        }
        else
        {
            Assert.Equal(position, world.Agents.Single(person => person.AgentId == actor).Position);
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "child_cared_for");
            Assert.Equal(1, world.Society.Inventory.GetLot("waiting-child-serving").Quantity);
        }
        Assert.Single(provider.Requests);
        world.Validate();
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(world.Agents.Single(person => person.AgentId == child).FullnessBasisPoints,
            restored.Agents.Single(person => person.AgentId == child).FullnessBasisPoints);
    }

    [Theory]
    [InlineData(WeatherKind.Clear, true)]
    [InlineData(WeatherKind.Rain, false)]
    [InlineData(WeatherKind.Storm, false)]
    public async Task APendingPersonalReplyUsesLiveUrgentFoodOrWeatherProtectionWithoutStartingAProject(WeatherKind weather, bool hungry)
    {
        using var setup = new WorldRuntime("waiting-survival", _ =>
            new CountingSelectingProvider(DecisionProviderKind.Deterministic, chooseIdle: true));
        setup.StageStarterContent();
        for (var tick = 0; tick < 6; tick++) Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        const string actor = "founder-scout";
        var state = setup.ExportState();
        var house = setup.WorldContent.Buildings.Single(building => building.LocalId == "house-1x1");
        var site = state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsBuildable(point) &&
            !state.Map.Resources.Any(resource => resource.Position == point) && !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Agents.Any(person => person.Position == point));
        var placement = setup.PlaceBuilding("waiting-warm-home", house.CanonicalId, site, "household:first");
        Assert.True(placement.Applied, placement.Failure);
        state = setup.ExportState();
        var start = state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsBuildable(point) &&
            state.Map.FootDistance(point, site) is >= 4 and <= 7 && state.Map.VegetationAt(point) != VegetationCover.Forest &&
            !state.Map.Resources.Any(resource => resource.Position == point) && !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Agents.Any(person => person.Position == point) && state.Map.IsReachableOnFoot(point, site));
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "waiting-own-food", "food", actor, 1);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = start,
                FullnessBasisPoints = hungry ? 2_050 : 9_000,
                Survival = new SurvivalCondition(hungry ? 10_000 : 4_000),
                LastDecisionContext = null,
                TravelCooldownTicks = 0
            } : person).ToArray(),
        };
        state = WeatherTestFixture.WithWeather(state, weather);
        var provider = new WaitingRoutineProvider("safe_idle");
        using var world = RestoreWaitingWorld(state, actor, provider);
        var dispatch = await world.TickModelWorkAsync();
        Assert.True(dispatch.Advanced);
        Assert.Contains(dispatch.Events, item => item.Kind == "hosted_decision_started" && item.Detail == actor);
        await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
        Assert.Contains(provider.Requests.Single().Candidates, candidate => candidate.Id == "household_leave");
        var buildings = world.WorldSimulation.Buildings.ToArray();
        var relationships = world.Society.Relationships.ToArray();
        for (var tick = 0; tick < 20; tick++) Assert.True((await world.TickModelWorkAsync()).Advanced);
        var person = world.Agents.Single(person => person.AgentId == actor);
        if (hungry)
        {
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "waiting-own-food");
            Assert.True(person.FullnessBasisPoints > 2_100);
        }
        else
        {
            Assert.NotEqual(start, person.Position);
            Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_moved" &&
                item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
        }
        Assert.Null(person.Project);
        Assert.Equal(buildings, world.WorldSimulation.Buildings);
        Assert.Equal(relationships, world.Society.Relationships);
        Assert.Single(provider.Requests);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "hosted_decision_completed");
        world.Validate();
    }

    private static WorldRuntime RestoreWaitingWorld(WorldRuntimeState state, string actor, WaitingRoutineProvider provider) =>
        WorldRuntime.Restore(state, id => id == actor ? provider :
            new CountingSelectingProvider(DecisionProviderKind.Deterministic, chooseIdle: true));

    private sealed class WaitingRoutineProvider(string firstChoice, int holdFromCall = 1) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ConcurrentQueue<AgentObservation> Requests { get; } = new();
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        private readonly InlineCompletionSource<bool> release = new(TaskCreationOptions.None);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Enqueue(request.Observation);
            if (Requests.Count >= holdFromCall)
            {
                Started.TrySetResult(true);
                await release.Task.WaitAsync(cancellationToken);
            }
            Assert.Contains(request.Observation.Candidates, candidate => candidate.Id == firstChoice);
            return ModelResponse(request, Kind, ProviderEpoch, firstChoice);
        }
    }
}
