namespace ClankerWorld.Simulation.World;

internal interface IKnowledgeReadOrdersWorld
{
    global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge { get; }
    global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId);
    bool HasUnknownArtifactContents(string actor, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact artifact);
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor);
    global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeReadOrderArtifact(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool KnowsMapFact(string agentId, global::ClankerWorld.Simulation.World.GridPoint position);
    bool KnowsRecipe(string actor, string recipeId);
    string? KnowledgeReadOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    int ReadIfKnowledgeArtifact(string lotId, string sourceAgentId, string recipientId, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? selectedArtifact = null);
    long WorldTick { get; }
    OrderStore Orders { get; }
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string KnowledgeReadOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
}
