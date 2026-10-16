using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class RetiredBuildingTests
{
    [Fact]
    public async Task CurrentCheckpointKeepsRetiredBuildingsAndFinishesProjectsUnderWay()
    {
        using var seed = new WorldRuntime("retired-current-save", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await seed.AdvanceOneTickAsync();
        var state = seed.ExportState();
        var shelter = seed.WorldContent.Buildings.Single(item => item.LocalId == "shelter");
        var storehouse = seed.WorldContent.Buildings.Single(item => item.LocalId == "storage");
        var (builder, helper) = (state.Agents[0], state.Agents[1]);
        var shelterSite = MovementTestWorld.BuildingSite(state, shelter, builder.AgentId);
        var shelterAccess = WorldContentSimulationRules.Footprint(shelter, shelterSite)
            .Append(BuildingMovementRules.EntranceFor(shelter, new("planned-shelter", shelter.CanonicalId, shelterSite, seed.WorldTick)));
        var storehouseSite = MovementTestWorld.BuildingSite(state, storehouse, helper.AgentId, shelterAccess);
        var inventory = state.Society.Society.Inventory;
        inventory = InventoryAuthority.AddLot(inventory, "retired-tool", "tool", builder.AgentId, 1);
        inventory = InventoryAuthority.AddLot(inventory, "retired-helper-tool", "tool", helper.AgentId, 1);
        inventory = InventoryAuthority.AddLot(inventory, "retired-helper-wood", "wood", helper.AgentId, 6);
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
            Agents = state.Agents.Select(person => person.AgentId == builder.AgentId ? person with
            {
                Position = shelterSite,
                FullnessBasisPoints = 9_000,
                Project = new(TownConstructionCandidateIds.Building(shelter.CanonicalId, shelterSite),
                    shelter.DisplayName, seed.WorldTick, "working", 10, LastTransitionTick: seed.WorldTick),
            } : person.AgentId == helper.AgentId ? person with
            {
                Position = storehouseSite,
                FullnessBasisPoints = 9_000,
                Project = new(TownConstructionCandidateIds.Building(storehouse.CanonicalId, storehouseSite),
                    storehouse.DisplayName, seed.WorldTick, "working", 0, LastTransitionTick: seed.WorldTick),
            } : person).ToArray(),
        };
        using var before = WorldRuntime.Restore(state, _ => new IdleProvider());
        await before.AdvanceOneTickAsync();
        Assert.Contains(before.WorldSimulation.Buildings, building => building.DefinitionId == shelter.CanonicalId);

        var saved = WorldRuntimeCodec.Encode(before.ExportState());
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new IdleProvider());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Contains(world.WorldSimulation.Buildings, building =>
            building.DefinitionId == shelter.CanonicalId && building.Position == shelterSite);
        Assert.Equal("working", world.Agents.Single(person => person.AgentId == helper.AgentId).Project!.Stage);

        for (var tick = 0; tick < 15 && !world.WorldSimulation.Buildings.Any(building =>
                 building.DefinitionId == storehouse.CanonicalId); tick++)
            await world.AdvanceOneTickAsync();
        Assert.Contains(world.WorldSimulation.Buildings, building =>
            building.DefinitionId == storehouse.CanonicalId && building.Position == storehouseSite);
        Assert.Equal("completed", world.Agents.Single(person => person.AgentId == helper.AgentId).Project!.Stage);
    }

    [Fact]
    public async Task OldResidentStudyStaysReviewableButNoNewStudyFollows()
    {
        using var seed = new WorldRuntime("retired-study", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await seed.AdvanceOneTickAsync();
        var state = seed.ExportState();
        var actor = state.Society.Society.Agents[0];
        var registry = ContentPackageRegistry.Restore(state.Content);
        var study = BuildingDesign.Create("private-inventor's shelter study", "shelter", 7);
        registry.Propose(study, seed.WorldTick, actor.Id);
        state = state with
        {
            Content = registry.ExportState(),
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Agents = state.Society.Society.Agents.Select(person => person.Id == actor.Id
                        ? person with { CurrentRole = SocietyWorkRole.Builder, Name = "private-inventor" }
                        : person).ToArray(),
                },
            },
            Agents = state.Agents.Select(person => person.AgentId == actor.Id
                ? person with { FullnessBasisPoints = 9_000, Proficiency = new WorkProficiency(Building: 9) }
                : person).ToArray(),
        };
        var provider = new RecordingProvider();
        using var world = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == actor.Id ? provider : new IdleProvider());
        for (var tick = 0; tick < 10; tick++) await world.AdvanceOneTickAsync();

        var package = Assert.Single(world.Content.Packages, item =>
            item.Manifest.PackageId.StartsWith("owner-building-", StringComparison.Ordinal));
        Assert.Equal(ContentPackageLifecycle.Proposed, package.Lifecycle);
        Assert.Equal("shelter", BuildingDesign.Read(package.Manifest).Purpose);
        Assert.Equal(actor.Id, new OwnerWorldObservationStore(world).GetSnapshot().ContentPackages
            .Single(item => item.PackageId == package.Manifest.PackageId).ProposedByAgentId);
        Assert.NotEmpty(provider.Candidates);
        Assert.DoesNotContain(provider.Candidates, id => id.StartsWith("invent:building:", StringComparison.Ordinal));
    }

    private sealed class RecordingProvider : IDecisionProvider
    {
        private readonly HashSet<string> candidates = new(StringComparer.Ordinal);
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public IReadOnlyCollection<string> Candidates
        {
            get { lock (candidates) return candidates.ToArray(); }
        }

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            lock (candidates)
                candidates.UnionWith(request.Observation.Candidates.Select(candidate => candidate.Id));
            return new IdleProvider().DecideAsync(request, cancellationToken);
        }
    }

    private sealed class IdleProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with
                {
                    Candidates = request.Observation.Candidates.Where(candidate => candidate.Id == "safe_idle").ToArray(),
                },
            }, cancellationToken);
    }
}
