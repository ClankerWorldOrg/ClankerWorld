namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IKnowledgeCopyOrdersWorld KnowledgeCopyOrdersWorld => new KnowledgeCopyOrdersAdapter(this);

    private sealed class KnowledgeCopyOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IKnowledgeCopyOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId) => Runtime.HeldKnowledgeArtifacts(ownerId);
        public bool HasCompleteCopyContents(string actor, global::ClankerWorld.Simulation.World.AgentKnowledgeArtifact source, global::ClankerWorld.Simulation.World.AgentKnowledgeFact[] facts, global::ClankerWorld.Simulation.World.AgentRecipeKnowledge[] recipes) => Runtime.HasCompleteCopyContents(actor, source, facts, recipes);
    }
}
