namespace ClankerWorld.Simulation.World;

public enum HouseholdLandResponseKind { Accept, Decline, Withdraw }

public sealed record HouseholdLandResponseChoice(string TownId, HouseholdLandResponseKind Kind, string RequestId)
{
    public static string Verb(HouseholdLandResponseKind kind) => kind switch
    { HouseholdLandResponseKind.Accept => "accept_land_use", HouseholdLandResponseKind.Decline => "decline_land_use", _ => "withdraw_land_use" };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{RequestId}|";
    public static HouseholdLandResponseChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var request, ""] ||
            !IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) ||
            !IdentityRules.Reference(request, DerivedId.MaximumLength, requireTrimmed: true)) return null;
        HouseholdLandResponseKind? kind = verb switch
        {
            "accept_land_use" => HouseholdLandResponseKind.Accept,
            "decline_land_use" => HouseholdLandResponseKind.Decline,
            "withdraw_land_use" => HouseholdLandResponseKind.Withdraw,
            _ => null
        };
        return kind is { } selected ? new(town, selected, request) : null;
    }
}

public sealed record HouseholdLandResponseActionPlan(string ActorId, HouseholdLandResponseChoice Choice,
    HouseholdLandUseRequest Request) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "respond to that selected household land request";
}
