using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IKnowledgeReadOrdersWorld
{
    WorldKnowledgeState Knowledge { get; }
    long WorldTick { get; }
    OrderStore Orders { get; }
    ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId);
    bool HasUnknownArtifactContents(string actor, AgentKnowledgeArtifact artifact);
    bool IsAdult(string actor);
    ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor);
    bool KnowsMapFact(string agentId, GridPoint position);
    bool KnowsRecipe(string actor, string recipeId);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    int ReadIfKnowledgeArtifact(string lotId, string sourceAgentId, string recipientId, AgentKnowledgeArtifact? selectedArtifact = null);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string KnowledgeReadOrderEffectId(OwnerQueuedInstruction instruction);
}
