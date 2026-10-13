using System.Globalization;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record FieldChoice(FarmWorkKind Kind, GridPoint Position, string? Crop = null)
{
    public string Encode() => $"farm:{Kind}:{Position.X}:{Position.Y}:{Crop ?? "-"}";
    public static FieldChoice? Decode(string candidate)
    {
        var parts = candidate.Split(':');
        return parts.Length == 5 && parts[0] == "farm" && Enum.TryParse<FarmWorkKind>(parts[1], out var kind) && Enum.IsDefined(kind) &&
            int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var x) &&
            int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var y)
            ? new(kind, new(x, y), parts[4] == "-" ? null : parts[4]) : null;
    }
}

public sealed record PlannedFieldTool(ToolWorkPlan Work, string OwnerId, string ItemKind);

public sealed record PlannedFieldSeed(string StockId, string OwnerId, string ItemKind,
    GridPoint Position, int InteractionRange, string? ReplantingReservationId);

public sealed record FieldActionPlan(string ActorId, FieldChoice Choice, string HouseholdId,
    string FarmhouseId, FarmFieldState? Field, PlannedFieldSeed? Seed,
    PlannedFieldTool? Hoe, PlannedFieldTool? Sickle, int WorkTicks,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        FarmWorkKind.Till => "prepare the selected field",
        FarmWorkKind.Plant => $"plant {Choice.Crop!.Replace('_', ' ')} in the field",
        FarmWorkKind.Tend => "tend the selected crop",
        _ => "harvest the selected crop",
    };
}
