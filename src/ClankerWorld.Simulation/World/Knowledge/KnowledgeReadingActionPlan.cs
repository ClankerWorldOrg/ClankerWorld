namespace ClankerWorld.Simulation.World;

public sealed record KnowledgeReadingActionPlan(string ActorId, AgentKnowledgeArtifact Artifact, string RecipientId, bool Share)
    : ActionPlan(ActorId, new KnowledgeReadingChoice(Artifact.Id, RecipientId, Share).Encode())
{
    public override string Activity => Share ? "share that selected held record" : "read that selected held record";
}

public sealed record KnowledgeReadingChoice(string ArtifactId, string RecipientId, bool Share)
{
    public string Encode() => IdentityRules.PrefixedReference(Share ? "knowledge_share:" : "knowledge_read:",
        Share ? ArtifactId + "|" + RecipientId : ArtifactId);

    public static KnowledgeReadingChoice? Decode(string candidate)
    {
        if (candidate.StartsWith("knowledge_read:", StringComparison.Ordinal))
            return new(candidate["knowledge_read:".Length..], "", false);
        if (!candidate.StartsWith("knowledge_share:", StringComparison.Ordinal)) return null;
        var encoded = candidate["knowledge_share:".Length..];
        var divider = encoded.LastIndexOf('|');
        return divider <= 0 || divider == encoded.Length - 1 ? null : new(encoded[..divider], encoded[(divider + 1)..], true);
    }
}
