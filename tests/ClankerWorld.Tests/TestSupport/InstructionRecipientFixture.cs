using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

internal static class InstructionRecipientFixture
{
    public static WorldRuntimeState AfterDeath(WorldRuntimeState state, string targetId)
    {
        var checkpoint = SocietyRules.Kill(state.Society.Society, targetId, SocietyDeathCause.Accident).Checkpoint;
        var physical = state.Agents.Single(item => item.AgentId == targetId);
        var deceased = checkpoint.GetAgent(targetId);
        return state with
        {
            Society = state.Society with { Society = checkpoint },
            Agents = state.Agents.Where(item => item.AgentId != targetId).ToArray(),
            DeceasedAgents = (state.DeceasedAgents ?? []).Append(
                new DeceasedAgentState(targetId, checkpoint.WorldTick,
                    checkpoint.AgeAt(deceased, checkpoint.WorldTick), physical)).ToArray(),
        };
    }
}
