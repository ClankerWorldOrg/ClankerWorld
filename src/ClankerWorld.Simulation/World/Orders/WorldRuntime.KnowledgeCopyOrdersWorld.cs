namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IKnowledgeCopyOrdersWorld KnowledgeCopyOrdersWorld => new KnowledgeCopyOrdersAdapter(this);

    private sealed class KnowledgeCopyOrdersAdapter(WorldRuntime runtime) : IKnowledgeCopyOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId) => runtime.HeldKnowledgeArtifacts(ownerId);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] FactsToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null) => runtime.FactsToWrite(actor, kind, source);
        public global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? source = null) => runtime.RecipesToWrite(actor, kind, source);
        public bool HasCompleteCopyContents(string actor, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact source, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes) => runtime.HasCompleteCopyContents(actor, source, facts, recipes);
        public bool AlreadyWrote(string actor, string kind, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes) => runtime.AlreadyWrote(actor, kind, facts, recipes);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact? KnowledgeCopySourceFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnowledgeCopySourceFor(instruction);
    }
}
