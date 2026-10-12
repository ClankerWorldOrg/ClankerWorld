using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

internal static class InstructionRecipientFixture
{
    public static WorldRuntimeState AfterDeath(WorldRuntimeState state, string targetId)
    {
        var checkpoint = SocietyRules.Kill(state.Society.Society, targetId, SocietyDeathCause.Accident).Checkpoint;
        var physical = state.Inhabitants.Single(item => item.InhabitantId == targetId);
        var deceased = checkpoint.GetInhabitant(targetId);
        return state with
        {
            Society = state.Society with { Society = checkpoint },
            Inhabitants = state.Inhabitants.Where(item => item.InhabitantId != targetId).ToArray(),
            DeceasedInhabitants = (state.DeceasedInhabitants ?? []).Append(
                new DeceasedInhabitantState(targetId, checkpoint.WorldTick,
                    checkpoint.AgeAt(deceased, checkpoint.WorldTick), physical)).ToArray(),
        };
    }
}
