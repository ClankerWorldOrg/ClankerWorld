using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IKnowledgeOrdersWorld
{
    SocietyCheckpoint Society { get; }
    string KnowledgeWritePrefix { get; }
    OrderStore Orders { get; }
    string KnowledgeCopyPrefix { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    bool IsAdult(string actor);
    ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor);
    bool MatchesKnowledgeSource(OrderTask order, string? source, bool bound);
    bool MayMakeKnowledgeArtifact(string actor);
    ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeCopySourceFor(OwnerQueuedInstruction instruction);
    bool CanSupplyWriting(string actor, string kind);
    ClankerWorld.Simulation.World.AgentKnowledgeFact[] FactsToWrite(string actor, string kind, AgentKnowledgeArtifact? source = null);
    ClankerWorld.Simulation.World.AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, AgentKnowledgeArtifact? source = null);
    bool AlreadyWrote(string actor, string kind, AgentKnowledgeFact[] facts, AgentRecipeKnowledge[] recipes);
    string KnowledgeCopyBlockedReason(OwnerQueuedInstruction instruction);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void ApplyKnowledgeWritingCandidate(string actor, AgentState person, string candidate, ActionPlan? originalPlan = null);
    void ReplaceKnowledgeWriting(AgentKnowledgeWritingProject project);
    void ContinueKnowledgeWriting(AgentKnowledgeWritingProject project);
    bool IsActiveOrder(string status);
    ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool IsKnowledgeOrder(string action);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    void CancelKnowledgeWriting(AgentKnowledgeWritingProject project, string reason);
}
