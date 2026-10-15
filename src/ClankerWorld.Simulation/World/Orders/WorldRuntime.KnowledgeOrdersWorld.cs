using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IKnowledgeOrdersWorld KnowledgeOrdersWorld => new KnowledgeOrdersAdapter(this);

    private sealed class KnowledgeOrdersAdapter(WorldRuntime runtime) : IKnowledgeOrdersWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public string KnowledgeWritePrefix => WorldRuntime.KnowledgeWritePrefix;
        public OrderStore Orders => runtime.orderStore;
        public string KnowledgeCopyPrefix => WorldRuntime.KnowledgeCopyPrefix;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => runtime.KnowledgeWritingFor(actor);
        public bool MatchesKnowledgeSource(OrderTask order, string? source, bool bound) => WorldRuntime.MatchesKnowledgeSource(order, source, bound);
        public bool MayMakeKnowledgeArtifact(string actor) => runtime.MayMakeKnowledgeArtifact(actor);
        public ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeCopySourceFor(OwnerQueuedInstruction instruction) => runtime.KnowledgeCopySourceFor(instruction);
        public bool CanSupplyWriting(string actor, string kind) => runtime.CanSupplyWriting(actor, kind);
        public ClankerWorld.Simulation.World.AgentKnowledgeFact[] FactsToWrite(string actor, string kind, AgentKnowledgeArtifact? source = null) => runtime.FactsToWrite(actor, kind, source);
        public ClankerWorld.Simulation.World.AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, AgentKnowledgeArtifact? source = null) => runtime.RecipesToWrite(actor, kind, source);
        public bool AlreadyWrote(string actor, string kind, AgentKnowledgeFact[] facts, AgentRecipeKnowledge[] recipes) => runtime.AlreadyWrote(actor, kind, facts, recipes);
        public string KnowledgeCopyBlockedReason(OwnerQueuedInstruction instruction) => runtime.KnowledgeCopyBlockedReason(instruction);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void ApplyKnowledgeWritingCandidate(string actor, AgentState person, string candidate, ActionPlan? originalPlan = null) => runtime.ApplyKnowledgeWritingCandidate(actor, person, candidate, originalPlan);
        public void ReplaceKnowledgeWriting(AgentKnowledgeWritingProject project) => runtime.ReplaceKnowledgeWriting(project);
        public void ContinueKnowledgeWriting(AgentKnowledgeWritingProject project) => runtime.ContinueKnowledgeWriting(project);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool IsKnowledgeOrder(string action) => WorldRuntime.IsKnowledgeOrder(action);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public void CancelKnowledgeWriting(AgentKnowledgeWritingProject project, string reason) => runtime.CancelKnowledgeWriting(project, reason);
    }
}
