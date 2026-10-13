namespace ClankerWorld.Simulation.World;

public enum HandcartControlKind { Attach, Park, Give }

public sealed record HandcartControlChoice(HandcartControlKind Kind, string? TargetId = null)
{
    public const string AttachPrefix = "attach_handcart:";
    public const string GivePrefix = "give_handcart:";
    public string Encode() => Kind == HandcartControlKind.Park ? "park_handcart" :
        (Kind == HandcartControlKind.Attach ? AttachPrefix : GivePrefix) + TargetId;
    public static HandcartControlChoice? Decode(string value) => value == "park_handcart" ? new(HandcartControlKind.Park) :
        value.StartsWith(AttachPrefix, StringComparison.Ordinal) ? new(HandcartControlKind.Attach, value[AttachPrefix.Length..]) :
        value.StartsWith(GivePrefix, StringComparison.Ordinal) ? new(HandcartControlKind.Give, value[GivePrefix.Length..]) : null;
}

public sealed record HandcartControlActionPlan(string ActorId, HandcartControlChoice Choice, string CartId,
    GridPoint Position, IReadOnlyList<PlannedStockMember> Family, HandcartHitch? Hitch,
    GridPoint? RecipientPosition, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    { HandcartControlKind.Attach => "attach the handcart", HandcartControlKind.Park => "park the handcart", _ => "give the handcart" };
}
