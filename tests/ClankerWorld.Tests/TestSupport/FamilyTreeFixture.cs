using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

/// <summary>Builds family history, such as biological parentage, into a prepared world.</summary>
internal static class FamilyTreeFixture
{
    public static WorldRuntimeState WithRelationships(WorldRuntimeState state, SocietyRelationshipType type,
        params (string From, string To)[] pairs)
    {
        var society = state.Society.Society;
        var added = pairs.Select(pair => new SocietyRelationship($"family-tree:{type}:{pair.From}:{pair.To}", 1, type,
            pair.From, pair.To, SocietyRelationshipState.Accepted, SocietyConsentState.ProtectedLifecycle,
            society.WorldTick, society.WorldTick, "family"));
        return state with
        {
            Society = state.Society with
            {
                Society = society with
                {
                    Relationships = society.Relationships.Concat(added).OrderBy(edge => edge.Id, StringComparer.Ordinal).ToArray(),
                },
            },
        };
    }

    /// <summary>Adds a relative who died before the save, for family trees larger than the four starting agents.</summary>
    public static (WorldRuntimeState State, string Id) WithDeadAncestor(WorldRuntimeState state, string key)
    {
        var society = state.Society.Society;
        var ancestor = society.Inhabitants[0] with
        {
            Id = $"{society.WorldId}:inhabitant:ancestor-{key}",
            Name = $"Ancestor-{key}",
            Status = SocietyInhabitantStatus.Dead,
            HouseholdId = null,
            ProviderBindingId = null,
            DeathTick = society.WorldTick,
            DeathCause = SocietyDeathCause.Accident,
        };
        return (state with
        {
            Society = state.Society with
            {
                Society = society with
                {
                    Inhabitants = society.Inhabitants.Append(ancestor).OrderBy(person => person.Id, StringComparer.Ordinal).ToArray(),
                },
            },
        }, ancestor.Id);
    }
}
