namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IKnowledgeOrdersWorld KnowledgeOrdersWorld => new KnowledgeOrdersAdapter(this);

    private sealed class KnowledgeOrdersAdapter(WorldRuntime runtime) : IKnowledgeOrdersWorld
    {
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => runtime.KnowledgeWritingFor(actor);
        public bool MatchesKnowledgeSource(global::ClankerWorld.Simulation.World.OrderTask order, string? source, bool bound) => WorldRuntime.MatchesKnowledgeSource(order, source, bound);
        public bool IsCurrentKnowledgeOrderProject(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => runtime.IsCurrentKnowledgeOrderProject(project);
        public bool MayMakeKnowledgeArtifact(string actor) => runtime.MayMakeKnowledgeArtifact(actor);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeCopySourceFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeCopySourceFor(runtime.KnowledgeCopyOrdersWorld, instruction);
        public bool CanSupplyWriting(string actor, string kind) => runtime.CanSupplyWriting(actor, kind);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] FactsToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null) => runtime.FactsToWrite(actor, kind, source);
        public global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null) => runtime.RecipesToWrite(actor, kind, source);
        public bool AlreadyWrote(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes) => runtime.AlreadyWrote(actor, kind, facts, recipes);
        public string KnowledgeCopyBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeCopyBlockedReason(runtime.KnowledgeCopyOrdersWorld, instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnowledgeOrderCandidateFor(instruction);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string KnowledgeOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnowledgeOrderBlockedReason(instruction);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public string KnowledgeWritePrefix => WorldRuntime.KnowledgeWritePrefix;
        public OrderStore Orders => runtime.orderStore;
        public string KnowledgeCopyPrefix => WorldRuntime.KnowledgeCopyPrefix;
        public void ApplyKnowledgeWritingCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyKnowledgeWritingCandidate(actor, person, candidate, originalPlan);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public void ReplaceKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => runtime.ReplaceKnowledgeWriting(project);
        public void ContinueKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => runtime.ContinueKnowledgeWriting(project);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public void ClearKnowledgeOrderBinding(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => runtime.ClearKnowledgeOrderBinding(project);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public void CancelKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project, string reason) => runtime.CancelKnowledgeWriting(project, reason);
    }
}
