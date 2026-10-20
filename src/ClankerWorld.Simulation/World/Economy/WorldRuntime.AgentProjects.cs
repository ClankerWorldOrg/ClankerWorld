using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>A durable public work plan, not hidden model reasoning.</summary>
public sealed record AgentProject(
    string CandidateId,
    string Label,
    long StartedTick,
    string Stage,
    int WorkDone = 0,
    string? Blocker = null,
    string? JobId = null,
    long LastTransitionTick = 0,
    bool RequiresFreshChoice = false,
    string? ToolMakingRequestId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OrderInstructionId = null);

public sealed partial class WorldRuntime
{
    private const int ProjectWorkTicks = 10;
    private const string WaitingForWorkSiteBlocker = "Waiting for a free work site";
    private const string FreshChoicePauseBlocker = "Materials for this work are unavailable. Choose another task for now.";
    private const int BlockedProjectRetryDelayTicks = 60;
    private static readonly (int X, int Y)[] ClayBankOffsets = [(0, -1), (1, 0), (0, 1), (-1, 0)];
    private static bool IsCompatibleSavedMap(WorldMap generated, WorldRuntimeState state)
    {
        if (MapManifestCodec.Digest(state.Map) != state.Map.ManifestDigest)
        {
            return false;
        }
        if (state.MapAuthoring is { } authoring)
        {
            var originalIds = generated.Resources.Select(resource => resource.Id)
                .Concat(generated.MapObjects.Select(item => item.Id)).ToHashSet(StringComparer.Ordinal);
            var originalPositions = generated.Resources.Select(resource => resource.Position)
                .Concat(generated.MapObjects.Select(item => item.Position)).ToHashSet();
            if (!authoring.IsValid(state.Map) ||
                authoring.Resources.Any(resource => originalIds.Contains(resource.Id) || originalPositions.Contains(resource.Position)) ||
                authoring.Objects.Any(item => originalIds.Contains(item.Id) || originalPositions.Contains(item.Position)) ||
                authoring.Terrain.Any(edit => originalPositions.Contains(edit.Position)))
                return false;
            generated = authoring.ApplyToBaseline(generated);
        }
        return SavedMapMatchesBaseline(state, generated);
    }

    private static bool SavedMapMatchesBaseline(WorldRuntimeState state, WorldMap baseline)
    {
        if (state.Geography is not null &&
            (state.Map.WrapsEastWest != baseline.WrapsEastWest ||
             !string.Equals(MapLayerManifestCodec.Digest(state.Map), MapLayerManifestCodec.Digest(baseline),
                 StringComparison.Ordinal)))
            return false;
        if (baseline.ManifestDigest == state.Map.ManifestDigest)
            return true;
        var baseIds = baseline.Resources.Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
        var added = state.Map.Resources.Where(resource => !baseIds.Contains(resource.Id)).ToArray();
        // A saved map may add the settlement's staged sites, its clay bank and
        // trees planted on new tiles. Everything else must match regeneration.
        var planted = added.Where(IsPlantedTree).ToArray();
        var staged = added.Where(resource => !IsPlantedTree(resource)).ToArray();
        if (added.Length == 0 ||
            added.Select(resource => resource.Position).Distinct().Count() != added.Length ||
            added.Any(resource => baseline.MapObjects.Any(item => item.Position == resource.Position) ||
                baseline.Resources.Any(item => item.Position == resource.Position)))
            return false;
        if (staged.Length > 0 && (state.SchemaVersion < 5 || staged.Length > 6 ||
            state.Content?.Packages.Any(package => package.Manifest.PackageId == HearthContent.PackageId &&
                package.Manifest.PackageDigest == HearthContent.Create().PackageDigest &&
                package.ActivationTick is not null) != true ||
            staged.Any(resource =>
            {
                if (resource.Id != "settlement-" + resource.Kind ||
                    resource.Kind is not ("stone" or "fiber" or "seed" or "grain_seed" or "wood" or "clay") ||
                    resource.IsRenewable != (resource.Kind is "fiber" or "seed" or "grain_seed") ||
                    resource.NaturalObjectKind != (resource.Kind == "wood" ? "fallen_wood" : null))
                    return true;
                return resource.Kind == "clay"
                    ? !IsReachableClayBank(baseline,
                        StartingStoragePosition(state.Map, state.WorldSimulation, state.Towns), resource.Position)
                    : !baseline.IsBuildable(resource.Position);
            })))
            return false;
        if (planted.Length > 0 && planted.Any(tree => !TreeGrowthRules.IsValidPlantedTree(baseline, tree)))
            return false;
        var original = state.Map with
        {
            Resources = state.Map.Resources.Where(resource => baseIds.Contains(resource.Id)).ToArray(),
        };
        return MapManifestCodec.Digest(original) == baseline.ManifestDigest;
    }

    private static bool IsReachableClayBank(WorldMap baseline, GridPoint origin, GridPoint point) =>
        baseline.HydrologyAt(point) == WaterKind.Land && baseline.IsBuildable(point) &&
        baseline.IsReachableOnFoot(origin, point) && ClayBankOffsets.Any(offset =>
        {
            var neighbor = baseline.WrapColumn(new GridPoint(point.X + offset.X, point.Y + offset.Y));
            return baseline.Contains(neighbor) &&
                baseline.HydrologyAt(neighbor) is WaterKind.River or WaterKind.Lake;
        });

    private void StageHearthContent()
    {
        var packages = contentRegistry.ExportState().Packages;
        if (packages.Any(package => package.Manifest.PackageId == HearthContent.PackageId) ||
            !packages.Any(package => package.Manifest.PackageId == StarterContent.PackageId && package.Lifecycle == ContentPackageLifecycle.Active))
        {
            return;
        }
        var manifest = HearthContent.Create();
        var resolution = ContentPackageResolver.Resolve(packages.Select(package => package.Manifest).Append(manifest), [manifest.PackageId]);
        contentRegistry.Propose(manifest, WorldTick);
        contentRegistry.Validate(manifest.PackageId, resolution, WorldTick);
        contentRegistry.Approve(manifest.PackageId, WorldTick);
        contentRegistry.Stage(manifest.PackageId, WorldTick);
        AppendEvent(new HearthContentStagedEventPayload(null, manifest.PackageId));
    }

    private void StageHouseContent()
    {
        var packages = contentRegistry.ExportState().Packages;
        if (packages.Any(package => package.Manifest.PackageId == HouseContent.PackageId) ||
            !packages.Any(package => package.Manifest.PackageId == HearthContent.PackageId &&
                package.Lifecycle == ContentPackageLifecycle.Active))
            return;
        var manifest = HouseContent.Create();
        var resolution = ContentPackageResolver.Resolve(packages.Select(package => package.Manifest).Append(manifest),
            [manifest.PackageId]);
        contentRegistry.Propose(manifest, WorldTick);
        contentRegistry.Validate(manifest.PackageId, resolution, WorldTick);
        contentRegistry.Approve(manifest.PackageId, WorldTick);
        contentRegistry.Stage(manifest.PackageId, WorldTick);
        AppendEvent(new HouseContentStagedEventPayload(null, manifest.PackageId));
    }

