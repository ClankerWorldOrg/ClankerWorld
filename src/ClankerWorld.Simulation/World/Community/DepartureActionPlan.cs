using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public enum DepartureChoiceKind { Leave, Found }

public sealed record DepartureChoice(DepartureChoiceKind Kind)
{
    public string Encode() => Kind == DepartureChoiceKind.Leave ? "household_leave" : "household_found";
    public static DepartureChoice? Decode(string value) => value switch
    {
        "household_leave" => new(DepartureChoiceKind.Leave),
        "household_found" => new(DepartureChoiceKind.Found),
        _ => null,
    };
}

public sealed record DepartureAllowance(InventoryLot Stock, int Quantity);

public sealed record DepartureActionPlan(string ActorId, DepartureChoice Choice, string? HouseholdId,
    string Cause, IReadOnlyList<string> CareGroup, IReadOnlyList<DepartureAllowance> Allowance)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind == DepartureChoiceKind.Leave ? "leave that household" : "found that household";
}
