namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private AgentKnowledgeArtifact? KnowledgeCopySourceFor(OwnerQueuedInstruction instruction) =>
        OrderSystem.KnowledgeCopySourceFor(KnowledgeCopyOrdersWorld, instruction);

    private string KnowledgeCopyBlockedReason(OwnerQueuedInstruction instruction) =>
        OrderSystem.KnowledgeCopyBlockedReason(KnowledgeCopyOrdersWorld, instruction);
}
