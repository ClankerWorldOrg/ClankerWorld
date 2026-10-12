using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

/// <summary>Gives unrelated family fixtures a legal chosen child name without introducing pending naming work.</summary>
internal static class ChosenBirthNameTestFixture
{
    internal static SocietyCheckpoint NameParent(SocietyCheckpoint state, string parentId)
    {
        var parent = state.GetAgent(parentId);
        if (parent.HasChosenName && AgentNameRules.SurnameKey(parent.Name) is not null) return state;
        var first = parent.HasChosenName ? AgentNameRules.FirstNameKey(parent.Name)!
            : "Parent" + Array.FindIndex(state.Agents.ToArray(), person => person.Id == parentId);
        return SocietyRules.RenameAgent(state, parentId, first + " Vale").Checkpoint;
    }

    internal static string ChildName(SocietyCheckpoint state, string parentId, string firstName) =>
        firstName + " " + (AgentNameRules.SurnameKey(state.GetAgent(parentId).Name)
            ?? throw new InvalidOperationException("The fixture parent must have a chosen surname before birth."));
}