    private void StageWarehouseContent()
    {
        var packages = contentRegistry.ExportState().Packages;
        if (packages.Any(package => package.Manifest.PackageId == WarehouseContent.PackageId) ||
            !packages.Any(package => package.Manifest.PackageId == HouseContent.PackageId &&
                package.Lifecycle == ContentPackageLifecycle.Active))
            return;
        var manifest = WarehouseContent.Create();
        var resolution = ContentPackageResolver.Resolve(packages.Select(package => package.Manifest).Append(manifest),
            [manifest.PackageId]);
        contentRegistry.Propose(manifest, WorldTick);
        contentRegistry.Validate(manifest.PackageId, resolution, WorldTick);
        contentRegistry.Approve(manifest.PackageId, WorldTick);
        contentRegistry.Stage(manifest.PackageId, WorldTick);
        AppendEvent(new WarehouseContentStagedEventPayload(null, manifest.PackageId));
    }

    private void StageFarmContent()
    {
        var packages = contentRegistry.ExportState().Packages;
        if (packages.Any(package => package.Manifest.PackageId == FarmContent.PackageId) ||
            !packages.Any(package => package.Manifest.PackageId == HouseContent.PackageId &&
                package.Lifecycle == ContentPackageLifecycle.Active))
            return;
        var manifest = FarmContent.Create();
        var resolution = ContentPackageResolver.Resolve(packages.Select(package => package.Manifest).Append(manifest),
            [manifest.PackageId]);
        contentRegistry.Propose(manifest, WorldTick);
        contentRegistry.Validate(manifest.PackageId, resolution, WorldTick);
        contentRegistry.Approve(manifest.PackageId, WorldTick);
        contentRegistry.Stage(manifest.PackageId, WorldTick);
        AppendEvent(new FarmContentStagedEventPayload(null, manifest.PackageId));
    }

    private void StageBlacksmithContent()
    {
        var packages = contentRegistry.ExportState().Packages;
        if (packages.Any(package => package.Manifest.PackageId == BlacksmithContent.PackageId) ||
            !packages.Any(package => package.Manifest.PackageId == HouseContent.PackageId &&
                package.Lifecycle == ContentPackageLifecycle.Active))
            return;
        var manifest = BlacksmithContent.Create();
        var resolution = ContentPackageResolver.Resolve(packages.Select(package => package.Manifest).Append(manifest),
            [manifest.PackageId]);
        contentRegistry.Propose(manifest, WorldTick);
        contentRegistry.Validate(manifest.PackageId, resolution, WorldTick);
        contentRegistry.Approve(manifest.PackageId, WorldTick);
        contentRegistry.Stage(manifest.PackageId, WorldTick);
        AppendEvent(new BlacksmithContentStagedEventPayload(null, manifest.PackageId));
    }

    private void StageHouseCookingContent()
    {
        var packages = contentRegistry.ExportState().Packages;
        if (packages.Any(package => package.Manifest.PackageId == HouseCookingContent.PackageId) ||
            !packages.Any(package => package.Manifest.PackageId == HouseContent.PackageId &&
                package.Lifecycle == ContentPackageLifecycle.Active))
            return;
        var manifest = HouseCookingContent.Create();
        var resolution = ContentPackageResolver.Resolve(packages.Select(package => package.Manifest).Append(manifest),
            [manifest.PackageId]);
        contentRegistry.Propose(manifest, WorldTick);
        contentRegistry.Validate(manifest.PackageId, resolution, WorldTick);
        contentRegistry.Approve(manifest.PackageId, WorldTick);
        contentRegistry.Stage(manifest.PackageId, WorldTick);
        AppendEvent(new HouseCookingContentStagedEventPayload(null, manifest.PackageId));
    }

    private void StageRestaurantContent() =>
        StageBuiltInContent(RestaurantContent.PackageId, HouseContent.PackageId, RestaurantContent.Create,
            "restaurant_content_staged");

    private void StagePotteryContent() =>
        StageBuiltInContent(PotteryContent.PackageId, HouseContent.PackageId, PotteryContent.Create,
            "pottery_content_staged");

    private void StageSiloContent() =>
        StageBuiltInContent(SiloContent.PackageId, FarmContent.PackageId, SiloContent.Create, "silo_content_staged");

    private void StageTailorContent() =>
        StageBuiltInContent(TailorContent.PackageId, HouseContent.PackageId, TailorContent.Create, "tailor_content_staged");

    /// <summary>
    /// Stages a shipped package once its dependency is active, so worlds that
    /// did not start with it still receive it. It activates on a later tick.
    /// </summary>
    private void StageBuiltInContent(string packageId, string requiredActivePackageId,
        Func<ContentPackageManifest> create, string eventKind)
    {
        var packages = contentRegistry.ExportState().Packages;
        if (packages.Any(package => package.Manifest.PackageId == packageId) ||
            !packages.Any(package => package.Manifest.PackageId == requiredActivePackageId &&
                package.Lifecycle == ContentPackageLifecycle.Active))
            return;
        var manifest = create();
        var resolution = ContentPackageResolver.Resolve(packages.Select(package => package.Manifest).Append(manifest),
            [manifest.PackageId]);
        contentRegistry.Propose(manifest, WorldTick);
        contentRegistry.Validate(manifest.PackageId, resolution, WorldTick);
        contentRegistry.Approve(manifest.PackageId, WorldTick);
        contentRegistry.Stage(manifest.PackageId, WorldTick);
        AppendEvent(WorldEvents.ContentStaged(eventKind, null, manifest.PackageId));
    }

