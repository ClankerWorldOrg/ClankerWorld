using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FarmingSystem
{
    internal static void ValidateFarmFields(FarmFieldState[] fields, WorldMap map, string seed,
        SocietyCheckpoint society, WorldContentSimulationState simulation, DeclarativeWorldContentState content,
        IReadOnlyList<GridPoint> roads)
    {
        ValidateLocalFields(fields);
        if (fields.Length == 0) return;
        var fertility = new LandFertility(map, seed);
        var ground = new GroundOccupancy(map, content, simulation, roads: roads);
        var occupied = GroundOccupancy.Combine(ground.Resources, ground.MapObjects,
            ground.Roads, ground.BuildingFootprints, ground.ExpansionSites);
        foreach (var field in fields)
        {
            if (!fertility.CanFarm(field.Position) || occupied.Contains(field.Position) ||
                !society.Households.Any(household => household.Id == field.HouseholdId) || field.LastWorkedTick > society.WorldTick ||
                field.Stage is not (FarmFieldStage.Preparing or FarmFieldStage.Prepared) && field.PlantedTick > society.WorldTick ||
                field.Stage is FarmFieldStage.Ready or FarmFieldStage.Harvested && field.ReadyTick > society.WorldTick)
                throw new InvalidDataException("A field has invalid land, ownership or crop state.");
            if (field.Work is { } work)
            {
                var worker = society.Agents.SingleOrDefault(person => person.Id == work.WorkerId);
                var workToolsValid = worker is not null && (work.Kind is FarmWorkKind.Till or FarmWorkKind.Tend
                    ? ToolProgressionRules.PlanWorkForLot(society.Inventory, work.WorkerId, ToolFamily.Hoe, work.HoeLotId) is not null
                    : work.Kind != FarmWorkKind.Harvest || work.SickleLotId is null ||
                        ToolProgressionRules.PlanWorkForLot(society.Inventory, work.WorkerId, ToolFamily.Sickle, work.SickleLotId) is not null);
                if (worker is null || worker.Status != SocietyAgentStatus.Active || !LifeStageRules.IsAdult(worker.AgeBand) ||
                    worker.HouseholdId != field.HouseholdId || work.LastWorkedTick > society.WorldTick || !workToolsValid)
                    throw new InvalidDataException("A field has invalid work in progress.");
                if (work.Kind == FarmWorkKind.Plant)
                {
                    var reservation = society.Inventory.Reservations.SingleOrDefault(item => item.Id == work.SeedReservationId);
                    var lot = society.Inventory.Lots.SingleOrDefault(item => item.Id == reservation?.LotId);
                    if (reservation is not { State: InventoryReservationState.Reserved, Quantity: 1, Purpose: "field_planting" } ||
                        reservation.OwnerId != work.WorkerId || reservation.ExpiryTick < society.WorldTick ||
                        lot is null || lot.OwnerId != work.WorkerId || lot.ItemKind != FarmFieldRules.PlantingItem(work.Crop!) ||
                        lot.GroundPosition is not null || lot.StorageBuildingId is not null || lot.DeliveryBuildingId is not null)
                        throw new InvalidDataException("Field planting needs its worker's actual reserved seed.");
                }
            }
            if (field.ReplantingReservationId is { } replantId)
            {
                var reservation = society.Inventory.Reservations.SingleOrDefault(item => item.Id == replantId);
                var lot = society.Inventory.Lots.SingleOrDefault(item => item.Id == reservation?.LotId);
                if (reservation is not { State: InventoryReservationState.Reserved, Quantity: 1, Purpose: "field_replanting" } ||
                    reservation.OwnerId != field.HouseholdId || lot is null || lot.OwnerId != field.HouseholdId ||
                    lot.ItemKind != FarmFieldRules.PlantingItem(field.Crop!))
                    throw new InvalidDataException("A field needs its own usable replanting reserve.");
            }
        }
    }
}
