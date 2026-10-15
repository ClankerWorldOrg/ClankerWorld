namespace ClankerWorld.Simulation.World;

public enum TownAdmissionChoiceKind { Request, Sponsor, Accept }

public sealed record TownAdmissionChoice(string TownId, TownAdmissionChoiceKind Kind, string SubjectToken)
{
    public static string Verb(TownAdmissionChoiceKind kind) => kind switch
    { TownAdmissionChoiceKind.Request => "admission", TownAdmissionChoiceKind.Sponsor => "request_admission", _ => "accept_admission" };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{SubjectToken}|";
    public static TownAdmissionChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var subject, ""] ||
            !IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true)) return null;
        TownAdmissionChoiceKind? kind = verb switch
        { "admission" => TownAdmissionChoiceKind.Request, "request_admission" => TownAdmissionChoiceKind.Sponsor, "accept_admission" => TownAdmissionChoiceKind.Accept, _ => null };
        return kind is { } selected ? new(town, selected, subject) : null;
    }
}

public sealed record TownAdmissionActionPlan(string ActorId, TownAdmissionChoice Choice, string Subject,
    string? PreviousTown, IReadOnlyList<string> CareGroup, TownAdmissionRecord? Approval) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "give that Town admission response";
}
