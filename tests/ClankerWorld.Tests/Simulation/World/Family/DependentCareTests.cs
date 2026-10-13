using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed partial class ParenthoodTests
{
    [Fact]
    public void EndingOneCareObligationPreservesOtherDependentsHouseholdProjection()
    {
        var adult = SocietyRules.CreateFounder("adult", "Adult");
        var child = SocietyRules.CreateFounder("child", "Child") with
        {
            BirthTick = 0,
            AgeBand = SocietyAgeBand.Infant,
            LastLifecycleYearChecked = 0,
        };
        var checkpoint = SocietyRules.CreateGenesis("multiple-care", [adult, child, child with { Id = "other", Name = "Other" }]);
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "home", "Home", ["adult", "child", "other"]).Checkpoint;
        var first = SocietyRules.AssumeInfantCare(checkpoint, "adult", "child");
        var second = SocietyRules.AssumeInfantCare(first.Checkpoint, "adult", "other");
        checkpoint = SocietyRules.RevokeRelationship(second.Checkpoint, first.CreatedId!, "adult").Checkpoint;
        Assert.Contains("adult", checkpoint.GetHousehold("home").CaregiverIds);
        checkpoint = SocietyRules.RevokeRelationship(checkpoint, second.CreatedId!, "adult").Checkpoint;
        Assert.DoesNotContain("adult", checkpoint.GetHousehold("home").CaregiverIds);
    }

    [Fact]
    public async Task OrphanedInfantGetsOneVoluntaryCaregiverAndRealCareAcrossRestart()
    {
        var state = await OrphanState();
        var child = state.Society.Society.Births.Single().ChildId;
        using var world = WorldRuntime.Restore(state, _ => new ParentProvider("guardian_accept:"));
        for (var tick = 0; tick < 40 && !world.Society.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                 edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted); tick++) await world.AdvanceOneTickAsync();
        var caregiver = Assert.Single(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted);
        Assert.Equal(SocietyConsentState.Accepted, caregiver.Consent);
        Assert.Equal([caregiver.ProposerId], caregiver.AcceptedBy);
        Assert.Equal(2, world.Society.Relationships.Count(edge => edge.Type == SocietyRelationshipType.BiologicalParentage && edge.TargetId == child));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "guardian_needed" && item.Detail == child);
        Assert.Single(world.ExportState().Events, item => item.Kind == "guardian_needed" && item.Detail == child);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "guardian_assigned" && item.Detail == child);
        world.Pause();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new ParentProvider("care:"));
        Assert.False((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.Resume();
        for (var tick = 0; tick < 80 && !restored.ExportState().Events.Any(item => item.Kind == "child_cared_for"); tick++)
            await restored.AdvanceOneTickAsync();
        Assert.Contains(restored.ExportState().Events, item => item.Kind == "child_cared_for");
        Assert.True(restored.Agents.Single(person => person.AgentId == child).FullnessBasisPoints > 3_000);
        Assert.Contains(new OwnerWorldObservationStore(restored).GetSnapshot().Agents.Single(person => person.Id == child).Relationships,
            edge => edge.Type == "caregiver");
    }

    [Fact]
    public async Task OlderDependentKeepsVisibleGuardianNeedUntilAnAdultAcceptsAcrossReload()
    {
        var state = await OrphanState(olderChild: true);
        var child = state.Society.Society.Births.Single().ChildId;
        using var world = WorldRuntime.Restore(state, _ => new ParentProvider("safe_idle"));
        for (var tick = 0; tick < 1; tick++)
            await world.AdvanceOneTickAsync();
        var pending = world.ExportState();
        var initialSearch = Assert.Single(pending.Agents, person => person.AgentId == child).GuardianSearch!;
        Assert.Equal("household", initialSearch.Stage);
        Assert.NotEmpty(initialSearch.OfferedAdultIds);
        Assert.Single(pending.Events, item => item.Kind == "guardian_needed" && item.Detail == child);
        var wrongStage = pending with
        {
            Agents = pending.Agents.Select(person => person.AgentId == child
                ? person with { GuardianSearch = initialSearch with { Stage = "relatives" } }
                : person).ToArray(),
        };
        var invalidSearch = Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(wrongStage, _ => new ParentProvider("safe_idle")));
        Assert.Contains("guardian search", invalidSearch.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Needs a guardian. No adult has accepted care yet; nearby adults may still feed them.",
            new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == child).SocialNotes);
        world.Pause();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var pendingReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new ParentProvider("safe_idle"));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(pendingReload.ExportState()));

        var acceptingAdult = initialSearch.OfferedAdultIds[0];
        var acceptingProvider = new ParentProvider("guardian_accept:");
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            actor => actor == acceptingAdult ? acceptingProvider : new ParentProvider("safe_idle"));
        restored.Resume();
        var stageTicks = restored.ExportState().WorldSystems!.Config.TicksPerDay;
        for (var tick = 0; tick < stageTicks && !restored.Society.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                 edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted); tick++)
            await restored.AdvanceOneTickAsync();
        var accepted = Assert.Single(restored.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted);
        Assert.Equal(acceptingAdult, accepted.ProposerId);
        Assert.Contains(acceptingProvider.SeenCandidates, candidate => candidate.Id == "guardian_accept:" + child);
        // Household replacement care is only for a child whose caregiver is still alive; an orphan goes through the search.
        Assert.DoesNotContain(acceptingProvider.SeenCandidates, candidate => candidate.Id == "household_accept_care:" + child);
        Assert.Null(SocietyRules.AcceptReplacementCare(state.Society.Society, acceptingAdult, child).CreatedId);
        Assert.True(accepted.EffectiveTick - state.Society.Society.WorldTick < stageTicks,
            "A newly opened household offer must be decided before its one-day stage expires.");
        Assert.Equal(SocietyConsentState.Accepted, accepted.Consent);
        Assert.Equal([accepted.ProposerId], accepted.AcceptedBy);
        Assert.Null(Assert.Single(restored.ExportState().Agents, person => person.AgentId == child).GuardianSearch);
        Assert.Single(restored.ExportState().Events, item => item.Kind == "guardian_needed" && item.Detail == child);
        Assert.Single(restored.ExportState().Events, item => item.Kind == "guardian_assigned" && item.Detail == child);

        using var waiting = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new ParentProvider("safe_idle"));
        waiting.Resume();
        var waitingStageTicks = waiting.ExportState().WorldSystems!.Config.TicksPerDay;
        for (var tick = 0; tick < waitingStageTicks; tick++) await waiting.AdvanceOneTickAsync();
        var townPending = waiting.ExportState();
        Assert.Equal("town", Assert.Single(townPending.Agents, person => person.AgentId == child).GuardianSearch!.Stage);
        Assert.Single(townPending.Events, item => item.Kind == "guardian_needed" && item.Detail == child);
        waiting.Pause();
        var townSave = WorldRuntimeCodec.Encode(waiting.ExportState());
        using var townRestored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(townSave), _ => new ParentProvider("safe_idle"));
        Assert.Equal(townSave, WorldRuntimeCodec.Encode(townRestored.ExportState()));
    }

    [Fact]
    public async Task OutsideHouseholdRelativeGetsFirstOfferDuringAContinuingProject()
    {
        const string relativeId = NormalPathWorld.Founder3;
        const string householdAdultId = NormalPathWorld.Founder4;
        const int testDay = 120;
        var buildProvider = new ParentProvider("build:building:");
        using var generated = NormalPathWorld.CreateGenerated("guardian-relative-priority-project", _ => new ParentProvider("safe_idle"),
            ticksPerDay: testDay, calendarOffsetTicks: 90);
        generated.Pause();
        var state = generated.ExportState();
        Assert.Equal(0, state.Society.Society.WorldTick);
        var originalDay = state.Society.Society.Config.TicksPerWorldDay;
        // Keep both complete guardian-search windows and their reloads, using a shorter
        // test day. Preserve founder ages and initialize weather for the matching calendar.
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Config = state.Society.Society.Config with { TicksPerWorldDay = testDay },
                    Agents = state.Society.Society.Agents.Select(person => person with
                    {
                        BirthTick = person.BirthTick / originalDay * testDay,
                        BirthLifeTick = person.BirthLifeTick is { } birth ? birth / originalDay * testDay : null,
                    }).ToArray(),
                },
            },
            WorldSystems = ClankerWorld.Simulation.World.RegionalWeatherRules.Initialize(state.WorldSystems! with
            {
                Config = state.WorldSystems.Config with { TicksPerDay = testDay },
                RegionalWeather = null,
            }, state.Map),
        };
        var originHouse = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-house-a");
        var relativeHouse = state.WorldSimulation.Buildings.Single(item => item.InstanceId == "first-town-house-b");
        var childHousehold = originHouse.HouseholdId!;
        var parents = state.Society.Society.GetHousehold(childHousehold).MemberIds.Order(StringComparer.Ordinal).ToArray();
        var relativeHousehold = relativeHouse.HouseholdId!;
        var relative = Assert.Single(state.Society.Society.GetHousehold(relativeHousehold).MemberIds, id => id == relativeId);
        var householdAdult = state.Society.Society.GetHousehold(relativeHousehold).MemberIds.Single(id => id == householdAdultId);
        Assert.Equal(2, parents.Length);
        Assert.DoesNotContain(parents, id => id == relative || id == householdAdult);

        var checkpoint = state.Society.Society;
        var relativeMembership = Assert.Single(checkpoint.Relationships, edge =>
            edge.Type == SocietyRelationshipType.HouseholdMembership && edge.TargetId == relative &&
            edge.State == SocietyRelationshipState.Accepted);
        checkpoint = SocietyRules.RevokeRelationship(checkpoint, relativeMembership.Id, relative).Checkpoint;
        checkpoint = SocietyRules.CreateHousehold(checkpoint, "household:guardian-relative", "Relative household", [relative]).Checkpoint;
        var buildInventory = checkpoint.Inventory with
        {
            Lots = checkpoint.Inventory.Lots.Where(lot => lot.OwnerId != relative || lot.ItemKind != "tool").ToArray(),
        };
        buildInventory = InventoryAuthority.AddLot(buildInventory, "guardian-search-project-wood", "wood", relative, 8);
        checkpoint = checkpoint with { Inventory = buildInventory };
        var cognition = state.Society.Cognition with
        {
            Queue = [],
            Runtimes = state.Society.Cognition.Runtimes.Select(runtime => runtime.AgentId == relative
                ? runtime with { CurrentIntention = null }
                : runtime).ToArray(),
        };
        state = state with
        {
            Society = state.Society with { Society = checkpoint, Cognition = cognition },
            Agents = state.Agents.Select(person => person.AgentId == relative
                ? person with { Project = null, LastDecisionContext = null }
                : person).ToArray(),
        };

        using var builder = WorldRuntime.Restore(state, actor => actor == relative
            ? buildProvider : new ParentProvider("safe_idle"));
        builder.Resume();
        for (var tick = 0; tick < 80 && builder.Agents.Single(person => person.AgentId == relative).Project is null; tick++)
            Assert.True((await builder.AdvanceOneTickAsync()).Advanced);
        var project = Assert.IsType<AgentProject>(
            builder.Agents.Single(person => person.AgentId == relative).Project);
        Assert.StartsWith("build:building:", project.CandidateId, StringComparison.Ordinal);
        Assert.Contains(buildProvider.SelectedCandidateIds, id => id == project.CandidateId);
        var projectIntention = Assert.Single(builder.ExportState().Society.Cognition.Runtimes,
            runtime => runtime.AgentId == relative).CurrentIntention;
        Assert.Equal(project.CandidateId, projectIntention?.CandidateId);
        builder.Pause();
        state = builder.ExportState();

        checkpoint = state.Society.Society;
        var partnershipId = "guardian-priority-parents";
        checkpoint = SocietyRules.ProposeRelationship(checkpoint, new(partnershipId, 1,
            SocietyRelationshipType.Partnership, parents[0], parents[1], checkpoint.WorldTick)).Checkpoint;
        checkpoint = SocietyRules.AcceptRelationship(checkpoint, partnershipId, 1, parents[1]).Checkpoint;
        var inventory = InventoryAuthority.AddLot(checkpoint.Inventory, "guardian-priority-birth-food", "food",
            childHousehold, 4, storageBuildingId: originHouse.InstanceId);
        checkpoint = checkpoint with { Inventory = inventory };
        checkpoint = ChosenBirthNameTestFixture.NameParent(checkpoint, parents[0]);
        var birth = SocietyRules.CommitBirth(checkpoint, new SocietyBirthRequest("guardian-priority-birth", 1,
            parents[0], parents[1], childHousehold, parents, parents, "guardian-priority-birth-food", 4,
            checkpoint.WorldTick, ChildName: ChosenBirthNameTestFixture.ChildName(checkpoint, parents[0], "Orphan"), PrimaryCaregiverId: parents[0]));
        var child = Assert.IsType<string>(birth.CreatedId);
        checkpoint = birth.Checkpoint;
        var grandparent = new SocietyRelationship("guardian-search-grandparent", 1,
            SocietyRelationshipType.BiologicalParentage, relative, parents[0],
            SocietyRelationshipState.Accepted, SocietyConsentState.ProtectedLifecycle,
            checkpoint.WorldTick, checkpoint.WorldTick, "family");
        checkpoint = checkpoint with
        {
            Relationships = checkpoint.Relationships.Append(grandparent).OrderBy(edge => edge.Id, StringComparer.Ordinal).ToArray(),
        };

        var deceased = new List<DeceasedAgentState>(state.DeceasedAgents ?? []);
        foreach (var parent in parents)
        {
            checkpoint = SocietyRules.Kill(checkpoint, parent, SocietyDeathCause.Accident, checkpoint.WorldTick).Checkpoint;
            var dead = checkpoint.GetAgent(parent);
            deceased.Add(new(parent, dead.DeathTick!.Value, checkpoint.AgeAt(dead, dead.DeathTick.Value),
                state.Agents.Single(person => person.AgentId == parent)));
        }
        Assert.Null(checkpoint.GetAgent(child).PrimaryCaregiverId);
        var oldHouseholdMembership = Assert.Single(checkpoint.Relationships, edge =>
            edge.Type == SocietyRelationshipType.HouseholdMembership && edge.TargetId == householdAdult &&
            edge.State == SocietyRelationshipState.Accepted);
        checkpoint = SocietyRules.RevokeRelationship(checkpoint, oldHouseholdMembership.Id, householdAdult).Checkpoint;
        checkpoint = SocietyRules.JoinHousehold(checkpoint, householdAdult, childHousehold).Checkpoint;
        Assert.Equal(childHousehold, checkpoint.GetAgent(householdAdult).HouseholdId);
        var childTile = state.Map.Tiles.Select(tile => tile.Position)
            .Where(point => state.Map.IsBuildable(point) &&
                !state.Agents.Any(person => person.Position == point) &&
                !state.Map.Resources.Any(resource => resource.Position == point) &&
                !state.Map.MapObjects.Any(startingSite => startingSite.Position == point) &&
                !(state.RoadTiles ?? []).Contains(point) &&
                !state.WorldSimulation!.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                    state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building))
                    .Contains(point))
            .OrderBy(point => Math.Abs(point.X - originHouse.Position.X) + Math.Abs(point.Y - originHouse.Position.Y))
            .ThenBy(point => point.Y).ThenBy(point => point.X).First();
        var activeIds = checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active)
            .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
        state = state with
        {
            Society = state.Society with { Society = checkpoint },
            DeceasedAgents = deceased,
            Survival = new SurvivalState(checkpoint.WorldTick, []),
            Agents = state.Agents.Where(person => !parents.Contains(person.AgentId, StringComparer.Ordinal))
                .Append(new AgentState(child, childTile, 10_000, 0, "curious", "grow with the household",
                    Survival: new SurvivalCondition(WarmthBasisPoints: 10_000)))
                .ToArray(),
            Towns = state.Towns!.Select(town => town with
            {
                ResidentIds = activeIds,
            }).ToArray(),
        };
        state = WithAdultCouncils(state);

        var guardianProvider = new ParentProvider("safe_idle");
        using var world = WorldRuntime.Restore(state, actor => actor == relative
            ? guardianProvider : new ParentProvider("safe_idle"));
        world.Resume();
        var initialRuntime = Assert.Single(world.ExportState().Society.Cognition.Runtimes,
            runtime => runtime.AgentId == relative);
        Assert.Equal(project.CandidateId, initialRuntime.CurrentIntention?.CandidateId);
        var priorGeneration = initialRuntime.DecisionGeneration;
        var stageTicks = world.ExportState().WorldSystems!.Config.TicksPerDay;
        for (var tick = 0; tick < stageTicks && !guardianProvider.SeenCandidates.Any(candidate => candidate.Id == "guardian_accept:" + child); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        Assert.Contains(guardianProvider.SeenCandidates, candidate => candidate.Id == "guardian_accept:" + child);
        Assert.True(Assert.Single(world.ExportState().Society.Cognition.Runtimes,
            runtime => runtime.AgentId == relative).DecisionGeneration > priorGeneration);
        var refreshedRuntime = Assert.Single(world.ExportState().Society.Cognition.Runtimes,
            runtime => runtime.AgentId == relative);
        Assert.Equal(guardianProvider.SelectedCandidateIds.Last(), refreshedRuntime.CurrentIntention?.CandidateId);
        Assert.Equal("safe_idle", refreshedRuntime.CurrentIntention?.CandidateId);
        Assert.Equal(project.CandidateId, Assert.Single(world.ExportState().Agents,
            person => person.AgentId == relative).Project?.CandidateId);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(project.CandidateId, Assert.Single(world.ExportState().Agents,
            person => person.AgentId == relative).Project?.CandidateId);
        var search = Assert.Single(world.ExportState().Agents, person => person.AgentId == child).GuardianSearch;
        Assert.NotNull(search);
        Assert.Equal("relatives", search.Stage);
        Assert.Equal([relative], search.OfferedAdultIds);

        world.Pause();
        var relativesSave = WorldRuntimeCodec.Encode(world.ExportState());
        using var waiting = WorldRuntime.Restore(WorldRuntimeCodec.Decode(relativesSave), _ => new ParentProvider("safe_idle"));
        Assert.Equal(relativesSave, WorldRuntimeCodec.Encode(waiting.ExportState()));
        waiting.Resume();
        for (var tick = 0; tick < stageTicks; tick++) Assert.True((await waiting.AdvanceOneTickAsync()).Advanced);
        var householdSearch = Assert.Single(waiting.ExportState().Agents, person => person.AgentId == child).GuardianSearch;
        Assert.NotNull(householdSearch);
        Assert.Equal("household", householdSearch.Stage);
        Assert.Equal(new[] { relative, householdAdult }.Order(StringComparer.Ordinal), householdSearch.OfferedAdultIds);

        waiting.Pause();
        var householdSave = WorldRuntimeCodec.Encode(waiting.ExportState());
        using var townWaiting = WorldRuntime.Restore(WorldRuntimeCodec.Decode(householdSave), _ => new ParentProvider("safe_idle"));
        Assert.Equal(householdSave, WorldRuntimeCodec.Encode(townWaiting.ExportState()));
        townWaiting.Resume();
        for (var tick = 0; tick < stageTicks; tick++) Assert.True((await townWaiting.AdvanceOneTickAsync()).Advanced);
        var townSearch = Assert.Single(townWaiting.ExportState().Agents, person => person.AgentId == child).GuardianSearch;
        Assert.NotNull(townSearch);
        Assert.Equal("town", townSearch.Stage);
        // The search widens: the relative and household adult are still asked alongside the Town.
        Assert.Contains(relative, townSearch.OfferedAdultIds);
        Assert.Contains(householdAdult, townSearch.OfferedAdultIds);
    }

    [Fact]
    public async Task AGuardianSearchSavedAfterAnOfferedAdultLeavesHomeStillLoads()
    {
        var state = await OrphanState(olderChild: true);
        var child = state.Society.Society.Births.Single().ChildId;
        using var probe = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            _ => new ParentProvider("safe_idle"));
        Assert.True((await probe.AdvanceOneTickAsync()).Advanced);
        var leaver = Assert.Single(probe.ExportState().Agents, person => person.AgentId == child)
            .GuardianSearch!.OfferedAdultIds[0];

        // The adult leaves in the same tick the search offered them care, after the search was refreshed.
        using var world = WorldRuntime.Restore(state, actor =>
            new ParentProvider(actor == leaver ? "household_leave" : "safe_idle"));
        for (var tick = 0; tick < 40 && world.Society.GetAgent(leaver).HouseholdId is not null; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Null(world.Society.GetAgent(leaver).HouseholdId);
        Assert.NotNull(Assert.Single(world.ExportState().Agents, person => person.AgentId == child).GuardianSearch);

        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new ParentProvider("safe_idle"));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task NearbyAdultCanFeedAnOrphanWhileGuardianSearchRemainsOpen()
    {
        var state = await OrphanState();
        var childId = state.Society.Society.Births.Single().ChildId;
        var adultId = state.Agents.First(person => person.AgentId != childId).AgentId;
        var child = state.Agents.Single(person => person.AgentId == childId);
        var feedingTile = state.Map.FootNeighbors(child.Position).First(point => state.Map.IsPassable(point) &&
            !state.Agents.Any(person => person.AgentId != adultId && person.Position == point));
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
                        "orphan-nearby-food", "berries", adultId, 1),
                },
            },
            Agents = state.Agents.Select(person => person.AgentId switch
            {
                var id when id == adultId => person with { Position = feedingTile, FullnessBasisPoints = 9_000, LastDecisionContext = null },
                var id when id == childId => person with { FullnessBasisPoints = 2_000, LastDecisionContext = null },
                _ => person,
            }).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, actor =>
            new ParentProvider(actor == adultId ? "care:" : "safe_idle"));
        for (var tick = 0; tick < 10 && !world.ExportState().Events.Any(item => item.Kind == "child_cared_for"); tick++)
            await world.AdvanceOneTickAsync();

        Assert.Contains(world.ExportState().Events, item => item.Kind == "child_cared_for" && item.Detail == childId);
        Assert.Null(world.Society.GetAgent(childId).PrimaryCaregiverId);
        Assert.NotNull(world.ExportState().Agents.Single(person => person.AgentId == childId).GuardianSearch);
        Assert.Single(world.ExportState().Events, item => item.Kind == "guardian_needed" && item.Detail == childId);
    }

    [Fact]
    public async Task AcceptedGuardianCanTendToAnIllOlderDependent()
    {
        var state = await OrphanState(olderChild: true);
        var childId = state.Society.Society.Births.Single().ChildId;
        var adultId = state.Agents.First(person => person.AgentId != childId).AgentId;
        using var proposing = WorldRuntime.Restore(state, actor =>
            new ParentProvider(actor == adultId ? "guardian_accept:" : "safe_idle"));
        for (var tick = 0; tick < 40 && proposing.Society.GetAgent(childId).PrimaryCaregiverId != adultId; tick++)
            await proposing.AdvanceOneTickAsync();

        Assert.Equal(adultId, proposing.Society.GetAgent(childId).PrimaryCaregiverId);
        var accepted = Assert.Single(proposing.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == adultId && edge.TargetId == childId && edge.State == SocietyRelationshipState.Accepted);
        var stateToTend = proposing.ExportState();
        var child = stateToTend.Agents.Single(person => person.AgentId == childId);
        var caregiverTile = stateToTend.Map.FootNeighbors(child.Position).First(point => stateToTend.Map.IsPassable(point) &&
            Math.Abs(point.X - child.Position.X) + Math.Abs(point.Y - child.Position.Y) == 1 &&
            !stateToTend.Agents.Any(person => person.AgentId != accepted.ProposerId && person.Position == point));
        const int initialIllness = 9_000;
        stateToTend = stateToTend with
        {
            Agents = stateToTend.Agents.Select(person => person.AgentId switch
            {
                var id when id == accepted.ProposerId => person with
                {
                    Position = caregiverTile,
                    FullnessBasisPoints = 4_500,
                    Survival = new SurvivalCondition(10_000, 0),
                    Project = null,
                    LastDecisionContext = null,
                    TravelCooldownTicks = 0,
                },
                var id when id == childId => person with
                {
                    FullnessBasisPoints = 9_000,
                    Survival = new SurvivalCondition(9_000, 0),
                    Project = null,
                    LastDecisionContext = null,
                },
                _ => person,
            }).ToArray(),
        };
        using var society = SocietyRuntime.Restore(stateToTend.Society);
        foreach (var resident in society.Checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
                     person.Id != accepted.ProposerId && person.Id != childId).ToArray())
        {
            society.Apply(checkpoint => SocietyRules.Kill(checkpoint, resident.Id,
                SocietyDeathCause.Accident, checkpoint.WorldTick));
        }
        var societyState = society.ExportState();
        Assert.Equal(accepted.ProposerId, societyState.Society.GetAgent(childId).PrimaryCaregiverId);
        Assert.True(SocietyRules.HasActivePrimaryCaregiver(societyState.Society, childId),
            $"Expected the retained guardian {accepted.ProposerId} to remain active after removing unrelated residents.");
        var projectInventory = societyState.Society.Inventory with
        {
            Lots = societyState.Society.Inventory.Lots.Where(lot => lot.ItemKind != "tool").ToArray(),
        };
        projectInventory = InventoryAuthority.AddLot(projectInventory,
            $"ill-dependent-project-wood:{accepted.ProposerId}", "wood", accepted.ProposerId, 8);
        societyState = societyState with
        {
            Society = societyState.Society with { Inventory = projectInventory },
        };
        stateToTend = stateToTend with
        {
            Society = societyState,
            Agents = stateToTend.Agents
                .Where(person => person.AgentId == accepted.ProposerId || person.AgentId == childId).ToArray(),
        };
        var stateWithProject = stateToTend with
        {
            Agents = stateToTend.Agents
                .Select(person => person.AgentId == accepted.ProposerId
                ? person with { Project = null }
                : person).ToArray(),
        };
        Assert.Null(stateWithProject.Agents.Single(person => person.AgentId == childId).GuardianSearch);

        var projectProvider = new ParentProvider("build:building:");
        using (var projectWorld = WorldRuntime.Restore(stateWithProject, actor =>
                   actor == accepted.ProposerId ? projectProvider : new ParentProvider("safe_idle")))
        {
            for (var tick = 0; tick < 5 && projectWorld.Agents.Single(person => person.AgentId == accepted.ProposerId).Project is null; tick++)
                Assert.True((await projectWorld.AdvanceOneTickAsync()).Advanced);

            var selectedProject = Assert.IsType<AgentProject>(
                projectWorld.Agents.Single(person => person.AgentId == accepted.ProposerId).Project);
            Assert.StartsWith("build:building:", selectedProject.CandidateId, StringComparison.Ordinal);
            Assert.Contains(projectProvider.SeenCandidates, candidate => candidate.Id == selectedProject.CandidateId);
            Assert.Equal(selectedProject.CandidateId,
                Assert.Single(projectWorld.ExportState().Society.Cognition.Runtimes, item => item.AgentId == accepted.ProposerId)
                    .CurrentIntention?.CandidateId);
            projectWorld.Pause();
            stateWithProject = projectWorld.ExportState();
        }

        stateWithProject = stateWithProject with
        {
            Agents = stateWithProject.Agents.Select(person => person.AgentId == childId
                ? person with { Survival = new SurvivalCondition(9_000, initialIllness) }
                : person).ToArray(),
        };
        var careProvider = new ParentProvider("care:");
        using var working = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(stateWithProject)), actor =>
            actor == accepted.ProposerId ? careProvider : new ParentProvider("safe_idle"));
        working.Resume();
        Assert.True(SocietyRules.HasActivePrimaryCaregiver(working.Society, childId),
            "The continuing-project scenario must start with an active accepted guardian.");

        Assert.True((await working.AdvanceOneTickAsync()).Advanced);
        Assert.True(SocietyRules.HasActivePrimaryCaregiver(working.Society, childId),
            "Advancing the unrelated project must not clear its dependent's active guardian.");

        var progressed = working.Agents.Single(person => person.AgentId == accepted.ProposerId).Project!;
        Assert.True(progressed.Stage is "acquiring" or "gathering" or "delivering" or "travelling" or "working" or "waiting" or "blocked",
            $"Expected the existing project to continue, but it is {progressed.Stage}.");
        Assert.Contains($"care:{childId}", careProvider.SelectedCandidateIds);
        Assert.DoesNotContain($"guardian_tend:{childId}", careProvider.SelectedCandidateIds);
        Assert.Contains(working.ExportState().Events, item => item.Kind == "child_cared_for" && item.Detail == childId);

        stateToTend = stateToTend with
        {
            Agents = stateToTend.Agents.Select(person => person.AgentId == childId
                ? person with { Survival = new SurvivalCondition(9_000, initialIllness) }
                : person).ToArray(),
        };
        var caregiverProvider = new ParentProvider("guardian_tend:");
        using var tending = WorldRuntime.Restore(stateToTend, actor =>
            actor == accepted.ProposerId ? caregiverProvider : new ParentProvider("safe_idle"));
        for (var tick = 0; tick < 8 && !tending.ExportState().Events.Any(item => item.Kind == "dependent_cared_for"); tick++)
            await tending.AdvanceOneTickAsync();

        var tended = tending.Agents.Single(person => person.AgentId == childId);
        Assert.Contains(tending.ExportState().Events, item => item.Kind == "dependent_cared_for" && item.Detail == childId);
        Assert.True(tended.Survival!.IllnessBasisPoints <= initialIllness - 250,
            $"Expected accepted-guardian care to relieve illness by at least 250 points; actual illness {tended.Survival.IllnessBasisPoints}.");
        Assert.Equal(10_000, tended.Survival.WarmthBasisPoints);
    }

    [Fact]
    public async Task ProtectedCareCannotReplaceALivingCaregiverOrForgeAnOlderChildsConsent()
    {
        var state = await OrphanState();
        var checkpoint = state.Society.Society;
        var child = checkpoint.Births.Single().ChildId;
        var adults = checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active && person.Id != child).ToArray();
        var accepted = SocietyRules.AssumeInfantCare(checkpoint, adults[0].Id, child);
        Assert.NotNull(accepted.CreatedId);
        Assert.Null(SocietyRules.AssumeInfantCare(accepted.Checkpoint, adults[1].Id, child).CreatedId);
        Assert.Null(SocietyRules.AssumeInfantCare(accepted.Checkpoint, adults[0].Id, child).CreatedId);
        var older = await OrphanState(olderChild: true);
        Assert.Null(SocietyRules.AssumeInfantCare(older.Society.Society, adults[0].Id, child).CreatedId);
    }

    [Fact]
    public async Task CaregiverCanWithdrawAndAnotherAdultCanVolunteerWithoutRewritingParentage()
    {
        var state = await OrphanState();
        var child = state.Society.Society.Births.Single().ChildId;
        var adult = state.Agents.First(person => person.AgentId != child).AgentId;
        using var world = WorldRuntime.Restore(state, actor => new ParentProvider(actor == adult ? "guardian_accept:" : "safe_idle"));
        for (var tick = 0; tick < 40 && !world.Society.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                 edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted); tick++) await world.AdvanceOneTickAsync();
        using var leaving = WorldRuntime.Restore(world.ExportState(), actor => new ParentProvider(actor == adult ? "guardian_end:" : "guardian_accept:"));
        for (var tick = 0; tick < 40; tick++) await leaving.AdvanceOneTickAsync();
        Assert.Contains(leaving.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId == adult && edge.TargetId == child && edge.State == SocietyRelationshipState.Revoked);
        Assert.Contains(leaving.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.ProposerId != adult && edge.TargetId == child && edge.State == SocietyRelationshipState.Accepted);
        Assert.Equal(state.Society.Society.Births, leaving.Society.Births);
    }

    [Fact]
    public async Task CaregiverTelemetryExposesOutcomeButNotPrivateNames()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-care-log-");
        try
        {
            var state = await OrphanState();
            state = state with
            {
                Society = state.Society with
                {
                    Society = state.Society.Society with
                    {
                        Agents = state.Society.Society.Agents.Select(person => person with { Name = $"private-care-secret-{person.Id}" }).ToArray(),
                    }
                }
            };
            var childId = state.Society.Society.Births.Single().ChildId;
            var guardianProvider = new ParentProvider("guardian_accept:");
            using var world = WorldRuntime.Restore(state, _ => guardianProvider);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(1));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world, new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            for (var tick = 0; tick < 40 && !logger.Messages.Any(message => message.Contains("event=guardian_assigned", StringComparison.Ordinal)); tick++)
                Assert.True(await service.TryAdvanceOnceAsync());
            Assert.Contains(guardianProvider.SeenCandidates, candidate => candidate.Id == "guardian_accept:" + childId);
            Assert.Contains(guardianProvider.SelectedCandidateIds, candidate => candidate == "guardian_accept:" + childId);
            Assert.Contains(world.Society.Relationships, edge => edge.Type == SocietyRelationshipType.Caregiver &&
                edge.TargetId == childId && edge.State == SocietyRelationshipState.Accepted);
            Assert.Contains(logger.Messages, message => message.Contains("family_activity", StringComparison.Ordinal) &&
                message.Contains("event=guardian_needed", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message => message.Contains("family_activity", StringComparison.Ordinal) &&
                message.Contains("event=guardian_assigned", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("private-care-secret", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<WorldRuntimeState> OrphanState(bool olderChild = false)
    {
        var state = await PreparedState();
        var first = state.Agents[0].AgentId;
        var second = state.Agents[1].AgentId;
        state = state with
        {
            Society = state.Society with
            {
                Society = ChosenBirthNameTestFixture.NameParent(state.Society.Society, first),
            },
        };
        using var world = WorldRuntime.Restore(state, actor => new ParentProvider(actor == first ? "parent_propose:" : "parent_accept:"));
        for (var tick = 0; tick < 605; tick++) await world.AdvanceOneTickAsync();
        state = world.ExportState();
        var child = Assert.Single(state.Society.Society.Births).ChildId;
        using var society = SocietyRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyRules.RenameAgent(checkpoint, child,
            ChosenBirthNameTestFixture.ChildName(checkpoint, first, "Orphan")));
        society.Apply(checkpoint => SocietyRules.Kill(checkpoint, first, SocietyDeathCause.Accident, checkpoint.WorldTick));
        society.Apply(checkpoint => SocietyRules.Kill(checkpoint, second, SocietyDeathCause.Accident, checkpoint.WorldTick));
        var societyState = society.ExportState();
        if (olderChild)
        {
            societyState = societyState with
            {
                Society = societyState.Society with
                {
                    Agents = societyState.Society.Agents.Select(person => person.Id == child ? person with
                    {
                        BirthTick = societyState.Society.WorldTick - 3 * societyState.Society.Config.TicksPerWorldYear,
                        LastLifecycleYearChecked = 3,
                        AgeBand = SocietyAgeBand.Child,
                    } : person).ToArray(),
                }
            };
        }
        return state with
        {
            Society = societyState,
            Agents = state.Agents.Where(person => person.AgentId != first && person.AgentId != second)
                .Select(person => person with { LastDecisionContext = null, FullnessBasisPoints = person.AgentId == child && !olderChild ? 2_000 : 9_000 }).ToArray(),
        };
    }
}
