using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IKnowledgeReadOrdersWorld KnowledgeReadOrdersWorld => new KnowledgeReadOrdersAdapter(this);

    private sealed class KnowledgeReadOrdersAdapter(WorldRuntime runtime) : IKnowledgeReadOrdersWorld
    {
        public WorldKnowledgeState Knowledge => runtime.knowledge;
        public long WorldTick => runtime.WorldTick;
        public OrderStore Orders => runtime.orderStore;
        public ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId) => runtime.HeldKnowledgeArtifacts(ownerId);
        public bool HasUnknownArtifactContents(string actor, AgentKnowledgeArtifact artifact) => runtime.HasUnknownArtifactContents(actor, artifact);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => runtime.KnowledgeWritingFor(actor);
        public bool KnowsMapFact(string agentId, GridPoint position) => runtime.KnowsMapFact(agentId, position);
        public bool KnowsRecipe(string actor, string recipeId) => runtime.KnowsRecipe(actor, recipeId);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public int ReadIfKnowledgeArtifact(string lotId, string sourceAgentId, string recipientId, AgentKnowledgeArtifact? selectedArtifact = null) => runtime.ReadIfKnowledgeArtifact(lotId, sourceAgentId, recipientId, selectedArtifact);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string KnowledgeReadOrderEffectId(OwnerQueuedInstruction instruction) => WorldRuntime.KnowledgeReadOrderEffectId(instruction);
    }
}
