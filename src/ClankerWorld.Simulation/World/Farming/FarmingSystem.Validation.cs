namespace ClankerWorld.Simulation.World;

internal sealed partial class FarmingSystem
{
    private void Commit(FarmingState next)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalFields(next.Fields);
        State = next;
    }

    internal static void ValidateLocalFields(IReadOnlyList<FarmFieldState> fields)
    {
        if (fields.Any(field => field is null))
            throw new InvalidDataException("A saved field record is missing.");
        if (fields.Select(field => field.Position).Distinct().Count() != fields.Count ||
            !fields.SequenceEqual(fields.OrderBy(field => field.Position.Y).ThenBy(field => field.Position.X)) ||
            fields.Where(field => field.Work is not null).GroupBy(field => field.Work!.WorkerId).Any(group => group.Count() > 1))
            throw new InvalidDataException("Fields must have distinct ordered tiles and one work site per worker.");
        foreach (var field in fields)
        {
            if (!Enum.IsDefined(field.Stage) || field.Cycle < 0 || field.LastWorkedTick < 0 ||
                field.Stage is FarmFieldStage.Preparing or FarmFieldStage.Prepared && field.Crop is not null ||
                field.Stage is not (FarmFieldStage.Preparing or FarmFieldStage.Prepared) &&
                    (!FarmFieldRules.IsCrop(field.Crop) || field.PlantedTick < 0 || field.ReadyTick <= field.PlantedTick) ||
                field.Stage is FarmFieldStage.Ready or FarmFieldStage.Harvested && !field.Tended ||
                field.Stage == FarmFieldStage.Preparing && field.Work?.Kind != FarmWorkKind.Till)
                throw new InvalidDataException("A field has invalid land, ownership or crop state.");
            if (field.Work is { } work && (work.OrderInstructionId is { } orderId &&
                !IdentityRules.Reference(orderId, DerivedId.MaximumLength) ||
                !Enum.IsDefined(work.Kind) || work.LastWorkedTick < 0 || work.LastWorkedTick != field.LastWorkedTick ||
                work.RemainingTicks < 1 || work.RemainingTicks > FarmFieldRules.WorkTicks(work.Kind) ||
                work.Kind == FarmWorkKind.Till && field.Stage != FarmFieldStage.Preparing ||
                work.Kind == FarmWorkKind.Tend && (field.Stage != FarmFieldStage.Growing || field.Tended) ||
                work.Kind == FarmWorkKind.Harvest && field.Stage != FarmFieldStage.Ready ||
                work.Kind == FarmWorkKind.Plant && (field.Stage is not (FarmFieldStage.Prepared or FarmFieldStage.Harvested) || !FarmFieldRules.IsCrop(work.Crop)) ||
                work.Kind != FarmWorkKind.Plant && (work.Crop is not null || work.SeedReservationId is not null) ||
                work.Kind is not (FarmWorkKind.Till or FarmWorkKind.Tend) && work.HoeLotId is not null ||
                work.Kind != FarmWorkKind.Harvest && work.SickleLotId is not null))
                throw new InvalidDataException("A field has invalid work in progress.");
            if (field.ReplantingReservationId is not null && field.Crop is null)
                throw new InvalidDataException("A field needs its own usable replanting reserve.");
        }
    }
}
