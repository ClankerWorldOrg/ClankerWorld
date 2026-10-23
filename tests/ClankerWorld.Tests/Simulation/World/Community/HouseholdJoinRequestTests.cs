using System.Collections.Concurrent;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

/// <summary>
/// An adult with no home asks a household that holds a House before any
/// construction is considered (#464). Every adult member must agree; nothing
/// is granted by standing nearby or while the request is pending.
/// </summary>
public sealed partial class HouseholdJoinRequestTests
{
    private const string Alpha = "household:first";
    private const string Beta = "household:second";

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task APendingHousingReplyKeepsAnAcceptedLessonWithoutAdmittingTheApplicant(bool memberIsTeacher, bool deferStartup)
    {
        var provider = new ScriptedProvider();
        using var initial = NormalPathWorld.CreateGenerated("waiting-housing-lesson", _ => provider);
        var member = initial.Society.GetHousehold(Alpha).MemberIds[0];
        var other = initial.Society.GetHousehold(Beta).MemberIds[0];
        var teacher = memberIsTeacher ? member : other;
        var learner = memberIsTeacher ? other : member;
        var state = initial.ExportState();
        var startingSite = initial.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-warehouse").Position;
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == teacher || person.AgentId == learner
                ? person with
                {
                    Position = startingSite,
                    FullnessBasisPoints = 9_000,
                    Project = null,
                    LastDecisionContext = null,
                    Skills = person.AgentId == teacher ? [new(SkillKind.Building, initial.WorldTick)] : null,
                } : person).ToArray(),
        };
        provider.Choices[learner] = "learn:building:" + teacher;
        provider.Choices[teacher] = "lesson_accept:" + learner;
        using var setup = WorldRuntime.Restore(state, _ => provider);
        await AdvanceUntil(setup, () => setup.Agents.Single(person => person.AgentId == learner).Lesson?.Progress is >= 3);
        Assert.InRange(setup.Agents.Single(person => person.AgentId == learner).Lesson!.Progress, 3, 10);
        const string applicant = "agent:dddddddddddddddddddddddddddddddd";
        Assert.Null(setup.AddAgent(applicant, TownTileBeside(setup, "first-town-house-a")));
        provider.Choices[applicant] = "household_ask:" + Alpha;
        await AdvanceUntil(setup, () => Housing(setup, applicant)?.Request is not null);
        provider.Choices[applicant] = "safe_idle";
        provider.Choices[teacher] = provider.Choices[learner] = "safe_idle";
        state = setup.ExportState();
        GridPoint? travellingLearner = null;
        if (memberIsTeacher)
        {
            travellingLearner = FreeTiles(setup).First(point => state.Map.FootDistance(point, startingSite) == 2);
            state = state with
            {
                Agents = state.Agents.Select(person => person.AgentId == learner
                    ? person with { Position = travellingLearner.Value, TravelCooldownTicks = 0, LastDecisionContext = null }
                    : person).ToArray(),
            };
            provider.Choices[learner] = "lesson_attend";
        }
        var request = Housing(state, applicant)!.Request!;
        Assert.Contains(member, request.Members);
        Assert.DoesNotContain(member, request.Approvals);
        var allowStart = deferStartup ? 0 : 1;
        var held = new HeldPhysicalTaskDecisionProvider();
        using var world = WorldRuntime.Restore(state, actor => actor == member ? held : provider);
        var harness = ModelWorkHarness.For(world);
        harness.Scheduler.CanStart = () => Volatile.Read(ref allowStart) == 1;
        world.SubmitInstruction(new("waiting-housing-lesson-guidance", "owner:test", member,
            OwnerInstructionKind.Suggestion, "Think about your next task."));
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        // Registration precedes manually controlled provider entry. The lesson
        // must continue while that exact request is pending in either phase.
        var (heldRequest, heldTask) = CapturePendingHousingCall(world, member);
        Assert.Equal(member, heldRequest.Observation.AgentId);
        Assert.Contains(heldRequest.Observation.Candidates, candidate => candidate.Id == "household_admit:" + applicant);
        Assert.False(heldTask.IsCompleted);
        if (deferStartup) Assert.False(held.Started.Task.IsCompleted);
        if (travellingLearner is { } start)
        {
            for (var tick = 0; tick < 8 && state.Map.FootDistance(
                world.Agents.Single(person => person.AgentId == learner).Position, startingSite) > 1; tick++)
                Assert.True((await world.TickModelWorkAsync()).Advanced);
            var arrival = world.Agents.Single(person => person.AgentId == learner).Position;
            Assert.NotEqual(start, arrival);
            Assert.InRange(state.Map.FootDistance(arrival, startingSite), 0, 1);
        }
        var before = world.Agents.Single(person => person.AgentId == learner).Lesson!;
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.TickModelWorkAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.TickModelWorkAsync()).Advanced);
        Assert.Equal(before.Progress + 3, world.Agents.Single(person => person.AgentId == learner).Lesson!.Progress);
        Assert.Equal(JsonSerializer.Serialize(request), JsonSerializer.Serialize(Housing(world, applicant)!.Request));
        Assert.Null(world.Society.GetAgent(applicant).HouseholdId);
        Assert.Empty(world.Agents.Single(person => person.AgentId == learner).Skills ?? []);
        Assert.False(heldTask.IsCompleted);
        if (deferStartup) Assert.False(held.Started.Task.IsCompleted);
        var beforeEntry = WorldRuntimeCodec.Encode(world.ExportState());
        Volatile.Write(ref allowStart, 1);
        harness.RunPendingWork();
        await ModelWorkHarness.WaitForSignalAsync(held.Started.Task);
        Assert.Same(heldRequest.Observation, Assert.Single(held.Requests));
        Assert.Equal(beforeEntry, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "hosted_decision_completed");
        world.Validate();
        world.Pause();
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HousingAnswerSuspendsBothParticipantsOfAnOngoingLesson(bool memberIsTeacher)
    {
        var provider = new ScriptedProvider();
        using var world = NormalPathWorld.CreateGenerated("housing-learning-pause", _ => provider);
        var applicant = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Null(world.AddAgent(applicant, TownTileBeside(world, "first-town-house-a")));
        provider.Choices[applicant] = "household_ask:" + Alpha;
        await AdvanceUntil(world, () => Housing(world, applicant)?.Request is not null);
        provider.Choices[applicant] = "safe_idle";
        world.Pause();
        var state = world.ExportState();
        var member = world.Society.GetHousehold(Alpha).MemberIds[0];
        var other = world.Society.GetHousehold(Beta).MemberIds[0];
        var teacher = memberIsTeacher ? member : other;
        var learner = memberIsTeacher ? other : member;
        var startingSite = world.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-warehouse").Position;
        const int progress = 3;
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == teacher || person.AgentId == learner
                ? person with
                {
                    Position = startingSite,
                    FullnessBasisPoints = 9_000,
                    Project = null,
                    LastDecisionContext = null,
                    Skills = person.AgentId == teacher ? [new(SkillKind.Building, world.WorldTick)] : null,
                    Lesson = person.AgentId == learner
                        ? new(teacher, SkillKind.Building, "training", progress, world.WorldTick, world.WorldTick) : null,
                } : person).ToArray(),
        };
        provider.Choices[teacher] = "lesson_teach:" + learner;
        provider.Choices[learner] = "lesson_attend";
        var bytes = WorldRuntimeCodec.Encode(state);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => provider);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.Resume();
        for (var tick = 0; tick < 5; tick++)
        {
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(progress, restored.Agents.Single(person => person.AgentId == learner).Lesson!.Progress);
        }
        Assert.Empty(restored.Agents.Single(person => person.AgentId == learner).Skills ?? []);
        Assert.NotNull(Housing(restored, applicant)!.Request);
        foreach (var adult in restored.Society.GetHousehold(Alpha).MemberIds)
            provider.Choices[adult] = "household_admit:" + applicant;
        restored.Pause();
        var readyToAnswer = restored.ExportState();
        readyToAnswer = readyToAnswer with
        {
            Agents = readyToAnswer.Agents.Select(person => person with { LastDecisionContext = null }).ToArray(),
        };
        using var answered = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(readyToAnswer)), _ => provider);
        answered.Resume();
        await AdvanceUntil(answered, () => answered.Society.GetAgent(applicant).HouseholdId == Alpha);
        await AdvanceUntil(answered, () => answered.Agents.Single(person => person.AgentId == learner).Lesson!.Stage == "completed");
        var skill = Assert.Single(answered.Agents.Single(person => person.AgentId == learner).Skills!);
        Assert.Equal(SkillKind.Building, skill.Kind);
        Assert.Equal(teacher, skill.TeacherId);
        var shown = new OwnerWorldObservationStore(answered).GetSnapshot();
        Assert.Equal(teacher, Assert.Single(shown.Agents.Single(person => person.Id == learner).Skills).TeacherId);
        Assert.DoesNotContain(shown.Agents.Single(person => person.Id == applicant).DecisionFactors, factor => factor.Key == "housing");
    }

    [Theory]
    [InlineData(true)]
    public async Task AdultAddedToHouseholdMustAnswerAnAlreadyPendingRequest(bool agrees)
    {
        var provider = new ScriptedProvider();
        using var seed = NormalPathWorld.CreateGenerated("housing-review-new-adult", _ => provider);
        var house = seed.WorldSimulation.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var builder = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Equal(Alpha, seed.AddAgent(builder, house.Position));
        var state = seed.ExportState();
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "housing-review-expansion-wood", "wood", builder, 4);
        state = FarmFieldTests.WithInventory(state, inventory);
        state = ExpansionLandFixture.WithRights(state, house, Enumerable.Range(-1, 3).SelectMany(dy =>
            Enumerable.Range(-1, 3).Select(dx => new GridPoint(house.Position.X + dx, house.Position.Y + dy))).Where(state.Map.IsLand));
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            _ => provider);
        var expansion = world.StartBuildingExpansion(builder, house.InstanceId);
        Assert.True(expansion.Applied, expansion.Failure);
        for (var tick = 0; tick < 20; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        house = world.WorldSimulation.Buildings.Single(item => item.InstanceId == house.InstanceId);
        Assert.Equal(1, house.Footprint!.Revision);

        var applicant = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Null(world.AddAgent(applicant, TownTileBeside(world, "first-town-house-a")));
        provider.Choices[applicant] = "household_ask:" + Alpha;
        await AdvanceUntil(world, () => Housing(world, applicant)?.Request is not null);
        provider.Choices[applicant] = "safe_idle";
        var originalMembers = world.Society.GetHousehold(Alpha).MemberIds.ToArray();
        provider.Choices[originalMembers[0]] = "household_admit:" + applicant;
        foreach (var member in originalMembers.Skip(1))
            provider.Choices[member] = ScriptedProvider.AnythingButHousing;
        await AdvanceUntil(world, () => Housing(world, applicant)?.Request?.Approvals.Contains(originalMembers[0], StringComparer.Ordinal) == true);
        var newAdult = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Equal(Alpha, world.AddAgent(newAdult, house.Position));
        foreach (var member in originalMembers.Skip(1))
            provider.Choices[member] = "household_admit:" + applicant;
        provider.Choices[newAdult] = ScriptedProvider.AnythingButHousing;
        await AdvanceUntil(world, () => Housing(world, applicant)?.Request is { } request &&
            originalMembers.All(member => request.Approvals.Contains(member, StringComparer.Ordinal)) &&
            provider.Offered.TryGetValue(newAdult, out var offered) && offered.ContainsKey("household_admit:" + applicant));

        var pending = Housing(world, applicant)!.Request!;
        Assert.Contains(newAdult, pending.Members);
        Assert.Contains(originalMembers[0], pending.Approvals);
        Assert.DoesNotContain(newAdult, pending.Approvals);
        Assert.Null(world.Society.GetAgent(applicant).HouseholdId);
        Assert.DoesNotContain(applicant, world.Society.GetHousehold(Alpha).MemberIds);
        Assert.DoesNotContain(provider.Offered[applicant].Keys, candidate => candidate is "collect_shared_food" or "haul_household_stock");
        provider.Choices[newAdult] = (agrees ? "household_admit:" : "household_refuse:") + applicant;
        await AdvanceUntil(world, () => world.Society.GetAgent(applicant).HouseholdId is not null ||
            world.ExportState().Events.Any(item => item.Kind == "housing_request_refused"));

        Assert.Equal(agrees ? Alpha : null, world.Society.GetAgent(applicant).HouseholdId);
        Assert.Contains(world.ExportState().Events, item => item.Kind == (agrees ? "household_joined" : "housing_request_refused"));
        world.Validate();
    }

    [Fact]
    public async Task PendingAdmissionIsRejectedIfAnotherAdultUsesTheLastPlace()
    {
        var provider = new ScriptedProvider();
        using var world = NormalPathWorld.CreateGenerated("housing-last-place-race", _ => provider);
        var applicant = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Null(world.AddAgent(applicant, TownTileBeside(world, "first-town-house-a")));
        provider.Choices[applicant] = "household_ask:" + Alpha;
        await AdvanceUntil(world, () => Housing(world, applicant)?.Request is not null);
        provider.Choices[applicant] = "safe_idle";

        var originalMembers = world.Society.GetHousehold(Alpha).MemberIds.ToArray();
        provider.Choices[originalMembers[0]] = "household_admit:" + applicant;
        provider.Choices[originalMembers[1]] = ScriptedProvider.AnythingButHousing;
        await AdvanceUntil(world, () => Housing(world, applicant)?.Request?.Approvals.Contains(
            originalMembers[0], StringComparer.Ordinal) == true);

        var house = world.WorldSimulation.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var newcomer = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Equal(Alpha, world.AddAgent(newcomer, house.Position));
        provider.Choices[newcomer] = ScriptedProvider.AnythingButHousing;
        await AdvanceUntil(world, () => Housing(world, applicant)?.Request?.Members.Contains(
            newcomer, StringComparer.Ordinal) == true);

        provider.Choices[originalMembers[1]] = "household_admit:" + applicant;
        provider.Choices[newcomer] = "household_admit:" + applicant;
        await AdvanceUntil(world, () => world.ExportState().Events.Any(item =>
            item.Kind == "housing_request_blocked_capacity" && item.Detail == $"{applicant}:{Alpha}"));

        Assert.Null(world.Society.GetAgent(applicant).HouseholdId);
        Assert.DoesNotContain(applicant, world.Society.GetHousehold(Alpha).MemberIds);
        Assert.Equal(originalMembers.Concat([newcomer]).Order(StringComparer.Ordinal),
            world.Society.GetHousehold(Alpha).MemberIds);
        Assert.Equal(3, world.Society.Agents.Count(person => person.Status == SocietyAgentStatus.Active &&
            person.HouseholdId == Alpha));
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "household_joined" &&
            item.Detail == $"{applicant}:{Alpha}");
    }

    [Fact]
    public async Task AdultOnTownLandMustAskAndEveryAdultMustAgreeBeforeJoining()
    {
        var provider = new ScriptedProvider();
        using var world = NormalPathWorld.CreateGenerated("housing-join", _ => provider);
        var agent = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Null(world.AddAgent(agent, TownTileBeside(world, "first-town-house-a")));
        provider.Choices[agent] = "household_ask:" + Alpha;
        await AdvanceUntil(world, () => world.ExportState().Events.Any(item => item.Kind == "housing_request_made"));

        // Standing beside a House grants nothing; the adult is told why and may ask.
        Assert.Null(world.Society.GetAgent(agent).HouseholdId);
        var offered = provider.Offered[agent];
        Assert.Contains("household_ask:" + Alpha, offered.Keys);
        Assert.Contains("household_ask:" + Beta, offered.Keys);
        Assert.DoesNotContain(offered.Keys, id => id.StartsWith("build:", StringComparison.Ordinal));
        Assert.All(offered.Values, description => Assert.Null(RetiredWording.Find(description)));
        Assert.Contains("no household", provider.HousingNotes[agent], StringComparison.Ordinal);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "housing_blocked" && item.Detail == $"{agent}:no_household");
        var request = world.Agents.Single(person => person.AgentId == agent).Housing!.Request!;
        Assert.Equal(Alpha, request.HouseholdId);
        var members = world.Society.GetHousehold(Alpha).MemberIds.Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(2, members.Length);
        Assert.Equal(members, request.Members);
        Assert.Equal(WorldRuntime.HousingRequestTicks, request.ExpiryTick - request.RequestedTick);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "housing_request_made" && item.Detail == $"{agent}:{Alpha}");

        // One agreement is not enough, and a pending request grants no household access.
        // The second adult keeps busy with other work rather than answering yet.
        provider.Choices[agent] = "safe_idle";
        provider.Choices[members[0]] = "household_admit:" + agent;
        provider.Choices[members[1]] = ScriptedProvider.AnythingButHousing;
        await AdvanceUntil(world, () => world.Agents.Single(person => person.AgentId == agent)
            .Housing?.Request?.Approvals.Contains(members[0], StringComparer.Ordinal) == true);
        Assert.Equal([members[0]], world.Agents.Single(person => person.AgentId == agent).Housing!.Request!.Approvals);
        Assert.Null(world.Society.GetAgent(agent).HouseholdId);
        Assert.DoesNotContain(world.Society.GetHousehold(Alpha).MemberIds, id => id == agent);
        Assert.DoesNotContain(provider.Offered[agent].Keys, id => id is "collect_shared_food" or "haul_household_stock");
        Assert.DoesNotContain(provider.Offered[agent].Keys, id => id.StartsWith("household_ask:", StringComparison.Ordinal));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var visible = new OwnerWorldObservationStore(world).GetSnapshot();
        var applicant = visible.Agents.Single(person => person.Id == agent);
        Assert.Equal("unhoused", applicant.DecisionFactors.Single(factor => factor.Key == "household").Detail);
        Assert.Contains("every adult member must agree", applicant.DecisionFactors.Single(factor => factor.Key == "housing").Detail, StringComparison.Ordinal);
        Assert.Contains(visible.Agents.Single(person => person.Id == members[0]).SocialNotes,
            note => note.StartsWith("Agreed to let", StringComparison.Ordinal));
        Assert.Contains(visible.Agents.Single(person => person.Id == members[1]).SocialNotes,
            note => note.Contains("asked to live", StringComparison.Ordinal));

        // The pending request survives a save, and replay from it is deterministic.
        world.Pause();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        provider.Choices[members[1]] = "household_admit:" + agent;
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => provider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => provider);
        Assert.Equal(Alpha, restored.Agents.Single(person => person.AgentId == agent).Housing!.Request!.HouseholdId);
        restored.Resume();
        replay.Resume();
        var ticks = 0;
        await AdvanceUntil(restored, () => restored.Society.GetAgent(agent).HouseholdId == Alpha, 80, () => ticks++);
        for (var tick = 0; tick < ticks; tick++)
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(restored.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));

        // With every adult's agreement the adult is a member, with a member's access.
        Assert.Contains(agent, restored.Society.GetHousehold(Alpha).MemberIds);
        Assert.Contains(restored.Society.Relationships, edge => edge.Type == SocietyRelationshipType.HouseholdMembership &&
            edge.State == SocietyRelationshipState.Accepted && edge.TargetId == agent && edge.HouseholdId == Alpha);
        Assert.Null(restored.Agents.Single(person => person.AgentId == agent).Housing);
        Assert.Contains(restored.ExportState().Events, item => item.Kind == "household_joined" && item.Detail == $"{agent}:{Alpha}");
        await AdvanceUntil(restored, () => provider.Offered[agent].ContainsKey("collect_shared_food"));
        var housed = new OwnerWorldObservationStore(restored).GetSnapshot().Agents.Single(person => person.Id == agent);
        Assert.DoesNotContain(housed.DecisionFactors, factor => factor.Key == "housing");
        Assert.NotEqual("unhoused", housed.DecisionFactors.Single(factor => factor.Key == "household").Detail);
        restored.Validate();
    }

    [Fact]
    public async Task OneRefusalEndsTheRequestAndThatHouseholdIsNotAskedAgainForAWhile()
    {
        var provider = new ScriptedProvider();
        using var world = NormalPathWorld.CreateGenerated("housing-refuse", _ => provider);
        var agent = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Null(world.AddAgent(agent, TownTileBeside(world, "first-town-house-a")));
        provider.Choices[agent] = "household_ask:" + Alpha;
        await AdvanceUntil(world, () => world.Agents.Single(person => person.AgentId == agent).Housing?.Request is not null);
        provider.Choices[agent] = "safe_idle";
        var members = world.Society.GetHousehold(Alpha).MemberIds.Order(StringComparer.Ordinal).ToArray();
        provider.Choices[members[0]] = "household_admit:" + agent;
        provider.Choices[members[1]] = "household_refuse:" + agent;
        await AdvanceUntil(world, () => world.ExportState().Events.Any(item => item.Kind == "housing_request_refused"));

        Assert.Null(world.Society.GetAgent(agent).HouseholdId);
        var housing = world.Agents.Single(person => person.AgentId == agent).Housing!;
        Assert.Null(housing.Request);
        Assert.Equal(Alpha, Assert.Single(housing.Refusals!).HouseholdId);
        Assert.Equal(world.WorldTick, Assert.Single(housing.Refusals!).Tick);

        // The other household can still be asked; the one that refused waits out its cooldown.
        provider.Choices[agent] = "household_ask:";
        await AdvanceUntil(world, () => world.Agents.Single(person => person.AgentId == agent).Housing?.Request is not null);
        Assert.DoesNotContain("household_ask:" + Alpha, provider.Offered[agent].Keys);
        Assert.Equal(Beta, world.Agents.Single(person => person.AgentId == agent).Housing!.Request!.HouseholdId);
        Assert.Equal("no_household", world.Agents.Single(person => person.AgentId == agent).Housing!.Blocker);
        world.Validate();
        _ = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public async Task RestoredHousingApprovalCannotOutliveItsWorldTickDeadline()
    {
        var provider = new ScriptedProvider();
        using var world = NormalPathWorld.CreateGenerated("housing-restore-expiry", _ => provider);
        var agent = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Null(world.AddAgent(agent, TownTileBeside(world, "first-town-house-a")));
        provider.Choices[agent] = "household_ask:" + Alpha;
        await AdvanceUntil(world, () => Housing(world, agent)?.Request is not null);
        provider.Choices[agent] = "safe_idle";
        var request = Housing(world, agent)!.Request!;
        await AdvanceUntil(world, () => world.WorldTick == request.ExpiryTick - 1,
            WorldRuntime.HousingRequestTicks);
        world.Pause();
        var beforeExpiry = world.ExportState();
        world.Resume();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(request.ExpiryTick, world.WorldTick);
        world.Pause();
        var atExpiry = world.ExportState();

        // The last eligible tick is a legitimate pending checkpoint and must
        // still load unchanged; paused wall time does not expire the request.
        using (var exactExpiry = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(atExpiry)), _ => provider))
        {
            Assert.Equal(request.ExpiryTick, exactExpiry.WorldTick);
            Assert.NotNull(Housing(exactExpiry, agent)!.Request);
            Assert.False((await exactExpiry.AdvanceOneTickAsync()).Advanced);
        }

        world.Resume();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        world.Pause();
        var afterExpiry = world.ExportState();
        foreach (var (checkpoint, mayJoin) in new[]
                     { (beforeExpiry, true), (atExpiry, false), (afterExpiry, false) })
        {
            // A malformed checkpoint may carry completed answers on an old
            // request. Its answers cannot grant access after the deadline.
            var answered = WithHousing(checkpoint, agent, housing => housing with
            {
                Request = request with { Approvals = request.Members.ToArray() },
                Refusals = null,
                Blocker = HousingBlockers.AwaitingAnswer,
            });
            using var restored = WorldRuntime.Restore(
                WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(answered)), _ => provider);
            restored.Resume();
            var step = await restored.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            Assert.Equal(mayJoin ? Alpha : null, restored.Society.GetAgent(agent).HouseholdId);
            Assert.Null(Housing(restored, agent)?.Request);
            if (mayJoin)
            {
                Assert.Equal(request.ExpiryTick, restored.WorldTick);
                Assert.Contains(step.Events, item => item.Kind == "household_joined");
            }
            else
            {
                Assert.Contains(step.Events, item => item.Kind == "housing_request_expired");
                Assert.DoesNotContain(step.Events, item => item.Kind == "household_joined");
                Assert.DoesNotContain(agent, restored.Society.GetHousehold(Alpha).MemberIds);
                Assert.Equal(restored.WorldTick, Assert.Single(Housing(restored, agent)!.Refusals!).Tick);
            }
        }
    }

    [Fact]
    public async Task AdultPlacedOnHouseholdPropertyJoinsWithoutConsentAndIsNeverAsked()
    {
        var provider = new ScriptedProvider();
        using var world = NormalPathWorld.CreateGenerated("housing-property", _ => provider);
        var house = world.WorldSimulation.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var agent = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Equal(Alpha, world.AddAgent(agent, house.Position));
        provider.Choices[agent] = "safe_idle";
        await AdvanceUntil(world, () => provider.Offered.ContainsKey(agent));
        for (var tick = 0; tick < 5; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        Assert.Equal(Alpha, world.Society.GetAgent(agent).HouseholdId);
        Assert.DoesNotContain(provider.Offered[agent].Keys, id => id.StartsWith("household_ask:", StringComparison.Ordinal));
        Assert.Contains("household_leave", provider.Offered[agent].Keys);
        Assert.Contains("House: 3/3 permanent places (is full)", provider.HousingNotes[agent], StringComparison.Ordinal);
        Assert.Contains("inside; absences still count", provider.HousingNotes[agent], StringComparison.Ordinal);
        Assert.Null(world.Agents.Single(person => person.AgentId == agent).Housing);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind.StartsWith("housing_", StringComparison.Ordinal));
        var visible = new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == agent);
        Assert.DoesNotContain(visible.DecisionFactors, factor => factor.Key == "housing");
    }

    [Fact]
    public void AddAgentCannotUseAnotherHouseholdBuildingToExceedHousePlaces()
    {
        using var seed = NormalPathWorld.CreateGenerated("house-resident-place-boundary", _ => new ScriptedProvider());
        var state = seed.ExportState();
        var house = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "resident-boundary-stone", "stone",
            Alpha, 2, storageBuildingId: house.InstanceId);
        state = FarmFieldTests.WithInventory(state, inventory);
        using var world = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => new ScriptedProvider());
        var houseDefinition = world.WorldContent.Buildings.Single(item => item.CanonicalId == house.DefinitionId);
        var footprint = BuildingStorageRules.EffectiveDefinition(houseDefinition, house);
        var initial = HouseResidentCapacityRules.Calculate(
            world.Society.Agents.Where(person => person.HouseholdId == Alpha), footprint.Width, footprint.Height);
        Assert.Equal(2, initial.ResidentCount);
        Assert.Equal(3, initial.Limit);

        var first = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Equal(Alpha, world.AddAgent(first, house.Position));
        var full = HouseResidentCapacityRules.Calculate(
            world.Society.Agents.Where(person => person.HouseholdId == Alpha), footprint.Width, footprint.Height);
        Assert.Equal(3, full.ResidentCount);
        Assert.Equal(3, full.Limit);
        Assert.False(full.IsOvercrowded);

        var farmhouse = world.WorldContent.Buildings.Single(item => item.LocalId == "farmhouse-1x1");
        var otherProperty = TownTileBeside(world, house.InstanceId);
        var placed = world.PlaceBuilding("alpha-other-property", farmhouse.CanonicalId, otherProperty, Alpha);
        Assert.True(placed.Applied, placed.Failure);
        var guest = world.Society.GetHousehold(Beta).MemberIds[0];
        Assert.True(world.SetHouseGuestInvitation(world.Society.GetHousehold(Alpha).MemberIds[0],
            house.InstanceId, guest, true).Applied);

        var awayState = world.ExportState();
        var occupiedTiles = awayState.WorldSimulation!.Buildings.SelectMany(building =>
            WorldContentSimulationRules.Footprint(awayState.WorldContent!.Buildings.Single(definition =>
                definition.CanonicalId == building.DefinitionId), building)).ToHashSet();
        var householdMember = awayState.Agents.Single(person => person.AgentId == first);
        var awayTile = awayState.Map.Tiles.Select(tile => tile.Position).First(position =>
            awayState.Map.IsBuildable(position) && !occupiedTiles.Contains(position) &&
            !awayState.Agents.Any(person => person.AgentId != householdMember.AgentId &&
                person.Position == position));
        awayState = awayState with
        {
            Agents = awayState.Agents.Select(person => person.AgentId == householdMember.AgentId
                ? person with { Position = awayTile }
                : person).ToArray(),
        };

        using var restored = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(awayState)),
            _ => new ScriptedProvider());
        var visible = new OwnerWorldObservationStore(restored).GetSnapshot().PlacedBuildings
            .Single(item => item.InstanceId == house.InstanceId);
        Assert.Equal(3, visible.ResidentLimit);
        Assert.Equal(3, visible.PermanentResidentCount);
        Assert.False(visible.HasDominantFamily);
        Assert.False(visible.IsOvercrowded);
        Assert.Equal(restored.Society.GetAgent(guest).Name, Assert.Single(visible.InvitedGuests!));
        Assert.True(restored.Agents.Count(person =>
            restored.Society.GetAgent(person.AgentId).HouseholdId == Alpha &&
            WorldContentSimulationRules.Footprint(restored.WorldContent.Buildings.Single(definition =>
                definition.CanonicalId == house.DefinitionId), house).Contains(person.Position)) < visible.PermanentResidentCount,
            "A resident who is away still takes a permanent House place.");

        var beforeState = WorldRuntimeCodec.Encode(restored.ExportState());
        var before = restored.Society.GetHousehold(Alpha).MemberIds.ToArray();
        var rejected = Assert.Throws<InvalidOperationException>(() => restored.AddAgent(
            "agent:" + Guid.NewGuid().ToString("N"), otherProperty));
        Assert.Contains("no free resident place", rejected.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, restored.Society.GetHousehold(Alpha).MemberIds);
        Assert.Equal(beforeState, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(3, restored.Society.Agents.Count(person => person.HouseholdId == Alpha &&
            person.Status == SocietyAgentStatus.Active));
    }

    [Fact]
    public void AddAgentOnPropertyOfAHouseholdWithoutAHouseStillJoinsIt()
    {
        using var seed = NormalPathWorld.CreateGenerated("house-resident-no-house", _ => new ScriptedProvider());
        var state = seed.ExportState();
        var house = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        // Empty the House so the owner may remove it.
        var stored = state.Society.Society.Inventory.Lots.Where(lot =>
            lot.StorageBuildingId == house.InstanceId || lot.DeliveryBuildingId == house.InstanceId)
            .Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => !stored.Contains(lot.Id) &&
                (lot.ContainerLotId is null || !stored.Contains(lot.ContainerLotId))).ToArray(),
            Reservations = state.Society.Society.Inventory.Reservations
                .Where(reservation => !stored.Contains(reservation.LotId)).ToArray(),
        };
        // The Farmhouse's stone comes from household stock outside the House.
        inventory = InventoryAuthority.AddLot(inventory, "no-house-farm-stone", "stone", Alpha, 2);
        state = FarmFieldTests.WithInventory(state, inventory);
        using var world = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => new ScriptedProvider());
        var farmhouse = world.WorldContent.Buildings.Single(item => item.LocalId == "farmhouse-1x1");
        var free = FreeTiles(world).ToHashSet();
        var town = world.Towns.Single();
        var property = MovementTestWorld.BuildingSite(world.ExportState(), farmhouse, string.Empty,
            allowed: point => free.Contains(point) && town.BorderTiles.Contains(point) &&
                state.Map.FootDistance(point, house.Position) <= 4);
        var placed = world.PlaceBuilding("alpha-farm-without-house", farmhouse.CanonicalId, property, Alpha);
        Assert.True(placed.Applied, placed.Failure);
        var removed = world.RemoveBuilding(house.InstanceId, house.TownId, Alpha);
        Assert.True(removed.Applied, removed.Failure);

        // With no House there are no resident places to fill, so the new agent joins as before.
        var agent = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Equal(Alpha, world.AddAgent(agent, property));
        Assert.Equal(Alpha, world.Society.GetAgent(agent).HouseholdId);
        world.Validate();
    }

    [Fact]
    public async Task PendingAdmissionCannotUseAFamilyBonusThatItWouldRemove()
    {
        var provider = new ScriptedProvider();
        using var seed = NormalPathWorld.CreateGenerated("housing-family-bonus-boundary", _ => provider);
        var house = seed.WorldSimulation.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var existing = seed.Society.GetHousehold(Alpha).MemberIds.ToArray();
        var third = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Equal(Alpha, seed.AddAgent(third, house.Position));
        var applicant = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Null(seed.AddAgent(applicant, TownTileBeside(seed, house.InstanceId)));

        seed.Pause();
        var state = seed.ExportState();
        var familyId = "domestic:parents:alice";
        var societyState = state.Society with
        {
            Society = state.Society.Society with
            {
                Agents = state.Society.Society.Agents.Select(person =>
                    existing.Contains(person.Id, StringComparer.Ordinal)
                        ? person with { DomesticFamilyUnitId = familyId }
                        : person).ToArray(),
            },
        };
        state = state with { Society = societyState };

        using var world = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            _ => provider);
        var current = HouseResidentCapacityRules.Calculate(
            world.Society.Agents.Where(person => person.HouseholdId == Alpha), 1, 1);
        Assert.Equal(3, current.ResidentCount);
        Assert.Equal(4, current.Limit);
        Assert.True(current.HasFreePlace);
        provider.Choices[applicant] = "household_ask:" + Alpha;
        world.Resume();
        await AdvanceUntil(world, () => provider.Offered.TryGetValue(applicant, out var offered) &&
            offered.Count > 0);

        Assert.DoesNotContain("household_ask:" + Alpha, provider.Offered[applicant].Keys);
        Assert.Contains("household_ask:" + Beta, provider.Offered[applicant].Keys);
        Assert.Null(world.Society.GetAgent(applicant).HouseholdId);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "housing_request_made" &&
            item.Detail == $"{applicant}:{Alpha}");
    }

    [Fact]
    public async Task HouseholdWithoutAHouseIsToldAboutMissingMaterialsAndThenNoLegalSite()
    {
        var provider = new ScriptedProvider();
        using var seed = new WorldRuntime("housing-sites", _ => provider, startPace: WorldStartPace.FounderSetup);
        var positions = new[] { new GridPoint(0, 0), new GridPoint(1, 2), new GridPoint(2, 2), new GridPoint(3, 2) };
        for (var index = 0; index < positions.Length; index++)
            seed.PlaceFounder($"founder:{index + 1:D32}", positions[index]);
        seed.StartWorld();
        Assert.True(seed.StageStarterContent());
        await AdvanceUntil(seed, () => seed.WorldContent.Buildings.Any(item => item.LocalId == "farmhouse-1x1"));
        var house = seed.WorldContent.Buildings.Single(item => item.LocalId == "house-1x1");
        var filler = seed.WorldContent.Buildings.Single(item => item.LocalId == "farmhouse-1x1");
        var placed = seed.PlaceBuilding("alpha-house", house.CanonicalId, FreeTiles(seed)[0], Alpha);
        Assert.True(placed.Applied, placed.Failure);
        var agent = "agent:" + Guid.NewGuid().ToString("N");
        var householdId = seed.AddAgent(agent, FreeTiles(seed)[0]) ?? "household:newcomers";

        // A household of its own, without a House and without wood.
        var state = seed.ExportState();
        var society = state.Society.Society;
        if (!society.Households.Any(item => item.Id == householdId))
        {
            var tick = society.WorldTick;
            society = society with
            {
                Households = society.Households.Append(new SocietyHousehold(householdId, "Newcomers", [agent], []))
                    .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
                Agents = society.Agents.Select(person => person.Id == agent ? person with { HouseholdId = householdId } : person).ToArray(),
                Relationships = society.Relationships.Append(new SocietyRelationship($"{householdId}:membership:{agent}", 1,
                        SocietyRelationshipType.HouseholdMembership, householdId, agent, SocietyRelationshipState.Accepted,
                        SocietyConsentState.ProtectedLifecycle, tick, tick, "household", householdId,
                        new[] { householdId, agent }.Order(StringComparer.Ordinal).ToArray()))
                    .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            };
        }
        state = state with { Society = state.Society with { Society = society } };
        using (var unstocked = WorldRuntime.Restore(state, _ => provider))
        {
            await AdvanceUntil(unstocked, () => Housing(unstocked, agent)?.Blocker == HousingBlockers.MissingMaterials, 5);
            Assert.DoesNotContain(provider.Offered.GetValueOrDefault(agent)?.Keys.ToArray() ?? [],
                id => id.StartsWith("household_ask:", StringComparison.Ordinal));
            Assert.Contains(unstocked.ExportState().Events, item => item.Kind == "housing_blocked" && item.Detail == $"{agent}:missing_materials");
            Assert.Contains("lacks the materials", new OwnerWorldObservationStore(unstocked).GetSnapshot().Agents
                .Single(person => person.Id == agent).DecisionFactors.Single(factor => factor.Key == "housing").Detail, StringComparison.Ordinal);
        }

        // With the wood in hand a House can be planned, so that is what the adult is told.
        var wood = house.BuildCosts.Single(cost => cost.ResourceId == "wood").Amount;
        state = state with
        {
            Society = state.Society with
            {
                Society = society with { Inventory = InventoryAuthority.AddLot(society.Inventory, "newcomer-wood", "wood", householdId, wood) },
            },
        };
        using (var stocked = WorldRuntime.Restore(state, _ => provider))
        {
            await AdvanceUntil(stocked, () => Housing(stocked, agent)?.Blocker == HousingBlockers.NoAuthorizedHome, 5);
            await AdvanceUntil(stocked, () => provider.Offered.GetValueOrDefault(agent)?.Keys.Any(id =>
                TownConstructionCandidateIds.TryParse(id, out var selection) && selection.IsBuilding &&
                selection.DefinitionId == house.CanonicalId) == true);
            Assert.Contains("holds no House yet", provider.HousingNotes[agent], StringComparison.Ordinal);
            state = stocked.ExportState();
        }

        // Standing in a House with every other tile taken, there is no legal site.
        // The owner's fill placements draw their costs from the first household.
        var fillStock = filler.BuildCosts.Aggregate(state.Society.Society.Inventory, (inventory, cost) =>
            InventoryAuthority.AddLot(inventory, "fill-" + cost.ResourceId, cost.ResourceId, Alpha, cost.Amount * 40));
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == agent
                ? person with { Position = placed.Position } : person).ToArray(),
            Society = state.Society with { Society = state.Society.Society with { Inventory = StockHoldTestState.Custody(fillStock) } },
        };
        using var crowded = WorldRuntime.Restore(state, _ => provider);
        // Each paid placement may lay a Road or reserve a door. Fill until
        // no remaining free tile can hold both the footprint and its access.
        var initialBuildings = crowded.WorldSimulation.Buildings.Count;
        while (FreeTiles(crowded).Select(site => crowded.PlaceBuilding(
                   $"fill-{site.X}-{site.Y}", filler.CanonicalId, site)).Any(result => result.Applied)) { }
        Assert.True(crowded.WorldSimulation.Buildings.Count > initialBuildings);
        await AdvanceUntil(crowded, () => Housing(crowded, agent)?.Blocker == HousingBlockers.NoLegalSite, 5);
        Assert.Contains(crowded.ExportState().Events, item => item.Kind == "housing_blocked" && item.Detail == $"{agent}:no_legal_site");
        Assert.True((await crowded.AdvanceOneTickAsync()).Advanced);
        Assert.DoesNotContain(provider.Offered[agent].Keys, id => id.StartsWith("build:building:", StringComparison.Ordinal));
        var ownerHousing = new OwnerWorldObservationStore(crowded).GetSnapshot().Agents
            .Single(person => person.Id == agent).DecisionFactors.Single(factor => factor.Key == "housing").Detail;
        Assert.Contains("no legal site", ownerHousing, StringComparison.Ordinal);
        Assert.Contains(HousingBlockers.LandRecoveryGuidance, ownerHousing, StringComparison.Ordinal);
        crowded.SubmitInstruction(new OwnerInstructionRequest("no-site-land-guidance", "owner:test", agent,
            OwnerInstructionKind.Suggestion, "Consider how to get land for the House that has no legal site."));
        await AdvanceUntil(crowded, () => provider.HousingNotes.GetValueOrDefault(agent)?.Contains("no legal site", StringComparison.Ordinal) == true, 5);
        Assert.True(provider.HousingNotes[agent]?.Contains("land", StringComparison.Ordinal) == true &&
            provider.HousingNotes[agent]?.Contains("Council", StringComparison.Ordinal) == true,
            provider.HousingNotes[agent] + "\n" + string.Join("\n", provider.Offered[agent].Where(item => item.Key.StartsWith("civic|", StringComparison.Ordinal))));
        Assert.Contains(HousingBlockers.LandRecoveryGuidance, provider.HousingNotes[agent], StringComparison.Ordinal);
        Assert.DoesNotContain(provider.Offered[agent].Keys, id => id.Contains("|request_land_use|", StringComparison.Ordinal) ||
            id.Contains("|claim_land|", StringComparison.Ordinal));
        crowded.Validate();
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(crowded.ExportState())));
        Assert.Equal(HousingBlockers.NoLegalSite, Housing(reloaded, agent)?.Blocker);
    }

    [Fact]
    public async Task ForgedOrOldSchemaHousingStateIsRefused()
    {
        var provider = new ScriptedProvider();
        using var world = NormalPathWorld.CreateGenerated("housing-forged", _ => provider);
        var agent = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Null(world.AddAgent(agent, TownTileBeside(world, "first-town-house-a")));
        provider.Choices[agent] = "household_ask:" + Alpha;
        await AdvanceUntil(world, () => Housing(world, agent)?.Request is not null);
        world.Pause();
        var state = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
        var request = Housing(state, agent)!.Request!;

        // Null collection elements must be rejected as damaged checkpoint data,
        // rather than escaping as an argument exception about the save ID.
        var document = System.Text.Json.Nodes.JsonNode.Parse(WorldRuntimeCodec.Encode(state))!;
        var savedApplicant = document["state"]!["agents"]!.AsArray()
            .Single(item => item!["agentId"]!.GetValue<string>() == agent)!;
        savedApplicant["housing"]!["request"]!["members"]![0] = null;
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(
            System.Text.Encoding.UTF8.GetBytes(document.ToJsonString())));

        var old = Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(state with { SchemaVersion = 31 }));
        Assert.Contains($"minimum supported schema {WorldRuntime.StateSchemaVersion}", old.Message,
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(WithHousing(state, agent,
            housing => housing with { Request = request with { Approvals = [agent] } })));
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(WithHousing(state, agent,
            housing => housing with { Request = request with { ExpiryTick = request.ExpiryTick + 1 } })));
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(WithHousing(state, agent,
            housing => housing with { Request = request with { HouseholdId = "household:nobody" } })));
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(WithHousing(state, agent,
            housing => housing with { Refusals = [new HousingRefusal("household:nobody", 0)] })));
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(WithHousing(state, agent,
            housing => housing with { Blocker = "no_such_reason" })));
        var housed = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Agents = state.Society.Society.Agents.Select(person => person.Id == agent
                        ? person with { HouseholdId = Beta } : person).ToArray(),
                },
            },
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(housed));
        using var intact = WorldRuntime.Restore(state);
        Assert.Equal(request, Housing(intact, agent)!.Request);
    }

    [Fact]
    public async Task WaitingForHousingWithABoundedHouseholdNameKeepsModelContextValid()
    {
        var provider = new ScriptedProvider();
        using var world = NormalPathWorld.CreateGenerated("housing-long-name", _ => provider);
        var agent = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Null(world.AddAgent(agent, TownTileBeside(world, "first-town-house-a")));
        provider.Choices[agent] = "household_ask:" + Alpha;
        await AdvanceUntil(world, () => Housing(world, agent)?.Request is not null);
        provider.Choices[agent] = "safe_idle";
        world.Pause();
        var householdName = new string('A', 128);
        var state = world.ExportState();
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Households = state.Society.Society.Households.Select(household => household.Id == Alpha
                        ? household with { Name = householdName } : household).ToArray(),
                },
            },
        };
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => provider);
        restored.Resume();
        await AdvanceUntil(restored, () => provider.HousingNotes.GetValueOrDefault(agent)?.Contains(householdName, StringComparison.Ordinal) == true);
        Assert.Contains("Every adult member must agree", provider.HousingNotes[agent], StringComparison.Ordinal);
    }

    private static HousingState? Housing(WorldRuntime world, string agent) =>
        world.Agents.Single(person => person.AgentId == agent).Housing;

    private static HousingState? Housing(WorldRuntimeState state, string agent) =>
        state.Agents.Single(person => person.AgentId == agent).Housing;

    private static WorldRuntimeState WithHousing(WorldRuntimeState state, string agent,
        Func<HousingState, HousingState> change)
    {
        return state with
        {
            Agents = state.Agents.Select(person => person.AgentId == agent
                ? person with { Housing = change(person.Housing!) } : person).ToArray(),
        };
    }

    private static (CognitionDecisionRequest Request, Task Task) CapturePendingHousingCall(WorldRuntime world, string member)
    {
        var call = Assert.Single(world.PendingModelWork, item => item.Transition.Kind == ModelWorkKind.Decision && item.Transition.ActorId == member);
        return (Assert.IsType<CognitionDecisionRequest>(call.DecisionRequest), call.Completion);
    }

    private static async Task AdvanceUntil(WorldRuntime world, Func<bool> condition, int maxTicks = 40, Action? onTick = null)
    {
        for (var tick = 0; tick < maxTicks && !condition(); tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            onTick?.Invoke();
        }
        Assert.True(condition(), $"The expected state was not reached within {maxTicks} ticks.");
    }

    /// <summary>Unclaimed Town land near a building: buildable, not built on, not a Road and empty.</summary>
    private static GridPoint TownTileBeside(WorldRuntime world, string instanceId)
    {
        var anchor = world.WorldSimulation.Buildings.Single(item => item.InstanceId == instanceId).Position;
        var town = world.Towns.Single();
        var free = FreeTiles(world).ToHashSet();
        return Enumerable.Range(1, 4)
            .SelectMany(distance => Enumerable.Range(-distance, 2 * distance + 1)
                .SelectMany(dx => Enumerable.Range(-distance, 2 * distance + 1)
                    .Select(dy => new GridPoint(anchor.X + dx, anchor.Y + dy))))
            .First(point => free.Contains(point) && town.BorderTiles.Contains(point));
    }

    /// <summary>Buildable tiles with no map object, resource, building, Road or agent, in a stable order.</summary>
    private static GridPoint[] FreeTiles(WorldRuntime world)
    {
        var state = world.ExportState();
        var map = state.Map;
        var definitions = world.WorldContent.Buildings.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
        var footprints = world.WorldSimulation.Buildings
            .SelectMany(building => WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building))
            .ToHashSet();
        var roads = world.RoadTiles.ToHashSet();
        return map.Tiles.Select(tile => tile.Position)
            .Where(point => map.IsBuildable(point) && !footprints.Contains(point) && !roads.Contains(point) &&
                !map.MapObjects.Any(item => item.Position == point) && !map.Resources.Any(item => item.Position == point) &&
                !state.Agents.Any(person => person.Position == point))
            .OrderBy(point => point.Y).ThenBy(point => point.X)
            .ToArray();
    }

    /// <summary>
    /// Chooses, for each agent, the first offered candidate with the scripted
    /// prefix, or safe_idle, and records what each agent was offered and told.
    /// <see cref="AnythingButHousing"/> lets the built-in rules choose among
    /// everything except housing answers, so the agent stays busy and is asked
    /// again soon instead of idling for a long time.
    /// </summary>
    private sealed class ScriptedProvider : IDecisionProvider
    {
        public const string AnythingButHousing = "!household_";

        public ConcurrentDictionary<string, string> Choices { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, Dictionary<string, string>> Offered { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, string?> HousingNotes { get; } = new(StringComparer.Ordinal);
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            Offered[observation.AgentId] = observation.Candidates.ToDictionary(item => item.Id, item => item.Description, StringComparer.Ordinal);
            HousingNotes[observation.AgentId] = observation.Self?.HousingNote;
            var prefix = Choices.GetValueOrDefault(observation.AgentId, "safe_idle");
            var candidates = prefix == AnythingButHousing
                ? observation.Candidates.Where(item => !item.Id.StartsWith("household_", StringComparison.Ordinal)).ToArray()
                : [observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(prefix, StringComparison.Ordinal))
                    ?? observation.Candidates.Single(item => item.Id == "safe_idle")];
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = observation with { Candidates = candidates },
            }, cancellationToken);
        }
    }
}
