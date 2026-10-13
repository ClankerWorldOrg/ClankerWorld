using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public enum ParenthoodChoiceKind { Propose, Accept, Decline, Cancel, Postpone }

public sealed record ParenthoodChoice(ParenthoodChoiceKind Kind, string OwnerId, string? Selector = null, string? HouseholdId = null)
{
    private static string Prefix(ParenthoodChoiceKind kind) => kind switch
    {
        ParenthoodChoiceKind.Propose => "parent_propose:",
        ParenthoodChoiceKind.Accept => "parent_accept:",
        ParenthoodChoiceKind.Decline => "parent_decline:",
        ParenthoodChoiceKind.Cancel => "parent_cancel:",
        ParenthoodChoiceKind.Postpone => "parent_postpone:",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public string Encode() => Prefix(Kind) + OwnerId + (Kind == ParenthoodChoiceKind.Accept ? ":" + Selector + ":" + Uri.EscapeDataString(HouseholdId!) : string.Empty);
    public static ParenthoodChoice? Decode(string value)
    {
        foreach (var kind in Enum.GetValues<ParenthoodChoiceKind>())
        {
            var prefix = Prefix(kind);
            if (!value.StartsWith(prefix, StringComparison.Ordinal) || value.Length <= prefix.Length) continue;
            if (kind != ParenthoodChoiceKind.Accept) return new(kind, value[prefix.Length..]);
            var home = value.LastIndexOf(':');
            var selector = home > 0 ? value.LastIndexOf(':', home - 1) : -1;
            if (selector <= prefix.Length || home <= selector + 1 || home == value.Length - 1) return null;
            var selected = value[(selector + 1)..home];
            var household = Uri.UnescapeDataString(value[(home + 1)..]);
            return selected is "initiator" or "acceptor" && !string.IsNullOrWhiteSpace(household)
                ? new(kind, value[prefix.Length..selector], selected, household) : null;
        }
        return null;
    }
}

public sealed record ParenthoodActionPlan(string ActorId, ParenthoodChoice Choice, string OwnerId, string PartnerId,
    ParenthoodPlan? Original, SocietyRelationship? Partnership, ContinuityCouple? Continuity, string? CaregiverId,
    string? IntendedHouseholdId) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        ParenthoodChoiceKind.Propose => "ask that partner about parenthood",
        ParenthoodChoiceKind.Accept => "accept the chosen caregiver and home",
        ParenthoodChoiceKind.Decline => "decline that parenthood request",
        ParenthoodChoiceKind.Cancel => "withdraw from that parenthood plan",
        _ => "postpone that parenthood plan",
    };
}
