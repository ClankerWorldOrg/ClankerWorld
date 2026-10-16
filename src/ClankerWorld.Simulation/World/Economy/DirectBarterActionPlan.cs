using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public enum DirectBarterKind { Propose, Wait, Accept, Decline }

public sealed record DirectBarterChoice(DirectBarterKind Kind, string Subject)
{
    public string Encode() => IdentityRules.PrefixedReference("trade_" + Kind.ToString().ToLowerInvariant() + ":", Subject);
    public static DirectBarterChoice? Decode(string value)
    {
        foreach (var kind in Enum.GetValues<DirectBarterKind>())
        {
            var prefix = "trade_" + kind.ToString().ToLowerInvariant() + ":";
            if (value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length) return new(kind, value[prefix.Length..]);
        }
        return null;
    }
}

public sealed record DirectBarterActionPlan(string ActorId, DirectBarterChoice Choice, string OtherId,
    string GiveId, string TakeId, string GiveKind, string TakeKind, GridPoint Destination,
    DirectBarterOffer? Offer, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "respond to that selected personal barter";
}
