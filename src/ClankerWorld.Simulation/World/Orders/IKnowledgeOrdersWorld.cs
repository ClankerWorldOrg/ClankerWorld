namespace ClankerWorld.Simulation.World;

internal interface IKnowledgeOrdersWorld
{
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor);
    bool MatchesKnowledgeSource(global::ClankerWorld.Simulation.World.OrderTask order, string? source, bool bound);
    bool IsCurrentKnowledgeOrderProject(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project);
    bool MayMakeKnowledgeArtifact(string actor);
    global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeCopySourceFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool CanSupplyWriting(string actor, string kind);
    global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] FactsToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null);
    global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null);
    bool AlreadyWrote(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes);
    string KnowledgeCopyBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string KnowledgeOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    string KnowledgeWritePrefix { get; }
    OrderStore Orders { get; }
    string KnowledgeCopyPrefix { get; }
    void ApplyKnowledgeWritingCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    void ReplaceKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project);
    void ContinueKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project);
    bool IsActiveOrder(string status);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    void ClearKnowledgeOrderBinding(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    void CancelKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project, string reason);
}
