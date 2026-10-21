namespace ClankerWorld.Simulation.World;

internal interface IKnowledgeCopyOrdersWorld
{
    global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId);
    global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] FactsToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null);
    global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null);
    bool HasCompleteCopyContents(string actor, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact source, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes);
    bool AlreadyWrote(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes);
    global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeCopySourceFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
}
