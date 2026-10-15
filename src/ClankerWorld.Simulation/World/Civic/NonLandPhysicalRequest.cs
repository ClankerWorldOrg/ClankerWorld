namespace ClankerWorld.Simulation.World;

public sealed record NonLandPhysicalRequest(bool Relay, string CandidateId)
{
    public string Encode() => CandidateId;
    public static NonLandPhysicalRequest? Decode(string candidate) =>
        candidate.StartsWith("nonviolent_remedy:", StringComparison.Ordinal) ? new(false, candidate) :
        candidate.StartsWith("nonviolent_relay:", StringComparison.Ordinal) ? new(true, candidate) : null;
}
