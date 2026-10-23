namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IKnowledgeOrdersWorld KnowledgeOrdersWorld => new KnowledgeOrdersAdapter(this);

    private sealed class KnowledgeOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IKnowledgeOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => Runtime.KnowledgeWritingFor(actor);
        public bool MatchesKnowledgeSource(global::ClankerWorld.Simulation.World.OrderTask order, string? source, bool bound) => WorldRuntime.MatchesKnowledgeSource(order, source, bound);
        public bool IsCurrentKnowledgeOrderProject(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => OrderSystem.IsCurrentKnowledgeOrderProject(Runtime.KnowledgeOrdersWorld, project);
        public bool MayMakeKnowledgeArtifact(string actor) => Runtime.MayMakeKnowledgeArtifact(actor);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeCopySourceFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeCopySourceFor(Runtime.KnowledgeCopyOrdersWorld, instruction);
        public bool CanSupplyWriting(string actor, string kind) => Runtime.CanSupplyWriting(actor, kind);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] FactsToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null) => Runtime.FactsToWrite(actor, kind, source);
        public global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null) => Runtime.RecipesToWrite(actor, kind, source);
        public bool AlreadyWrote(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes) => Runtime.AlreadyWrote(actor, kind, facts, recipes);
        public string KnowledgeCopyBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeCopyBlockedReason(Runtime.KnowledgeCopyOrdersWorld, instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeOrderCandidateFor(Runtime.KnowledgeOrdersWorld, instruction);
        public string KnowledgeOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeOrderBlockedReason(Runtime.KnowledgeOrdersWorld, instruction);
        public string KnowledgeWritePrefix => WorldRuntime.KnowledgeWritePrefix;
        public string KnowledgeCopyPrefix => WorldRuntime.KnowledgeCopyPrefix;
        public void ApplyKnowledgeWritingCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyKnowledgeWritingCandidate(actor, person, candidate, originalPlan);
        public void ReplaceKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => Runtime.ReplaceKnowledgeWriting(project);
        public void ContinueKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => Runtime.ContinueKnowledgeWriting(project);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public void ClearKnowledgeOrderBinding(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project) => OrderSystem.ClearKnowledgeOrderBinding(Runtime.KnowledgeOrdersWorld, project);
        public void CancelKnowledgeWriting(global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject project, string reason) => Runtime.CancelKnowledgeWriting(project, reason);
    }
}