    private void AddStartingResources()
    {
        var ground = Ground;
        var occupied = GroundOccupancy.Combine(ground.MapObjects, ground.Resources, ground.Fields,
            ground.RoadsAndBridgeEntrances, ground.BuildingFootprints, ground.ExpansionSites);
        var additions = new List<MapResource>();
        var townStorage = SharedStoragePosition;
        var startingSiteChunk = worldSystems.Chunks.Single(chunk => chunk.Coordinate ==
            ChunkRules.ToChunkCoordinate(townStorage, chunk.ChunkSize));
        var startingSiteOrigin = startingSiteChunk.Coordinate.Origin(startingSiteChunk.ChunkSize);
        if (!map.Resources.Any(resource => resource.Id == "settlement-clay"))
        {
            var bank = FreshWaterClayBank(townStorage, occupied);
            if (bank is null)
            {
                AppendEvent(new StartingResourceBlockedEventPayload(null, "clay"));
            }
            else
            {
                additions.Add(new MapResource("settlement-clay", "clay", bank.Value, false));
                occupied.Add(bank.Value);
            }
        }
        foreach (var kind in new[] { "stone", "fiber", "seed", "grain_seed", "wood" })
        {
            var id = "settlement-" + kind;
            if (map.Resources.Any(resource => resource.Id == id))
            {
                continue;
            }
            var tile = map.Tiles.FirstOrDefault(tile =>
                tile.Position.X >= Math.Max(startingSiteOrigin.X, townStorage.X - 1) &&
                tile.Position.X < Math.Min(startingSiteOrigin.X + startingSiteChunk.Width, townStorage.X + 6) &&
                tile.Position.Y >= Math.Max(startingSiteOrigin.Y, townStorage.Y - 1) &&
                tile.Position.Y < Math.Min(startingSiteOrigin.Y + startingSiteChunk.Height, townStorage.Y + 5) &&
                map.IsBuildable(tile.Position) && !occupied.Contains(tile.Position));
            if (tile is null)
            {
                AppendEvent(new StartingResourceBlockedEventPayload(null, kind));
                continue;
            }
            additions.Add(new MapResource(id, kind, tile.Position,
                kind is "fiber" or "seed" or "grain_seed",
                NaturalObjectKind: kind == "wood" ? "fallen_wood" : null));
            occupied.Add(tile.Position);
        }
        if (additions.Count == 0)
        {
            return;
        }
        map = map with { Resources = map.Resources.Concat(additions).OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray() };
        map = map with { ManifestDigest = MapManifestCodec.Digest(map) };
        var changedChunkCoordinates = additions.Select(resource =>
                ChunkRules.ToChunkCoordinate(resource.Position, startingSiteChunk.ChunkSize))
            .ToHashSet();
        environmentSystem.AddStartingResources(map, additions, changedChunkCoordinates, WorldTick);
        SyncEcologyResourceStates();
        AppendEvent(new StartingResourcesAddedEventPayload(null, string.Join(',', additions.Select(resource => resource.Kind))));
    }

    private GridPoint? FreshWaterClayBank(GridPoint origin, HashSet<GridPoint> occupied)
    {
        var water = map.Tiles.Select(tile => tile.Position)
            .Where(point => map.HydrologyAt(point) is WaterKind.River or WaterKind.Lake)
            .ToHashSet();
        var banks = new HashSet<GridPoint>();
        foreach (var point in water)
        {
            foreach (var (dx, dy) in ClayBankOffsets)
            {
                var bank = map.WrapColumn(new GridPoint(point.X + dx, point.Y + dy));
                if (!occupied.Contains(bank) && IsReachableClayBank(map, origin, bank))
                    banks.Add(bank);
            }
        }
        if (banks.Count == 0)
            return null;
        return banks.OrderBy(point => map.FootDistance(origin, point))
            .ThenBy(point => point.Y).ThenBy(point => point.X).First();
    }

    private static void ValidateProject(AgentProject project, long worldTick)
    {
        if (string.IsNullOrWhiteSpace(project.CandidateId) ||
            !TownConstructionCandidateIds.TryParse(project.CandidateId, out _) ||
            string.IsNullOrWhiteSpace(project.Label) || project.Label.Length > 256 ||
            project.StartedTick < 0 || project.StartedTick > worldTick ||
            project.LastTransitionTick < project.StartedTick || project.LastTransitionTick > worldTick ||
            project.WorkDone is < 0 or > ProjectWorkTicks ||
            project.Stage is not ("acquiring" or "gathering" or "delivering" or "travelling" or "working" or "waiting" or "blocked" or "paused" or "completed" or "cancelled") ||
            project.RequiresFreshChoice && (project.Stage != "paused" || project.JobId is not null) ||
            project.OrderInstructionId is { } orderId && (string.IsNullOrWhiteSpace(orderId) ||
                orderId != orderId.Trim() || orderId.Any(char.IsControl)))
        {
            throw new InvalidDataException("The saved agent project is invalid.");
        }
    }

    private bool CanContinueProject(AgentState state, bool waitingForModel = false) =>
        IsAdult(state.AgentId) &&
        state.Project is { Stage: not ("completed" or "cancelled") } project &&
        state.Exploration is not { OutingPath.Count: > 0, Goal: not null } &&
        project.OrderInstructionId is null &&
        !project.RequiresFreshChoice &&
        (project.Stage != "blocked" || WorldTick - project.LastTransitionTick < BlockedProjectRetryDelayTicks) &&
        !NeedsUrgentFood(state) &&
        (waitingForModel || !HasTradeResponse(state.AgentId) &&
        !HasFoodPolicyDecision(state.AgentId) &&
        !HasHousingDecision(state.AgentId) &&
        !HasFamilyDecision(state.AgentId) &&
        !HasParenthoodDecision(state.AgentId) &&
        !HasDependentCareDecision(state.AgentId) &&
        !HasLearningDecision(state.AgentId) &&
        !agents.Keys.Any(other => TradeOpportunity(state.AgentId, other) is not null)) &&
        (!NeedsUrgentWarmth(state) || IsProtectiveProject(state.Project)) &&
        PendingInstructionFor(state.AgentId) is null;

