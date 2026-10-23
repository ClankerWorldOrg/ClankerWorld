using System.Text.Json;

namespace ClankerWorld.Simulation.World;

public abstract record OrnamentChoice
{
    public const string WearPrefix = "wear_ornament:";
    public const string GiftPrefix = "gift_ornament:";
    public const string RemoveId = "remove_ornament";
    public abstract string Encode();
    internal static string Identity(string prefix, params string[] ids) => DerivedId.Hash(JsonSerializer.Serialize(ids), prefix);
}

public sealed record WearOrnamentChoice(string StockId) : OrnamentChoice
{
    public override string Encode() => Identity(WearPrefix, StockId);
}

public sealed record RemoveOrnamentChoice : OrnamentChoice
{
    public override string Encode() => RemoveId;
}

public sealed record GiftOrnamentChoice(string StockId, string RecipientId) : OrnamentChoice
{
    public override string Encode() => Identity(GiftPrefix, StockId, RecipientId);
}

public abstract record OrnamentActionPlan(string ActorId, OrnamentChoice Choice) : ActionPlan(ActorId, Choice.Encode());

public sealed record WearOrnamentActionPlan(string ActorId, WearOrnamentChoice Selected,
    string OwnerId, string ItemKind, string? PreviousSlotId, GridPoint Position, int InteractionRange,
    IReadOnlyList<GridPoint> Route) : OrnamentActionPlan(ActorId, Selected)
{
    public override string Activity => "wear the selected ornament";
}

public sealed record RemoveOrnamentActionPlan(string ActorId, string StockId)
    : OrnamentActionPlan(ActorId, new RemoveOrnamentChoice())
{
    public override string Activity => "remove the selected ornament";
}

public sealed record GiftOrnamentActionPlan(string ActorId, GiftOrnamentChoice Selected, string ItemKind,
    string? PreviousSlotId) : OrnamentActionPlan(ActorId, Selected)
{
    public override string Activity => "give the selected ornament";
}

public sealed record OrnamentRequestChoice(string CandidateId)
{
    public string Encode() => CandidateId;
    public static OrnamentRequestChoice? Decode(string candidate) => candidate == OrnamentChoice.RemoveId ||
        candidate.StartsWith(OrnamentChoice.WearPrefix, StringComparison.Ordinal) || candidate.StartsWith(OrnamentChoice.GiftPrefix, StringComparison.Ordinal)
        ? new(candidate) : null;
}
