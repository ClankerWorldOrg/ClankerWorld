using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

/// <summary>Accepted care whose dependent still needs a physical home and Town placement.</summary>
public sealed record GuardianPlacementState(
    string CaregiverId,
    string CareRelationshipId,
    int CareRevision,
    long StartedTick,
    string Stage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DestinationHouseholdId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DestinationTownId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HouseId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HouseDefinitionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? HousePlacedTick = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? HousePosition = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Blocker = null);

public sealed partial class WorldRuntime
{
    // Only the guardian-placement continuation consults this budget. Ordinary
    // movement retains its existing scheduling; even a cooldown wait spends
    // the pending dependent's movement opportunity for this tick.
    private readonly FamilyTurnState familyTurn = new();
    private FamilyTurnState guardianPlacementActions => familyTurn;

    private bool TryApplyGuardianPlacementCandidate(string actor, string candidate, ActionPlan? originalPlan = null)
    {
        if (GuardianTravelChoice.Decode(candidate) is not { } choice) return false;
        var plan = originalPlan as GuardianTravelActionPlan;
        if (plan is null)
        {
            FamilySystem.RefreshGuardianPlacement(this.FamilyGuardianWorld, (choice.Kind == GuardianTravelKind.Relocate ? choice.TargetId : actor));
            plan = FamilySystem.PlanGuardianTravel(this.FamilyGuardianWorld, actor, choice);
        }
        if (plan is not null) ApplyGuardianTravelPlan(plan);
        return true;
    }

    private string? GuardianPlacementModelNote(string actor)
    {
        var child = agents[actor].GuardianPlacement is not null ? actor : agents.Values
            .Where(person => person.GuardianPlacement?.CaregiverId == actor)
            .OrderBy(person => person.AgentId, StringComparer.Ordinal).Select(person => person.AgentId).FirstOrDefault();
        if (child is null || agents[child].GuardianPlacement is not { } placement) return HousingNote(actor);
        var stage = placement.Stage == "collecting" ? "collecting the child" : "travelling together";
        // The observation's housing slot is bounded. Keep the accepted task and
        // current blocker before ordinary housing detail and the repeated name.
        var note = $"Accepted guardian placement pending: {stage}. {placement.Blocker} {HousingNote(actor)} Child: {society.Checkpoint.GetAgent(child).Name}.".Trim();
        return BoundedText.Excerpt(note, 256);
    }
}
