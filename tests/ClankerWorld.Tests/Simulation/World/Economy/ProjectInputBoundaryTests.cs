using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class ProjectInputBoundaryTests
{
    [Fact]
    public async Task CropPreparationDoesNotUseAnotherHouseholdsSeeds()
    {
        var (state, actor, household, point) = FarmFieldTests.PreparedFarmer("foreign-field-stock");
        var inventory = state.Society.Society.Inventory;
        foreach (var lot in inventory.Lots.Where(lot => lot.OwnerId == household && lot.ItemKind is "grain_seed" or "cultivated_green_seed" or "potatoes").ToArray())
        {
            inventory = InventoryAuthority.Reserve(inventory, "used-" + lot.Id, household, lot.Id, lot.Quantity, "fixture", long.MaxValue);
            inventory = InventoryAuthority.ConsumeReservation(inventory, "used-" + lot.Id);
        }
        var foreign = inventory.Lots.Where(lot => lot.OwnerId != household && lot.ItemKind is "grain_seed" or "cultivated_green_seed" or "potatoes")
            .Select(lot => (lot.Id, lot.OwnerId, lot.Quantity)).ToArray();
        Assert.NotEmpty(foreign);
        state = FarmFieldTests.WithInventory(state, inventory) with { Fields = [new(point, household, FarmFieldStage.Prepared)] };
        var recorder = new ActionCoverageRecorder();
        using var world = WorldRuntime.Restore(state, id => id == actor ? recorder : new Idle());
        var foreignSeed = inventory.Lots.First(lot => lot.OwnerId != household && lot.ItemKind == "grain_seed");
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False(world.StartFieldWork(actor, point, FarmWorkKind.Plant, "grain", foreignSeed.Id).Accepted);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 6; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.DoesNotContain(recorder.OfferedByAgent[actor].Keys, id => id.StartsWith("farm:Plant:", StringComparison.Ordinal));
        Assert.Equal(foreign, world.Society.Inventory.Lots.Where(lot => lot.OwnerId != household && lot.ItemKind is "grain_seed" or "cultivated_green_seed" or "potatoes")
            .Select(lot => (lot.Id, lot.OwnerId, lot.Quantity)).ToArray());
        world.Validate();
    }

    [Fact]
    public async Task CookingWaitsForOnSiteIngredientsAndCanResumeAfterTheyReturn()
    {
        using var seed = await Prepared();
        var state = seed.ExportState();
        var beta = state.Society.Society.Agents.First(person => person.HouseholdId == "household:second").Id;
        var house = seed.WorldContent.Buildings.Single(item => item.LocalId == "house-1x1");
        var recipe = seed.WorldContent.Recipes.Single(item => item.LocalId == "house-meal");
        BuildingPlacementResult? placement = null;
        foreach (var point in state.Map.Tiles.Select(tile => tile.Position))
        {
            var candidate = seed.PlaceBuilding("cook-home", house.CanonicalId, point, "household:second");
            if (!candidate.Applied) continue;
            placement = candidate;
            break;
        }
        Assert.NotNull(placement);
        var placed = seed.WorldSimulation.Buildings.Single(item => item.InstanceId == "cook-home");
        state = seed.ExportState();
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == beta ? person with
            {
                Position = placed.Position,
                FullnessBasisPoints = 9_500,
                Project = new AgentProject("build:recipe:" + recipe.CanonicalId, "Cook", state.Society.Society.WorldTick, "working", 10,
                    LastTransitionTick: state.Society.Society.WorldTick),
            } : person).ToArray(),
        };
        using var blocked = WorldRuntime.Restore(state, _ => new Idle());
        _ = await blocked.AdvanceOneTickAsync();
        Assert.Equal("blocked", blocked.Agents.Single(person => person.AgentId == beta).Project!.Stage);
        Assert.Empty(blocked.WorldSimulation.ProductionJobs);
        state = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(blocked.ExportState()));
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = state.Society.Society.Inventory with
                    {
                        Lots = state.Society.Society.Inventory.Lots.Select(lot =>
                        lot.OwnerId == "household:second" && lot.ItemKind is "potatoes" or "wood"
                            ? lot with { StorageBuildingId = "cook-home" } : lot).ToArray()
                    },
                }
            }
        };
        using var replenished = WorldRuntime.Restore(state, _ => new Idle());
        for (var tick = 0; tick < 80 && replenished.WorldSimulation.ProductionJobs.Count == 0; tick++)
            _ = await replenished.AdvanceOneTickAsync();
        Assert.Contains(replenished.WorldSimulation.ProductionJobs, job => job.WorkerId == beta && job.RecipeId == recipe.CanonicalId);
        _ = WorldRuntimeCodec.Encode(replenished.ExportState());
    }

    [Fact]
    public async Task ToolProjectUsesReachableWorkshopWhenFirstIsOccupied()
    {
        using var seed = await Prepared();
        var workshop = seed.WorldContent.Buildings.Single(item => item.LocalId == "workshop");
        var recipe = seed.WorldContent.Recipes.Single(item => item.LocalId == "tools");
        var initial = seed.ExportState();
        var adults = initial.Society.Society.Agents.Where(person => person.HouseholdId == "household:first").ToArray();
        var actor = adults[0].Id;
        var blocker = adults[1].Id;
        var actorPosition = initial.Agents.Single(person => person.AgentId == actor).Position;
        var initialTown = Assert.Single(initial.Towns!);
        var reachableBuildSites = initial.Map.Tiles.Select(tile => tile.Position)
            .Where(position => initial.Map.IsBuildable(position) &&
                TownBorderRules.IsWithinOrAdjacent(initialTown, position, workshop.Width, workshop.Height) &&
                initial.Map.IsReachableOnFoot(actorPosition, position))
            .OrderBy(position => initial.Map.FootDistance(actorPosition, position))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .ToArray();
        GridPoint PlaceWorkshop(string instanceId, string label, IEnumerable<GridPoint> sites)
        {
            GridPoint? selected = null;
            var failures = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var position in sites)
            {
                var placement = seed.PlaceBuilding(instanceId, workshop.CanonicalId, position);
                if (!placement.Applied)
                {
                    var failure = placement.Failure ?? "<no failure reason>";
                    failures[failure] = failures.GetValueOrDefault(failure) + 1;
                    continue;
                }

                selected = position;
                break;
            }
            Assert.True(selected.HasValue,
                $"No reachable {label} workshop site on {initial.Map.Width}x{initial.Map.Height} map " +
                $"({reachableBuildSites.Length} candidate tiles). Placement failures: " +
                string.Join("; ", failures.Select(item => $"{item.Key} ({item.Value})")));
            return selected.Value;
        }
        var firstWorkshopPosition = PlaceWorkshop("a-workshop", "first", reachableBuildSites);
        _ = PlaceWorkshop("z-workshop", "second", reachableBuildSites.Where(position => position != firstWorkshopPosition &&
            initial.Agents.All(person => person.Position != position)));
        var state = seed.ExportState();
        // No role: a communal Workshop serves any Town resident.
        var society = state.Society.Society;
        Assert.Equal(SocietyWorkRole.Unassigned, society.GetAgent(actor).CurrentRole);
        var inventory = society.Inventory;
        foreach (var lot in inventory.Lots.Where(lot => lot.ItemKind == "tool" && lot.Quantity > 0).ToArray())
        {
            inventory = InventoryAuthority.Reserve(inventory, "use-" + lot.Id, lot.OwnerId, lot.Id, lot.Quantity, "used", 1000);
            inventory = InventoryAuthority.ConsumeReservation(inventory, "use-" + lot.Id);
        }
        inventory = InventoryAuthority.AddLot(inventory, "crafting-tool", "tool", actor, 1);
        state = state with
        {
            Society = state.Society with { Society = society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == blocker ? firstWorkshopPosition : person.Position,
                FullnessBasisPoints = 9_500,
            }).ToArray(),
        };
        var chooser = new ToolChooser("build:recipe:" + recipe.CanonicalId);
        IDecisionProvider Provider(string id) => id == actor ? chooser : new Idle();
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), Provider);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(chooser.SelectedRecipe, string.Join(", ", chooser.LastCandidateIds));
        var checkpoint = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
        using var continuing = WorldRuntime.Restore(checkpoint, Provider);
        for (var tick = 0; tick < 60 && !continuing.WorldSimulation.ProductionJobs.Any(job =>
                 job.WorkerId == actor && job.State == WorldProductionJobState.Completed); tick++)
            Assert.True((await continuing.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(continuing.WorldSimulation.ProductionJobs, job => job.WorkerId == actor &&
            job.BuildingInstanceId == "z-workshop" && job.State == WorldProductionJobState.Completed);
        Assert.Equal(firstWorkshopPosition, continuing.Agents.Single(person => person.AgentId == blocker).Position);
        Assert.DoesNotContain(continuing.WorldSimulation.ProductionJobs, job => job.WorkerId == actor && job.BuildingInstanceId == "a-workshop");
        Assert.Contains(continuing.Society.Inventory.Lots, lot => lot.OwnerId == "household:first" && lot.ItemKind == "tool" && lot.Quantity > 0);
        _ = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(continuing.ExportState()));
    }

    private sealed class ToolChooser(string recipeCandidate) : IDecisionProvider
    {
        public bool SelectedRecipe { get; private set; }
        public IReadOnlyList<string> LastCandidateIds { get; private set; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            LastCandidateIds = request.Observation.Candidates.Select(candidate => candidate.Id).ToArray();
            var choice = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == recipeCandidate)
                ?? request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            SelectedRecipe |= choice.Id == recipeCandidate;
            return new DeterministicDecisionProvider().DecideAsync(request with { Observation = request.Observation with { Candidates = [choice] } }, cancellationToken);
        }
    }

    private static async Task<WorldRuntime> Prepared()
    {
        var world = new WorldRuntime("project-input-boundary", _ => new Idle(), startPace: WorldStartPace.FounderSetup);
        var positions = new[] { new GridPoint(0, 0), new GridPoint(1, 2), new GridPoint(2, 2), new GridPoint(3, 2) };
        for (var i = 0; i < positions.Length; i++) world.PlaceFounder("founder:" + (i + 1).ToString("x32", CultureInfo.InvariantCulture), positions[i]);
        world.StartWorld();
        world.StageStarterContent();
        for (var tick = 0; tick < 9; tick++) _ = await world.AdvanceOneTickAsync();
        return world;
    }

    private sealed class Idle : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var idle = request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with { Observation = request.Observation with { Candidates = [idle] } }, cancellationToken);
        }
    }
}
