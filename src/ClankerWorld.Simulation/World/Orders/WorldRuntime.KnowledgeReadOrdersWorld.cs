namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IKnowledgeReadOrdersWorld KnowledgeReadOrdersWorld => new KnowledgeReadOrdersAdapter(this);

    private sealed class KnowledgeReadOrdersAdapter(WorldRuntime runtime) : IKnowledgeReadOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => runtime.knowledge;
        public global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId) => runtime.HeldKnowledgeArtifacts(ownerId);
        public bool HasUnknownArtifactContents(string actor, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact artifact) => runtime.HasUnknownArtifactContents(actor, artifact);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => runtime.KnowledgeWritingFor(actor);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeReadOrderArtifact(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnowledgeReadOrderArtifact(instruction);
        public bool KnowsMapFact(string agentId, global::ClankerWorld.Simulation.World.GridPoint position) => runtime.KnowsMapFact(agentId, position);
        public bool KnowsRecipe(string actor, string recipeId) => runtime.KnowsRecipe(actor, recipeId);
        public string? KnowledgeReadOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnowledgeReadOrderBlocker(instruction);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public int ReadIfKnowledgeArtifact(string lotId, string sourceAgentId, string recipientId, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? selectedArtifact = null) => runtime.ReadIfKnowledgeArtifact(lotId, sourceAgentId, recipientId, selectedArtifact);
        public long WorldTick => runtime.WorldTick;
        public OrderStore Orders => runtime.orderStore;
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string KnowledgeReadOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.KnowledgeReadOrderEffectId(instruction);
    }
}