    private bool PauseExpiredUnfillableHouseholdRecipeProject(string agentId)
    {
        if (agents[agentId].Project is not
            {
                Stage: "blocked",
                JobId: null,
            } project || !IsIngredientBlocker(project.Blocker) ||
            WorldTick - project.LastTransitionTick < BlockedProjectRetryDelayTicks ||
            !TownConstructionCandidateIds.TryParse(project.CandidateId, out var selection) || selection.IsBuilding)
            return false;

        var recipe = worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == selection.DefinitionId);
        if (recipe is null || !TryFindRecipeSite(recipe, out var siteId, out _, agentId) ||
            worldSimulation.Buildings.FirstOrDefault(item => item.InstanceId == siteId) is not
            { HouseholdId: { } householdId } building ||
            !worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                definition.Tags.Any(IsHouseholdBuildingTag)) ||
            HasIngredientsAtBuilding(recipe.Inputs, householdId, building.InstanceId) ||
            worldSimulation.ProductionJobs.Concat(worldSimulation.CropBuilds ?? []).Any(job =>
                job.WorkerId == agentId && job.RecipeId == recipe.CanonicalId &&
                job.State == WorldProductionJobState.Running))
            return false;

        var missingKinds = recipe.Inputs.Where(input => society.Checkpoint.Inventory.Lots
                .Where(lot => lot.OwnerId == householdId && lot.StorageBuildingId == building.InstanceId &&
                    lot.ItemKind == input.ResourceId)
                .Sum(AvailableLotQuantity) < input.Amount)
            .Select(input => input.ResourceId).ToHashSet(StringComparer.Ordinal);
        if (HasActionableProjectInputSupply(householdId, building, missingKinds))
            return false;

        SetProject(agentId, project with
        {
            Stage = "paused",
            Blocker = FreshChoicePauseBlocker,
            RequiresFreshChoice = true,
        });
        return true;
    }

    private bool HasActionableProjectInputSupply(string householdId, PlacedBuilding building,
        HashSet<string> missingKinds)
    {
        if (missingKinds.Count == 0) return false;
        var residents = society.Checkpoint.Agents.Where(person => person.HouseholdId == householdId &&
                person.Status == SocietyAgentStatus.Active && IsAdult(person.Id))
            .Select(person => person.Id).ToArray();
        foreach (var resident in residents)
        {
            foreach (var candidate in CreateCandidates(resident, agents[resident]))
            {
                if (candidate.Id.StartsWith(SupplyWorkstationPrefix, StringComparison.Ordinal) &&
                    candidate.DestinationId == building.InstanceId &&
                    missingKinds.Contains(candidate.Id[SupplyWorkstationPrefix.Length..]))
                    return true;

                if (candidate.Id == "haul_smith_input" && candidate.DestinationId == building.InstanceId &&
                    BlacksmithInputForDelivery(householdId, building, resident) is { } smithInput &&
                    missingKinds.Contains(smithInput.ItemKind))
                    return true;

                if (candidate.Id is "gather_smith_ore" or "deliver_smith_ore" &&
                    candidate.DestinationId == building.InstanceId && missingKinds.Contains("iron_ore"))
                    return true;

                if (candidate.Id.StartsWith("assist:", StringComparison.Ordinal) &&
                    missingKinds.Contains(candidate.Id["assist:".Length..]))
                    return true;

                if (candidate.Id == "haul_household_stock" && candidate.DestinationId == building.InstanceId &&
                    society.Checkpoint.Inventory.Lots.Any(lot => lot.DeliveryBuildingId == building.InstanceId &&
                        missingKinds.Contains(lot.ItemKind) && AvailableLotQuantity(lot) > 0 &&
                        (lot.OwnerId == resident || lot.OwnerId == householdId)) ||
                    candidate.Id == "haul_household_stock" && candidate.DestinationId == building.InstanceId &&
                    UnlocatedHouseholdStock(householdId) is { } stock && missingKinds.Contains(stock.ItemKind))
                    return true;
            }
        }
        return false;
    }

    private void BeginProject(string agentId, AgentState state, string candidateId, string? toolMakingRequestId = null, ActionPlan? originalPlan = null)
    {
        if (!TownConstructionCandidateIds.TryParse(candidateId, out var selected))
            return;

        if (selected.IsBuilding &&
            worldContent.BuildingDefinitions.TryGetValue(selected.DefinitionId, out var workBuilding) &&
            society.Checkpoint.GetAgent(agentId).HouseholdId is { } workHousehold &&
            !WorkBuildingHasInputs(agentId, workHousehold, workBuilding))
        {
            AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "This building has no reachable inputs for its work."));
            return;
        }

        var constructionPlan = originalPlan as ConstructionActionPlan;
        if (selected.IsBuilding && selected.SitePosition is { } chosenSite && constructionPlan is null &&
            worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == selected.DefinitionId) is { } design)
            constructionPlan = PlanConstruction(agentId, design, chosenSite);
        if (constructionPlan is not null && CheckConstruction(constructionPlan).Refusal is { } constructionRefusal)
        { RefuseChoice(constructionPlan, constructionRefusal); return; }
        if (constructionPlan is not null)
            constructionProjectPlans[agentId] = (state.Project is { Stage: not ("completed" or "cancelled") } previousBuilding &&
                TownConstructionCandidateIds.TryParse(previousBuilding.CandidateId, out var previousSelection) && previousSelection.IsBuilding &&
                previousSelection.DefinitionId == selected.DefinitionId ? previousBuilding.StartedTick : WorldTick, constructionPlan);

        var recipePlan = originalPlan as RecipeProjectActionPlan;
        if (!selected.IsBuilding && originalPlan is not RecipeGoalActionPlan && recipePlan is null && worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == selected.DefinitionId) is { } recipe &&
            TryFindRecipeSite(recipe, out var site, out _, agentId))
            recipePlan = PlanRecipeProject(agentId, recipe, worldSimulation.Buildings.Single(item => item.InstanceId == site));
        if (recipePlan is not null && CheckRecipeProject(recipePlan).Refusal is { } refusal)
        { RefuseChoice(recipePlan, refusal); return; }
        if (recipePlan is not null)
            productionSystem.RememberRecipePlan(agentId, state.Project is { Stage: not ("completed" or "cancelled") } previous && previous.CandidateId == candidateId
                ? previous.StartedTick : WorldTick, RecipeProjectInstructionId(agentId, recipePlan.Recipe.CanonicalId), recipePlan);

        if (state.Project is { Stage: not ("completed" or "cancelled") } existing &&
            TownConstructionCandidateIds.TryParse(existing.CandidateId, out var current) &&
            current.IsBuilding == selected.IsBuilding && current.DefinitionId == selected.DefinitionId)
        {
            if (existing.RequiresFreshChoice)
            {
                state = state with
                {
                    Project = existing with
                    {
                        CandidateId = candidateId,
                        Stage = "acquiring",
                        Blocker = null,
                        LastTransitionTick = WorldTick,
                        RequiresFreshChoice = false,
                    },
                };
                agents[agentId] = state;
                AppendEvent(new ProjectFreshChoiceResumedEventPayload(agentId, candidateId));
            }
            else if (existing.CandidateId != candidateId)
            {
                state = state with
                {
                    Project = existing with
                    {
                        CandidateId = candidateId,
                        Stage = "acquiring",
                        Blocker = null,
                        LastTransitionTick = WorldTick,
                    },
                };
                agents[agentId] = state;
                AppendEvent(new ProjectSiteReselectedEventPayload(agentId, selected.DefinitionId));
            }
            else if (existing.Stage == "blocked")
            {
                state = state with { Project = existing with { LastTransitionTick = WorldTick } };
                agents[agentId] = state;
            }
            ContinueProject(agentId, state);
            return;
        }

        var definitionId = selected.DefinitionId;
        if (selected.IsBuilding &&
            worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == definitionId) is { } shop &&
            !TownHasRoomForSharedShop(agentId, HouseholdBuildingKind(shop)))
        {
            AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "town_shop_limit"));
            return;
        }
        // Two members can choose in the same tick; the household still plans one of each kind.
        if (selected.IsBuilding &&
            worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == definitionId) is { } planned &&
            HouseholdBuildingKind(planned) is { } plannedKind &&
            society.Checkpoint.GetAgent(agentId).HouseholdId is { } planningHousehold &&
            HouseholdBuildingProjectInProgress(planningHousehold, plannedKind))
        {
            AppendEvent(new BuildRejectedEventPayload(agentId, candidateId, "household_plan_in_progress"));
            return;
        }
        var label = selected.IsBuilding
            ? worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == definitionId)?.DisplayName
            : worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == definitionId)?.DisplayName;
        if (label is null)
            return;
        state = state with { Project = new AgentProject(candidateId, label, WorldTick, "acquiring", LastTransitionTick: WorldTick, ToolMakingRequestId: toolMakingRequestId) };
        agents[agentId] = state;
        AppendEvent(new ProjectChosenEventPayload(agentId, candidateId));
        ContinueProject(agentId, state);
    }

    private void ContinueProject(string agentId, AgentState state)
    {
        var project = state.Project!;
        RecordAttemptedAction(agentId, project.CandidateId);
        if (project.JobId is not null)
        {
            var job = worldSimulation.ProductionJobs.Concat(worldSimulation.CropBuilds ?? [])
                .FirstOrDefault(item => item.JobId == project.JobId);
            SetProject(agentId, project with
            {
                Stage = job?.State switch { WorldProductionJobState.Running => "waiting", WorldProductionJobState.Completed => "completed", _ => "cancelled" },
                Blocker = job is null || job.State == WorldProductionJobState.Cancelled ? "Production was removed or cancelled" : null,
            });
            return;
        }

        if (!TownConstructionCandidateIds.TryParse(project.CandidateId, out var selection))
        {
            SetProject(agentId, project with { Stage = "cancelled", Blocker = "This building choice can't be used" });
            return;
        }
        var definitionId = selection.DefinitionId;
        var building = selection.IsBuilding ? worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == definitionId) : null;
        var recipe = selection.IsBuilding ? null : worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == definitionId);
        if (building is null && recipe is null)
        {
            SetProject(agentId, project with { Stage = "cancelled", Blocker = "This building or recipe is no longer available" });
            return;
        }
        var constructionPlan = building is null ? null : RetainedConstruction(agentId, project);
        if (building is not null && selection.SitePosition is { } plannedSite)
        {
            if (constructionPlan is null)
            {
                constructionPlan = PlanConstruction(agentId, building, plannedSite);
                constructionProjectPlans[agentId] = (project.StartedTick, constructionPlan);
            }
        }
        if (building?.Tags.Contains("house", StringComparer.Ordinal) == true)
        {
            var householdId = society.Checkpoint.GetAgent(agentId).HouseholdId;
            if (householdId is null)
            {
                SetProject(agentId, project with { Stage = "cancelled", Blocker = "A household is required to own a House." });
                return;
            }
            if (HouseForHousehold(householdId) is not null)
            {
                SetProject(agentId, project with { Stage = "cancelled", Blocker = "This household already has a House." });
                return;
            }
        }
        if (building is not null && HouseholdBuildingKind(building) is { } kind && kind != "house")
        {
            var householdId = society.Checkpoint.GetAgent(agentId).HouseholdId;
            if (householdId is null)
            {
                SetProject(agentId, project with { Stage = "cancelled", Blocker = "A household is required to hold this building." });
                return;
            }
            if (HouseholdBuildingWithTag(householdId, kind) is not null)
            {
                SetProject(agentId, project with { Stage = "cancelled", Blocker = $"This household already holds a {building.DisplayName}." });
                return;
            }
            if (kind == "silo" && FarmhouseForHousehold(householdId) is null)
            {
                SetProject(agentId, project with { Stage = "cancelled", Blocker = "Only the household holding a Farmhouse builds a Silo." });
                return;
            }
        }
        if (building is not null &&
            society.Checkpoint.GetAgent(agentId).HouseholdId is { } workHousehold &&
            !WorkBuildingHasInputs(agentId, workHousehold, building))
        {
            SetProject(agentId, project with { Stage = "blocked", Blocker = "This building has no reachable inputs for its work." });
            return;
        }
        if (building?.Tags.Contains("warehouse", StringComparer.Ordinal) == true &&
            (TownForResident(agentId) is not { } townId ||
             worldSimulation.Buildings.Any(placed => placed.TownId == townId &&
                 worldContent.Buildings.Any(definition => definition.CanonicalId == placed.DefinitionId &&
                     definition.Tags.Contains("warehouse", StringComparer.Ordinal)))))
        {
            SetProject(agentId, project with
            {
                Stage = "cancelled",
                Blocker = "This Town already has a Warehouse, or the builder is no longer a resident.",
            });
            return;
        }
        if (constructionPlan is not null && CheckConstruction(constructionPlan, checkDestination: false).Refusal is { } constructionRefusal)
        {
            RefuseChoice(constructionPlan, constructionRefusal);
            SetProject(agentId, project with { Stage = "paused", Blocker = constructionRefusal.Explanation, RequiresFreshChoice = true });
            return;
        }
        var inputs = building?.BuildCosts ?? recipe!.Inputs;
        PlacedBuilding? recipeBuilding = null;
        var recipePlan = RetainedRecipeProject(agentId, project);
        GridPoint? inputWorkSite = selection.SitePosition;
        if (recipe is not null)
        {
            if (recipePlan is null && TryFindRecipeSite(recipe, out var restoredSite, out _, agentId))
            {
                recipePlan = PlanRecipeProject(agentId, recipe, worldSimulation.Buildings.Single(item => item.InstanceId == restoredSite));
                productionSystem.RememberRecipePlan(agentId, project.StartedTick, RecipeProjectInstructionId(agentId, recipePlan.Recipe.CanonicalId), recipePlan);
            }
            if (recipePlan is null)
            {
                SetProject(agentId, project with { Stage = "blocked", Blocker = WaitingForWorkSiteBlocker });
                return;
            }
            if (CheckRecipeProject(recipePlan).Refusal is { } refusal)
            {
                RefuseChoice(recipePlan, refusal);
                SetProject(agentId, project with { Stage = "paused", Blocker = refusal.Explanation, RequiresFreshChoice = true });
                return;
            }
            recipeBuilding = recipePlan.Workstation;
            inputWorkSite = recipeBuilding.Position;
        }
        var constructionOwner = recipe is not null ? IsHandcartRecipe(recipe) ? agentId : ProductionOwnerFor(recipeBuilding, agentId)
            : constructionPlan?.Inputs is not null ? constructionPlan.MaterialOwnerId : BuildingConstructionOwner(agentId, building!);
        if (recipe is not null && !IsHandcartRecipe(recipe) && recipeBuilding?.HouseholdId is not null &&
            worldContent.Buildings.Any(definition => definition.CanonicalId == recipeBuilding.DefinitionId &&
                definition.Tags.Any(IsHouseholdBuildingTag)) &&
            !HasIngredientsAtBuilding(recipe.Inputs, constructionOwner, recipeBuilding.InstanceId))
        {
            if (FinishProjectHouseholdDelivery(agentId, state))
                return;
            SetProject(agentId, project with { Stage = "blocked", Blocker = MissingProductionIngredients(recipe, constructionOwner, recipeBuilding.InstanceId) });
            return;
        }
        var missing = inputs.FirstOrDefault(input => !HasAvailableQuantities([input], constructionOwner));
        if (missing.Amount > 0)
        {
            if (FinishProjectHouseholdDelivery(agentId, state))
                return;
            if (constructionOwner == agentId && inputWorkSite is null && building is not null)
            {
                var rankedInputSites = TownLayoutService.RankConstructionSites(
                    CreateTownLayoutContext(agentId, null, building), building);
                if (rankedInputSites.Count > 0) inputWorkSite = rankedInputSites[0].Position;
            }
            AcquireProjectInput(agentId, state, missing, constructionOwner, inputWorkSite);
            return;
        }
        if (constructionPlan is { Inputs: null } && PlanConstructionInputs(building!.BuildCosts, constructionOwner) is { } selectedInputs)
        {
            constructionPlan = constructionPlan with { MaterialOwnerId = constructionOwner, Inputs = selectedInputs };
            constructionProjectPlans[agentId] = (project.StartedTick, constructionPlan);
        }
        GridPoint position;
        if (building is not null)
        {
            var layout = CreateTownLayoutContext(agentId, selection.SitePosition, building);
            if (selection.SitePosition is { } selectedSite)
            {
                if (!TownLayoutService.TryEvaluateConstructionSite(layout, building, selectedSite, out _))
                {
                    var blocker = project.OrderInstructionId is null
                        ? "The selected site is no longer legal; fresh ranked choices return after 60 ticks."
                        : "The ordered construction site is no longer legal.";
                    var newlyRejected = project.Stage != "blocked" || project.Blocker != blocker;
                    SetProject(agentId, project with
                    {
                        Stage = "blocked",
                        Blocker = blocker,
                    });
                    if (newlyRejected)
                    {
                        AppendEvent(new TownLayoutSiteRejectedEventPayload(agentId, TownForResident(agentId) ?? "none", building.CanonicalId, selectedSite.X, selectedSite.Y));
                    }
                    return;
                }
                position = selectedSite;
            }
            else if (TownLayoutService.RankConstructionSites(layout, building) is { Count: > 0 } rankedSites)
            {
                // Older saved projects accepted a building before the chooser
                // included a site. Preserve them by assigning the current top
                // legal option once, then persist that exact choice.
                project = project with { CandidateId = TownConstructionCandidateIds.Building(building.CanonicalId, rankedSites[0].Position) };
                SetProject(agentId, project);
                position = rankedSites[0].Position;
            }
            else
            {
                SetProject(agentId, project with { Stage = "blocked", Blocker = "No site where this can be built is free right now. " + HousingBlockers.LandRecoveryGuidance });
                return;
            }
        }
        else position = recipePlan!.Workstation.Position;
        if (state.Position != position)
        {
            SetProject(agentId, project with { Stage = "travelling", Blocker = null });
            MoveToward(agentId, agents[agentId], position, "project");
            return;
        }
        if (project.WorkDone < ProjectWorkTicks)
        {
            if (!IllnessRules.AllowsWork(agentId, WorldTick,
                    state.Survival?.IllnessBasisPoints ?? 0))
            {
                SetProject(agentId, project with { Stage = "working", Blocker = null });
                return;
            }
            var hammer = ToolProgressionRules.PlanWork(society.Checkpoint.Inventory,
                agentId, ToolFamily.Hammer);
            var work = (hammer?.WorkUnits ?? 1) + ProjectPracticeBonus(state, project);
            if (hammer is not null)
                ApplyToolWork(agentId, hammer);
            var done = SkilledWorkProgress(agentId,
                building is not null ? SkillKind.Building : SkillForRecipe(recipe!), project.WorkDone, work);
            SetProject(agentId, project with { Stage = "working", WorkDone = Math.Min(ProjectWorkTicks, done), Blocker = null });
            return;
        }
        var buildingInstanceId = building is null ? null : project.OrderInstructionId is { } orderId
            ? ConstructionInstanceId(orderId) : BuildInstanceId(agentId, building);
        ApplyBuildDecision(agentId, state, project.CandidateId);
        if (buildingInstanceId is not null && worldSimulation.Buildings.Any(item => item.InstanceId == buildingInstanceId))
        {
            SetProject(agentId, agents[agentId].Project! with { Stage = "completed", Blocker = null });
        }
        else if (recipe is not null)
        {
            var job = worldSimulation.ProductionJobs.Concat(worldSimulation.CropBuilds ?? [])
                .FirstOrDefault(item => item.WorkerId == agentId && item.StartedTick == WorldTick && item.RecipeId == definitionId && (project.ToolMakingRequestId is null || item.ToolMakingRequestId == project.ToolMakingRequestId));
            if (job is not null)
            {
                SetProject(agentId, project with { Stage = "waiting", JobId = job.JobId, Blocker = null });
            }
            else
            {
                SetProject(agentId, project with { Stage = "blocked", Blocker = "Production could not start; waiting to retry" });
            }
        }
    }

    /// <summary>
    /// Whether someone else is already queued for the kind of site this recipe
    /// needs: fertile land for crops, or the recipe's workstation design. They
    /// keep their turn instead of losing it to new starts every time it frees.
    /// </summary>
    private bool AnotherAgentWaitsForWorkSite(string actor, RecipeDefinition recipe) =>
        agents.Values.Any(other => other.AgentId != actor &&
            other.Project is { Stage: "blocked", Blocker: WaitingForWorkSiteBlocker } waiting &&
            TownConstructionCandidateIds.TryParse(waiting.CandidateId, out var selection) && !selection.IsBuilding &&
            worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == selection.DefinitionId) is { } queued &&
            (queued.IsCrop ? recipe.IsCrop : !recipe.IsCrop && queued.WorkstationBuildingId == recipe.WorkstationBuildingId));

    private bool FinishProjectHouseholdDelivery(string actor, AgentState state)
    {
        if (!IsAdult(actor) || society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            CarriedHouseDelivery(actor) is not { } carried || carried.OwnerId != actor ||
            worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == carried.DeliveryBuildingId &&
                building.HouseholdId == householdId) is not { } destination)
            return false;
        if (!CanDeliverHouseDelivery(carried))
        {
            SetProject(actor, state.Project! with
            {
                Stage = "blocked",
                Blocker = "Household storage is full; make room for the carried delivery.",
            });
            return true;
        }
        if (state.Position != destination.Position &&
            Reach(actor, destination.Position, ReachPurpose.HouseholdReturn, 0, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return false;
        SetProject(actor, state.Project! with { Stage = "delivering", Blocker = "Completing the household supply delivery" });
        HaulHouseholdStock(actor, agents[actor]);
        return true;
    }

    private void AcquireProjectInput(string agentId, AgentState state,
        ContentQuantity input, string constructionOwner, GridPoint? personalWorkSite)
    {
        var plan = ProjectInputDefinition.Plan(this, agentId, input, constructionOwner, personalWorkSite);
        TryRegisteredAction(ActionPhase.ProjectInput, new(agentId, state, plan.CandidateId, false, plan));
    }

    private ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, MapResource source,
        bool useHarvestBonus = true)
    {
        var ecology = worldSystems.Ecology.GetResource(source.Id);
        var plan = ToolProgressionRules.PlanGather(itemKind, source, society.Checkpoint.Inventory, actor,
            ecology.Quantity);
        if (plan is null || plan.ToolLotId is null)
            return plan;
        // The required tool remains carried. A bonus that cannot fit even
        // after setting down other cargo must use the existing smaller harvest.
        var capacity = PersonalEquipmentRules.Capacity(society.Checkpoint.Inventory, actor, agents[actor].Equipment) +
            HorseCargoCapacity(actor);
        if (useHarvestBonus && checked(plan.Quantity + plan.TreeSeedQuantity + 1) <= capacity)
            return plan;
        return plan with { Quantity = Math.Min(plan.Quantity, 4) };
    }

    private int ProjectMaterialCarryUnits(string actor, string itemKind, MapResource source,
        bool useHarvestBonus = true)
    {
        var plan = ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus);
        return plan is null ? int.MaxValue : checked(plan.Quantity + plan.TreeSeedQuantity);
    }

    private MaterialGatherEffect? GatherProjectMaterial(string agentId, AgentState state, string itemKind,
        MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null,
        GridPoint? returnTo = null, int returnRange = 0)
    {
        var plan = ProjectMaterialHarvest(agentId, itemKind, source, useHarvestBonus);
        returnTo ??= worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == deliveryBuildingId)?.Position;
        if (returnTo is { } destination && !CanReturnWithHarvest(agentId, itemKind, source,
            destination, returnRange, useHarvestBonus)) return null;
        if (plan is null)
        {
            CollectToolForGathering(agentId, state, itemKind, source);
            return null;
        }
        return GatherPlannedProjectMaterial(agentId, state, itemKind, source, plan, deliveryBuildingId);
    }

    private MaterialGatherEffect? GatherPlannedProjectMaterial(string agentId, AgentState state, string itemKind,
        MapResource source, ToolGatheringPlan plan, string? deliveryBuildingId = null)
    {
        var ecology = worldSystems.Ecology.GetResource(source.Id);
        if (!IsWithinInteractionRange(state.Position, source.Position, ResourceInteractionRange))
        {
            MoveToward(agentId, agents[agentId], source.Position, "materials", ResourceInteractionRange);
            return null;
        }
        if (FreeCarryCapacity(agentId) < checked(plan.Quantity + plan.TreeSeedQuantity))
        {
            AppendEvent(new CarryingFullEventPayload(agentId));
            return null;
        }
        var harvest = EcologyRules.Harvest(ecology, 1);
        if (!harvest.IsValid || harvest.Resource is null)
        {
            return null;
        }
        var harvested = source.TreeKind is not null && harvest.Resource.Quantity == 0 && source.IsRenewable
            ? harvest.Resource with
            {
                NextRegenerationDay = WorldCalendarRules.FromTick(WorldTick, worldSystems.Config).DayIndex +
                    harvest.Resource.RegenerationIntervalDays,
            }
            : harvest.Resource;
        // The inventory helper also applies one unit of tool wear. Commit that
        // together with the output before reducing the finite ecology stock.
        // A failed inventory/capacity transition therefore leaves the source
        // untouched and does not wear a tool.
        ApplyGatheringInventory(agentId, itemKind, plan, deliveryBuildingId);
        environmentSystem.ReplaceEcologyResource(source.Id, harvested);
        SyncEcologyResourceStates();
        RecordNonLandConduct(agentId, plan.FellTree ? "fell_tree" : "gather_material", source.Position,
            source.Id, itemKind, plan.Quantity, $"gather-material:{WorldTick}:{agentId}:{nextEventId}");
        AppendEvent(new MaterialGatheredEventPayload(agentId, itemKind, plan.Quantity));
        if (plan.FellTree)
            AppendEvent(new TreeHarvestedEventPayload(agentId, source.Id, source.TreeKind));
        if (plan.TreeSeedQuantity > 0)
        {
            // A felled tree also gives a seed that can replant a stump or
            // start a new tree elsewhere.
            AppendEvent(new TreeSeedCollectedEventPayload(agentId, source.Id, plan.TreeSeedQuantity));
        }
        return new MaterialGatherEffect(MaterialLotId(WorldTick, agentId), itemKind, plan.Quantity, source.Id);
    }

    private static InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId,
        string? deliveryBuildingId) => deliveryBuildingId is null ? inventory :
        InventoryAuthority.AssignDelivery(inventory, lotId, deliveryBuildingId);

    private IEnumerable<(string Requester, ContentQuantity Input, string OwnerId)> ProjectRequests(string helperId)
    {
        foreach (var person in agents.Values.OrderBy(person => person.AgentId, StringComparer.Ordinal))
        {
            if (person.AgentId == helperId ||
                person.Project is not { Stage: not ("completed" or "cancelled" or "waiting") } project ||
                !TownConstructionCandidateIds.TryParse(project.CandidateId, out var selection))
            {
                continue;
            }
            var building = selection.IsBuilding ? worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == selection.DefinitionId) : null;
            if (selection.IsBuilding && building is null) continue;
            var inputs = selection.IsBuilding
                ? building!.BuildCosts
                : worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == selection.DefinitionId)?.Inputs;
            var constructionOwner = selection.IsBuilding
                ? BuildingConstructionOwner(person.AgentId, building!)
                : worldContent.Recipes.Any(item =>
                    item.CanonicalId == selection.DefinitionId &&
                    (item.Tags.Contains("grain", StringComparer.Ordinal) ||
                     item.WorkstationBuildingId is { } workstationId && worldContent.Buildings.Any(definition =>
                         definition.CanonicalId == workstationId &&
                         definition.Tags.Any(IsHouseholdBuildingTag))))
                ? HouseholdFor(person.AgentId) :
                    society.Checkpoint.GetAgent(person.AgentId).HouseholdId is null
                        ? person.AgentId : HouseholdId;
            foreach (var input in inputs ?? [])
            {
                if (!HasAvailableQuantities([input], constructionOwner))
                {
                    yield return (person.AgentId, input, constructionOwner);
                }
            }
        }
    }

    private MapResource? MaterialSource(string itemKind, string actor, GridPoint? returnTo = null,
        int returnRange = 0, bool useHarvestBonus = true)
    {
        // A source scan observes one inventory snapshot. Reuse tool availability
        // during that scan; gathering rechecks the actual tool and capacity.
        var reachableToolCache = new Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>();
        // Sort the cheap matches, then check tools and routes nearest first. A
        // Small world can hold thousands of trees; the nearest usable one wins
        // either way, so checking every one first only costs time.
        return map.Resources
            .Where(resource =>
                (resource.Kind == itemKind || (itemKind == "wood" && resource.Kind == "construction")) &&
                resources.GetValueOrDefault(resource.Id) == ResourceState.Available)
            .OrderBy(resource => map.FootDistance(agents[actor].Position, resource.Position))
            .ThenBy(resource => resource.Id, StringComparer.Ordinal)
            .FirstOrDefault(resource =>
                CanGatherFromSource(actor, itemKind, resource, reachableToolCache) &&
                CanReachTerrain(actor, resource.Position, ReachPurpose.ProjectInput, 0, SwimmingPolicy.WhenCapable) &&
                (IsWithinInteractionRange(agents[actor].Position, resource.Position, ResourceInteractionRange) ||
                 Reach(actor, resource.Position, ReachPurpose.ProjectInput, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0) &&
                (returnTo is null || CanReturnWithHarvest(actor, itemKind, resource, returnTo.Value, returnRange, useHarvestBonus)));
    }

    private bool CanAcquireProjectInputs(IReadOnlyList<ContentQuantity> inputs, string? ownerId = null,
        string? residentId = null,
        Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>? reachableToolCache = null,
        Lazy<PlacedBuilding[]>? accessibleWarehouses = null)
    {
        // Candidate evaluation is a read-only snapshot. Reuse a shared tool's route
        // result within one agent's candidate query. Other callers start
        // fresh, and actions revalidate before gathering.
        reachableToolCache ??= new Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>();
        accessibleWarehouses ??= residentId is null ? null : new Lazy<PlacedBuilding[]>(
            () => WarehousesAccessibleTo(residentId).ToArray(), LazyThreadSafetyMode.None);
        return inputs.All(input =>
        {
            var stored = society.Checkpoint.Inventory.Lots.Where(lot => lot.ItemKind == input.ResourceId &&
                    (lot.OwnerId == (ownerId ?? HouseholdId) || agents.ContainsKey(lot.OwnerId) &&
                        (ownerId is null || HouseholdFor(lot.OwnerId) == ownerId)))
                .Sum(lot => (long)AvailableLotQuantity(lot));
            var communal = residentId is null ? 0 : accessibleWarehouses!.Value
                .SelectMany(warehouse => society.Checkpoint.Inventory.Lots.Where(lot =>
                    lot.OwnerId == warehouse.TownId && lot.StorageBuildingId == warehouse.InstanceId &&
                    lot.ItemKind == input.ResourceId))
                .Sum(lot => (long)AvailableLotQuantity(lot));
            var remaining = input.Amount - stored - communal;
            if (remaining <= 0)
                return true;
            if (residentId is null)
                return false;

            foreach (var resource in map.Resources)
            {
                // Most sources cannot supply this input. Check the cheap kind
                // match before looking up their current availability by ID.
                if (resource.Kind != input.ResourceId && !(input.ResourceId == "wood" && resource.Kind == "construction") ||
                    resources.GetValueOrDefault(resource.Id) != ResourceState.Available)
                    continue;

                var sourceQuantity = worldSystems.Ecology.GetResource(resource.Id).Quantity;
                if (sourceQuantity <= 0 || !CanReachTerrain(residentId, resource.Position, ReachPurpose.ProjectInput, 0, SwimmingPolicy.WhenCapable))
                    continue;

                var gatherQuantity = AvailableGatherQuantity(residentId, input.ResourceId, resource, reachableToolCache);
                if (gatherQuantity <= 0)
                    continue;

                remaining -= (long)sourceQuantity * gatherQuantity;
                if (remaining <= 0)
                    return true;
            }

            return false;
        });
    }

    private void AddProjectAssistanceCandidates(List<CognitionCandidate> candidates, string helperId)
    {
        foreach (var request in ProjectRequests(helperId))
        {
            var choice = new ProjectAssistanceChoice(request.Input.ResourceId);
            if (candidates.Any(candidate => candidate.Id == choice.Encode())) continue;
            var plan = PlanProjectAssistance(helperId, request);
            if (plan is not null && CheckActionPlan(plan).Allowed)
                candidates.Add(new CognitionCandidate(plan.CandidateId,
                    $"Help {society.Checkpoint.GetAgent(request.Requester).Name}: gather and share {request.Input.ResourceId} for their project.", 15)
                { ActionPlan = plan });
        }
    }

    private bool CanReceiveProjectAssistance((string Requester, ContentQuantity Input, string OwnerId) request)
    {
        var house = HouseForHousehold(request.OwnerId);
        return house is null
            ? FreeCarryCapacity(request.Requester) > 0
            : DestinationRoom(house.InstanceId) > 0;
    }

    private void AssistProject(string helperId, AgentState state, string itemKind, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as ProjectAssistanceActionPlan;
        plan ??= PlanRequestedAssistance(helperId, itemKind);
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedProjectAssistance(plan, state, itemKind);
    }

    private void ApplyCheckedProjectAssistance(ProjectAssistanceActionPlan plan, AgentState state, string itemKind)
    {
        var helperId = plan.Actor;
        if (plan.LotId is null)
        {
            GatherPlannedProjectMaterial(helperId, state, itemKind, plan.Source!, plan.Harvest!);
            return;
        }
        if (!IsWithinInteractionRange(state.Position, plan.Destination, plan.Range))
        {
            MoveToward(helperId, state, plan.Destination, "share_materials", plan.Range);
            return;
        }
        var recipient = plan.RecipientId;
        var quantity = plan.Quantity;
        ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory, $"project-share:{WorldTick}:{helperId}",
            helperId, recipient, plan.LotId, quantity, "project_request_fulfilled",
            plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(helperId, plan.HouseId)));
        IncreaseTrust(plan.RequesterId, helperId, 2, "material_help");
        // Preserve short keys and the cooperation prefix, while keeping native
        // descendants' complete identities within the memory recall boundary.
        var memoryId = ProjectGratitudeMemoryId(plan.RequesterId, helperId);
        if (!society.Checkpoint.Memories.Any(memory => memory.Id == memoryId))
        {
            society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint, new SocietySocialMemory(
                Identity.Parse(memoryId), plan.RequesterId, helperId,
                $"Grateful for {society.Checkpoint.GetAgent(helperId).Name}'s help with project materials.", "public", WorldTick)
            { Kind = SocietyMemoryKind.Relationship }));
        }
        AppendEvent(new ProjectRequestFulfilledEventPayload(helperId, plan.RequesterId, itemKind, quantity));
    }

    private int AvailableLotQuantity(InventoryLot lot) => AvailableLotQuantity(society.Checkpoint.Inventory, lot);

    private static int AvailableLotQuantity(InventoryCheckpoint inventory, InventoryLot lot) =>
        InventoryRules.AvailableQuantity(InventoryIndex.For(inventory), lot);

    private void SetProject(string agentId, AgentProject project)
    {
        var state = agents[agentId];
        if (state.Project is { RequiresFreshChoice: true } && !project.RequiresFreshChoice)
        {
            productionSystem.ForgetRecipePlan(agentId);
            constructionProjectPlans.Remove(agentId);
        }
        if (state.Project?.Stage != project.Stage || state.Project?.Blocker != project.Blocker)
        {
            project = project with { LastTransitionTick = WorldTick };
            AppendEvent(new ProjectProgressEventPayload(agentId, project.Stage, project.Blocker ?? project.Label));
        }
        agents[agentId] = state with { Project = project };
    }
}
