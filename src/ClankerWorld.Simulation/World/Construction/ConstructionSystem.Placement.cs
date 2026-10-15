using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal sealed partial class ConstructionSystem
{
    internal BuildingPlacementResult PlaceBuilding(
        IConstructionPlacementWorld world,
        string instanceId,
        string definitionId,
        GridPoint position,
        string eventKind,
        string? assignedTownId = null,
        string? householdId = null,
        string? constructionOwnerId = null,
        string? constructionInstructionId = null,
        IReadOnlyList<ConstructionMaterialInput>? selectedMaterials = null)
    {
        try
        {
            var normalizedInstanceId = world.NormalizeRequiredText(instanceId, nameof(instanceId));
            var normalizedDefinitionId = world.NormalizeRequiredText(definitionId, nameof(definitionId));
            ContentPackageRules.ValidateLocalId(normalizedInstanceId);
            var definition = world.Content.Buildings.SingleOrDefault(item => item.CanonicalId == normalizedDefinitionId);
            if (definition is null)
            {
                return BuildingPlacementResult.Rejected(
                    normalizedInstanceId,
                    normalizedDefinitionId,
                    position,
                    $"Building definition '{normalizedDefinitionId}' is not active.");
            }

            if (PortNavigationRules.IsPort(definition))
                return BuildingPlacementResult.Rejected(normalizedInstanceId, normalizedDefinitionId, position,
                    "A Port needs a Council-approved project, delivered Town materials and completed building work.");

            if (definition.Tags.Contains(TownHallContent.HallTag, StringComparer.Ordinal) ||
                StreetLanternContent.IsLantern(definition.CanonicalId))
                return BuildingPlacementResult.Rejected(normalizedInstanceId, normalizedDefinitionId, position,
                    StreetLanternContent.IsLantern(definition.CanonicalId)
                        ? $"{definition.DisplayName} needs a Council-approved project, delivered materials and completed building work."
                        : "The Town Hall needs a Council-approved project, delivered materials and completed building work.");

            if (definition.Tags.Any(tag => tag is MarketContent.HallTag or MarketContent.StallTag))
                return BuildingPlacementResult.Rejected(normalizedInstanceId, normalizedDefinitionId, position,
                    "A Market or stall needs a Council-approved project, delivered Town materials and completed building work.");

            if (State.Buildings.Any(item => item.InstanceId == normalizedInstanceId))
            {
                return BuildingPlacementResult.Rejected(
                    normalizedInstanceId,
                    normalizedDefinitionId,
                    position,
                    $"Building instance '{normalizedInstanceId}' already exists.");
            }

            if (world.BuildingIdentityIsReserved(normalizedInstanceId, constructionInstructionId))
                return BuildingPlacementResult.Rejected(normalizedInstanceId, normalizedDefinitionId, position,
                    $"Building instance '{normalizedInstanceId}' is reserved for building orders.");

            if (assignedTownId is not null && !world.Towns.Any(item => item.Id == assignedTownId))
                return BuildingPlacementResult.Rejected(normalizedInstanceId, normalizedDefinitionId, position,
                    "The assigned Town does not exist.");

            var isHouse = definition.Tags.Contains("house", StringComparer.Ordinal);
            var acceptsHouseholdOwner = world.IsHouseholdBuilding(definition);
            if (isHouse && householdId is null || householdId is not null && !acceptsHouseholdOwner ||
                householdId is not null && !world.Society.Households.Any(item => item.Id == householdId))
                return BuildingPlacementResult.Rejected(normalizedInstanceId, normalizedDefinitionId, position,
                    "A House requires an existing household; only household work buildings may take household ownership.");

            if (definition.Tags.Contains("warehouse", StringComparer.Ordinal) &&
                (assignedTownId is null || State.Buildings.Any(building =>
                    building.TownId == assignedTownId && world.Content.Buildings.Any(existing =>
                        existing.CanonicalId == building.DefinitionId &&
                        existing.Tags.Contains("warehouse", StringComparer.Ordinal)))))
                return BuildingPlacementResult.Rejected(normalizedInstanceId, normalizedDefinitionId, position,
                    "A Warehouse must join a Town that does not already have one.");

            if (!world.CanPlaceBuilding(definition, position, out var placementFailure))
            {
                return BuildingPlacementResult.Rejected(
                    normalizedInstanceId,
                    normalizedDefinitionId,
                    position,
                    placementFailure);
            }

            var materialOwner = constructionOwnerId ?? householdId ?? WorldRuntime.CivicSharedRules.HouseholdId;
            selectedMaterials ??= PlanConstructionInputs(world, definition.BuildCosts, materialOwner);
            if (selectedMaterials is null)
            {
                var missing = definition.BuildCosts.First(cost => world.ConstructionGoods(world.Society.Inventory,
                    new(GoodsUse.Holdings, null, GoodsOwners.One(materialOwner), GoodsKinds.One(cost.ResourceId))).Sum(match => (long)match.Quantity) < cost.Amount);
                var available = world.ConstructionGoods(world.Society.Inventory,
                    new(GoodsUse.Holdings, null, GoodsOwners.One(materialOwner), GoodsKinds.One(missing.ResourceId))).Sum(match => (long)match.Quantity);
                return BuildingPlacementResult.Rejected(normalizedInstanceId, normalizedDefinitionId, position,
                    $"Insufficient '{missing.ResourceId}' for building:{normalizedInstanceId}; missing {(missing.Amount - available).ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
            }
            if (CheckConstructionInputs(world, materialOwner, selectedMaterials).Refusal is { } materialRefusal)
                return BuildingPlacementResult.Rejected(normalizedInstanceId, normalizedDefinitionId, position, materialRefusal.Explanation);
            world.ConsumeConstructionInputs(selectedMaterials, $"building:{normalizedInstanceId}", materialOwner);
            var placed = new PlacedBuilding(
                normalizedInstanceId,
                definition.CanonicalId,
                position,
                world.WorldTick,
                assignedTownId,
                householdId);
            Buildings.PlaceBuildings([placed]);
            if (assignedTownId is not null)
                world.AssignBuildingToTown(placed, definition);
            world.AppendEvent(eventKind, $"{placed.InstanceId}:{placed.DefinitionId}:{position.X},{position.Y}" +
                (householdId is null ? string.Empty : $":household={householdId}"));
            return BuildingPlacementResult.Success(placed);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return BuildingPlacementResult.Rejected(
                instanceId?.Trim() ?? string.Empty,
                definitionId?.Trim() ?? string.Empty,
                position,
                exception.Message);
        }
    }

}
