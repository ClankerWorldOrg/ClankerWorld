namespace ClankerWorld.Simulation.World;

public sealed record PlannedWritingMaterial(string LotId, string ItemKind, int Quantity);
public sealed record PlannedWritingPickup(string LotId, string OwnerId, string ItemKind, int Quantity, GridPoint Position, int Range);

public sealed record KnowledgeWritingActionPlan(string ActorId, string SelectedCandidateId, string Kind,
    AgentKnowledgeArtifact? Source, IReadOnlyList<AgentKnowledgeFact> Facts, IReadOnlyList<AgentRecipeKnowledge> Recipes,
    IReadOnlyList<PlannedWritingMaterial> Materials, PlannedWritingPickup? Pickup, AgentKnowledgeWritingProject? Project)
    : ActionPlan(ActorId, SelectedCandidateId)
{
    public override string Activity => "write that selected knowledge record";
}

public enum KnowledgeWritingKind { Write, Copy, Continue }

public sealed record KnowledgeWritingChoice(KnowledgeWritingKind Kind, string Subject)
{
    public string Encode() => Kind switch
    {
        KnowledgeWritingKind.Write => IdentityRules.PrefixedReference("knowledge_write:", Subject),
        KnowledgeWritingKind.Copy => IdentityRules.PrefixedReference("knowledge_copy:", Subject),
        _ => "knowledge_continue",
    };

    public static KnowledgeWritingChoice? Decode(string candidate) => candidate == "knowledge_continue"
        ? new(KnowledgeWritingKind.Continue, "")
        : candidate.StartsWith("knowledge_write:", StringComparison.Ordinal) ? new(KnowledgeWritingKind.Write, candidate["knowledge_write:".Length..])
        : candidate.StartsWith("knowledge_copy:", StringComparison.Ordinal) ? new(KnowledgeWritingKind.Copy, candidate["knowledge_copy:".Length..]) : null;
}
