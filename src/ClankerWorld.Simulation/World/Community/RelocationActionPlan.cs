namespace ClankerWorld.Simulation.World;

public sealed record RelocationChoice
{
    public const string Id = "household_volunteer";
    public static string Encode() => Id;
    public static RelocationChoice? Decode(string value) => value == Id ? new() : null;
}

public sealed record PlannedRelocationNotice(string ActorId, RelocationNotice Notice);

public sealed record RelocationActionPlan(string ActorId, RelocationChoice Choice, string HouseholdId, PlacedBuilding House,
    IReadOnlyList<HouseRelocationRules.SelectedAdult> Selected, IReadOnlyList<PlannedRelocationNotice> Replaced,
    long? InheritedDeadline) : ActionPlan(ActorId, RelocationChoice.Encode())
{
    public override string Activity => "volunteer to move out of that House";
}
