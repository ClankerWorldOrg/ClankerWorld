using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IKnowledgeCopyOrdersWorld KnowledgeCopyOrdersWorld => new KnowledgeCopyOrdersAdapter(this);

    private sealed class KnowledgeCopyOrdersAdapter(WorldRuntime runtime) : IKnowledgeCopyOrdersWorld
    {
        public ClankerWorld.Simulation.World.AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId) => runtime.HeldKnowledgeArtifacts(ownerId);
        public ClankerWorld.Simulation.World.AgentKnowledgeFact[] FactsToWrite(string actor, string kind, AgentKnowledgeArtifact? source = null) => runtime.FactsToWrite(actor, kind, source);
        public ClankerWorld.Simulation.World.AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, AgentKnowledgeArtifact? source = null) => runtime.RecipesToWrite(actor, kind, source);
        public bool HasCompleteCopyContents(string actor, AgentKnowledgeArtifact source, AgentKnowledgeFact[] facts, AgentRecipeKnowledge[] recipes) => runtime.HasCompleteCopyContents(actor, source, facts, recipes);
        public bool AlreadyWrote(string actor, string kind, AgentKnowledgeFact[] facts, AgentRecipeKnowledge[] recipes) => runtime.AlreadyWrote(actor, kind, facts, recipes);
    }
}
