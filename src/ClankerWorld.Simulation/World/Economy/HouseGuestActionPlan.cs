namespace ClankerWorld.Simulation.World;

public sealed record HouseGuestChoice(string GuestId, bool Invited)
{
    public const string InvitePrefix = "invite_house_guest:";
    public const string RevokePrefix = "revoke_house_guest:";
    public string Encode() => (Invited ? InvitePrefix : RevokePrefix) + GuestId;
    public static HouseGuestChoice? Decode(string candidate) => candidate.StartsWith(InvitePrefix, StringComparison.Ordinal)
        ? new(candidate[InvitePrefix.Length..], true) : candidate.StartsWith(RevokePrefix, StringComparison.Ordinal)
            ? new(candidate[RevokePrefix.Length..], false) : null;
}

public sealed record HouseGuestActionPlan(string ActorId, HouseGuestChoice Choice, PlacedBuilding House,
    string? ActorHouseholdId, string GuestName, bool OfferedChoice)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Invited ? $"invite {GuestName} to shelter in the House" : $"end {GuestName}'s invitation to the House";
}
