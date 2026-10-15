namespace ClankerWorld.Simulation.World;

internal sealed partial class AgentStore
{
    private readonly Dictionary<string, AgentState> living = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DeceasedAgentState> deceased = new(StringComparer.Ordinal);

    internal AgentStore() { }

    internal AgentStore(IEnumerable<AgentState> people, IEnumerable<DeceasedAgentState> history)
    {
        foreach (var person in people) living.Add(person.AgentId, person);
        foreach (var person in history) deceased.Add(person.AgentId, person);
    }

    internal IReadOnlyDictionary<string, AgentState> Living => living;
    internal IReadOnlyDictionary<string, DeceasedAgentState> Deceased => deceased;

    internal void PlaceStartingAgent(string id, GridPoint position, string personality, string aspiration) =>
        Add(new(id, position, 6_500, 0, personality, aspiration));

    internal void PlaceFounder(string id, GridPoint position) =>
        Add(new(id, position, 6_500, 0, "undecided", "find a purpose", IdentityChoicePending: true));

    internal void RecordBirth(string id, GridPoint position) =>
        Add(new(id, position, 8_000, 0, "undecided", "find a purpose", Survival: new SurvivalCondition(), IdentityChoicePending: true));

    internal void RecordDeath(string id, DeceasedAgentState person)
    {
        if (SystemInvariantChecks.Enabled)
        {
            ValidateLocalAgent(person.LastPhysical, id);
            if (person.AgentId != id || person.DeathTick < 0 || person.AgeAtDeath < 0)
                throw new InvalidDataException("Agent death history has an invalid local state.");
        }
        deceased.Add(id, person);
    }

    internal void RemovePhysicalPresence(string id) => living.Remove(id);
    internal void BeginProject(string id, AgentState prepared) => Commit(id, prepared);
    internal void CompleteDependentCare(string id, AgentState prepared) => Commit(id, prepared);
    internal void CompleteExploration(string id, AgentState prepared) => Commit(id, prepared);
    internal void ResumeExploration(string id, AgentState prepared) => Commit(id, prepared);
    internal void RestoreEquipmentAfterFailure(string id, AgentState before) => Commit(id, before);
    internal void ApplyDeveloperNeedEdit(string id, AgentState prepared) => Commit(id, prepared);

    private void Add(AgentState person)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalAgent(person, person.AgentId);
        living.Add(person.AgentId, person);
    }

    private void Commit(string id, AgentState next)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalAgent(next, id);
        living[id] = next;
    }

    internal static void ValidateLocalAgent(AgentState person, string id)
    {
        CommunitySystem.ValidateLocalAgentCommunity(person);
        FamilySystem.ValidateLocalAgentFamily(person);
        if (person.AgentId != id || string.IsNullOrWhiteSpace(id) ||
            person.FullnessBasisPoints is < 0 or > 10_000 || person.MoveWaitTicks < 0 || person.TravelCooldownTicks < 0)
            throw new InvalidDataException("Physical agent state has invalid local values.");
    }
}
