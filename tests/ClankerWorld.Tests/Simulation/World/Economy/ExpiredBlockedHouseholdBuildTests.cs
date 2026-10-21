using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class ExpiredBlockedHouseholdBuildTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredUnfillableHouseholdRecipePausesForFreshChoiceAndResumesAfterInputsReturn(bool fullCarriers)
    {
        using var setup = NormalPathWorld.CreateGenerated("expired-household-recipe-inputs", _ => new EconomyIdleProvider());
        var state = setup.ExportState();
        var projectStartedTick = state.Society.Society.WorldTick;
        var blacksmith = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-blacksmith");
        var household = blacksmith.HouseholdId!;
        var recipe = setup.WorldContent.Recipes.Single(item => item.LocalId == "wooden-axe");
        var candidateId = "build:recipe:" + recipe.CanonicalId;
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == household &&
            person.AgeBand == SocietyAgeBand.Adult).Id;
        var members = state.Society.Society.Agents.Where(person => person.HouseholdId == household)
            .Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var inventory = state.Society.Society.Inventory;
        var removedWood = inventory.Lots.Where(lot => lot.ItemKind == "wood")
            .Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => !removedWood.Contains(lot.Id)).ToArray(),
            Reservations = inventory.Reservations.Where(item => !removedWood.Contains(item.LotId)).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "blocked-axe-wood", "wood", household, 2,
            storageBuildingId: blacksmith.InstanceId);
        if (fullCarriers)
        {
            var house = state.WorldSimulation.Buildings.First(building => building.HouseholdId == household &&
                setup.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)
                    .Tags.Contains("house", StringComparer.Ordinal));
            inventory = InventoryAuthority.AddLot(inventory, "uncollectable-axe-wood", "wood", household, 1,
                storageBuildingId: house.InstanceId);
            foreach (var person in state.Agents.Where(person => members.Contains(person.AgentId)))
            {
                var cargo = PersonalEquipmentRules.CarriedQuantity(inventory, person.AgentId, person.Equipment);
                var capacity = PersonalEquipmentRules.Capacity(inventory, person.AgentId, person.Equipment);
                Assert.InRange(cargo, 0, capacity);
                if (cargo < capacity)
                    inventory = InventoryAuthority.AddLot(inventory, "full-carrier:" + person.AgentId, "clay",
                        person.AgentId, capacity - cargo);
            }
        }
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
        };

        // Let the ordinary tick-one settlement-resource bootstrap run first so
        // its fallen-wood cache is included in the exhausted-source fixture.
        using (var bootstrap = WorldRuntime.Restore(state, _ => new EconomyIdleProvider()))
        {
            Assert.True((await bootstrap.AdvanceOneTickAsync()).Advanced);
            state = bootstrap.ExportState();
        }
        inventory = state.Society.Society.Inventory;
        var woodSourceIds = state.Map.Resources.Where(resource => resource.Kind is "construction" or "wood" ||
                resource.NaturalObjectKind == "fallen_wood")
            .Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
            Resources = state.Resources.Select(resource => woodSourceIds.Contains(resource.ResourceId)
                ? resource with { State = ResourceState.Depleted }
                : resource).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(resource =>
                        woodSourceIds.Contains(resource.Id)
                            ? resource with { Quantity = 0, State = EcologyResourceState.Depleted, NextRegenerationDay = 100 }
                            : resource).ToArray(),
                },
            },
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with
                {
                    Position = blacksmith.Position,
                    FullnessBasisPoints = 9_500,
                    LastDecisionContext = null,
                    Project = new AgentProject(candidateId, recipe.DisplayName,
                        projectStartedTick, "blocked", 4,
                        "Waiting for ingredients at this household building",
                        LastTransitionTick: projectStartedTick),
                }
                : members.Contains(person.AgentId)
                    ? person with { FullnessBasisPoints = 9_500 }
                    : person).ToArray(),
        };

        var recorder = new ActionCoverageRecorder(chooseIdle: true);
        using var blocked = WorldRuntime.Restore(state, _ => recorder);
        var original = blocked.Agents.Single(item => item.AgentId == actor).Project!;
        for (var tick = 0; tick < 75 &&
             !blocked.Agents.Single(item => item.AgentId == actor).Project!.RequiresFreshChoice; tick++)
            Assert.True((await blocked.AdvanceOneTickAsync()).Advanced);

        var paused = blocked.Agents.Single(item => item.AgentId == actor).Project!;
        Assert.Equal("paused", paused.Stage);
        Assert.True(paused.RequiresFreshChoice);
        Assert.Equal("Materials for this work are unavailable. Choose another task for now.", paused.Blocker);
        Assert.Equal(original.CandidateId, paused.CandidateId);
        Assert.Equal(original.StartedTick, paused.StartedTick);
        Assert.Equal(original.WorkDone, paused.WorkDone);
        Assert.Null(paused.JobId);
        if (fullCarriers)
            Assert.Equal(1, blocked.Society.Inventory.GetLot("uncollectable-axe-wood").Quantity);
        Assert.DoesNotContain(blocked.WorldSimulation.ProductionJobs, job => job.WorkerId == actor &&
            job.RecipeId == recipe.CanonicalId && job.State == WorldProductionJobState.Running);
        Assert.DoesNotContain(recorder.OfferedByAgent.Values.SelectMany(items => items.Keys), id =>
            id is "haul_smith_input" or "gather_smith_ore" or "deliver_smith_ore" or "supply_workstation:wood" ||
            id == "gather_building_material:wood" || id == "assist:wood");

        var encodedPaused = WorldRuntimeCodec.Encode(blocked.ExportState());
        var savedPaused = WorldRuntimeCodec.Decode(encodedPaused);
        Assert.Equal(encodedPaused, WorldRuntimeCodec.Encode(savedPaused));
        Assert.Equal(paused, savedPaused.Agents.Single(item => item.AgentId == actor).Project);
        var invalidFlagState = savedPaused with
        {
            Agents = savedPaused.Agents.Select(person => person.AgentId == actor
                ? person with { Project = paused with { Stage = "blocked" } }
                : person).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(invalidFlagState, _ => new EconomyIdleProvider()));
        var urgentPauseState = savedPaused with
        {
            Agents = savedPaused.Agents.Select(person => person.AgentId == actor
                ? person with { FullnessBasisPoints = 1_000, LastDecisionContext = null }
                : person).ToArray(),
        };
        using (var urgentPause = WorldRuntime.Restore(urgentPauseState, _ => new EconomyIdleProvider()))
        {
            Assert.True((await urgentPause.AdvanceOneTickAsync()).Advanced);
            var urgentProject = urgentPause.Agents.Single(item => item.AgentId == actor).Project!;
            Assert.True(urgentProject.RequiresFreshChoice);
            Assert.Equal("Meeting food needs", urgentProject.Blocker);
        }
        var replenishedInventory = InventoryAuthority.AddLot(savedPaused.Society.Society.Inventory,
            "returned-axe-wood", "wood", household, 1, storageBuildingId: blacksmith.InstanceId);
        savedPaused = savedPaused with
        {
            Society = savedPaused.Society with
            {
                Society = savedPaused.Society.Society with { Inventory = replenishedInventory },
            },
            Agents = savedPaused.Agents.Select(person => person.AgentId == actor
                ? person with { LastDecisionContext = null }
                : person).ToArray(),
        };
        var resumeProvider = new SelectCandidateProvider(candidateId);
        using var resumed = WorldRuntime.Restore(savedPaused, id => id == actor ? resumeProvider : new EconomyIdleProvider());
        for (var tick = 0; tick < 75 && !resumed.WorldSimulation.ProductionJobs.Any(job =>
                 job.WorkerId == actor && job.RecipeId == recipe.CanonicalId &&
                 job.State is WorldProductionJobState.Running or WorldProductionJobState.Completed); tick++)
            Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);

        Assert.Contains(candidateId, resumeProvider.SelectedCandidateIds);
        var resumedProject = resumed.Agents.Single(item => item.AgentId == actor).Project!;
        Assert.False(resumedProject.RequiresFreshChoice);
        Assert.Equal(original.CandidateId, resumedProject.CandidateId);
        Assert.Equal(original.StartedTick, resumedProject.StartedTick);
        Assert.InRange(resumedProject.WorkDone, original.WorkDone, 10);
        Assert.Contains(resumed.WorldSimulation.ProductionJobs, job => job.WorkerId == actor &&
            job.RecipeId == recipe.CanonicalId && job.State is WorldProductionJobState.Running or WorldProductionJobState.Completed);
        resumed.Validate();
    }

    [Fact]
    public async Task ExpiredBlockedPlanOffersANewSiteToItsOwnerAndKeepsTheHouseholdClaim()
    {
        using var setup = NormalPathWorld.CreateGenerated("probe-a", _ => new EconomyIdleProvider());
        var state = setup.ExportState();
        var farmhouse = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-farmhouse");
        var household = farmhouse.HouseholdId!;
        var members = state.Society.Society.Agents.Where(item => item.HouseholdId == household &&
                item.AgeBand == SocietyAgeBand.Adult)
            .Select(item => item.Id).Order(StringComparer.Ordinal).ToArray();
        Assert.True(members.Length >= 2);
        var actor = members[0];
        var sibling = members[1];
        var silo = setup.WorldContent.Buildings.Single(item => item.LocalId == "silo-1x1");
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in silo.BuildCosts)
        {
            inventory = InventoryAuthority.AddLot(inventory, $"retry-silo-{cost.ResourceId}", cost.ResourceId,
                household, cost.Amount * 2);
        }
        if (household != "household:first")
        {
            inventory = InventoryAuthority.AddLot(inventory, "retry-blocker-wood", "wood", "household:first", 4);
        }
        var provider = new RetryChoiceProvider();
        provider.ChooseInitialBuild(actor, silo.CanonicalId);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person.AgentId == actor || person.AgentId == sibling
                ? person with { FullnessBasisPoints = 9_500, LastDecisionContext = null, Project = null }
                : person).ToArray(),
        };
        using var observing = WorldRuntime.Restore(state, _ => provider);
        for (var tick = 0; tick < 120 && (provider.InitialCandidateId is null ||
                 observing.Agents.Single(item => item.AgentId == actor).Project?.CandidateId != provider.InitialCandidateId); tick++)
        {
            Assert.True((await observing.AdvanceOneTickAsync()).Advanced);
        }

        var originallyOffered = provider.InitialCandidateId;
        Assert.NotNull(originallyOffered);
        var project = observing.Agents.Single(item => item.AgentId == actor).Project;
        Assert.NotNull(project);
        Assert.Equal(originallyOffered, project.CandidateId);
        for (var tick = 0; tick < 120 && observing.Agents.Single(item => item.AgentId == actor)
                 .Project!.WorkDone < 4; tick++)
        {
            Assert.True((await observing.AdvanceOneTickAsync()).Advanced);
        }

        project = observing.Agents.Single(item => item.AgentId == actor).Project;
        Assert.NotNull(project);
        Assert.InRange(project.WorkDone, 4, 9);
        Assert.True(TownConstructionCandidateIds.TryParse(originallyOffered, out var blockedSelection));
        var blockedSite = blockedSelection.SitePosition!.Value;
        var workBeforeBlock = project.WorkDone;
        var blocker = observing.WorldContent.Buildings.Single(item => item.LocalId == "fire");
        var occupied = observing.PlaceBuilding("retry-blocked-site", blocker.CanonicalId, blockedSite);
        Assert.True(occupied.Applied, occupied.Failure);
        Assert.True((await observing.AdvanceOneTickAsync()).Advanced);
        project = observing.Agents.Single(item => item.AgentId == actor).Project;
        Assert.NotNull(project);
        Assert.Equal("blocked", project.Stage);
        Assert.Equal(workBeforeBlock, project.WorkDone);

        state = observing.ExportState();
        var blockedSince = state.Society.Society.WorldTick;
        var retainedWork = project.WorkDone;
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with
                {
                    FullnessBasisPoints = 9_500,
                }
                : person.AgentId == sibling ? person with { LastDecisionContext = null } : person).ToArray(),
        };
        var householdMaterialsBeforeRetry = MaterialTotals(state.Society.Society.Inventory, household);
        provider.Arm(actor, silo.CanonicalId, blockedSite);
        using var world = WorldRuntime.Restore(state, _ => provider);

        for (var tick = 0; tick < 75 && world.Agents.Single(item => item.AgentId == actor)
                 .Project?.CandidateId == originallyOffered; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        }

        var actorRequests = provider.RequestsFor(actor);
        var retryRequest = Assert.Single(actorRequests, item => item.CandidateIds.Any(id =>
            TownConstructionCandidateIds.TryParse(id, out var selection) && selection.IsBuilding &&
            selection.DefinitionId == silo.CanonicalId && selection.SitePosition != blockedSite));
        Assert.True(retryRequest.WorldTick - blockedSince >= 60,
            $"The blocked site was retried after only {retryRequest.WorldTick - blockedSince} ticks.");
        var siblingRequests = provider.RequestsFor(sibling);
        Assert.NotEmpty(siblingRequests);
        Assert.DoesNotContain(siblingRequests.SelectMany(item => item.CandidateIds), id =>
            TownConstructionCandidateIds.TryParse(id, out var selection) && selection.IsBuilding &&
            selection.DefinitionId == silo.CanonicalId);

        var retriedPerson = world.Agents.Single(item => item.AgentId == actor);
        Assert.NotNull(retriedPerson.Project);
        Assert.NotEqual(originallyOffered, retriedPerson.Project.CandidateId);
        Assert.InRange(retriedPerson.Project.WorkDone, retainedWork, 10);
        Assert.Equal(householdMaterialsBeforeRetry, MaterialTotals(world.Society.Inventory, household));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "project_site_reselected" &&
            item.Detail.StartsWith(actor + ":" + silo.CanonicalId, StringComparison.Ordinal));
        Assert.Contains(world.WorldSimulation.Buildings, item => item.InstanceId == "retry-blocked-site" &&
            item.Position == blockedSite);
        Assert.Null(world.Agents.Single(item => item.AgentId == sibling).Project);
        world.Validate();
    }

    private static (string ItemKind, int Quantity)[] MaterialTotals(InventoryCheckpoint inventory, string ownerId) =>
        inventory.Lots.Where(lot => lot.OwnerId == ownerId && lot.ItemKind is "wood" or "stone")
            .GroupBy(lot => lot.ItemKind, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (group.Key, group.Sum(lot => lot.Quantity))).ToArray();

    private sealed record CapturedRequest(long WorldTick, string[] CandidateIds);

    private sealed class RetryChoiceProvider : IDecisionProvider
    {
        private readonly ConcurrentDictionary<string, ConcurrentQueue<CapturedRequest>> requests = new(StringComparer.Ordinal);
        private string? actor;
        private string? definitionId;
        private GridPoint? blockedSite;
        private bool chooseInitialBuild;
        private bool chooseReplacement;

        public string? InitialCandidateId { get; private set; }

        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public CapturedRequest[] RequestsFor(string agentId) =>
            requests.TryGetValue(agentId, out var captured) ? captured.ToArray() : [];

        public void Arm(string targetActor, string targetDefinitionId, GridPoint oldSite)
        {
            requests.Clear();
            actor = targetActor;
            definitionId = targetDefinitionId;
            blockedSite = oldSite;
            chooseInitialBuild = false;
            chooseReplacement = true;
        }

        public void ChooseInitialBuild(string targetActor, string targetDefinitionId)
        {
            actor = targetActor;
            definitionId = targetDefinitionId;
            chooseInitialBuild = true;
            chooseReplacement = false;
        }

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var candidates = observation.Candidates;
            requests.GetOrAdd(observation.AgentId, _ => new ConcurrentQueue<CapturedRequest>())
                .Enqueue(new CapturedRequest(observation.WorldTick, candidates.Select(item => item.Id).ToArray()));
            var selected = candidates.Single(item => item.Id == "safe_idle");
            if (observation.AgentId == actor)
            {
                var matchingBuilding = candidates.FirstOrDefault(item => TownConstructionCandidateIds.TryParse(item.Id, out var selection) &&
                    selection.IsBuilding && selection.DefinitionId == definitionId &&
                    (!chooseReplacement || selection.SitePosition != blockedSite));
                if (chooseInitialBuild && matchingBuilding is not null)
                {
                    selected = matchingBuilding;
                    InitialCandidateId = selected.Id;
                    chooseInitialBuild = false;
                }
                else if (chooseReplacement && matchingBuilding is not null)
                {
                    selected = matchingBuilding;
                }
            }
            var probabilities = candidates.ToDictionary(item => item.Id,
                item => item.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId,
                Kind, ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration,
                observation.ObservationDigest, selected.Id, 1, probabilities));
        }
    }


    private sealed class SelectCandidateProvider(string candidateId) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public List<string> SelectedCandidateIds { get; } = [];

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var candidates = request.Observation.Candidates;
            var selected = candidates.FirstOrDefault(item => item.Id == candidateId) ??
                candidates.Single(item => item.Id == "safe_idle");
            SelectedCandidateIds.Add(selected.Id);
            var probabilities = candidates.ToDictionary(item => item.Id,
                item => item.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId,
                request.Observation.AgentId, Kind, ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected.Id, 1, probabilities));
        }
    }
}
