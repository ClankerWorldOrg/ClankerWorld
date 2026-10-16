using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public enum PartnershipChoiceKind { Propose, Accept, Refuse, Leave, Choose, Postpone }

public sealed record PartnershipChoice(PartnershipChoiceKind Kind, string TargetId)
{
    private static string Prefix(PartnershipChoiceKind kind) => kind switch
    {
        PartnershipChoiceKind.Propose => "partner_propose:",
        PartnershipChoiceKind.Accept => "partner_accept:",
        PartnershipChoiceKind.Refuse => "partner_refuse:",
        PartnershipChoiceKind.Leave => "partner_leave:",
        PartnershipChoiceKind.Choose => "partner_choose:",
        PartnershipChoiceKind.Postpone => "partner_postpone:",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public string Encode() => Prefix(Kind) + TargetId;
    public static PartnershipChoice? Decode(string value)
    {
        foreach (var kind in Enum.GetValues<PartnershipChoiceKind>())
        {
            var prefix = Prefix(kind);
            if (value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length)
                return new(kind, value[prefix.Length..]);
        }
        return null;
    }
}

public sealed record PartnershipActionPlan(string ActorId, PartnershipChoice Choice, string PartnerId,
    SocietyRelationship? Original, ContinuitySingle? OriginalSingle = null) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        PartnershipChoiceKind.Propose => "propose that partnership",
        PartnershipChoiceKind.Accept => "accept that partnership proposal",
        PartnershipChoiceKind.Refuse => "refuse that partnership proposal",
        PartnershipChoiceKind.Choose => "choose that partner under the continuity rule",
        PartnershipChoiceKind.Postpone => "put off choosing a partner",
        _ => "leave that partnership",
    };
}
