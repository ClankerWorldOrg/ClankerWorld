using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public static class RelationshipPlan
{
    public static bool Matches(SocietyRelationship? current, SocietyRelationship original) => current is not null &&
        current with { AcceptedBy = null } == original with { AcceptedBy = null } &&
        current.AcceptedParties.SequenceEqual(original.AcceptedParties, StringComparer.Ordinal);
}
