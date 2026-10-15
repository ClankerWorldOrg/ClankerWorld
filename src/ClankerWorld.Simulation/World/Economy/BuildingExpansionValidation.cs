using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static void ValidateBuildingExpansionState(WorldContentSimulationState simulation,
        DeclarativeWorldContentState content, SocietyCheckpoint society, WorldMap map, int schemaVersion)
    {
        if (schemaVersion < 30 && (simulation.Buildings.Any(item => item.Footprint is not null) ||
                simulation.BuildingExpansions is { Count: > 0 } || simulation.GuestInvitations is { Count: > 0 }))
            throw new InvalidDataException("Building expansions and guest invitations require world schema 30.");
        BuildingStore.ValidateLocalState(WorldConstructionState.From(simulation));
        var buildings = simulation.Buildings.ToDictionary(item => item.InstanceId, StringComparer.Ordinal);
        var definitions = content.BuildingDefinitions;
        var people = society.Agents.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var allJobIds = simulation.ProductionJobs.Concat(simulation.CropBuilds ?? []).Select(item => item.JobId)
            .ToHashSet(StringComparer.Ordinal);
        var activeBuildings = new HashSet<string>(StringComparer.Ordinal);
        var activeExpansionTiles = new HashSet<GridPoint>();
        var expansionJobs = simulation.BuildingExpansions ?? [];
        foreach (var job in expansionJobs)
        {
            if (job.State == WorldProductionJobState.Paused && job.PausedAtTick > society.WorldTick)
                throw new InvalidDataException("The saved expansion pause differs from the world clock.");
            var hasBuilding = buildings.TryGetValue(job.BuildingInstanceId, out var building);
            var definitionId = job.DefinitionId ?? building?.DefinitionId;
            // Paused work keeps its building and footprint exactly as running work does.
            var active = job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused;
            if (!allJobIds.Add(job.JobId) || !people.ContainsKey(job.WorkerId) ||
                active && (!hasBuilding || job.OwnerId != (building!.HouseholdId ?? building.TownId)) ||
                !hasBuilding && (active || schemaVersion < 35 || string.IsNullOrWhiteSpace(job.DefinitionId)) ||
                active && hasBuilding &&
                    job.DefinitionId is not null && job.DefinitionId != building!.DefinitionId ||
                definitionId is null || !definitions.TryGetValue(definitionId, out var definition) ||
                !BuildingStorageRules.IsSupported(definition, job.TargetFootprint) ||
                job.StartedTick > society.WorldTick ||
                job.State == WorldProductionJobState.Running && job.CompletionTick <= society.WorldTick ||
                !map.Contains(job.TargetPosition))
                throw new InvalidDataException("The saved building expansion is malformed.");
            if (job.State is not (WorldProductionJobState.Running or WorldProductionJobState.Paused)) continue;
            if (!hasBuilding || !activeBuildings.Add(job.BuildingInstanceId) || building!.Position != job.ExpectedPosition ||
                (building.Footprint?.Revision ?? 0) != job.ExpectedRevision)
                throw new InvalidDataException("The saved expansion no longer refers to its original building footprint.");
            var target = BuildingStorageRules.WithSize(definition!, job.TargetFootprint.Width, job.TargetFootprint.Height);
            var targetTiles = WorldContentSimulationRules.Footprint(target, job.TargetPosition).ToArray();
            if (!WorldContentSimulationRules.Footprint(definition, building).All(targetTiles.Contains) ||
                targetTiles.Any(tile => !map.IsBuildable(tile)) ||
                targetTiles.Any(tile => !activeExpansionTiles.Add(tile)))
                throw new InvalidDataException("The saved expansion has an invalid or competing footprint.");
            var reserved = new Dictionary<string, int>(StringComparer.Ordinal);
            var buildingSite = new InventoryGroundPosition(building.Position.X, building.Position.Y);
            foreach (var id in job.InputReservationIds)
            {
                var reservation = society.Inventory.Reservations.SingleOrDefault(item => item.Id == id);
                var lot = reservation is null ? null : society.Inventory.Lots.SingleOrDefault(item => item.Id == reservation.LotId);
                if (reservation is null || lot is null || reservation.Purpose != job.JobId ||
                    reservation.OwnerId != lot.OwnerId || lot.OwnerId != job.OwnerId && lot.OwnerId != job.WorkerId ||
                    (lot.OwnerId == job.OwnerId
                        ? lot.StorageBuildingId != building.InstanceId && lot.GroundPosition != buildingSite ||
                            lot.DeliveryBuildingId is not null
                        : lot.StorageBuildingId is not null || lot.DeliveryBuildingId is not null ||
                            lot.GroundPosition is not null) ||
                    reservation.State != InventoryReservationState.Reserved || reservation.ExpiryTick != (job.State == WorldProductionJobState.Paused ? long.MaxValue : job.CompletionTick))
                    throw new InvalidDataException("The expansion is missing its reserved materials.");
                reserved[lot.ItemKind] = reserved.GetValueOrDefault(lot.ItemKind) + reservation.Quantity;
            }
            var expected = BuildingStorageRules.ExpansionCosts(definition, building, job.TargetFootprint);
            if (expected.Count != reserved.Count || expected.Any(cost => reserved.GetValueOrDefault(cost.ResourceId) != cost.Amount))
                throw new InvalidDataException("The expansion's reserved materials do not match its footprint.");
        }
        var invitations = simulation.GuestInvitations ?? [];
        foreach (var invitation in invitations)
        {
            if (!buildings.TryGetValue(invitation.HouseInstanceId, out var house) || house.HouseholdId is null ||
                !definitions[house.DefinitionId].Tags.Contains("house", StringComparer.Ordinal) ||
                !people.ContainsKey(invitation.GuestId) || !people.ContainsKey(invitation.InvitedById) ||
                invitation.ChangedTick > society.WorldTick)
                throw new InvalidDataException("The saved House guest invitation is malformed.");
        }

    }
}
