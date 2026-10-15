namespace ClankerWorld.Simulation.World;

public sealed record MedicalTreatmentChoice(string PatientId, string ItemKind = "medicine")
{
    public const string Prefix = "medical_treat:";
    public string Encode() => Prefix + PatientId + ":" + ItemKind;
    public static MedicalTreatmentChoice? Decode(string value)
    {
        var separator = value.LastIndexOf(':');
        return value.StartsWith(Prefix, StringComparison.Ordinal) && separator > Prefix.Length
            ? new(value[Prefix.Length..separator], value[(separator + 1)..]) : null;
    }
}

public sealed record MedicalTreatmentActionPlan(string ActorId, MedicalTreatmentChoice Choice, string StockId,
    string StockOwner, GoodsPlace Place, int MinimumIllness, bool AllowBoat)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "apply the medicine dose";
}
