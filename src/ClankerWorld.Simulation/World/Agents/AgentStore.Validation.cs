using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AgentStore
{
    internal static void ValidateLocalEquipment(PersonalEquipment equipment)
    {
        static bool InvalidId(string? id) => id is not null && (string.IsNullOrWhiteSpace(id) || id.Any(char.IsControl));
        if (InvalidId(equipment.ClothingLotId) || InvalidId(equipment.CarryAidLotId) || InvalidId(equipment.OrnamentLotId) ||
            equipment.OrnamentLotId is not null && (equipment.OrnamentLotId == equipment.ClothingLotId || equipment.OrnamentLotId == equipment.CarryAidLotId) ||
            equipment.ClothingLotId is not null && equipment.ClothingLotId == equipment.CarryAidLotId ||
            equipment.Repair is { } repair && (InvalidId(repair.LotId) || InvalidId(repair.BuildingId) || InvalidId(repair.OrderInstructionId) ||
                repair.OrderInstructionId is { Length: > DerivedId.MaximumLength } || repair.LotId is null || repair.BuildingId is null ||
                repair.StartedTick < 0 || repair.WorkDone < 0 || repair.WorkDone >= PersonalEquipmentRules.RepairWorkTicks ||
                repair.MaterialReservationIds is null || repair.MaterialReservationIds.Count is < 1 or > 2 ||
                repair.MaterialReservationIds.Any(InvalidId) || repair.MaterialReservationIds.Any(id => id is null) ||
                repair.MaterialReservationIds.Distinct(StringComparer.Ordinal).Count() != repair.MaterialReservationIds.Count))
            throw new InvalidDataException("The saved personal equipment is invalid.");
    }

    internal static void ValidateLocalSurvival(SurvivalCondition condition)
    {
        if (condition.WarmthBasisPoints is < 0 or > 10_000 || condition.IllnessBasisPoints is < 0 or > 10_000 ||
            condition.NutritionBasisPoints is < 0 or > 10_000 ||
            condition.LastMealKind is not (null or "crops" or "cooked" or "foraged" or "rations" or "orchard") &&
                !FoodRules.PreparedKinds.Contains(condition.LastMealKind, StringComparer.Ordinal))
            throw new InvalidDataException("The saved agent survival condition is invalid.");
    }
}
