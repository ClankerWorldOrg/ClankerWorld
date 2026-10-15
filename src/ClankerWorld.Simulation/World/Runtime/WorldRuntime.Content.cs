using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public static ContentResolutionResult PreviewContent(
        IEnumerable<ContentPackageManifest> availablePackages,
        IEnumerable<string> rootPackageIds) =>
        ContentPackageResolver.Resolve(availablePackages, rootPackageIds);

    public static ContentPreviewResult PreviewWorldContent(
        IEnumerable<ContentPackageManifest> availablePackages,
        IEnumerable<string> rootPackageIds,
        DeclarativeWorldContentState? baseWorldContent = null) =>
        ContentPackagePreview.Run(availablePackages, rootPackageIds, baseWorldContent);

    public ContentResolutionResult ResolveContent(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        gate.Wait();
        try
        {
            var available = contentRegistry.ExportState().Packages
                .Select(package => package.Manifest)
                .ToArray();
            return ContentPackageResolver.Resolve(available, [packageId]);
        }
        finally
        {
            gate.Release();
        }
    }

    public ContentPackageRecord ProposeContent(ContentPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        gate.Wait();
        try
        {
            var record = contentRegistry.Propose(manifest, WorldTick);
            AppendEvent("content_proposed", manifest.PackageId);
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public bool StageStarterContent()
    {
        gate.Wait();
        try
        {
            if (society.Checkpoint.IsPaused || contentRegistry.ExportState().Packages
                .Any(package => package.Manifest.PackageId == StarterContent.PackageId))
            {
                // Never undo an owner's rollback or quarantine, or mutate a paused save.
                return false;
            }

            var manifest = StarterContent.Create();
            _ = ContentDefinitionPayloadCodec.ApplyPackage(worldContent, manifest);
            var resolution = ContentPackageResolver.Resolve([manifest], [manifest.PackageId]);
            contentRegistry.Propose(manifest, WorldTick);
            contentRegistry.Validate(manifest.PackageId, resolution, WorldTick);
            contentRegistry.Approve(manifest.PackageId, WorldTick);
            contentRegistry.Stage(manifest.PackageId, WorldTick);
            AppendEvent("starter_content_staged", manifest.PackageId);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Install trusted shipped content before the first paused Town layout is accepted.</summary>
    public void InitializeFirstTownContent()
    {
        gate.Wait();
        try
        {
            if (geographyOptions is null || founderSetup is not { Started: false } ||
                !society.Checkpoint.IsPaused || WorldTick != 0 ||
                contentRegistry.ExportState().Packages.Count != 0)
                throw new InvalidOperationException("Initial content is available only to a fresh paused generated world.");
            ContentPackageManifest[] manifests =
            [
                StarterContent.Create(), HearthContent.Create(), HouseContent.Create(), HouseToolsContent.Create(),
                WarehouseContent.Create(), FarmContent.Create(), BlacksmithContent.Create(),
                HouseCookingContent.Create(), PotteryContent.Create(), SiloContent.Create(), TailorContent.Create(),
                RestaurantContent.Create(), StoreContent.Create(), CareContent.Create(), OrnamentContent.Create(), TownHallContent.Create(),
                KnowledgeContent.Create(), MarketContent.Create(), StreetLanternContent.Create(), PortContent.Create(),
                FarmhouseVariantContent.Create(), BlacksmithVariantContent.Create(), TailorVariantContent.Create(),
                ClinicVariantContent.Create(), RestaurantVariantContent.Create(), AnimalContent.Create(),
            ];
            foreach (var manifest in manifests)
            {
                var packages = contentRegistry.ExportState().Packages;
                var resolution = ContentPackageResolver.Resolve(
                    packages.Select(package => package.Manifest).Append(manifest), [manifest.PackageId]);
                var definitions = ContentDefinitionPayloadCodec.ApplyPackage(worldContent, manifest);
                contentRegistry.Propose(manifest, 0);
                contentRegistry.Validate(manifest.PackageId, resolution, 0);
                contentRegistry.Approve(manifest.PackageId, 0);
                contentRegistry.Stage(manifest.PackageId, 0);
                var reservation = assetReservations.TryReservePackage(manifest.PackageId,
                    manifest.AssetReservations ?? [], 0);
                if (!reservation.IsSuccess)
                    throw new InvalidOperationException($"Initial content assets were rejected: {reservation.FailureCode}");
                contentRegistry.ActivateAtCreation(manifest.PackageId);
                worldContent = definitions;
                AppendEvent("initial_content_activated", manifest.PackageId);
            }
        }
        finally { gate.Release(); }
    }

    public ContentPackageRecord ValidateContent(
        string packageId,
        ContentResolutionResult resolution)
    {
        gate.Wait();
        try
        {
            var manifest = GetContentManifest(packageId);
            _ = ContentDefinitionPayloadCodec.ApplyPackage(worldContent, manifest);
            var record = contentRegistry.Validate(packageId, resolution, WorldTick);
            AppendEvent("content_validated", packageId);
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public ContentPackageRecord ApproveContent(string packageId)
    {
        gate.Wait();
        try
        {
            var record = contentRegistry.Approve(packageId, WorldTick);
            AppendEvent("content_approved", packageId);
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public ContentPackageRecord StageContent(string packageId)
    {
        gate.Wait();
        try
        {
            var manifest = GetContentManifest(packageId);
            _ = ContentDefinitionPayloadCodec.ApplyPackage(worldContent, manifest);
            var record = contentRegistry.Stage(packageId, WorldTick);
            AppendEvent("content_staged", packageId);
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public BuildingPlacementResult PlaceBuilding(
        string instanceId,
        string definitionId,
        GridPoint position,
        string? householdId = null)
    {
        gate.Wait();
        try
        {
            var definition = worldContent.Buildings.SingleOrDefault(item => item.CanonicalId == definitionId);
            var assignedTown = definition is null ? null : TownForOwnerPlacement(position, definition);
            return PlaceBuildingCore(instanceId, definitionId, position, "building_placed", assignedTown?.Id, householdId);
        }
        finally
        {
            gate.Release();
        }
    }

    private BuildingPlacementResult PlaceBuildingCore(string instanceId, string definitionId, GridPoint position, string eventKind,
        string? assignedTownId = null, string? householdId = null, string? constructionOwnerId = null,
        string? constructionInstructionId = null, IReadOnlyList<ConstructionMaterialInput>? selectedMaterials = null) =>
        constructionSystem.PlaceBuilding(ConstructionPlacementWorld, instanceId, definitionId, position, eventKind,
            assignedTownId, householdId, constructionOwnerId, constructionInstructionId, selectedMaterials);

    public ProductionStartResult StartProduction(
        string recipeId,
        string buildingInstanceId,
        string workerId)
    {
        gate.Wait();
        try
        {
            return StartProductionCore(recipeId, buildingInstanceId, workerId, "recipe_started");
        }
        finally
        {
            gate.Release();
        }
    }

    private ProductionStartResult StartProductionCore(string recipeId, string buildingInstanceId, string workerId,
        string eventKind, string? orderInstructionId = null, RecipeProjectActionPlan? originalPlan = null) =>
        productionSystem.StartProduction(ProductionStartWorld, ProjectGoalCapabilities, recipeId, buildingInstanceId,
            workerId, eventKind, orderInstructionId, originalPlan);

    public ContentPackageRecord RollbackContent(string packageId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        gate.Wait();
        try
        {
            var manifest = GetContentManifest(packageId);
            if (towns.SelectMany(town => town.Projects).Any(project => project.Plan.DefinitionId
                    .StartsWith(manifest.PackageDigest + "/", StringComparison.Ordinal)) ||
                towns.SelectMany(town => town.Council?.Proposals ?? []).Any(proposal =>
                    proposal.Status is "pending" or "passed" && proposal.Project is { } plan &&
                    plan.DefinitionId.StartsWith(manifest.PackageDigest + "/", StringComparison.Ordinal)))
                throw new InvalidOperationException("This content is referenced by retained Town construction approvals and material receipts.");
            var packageRecipes = manifest.Definitions.Where(definition => definition.Kind == RecipeDefinition.SchemaKind)
                .Select(definition => definition.CanonicalId(manifest.PackageDigest)).ToHashSet(StringComparer.Ordinal);
            if (toolMakingRequests.Any(request => !ToolMakingRequestRules.IsTerminal(request.Status) &&
                    packageRecipes.Contains(request.RecipeId)))
                throw new InvalidOperationException("Content referenced by active tool requests requires an explicit migration before removal.");
            WorldContentSimulationRules.RemovePackage(worldSimulation, manifest.PackageDigest);
            if (agents.Values.Any(person => person.Project is { } project &&
                (project.CandidateId.StartsWith($"build:building:{manifest.PackageDigest}/", StringComparison.Ordinal) ||
                 project.CandidateId.StartsWith($"build:recipe:{manifest.PackageDigest}/", StringComparison.Ordinal))))
            {
                throw new InvalidOperationException("Content referenced by agent projects requires an explicit migration before removal.");
            }
            var remainingContent = ContentDefinitionApplicator.RemovePackage(worldContent, manifest.PackageDigest);
            var completed = completedInstructionIds.ToArray();
            var people = society.Checkpoint.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
            // Cancelled orders remain checkpoint history and still need their original content targets.
            if (instructionsByIdempotency.Values.Any(instruction =>
                instruction.Task is { Action: "produce_item" or "deliver_stock" or "construct_building" or "expand_building" } order &&
                !IsValidSavedOrder(order, instruction, completed, people, WorldTick, remainingContent)))
                throw new InvalidOperationException("Content referenced by owner orders requires an explicit migration before removal.");
            if (instructionsByIdempotency.Values.Any(instruction =>
                instruction.Task?.ShelterBinding?.DefinitionId is { } definitionId &&
                !remainingContent.Buildings.Any(definition => definition.CanonicalId == definitionId)))
                throw new InvalidOperationException("Content referenced by shelter orders requires an explicit migration before removal.");
            var record = contentRegistry.Rollback(packageId, WorldTick, reason);
            worldContent = remainingContent;
            if (survivalState is not null)
            {
                survivalState = survivalState with
                {
                    Fires = survivalState.Fires.Where(fire =>
                    worldSimulation.Buildings.Any(building => building.InstanceId == fire.BuildingId)).ToArray()
                };
            }
            assetReservations.ReleasePackage(packageId, WorldTick);
            AppendEvent("content_rolled_back", $"{packageId}:{reason.Trim()}");
            return record;
        }
        finally
        {
            gate.Release();
        }
    }

    public DeclarativeWorldContentState WorldContent => worldContent;

    private ContentPackageManifest GetContentManifest(string packageId)
    {
        ContentPackageRules.ValidatePackageId(packageId);
        return contentRegistry.ExportState().Packages
            .SingleOrDefault(package => package.Manifest.PackageId == packageId)?.Manifest
            ?? throw new KeyNotFoundException($"Package '{packageId}' has no lifecycle record.");
    }

    private static DeclarativeWorldContentState RebuildWorldContent(ContentRegistryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var result = new DeclarativeWorldContentState([], []);
        var active = state.Packages.Where(package => package.Lifecycle == ContentPackageLifecycle.Active)
            .ToDictionary(package => package.Manifest.PackageId, StringComparer.Ordinal);
        var resolution = ContentPackageResolver.Resolve(active.Values.Select(package => package.Manifest), active.Keys);
        if (!resolution.IsSuccess)
        {
            throw new InvalidDataException("Active world content has an invalid dependency graph.");
        }
        foreach (var entry in resolution.Lock)
        {
            result = ContentDefinitionPayloadCodec.ApplyPackage(result, active[entry.PackageId].Manifest);
        }

        return result;
    }

    private void ValidateAssetReservationsAgainstActivePackages()
    {
        var expected = new WorldAssetReservationLedger(assetReservations.Policy);
        foreach (var package in contentRegistry.ExportState().Packages
                     .Where(item => item.Lifecycle == ContentPackageLifecycle.Active)
                     .OrderBy(item => item.ActivationTick ?? long.MaxValue)
                     .ThenBy(item => item.Manifest.PackageId, StringComparer.Ordinal))
        {
            var result = expected.TryReservePackage(
                package.Manifest.PackageId,
                package.Manifest.AssetReservations ?? [],
                package.ActivationTick ?? WorldTick);
            if (!result.IsSuccess)
            {
                throw new InvalidDataException(
                    $"Active package '{package.Manifest.PackageId}' cannot be reconstructed in the world asset reservation ledger: {result.Diagnostic}");
            }
        }

        var expectedReservations = expected.ExportState().Reservations;
        var actualReservations = assetReservations.ExportState().Reservations;
        if (!expectedReservations.SequenceEqual(actualReservations))
        {
            throw new InvalidDataException("The world asset reservation ledger does not match active package reservations.");
        }
    }

}
