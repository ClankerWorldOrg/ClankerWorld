using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MentorOwnerOrderTests
{
    private static readonly Lazy<Task<(WorldRuntimeState State, string Actor, GridPoint HousePosition)>> Generated =
        new(() => FoodCapacityTestFixture.Generated("mentor-order-block-audit"));

    [Theory]
    [InlineData("active")]
    [InlineData("cancelled")]
    [InlineData("suggestion")]
    [InlineData("already-requested")]
    public async Task OnlyAnAdultWhoCanAnswerALessonIsOfferedAsAFreeMentorAcrossReload(string mode)
    {
        var (state, teacher, home) = await Generated.Value;
        var household = state.Society.Society.GetAgent(teacher).HouseholdId!;
        var learner = state.Society.Society.GetHousehold(household).MemberIds.Single(id => id != teacher);
        var inventory = FoodCapacityTestFixture.WithoutPersonalCargo(state, teacher);
        inventory = InventoryAuthority.AddLot(inventory, "mentor-personal-axe", "wooden_axe", teacher, 1);
        state = FoodCapacityTestFixture.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == teacher || person.AgentId == learner ? home : person.Position,
                FullnessBasisPoints = 9_500,
                Survival = new(),
                Project = null,
                LastDecisionContext = null,
                Skills = person.AgentId == teacher ? [new(SkillKind.Building, state.Society.Society.WorldTick)] : null,
                Lesson = null,
                TravelCooldownTicks = 0,
                MoveWaitTicks = 0,
            }).ToArray(),
        };
        state = MovementTestWorld.ClearOtherBuildingAccess(state);
        var roles = state.Society.Society.Agents.ToDictionary(person => person.Id, person => person.CurrentRole);
        var offers = new List<string[]>();
        var answerLessons = mode != "already-requested";
        WorldRuntime Restore(byte[] bytes, List<string[]> observed) => WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(bytes), actor => new LessonChoices(actor == learner ? "learn:building:" + teacher :
                actor == teacher && answerLessons ? "lesson_accept:" : "safe_idle", actor == learner ? observed : []));
        using var setup = Restore(WorldRuntimeCodec.Encode(state), offers);
        if (mode == "already-requested")
        {
            for (var tick = 0; tick < 6 && setup.Agents.Single(person => person.AgentId == learner).Lesson?.Stage != "requested"; tick++)
                Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
            Assert.Equal("requested", setup.Agents.Single(person => person.AgentId == learner).Lesson?.Stage);
        }
        var order = setup.SubmitInstruction(new("mentor-owner-order", "owner:test", teacher,
            mode == "suggestion" ? OwnerInstructionKind.Suggestion : OwnerInstructionKind.Order, "repeat gather wood"));
        if (mode == "cancelled") Assert.True(setup.CancelOrder(new("cancel-mentor-order", "owner:test", setup.Society.WorldId,
            teacher, order.InstructionId)).Changed);
        answerLessons = true;
        var initial = WorldRuntimeCodec.Encode(setup.ExportState());
        using var world = Restore(initial, offers);
        using var replay = Restore(initial, []);
        Assert.Equal(initial, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(initial, WorldRuntimeCodec.Encode(world.ExportState()));
        await Tick();
        Assert.NotEmpty(offers);
        if (mode != "already-requested")
            Assert.Equal(mode != "active", offers.SelectMany(items => items).Contains("learn:building:" + teacher, StringComparer.Ordinal));
        if (mode is "active" or "already-requested")
        {
            if (mode == "active") Assert.Null(world.Agents.Single(person => person.AgentId == learner).Lesson);
            else
            {
                for (var tick = 0; tick < 6; tick++) await Tick();
                Assert.Equal("requested", world.Agents.Single(person => person.AgentId == learner).Lesson?.Stage);
                Assert.Equal(0, world.Agents.Single(person => person.AgentId == learner).Lesson?.Progress);
            }
            var cancel = new OwnerOrderCancelRequest("release-mentor-after-wait", "owner:test", world.Society.WorldId,
                teacher, order.InstructionId);
            Assert.True(world.CancelOrder(cancel).Changed);
            Assert.True(replay.CancelOrder(cancel).Changed);
        }
        for (var tick = 0; tick < 100 && world.Agents.Single(person => person.AgentId == learner).Lesson?.Stage != "completed"; tick++)
            await Tick();
        var person = world.Agents.Single(person => person.AgentId == learner);
        Assert.Equal("completed", person.Lesson?.Stage);
        var learned = Assert.Single(person.Skills!, skill => skill.Kind == SkillKind.Building);
        Assert.Equal(teacher, learned.TeacherId);
        Assert.All(world.Society.Agents, member => Assert.Equal(roles[member.Id], member.CurrentRole));
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = Restore(saved, []);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(loaded.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(loaded.ExportState()));

        async Task Tick()
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            world.Validate();
        }
    }

    private sealed class LessonChoices(string preferred, List<string[]> offers) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            offers.Add(request.Observation.Candidates.Select(item => item.Id).ToArray());
            var choice = request.Observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(preferred, StringComparison.Ordinal)) ??
                request.Observation.Candidates.FirstOrDefault(item => item.Id is "lesson_attend" || item.Id.StartsWith("lesson_teach:", StringComparison.Ordinal)) ??
                (request.Observation.OperativeOrderInstructionId is not null ? request.Observation.Candidates.FirstOrDefault(item => item.Id != "safe_idle") : null) ??
                request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [choice] } }, cancellationToken);
        }
    }
}
