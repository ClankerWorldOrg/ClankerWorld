namespace ClankerWorld.Simulation.World;

public sealed record MedicalConsentChoice(string CaregiverId, bool Allow)
{
    public const string AllowPrefix = "medical_allow:";
    public const string RevokePrefix = "medical_revoke:";
    public string Encode() => (Allow ? AllowPrefix : RevokePrefix) + CaregiverId;
    public static MedicalConsentChoice? Decode(string value) => value.StartsWith(AllowPrefix, StringComparison.Ordinal) ? new(value[AllowPrefix.Length..], true) :
        value.StartsWith(RevokePrefix, StringComparison.Ordinal) ? new(value[RevokePrefix.Length..], false) : null;
}

public sealed record MedicalConsentActionPlan(string ActorId, MedicalConsentChoice Choice,
    IReadOnlyList<string> OriginalCaregivers, IReadOnlyList<string> ResultingCaregivers, string? InterruptedDoseId)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Allow ? "allow medical care" : "withdraw medical care permission";
}
