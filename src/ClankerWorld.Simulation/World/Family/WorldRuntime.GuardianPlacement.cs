using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

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

    private void BeginGuardianPlacement(string child, string caregiver, string careRelationshipId) =>
        FamilySystem.BeginGuardianPlacement(FamilyGuardianWorld, child, caregiver, careRelationshipId);

    private void SetGuardianPlacement(string child, GuardianPlacementState? placement) =>
        FamilySystem.SetGuardianPlacement(FamilyGuardianWorld, child, placement);

    private bool IsCurrentGuardianPlacement(string child, GuardianPlacementState placement) =>
        FamilySystem.IsCurrentGuardianPlacement(FamilyGuardianWorld, child, placement);

    private void ReconcileGuardianPlacements() =>
        FamilySystem.ReconcileGuardianPlacements(FamilyGuardianWorld);

    private void RefreshGuardianPlacement(string child) =>
        FamilySystem.RefreshGuardianPlacement(FamilyGuardianWorld, child);

    private static bool IsGuardianHomeBlocker(string? blocker) =>
        FamilySystem.IsGuardianHomeBlocker(blocker);

    private string? GuardianPlacementActivityBlocker(string child, GuardianPlacementState placement) =>
        FamilySystem.GuardianPlacementActivityBlocker(FamilyGuardianWorld, child, placement);

    private string? GuardianPlacementHomeBlocker(string child, GuardianPlacementState placement) =>
        FamilySystem.GuardianPlacementHomeBlocker(FamilyGuardianWorld, child, placement);

    private bool HasGuardianPlacementTask(string actor) =>
        FamilySystem.HasGuardianPlacementTask(FamilyGuardianWorld, actor);

    private string? GuardianPlacementChildFor(string caregiver) =>
        FamilySystem.GuardianPlacementChildFor(FamilyGuardianWorld, caregiver);

    private CognitionCandidate? GuardianPlacementCandidate(string actor) =>
        FamilySystem.GuardianPlacementCandidate(FamilyGuardianWorld, actor);

    private bool TryApplyGuardianPlacementCandidate(string actor, string candidate, ActionPlan? originalPlan = null)
    {
        if (GuardianTravelChoice.Decode(candidate) is not { } choice) return false;
        var plan = originalPlan as GuardianTravelActionPlan;
        if (plan is null)
        {
            RefreshGuardianPlacement(choice.Kind == GuardianTravelKind.Relocate ? choice.TargetId : actor);
            plan = PlanGuardianTravel(actor, choice);
        }
        if (plan is not null) ApplyGuardianTravelPlan(plan);
        return true;
    }

    private bool ContinueGuardianPlacement(string actor) =>
        FamilySystem.ContinueGuardianPlacement(FamilyGuardianWorld, actor);

    private void EscortGuardianChild(string caregiver, string child) =>
        FamilySystem.EscortGuardianChild(FamilyGuardianWorld, caregiver, child);

    private void MoveForGuardianPlacement(string actor, string child, GridPoint destination, string reason, int range = 0) =>
        FamilySystem.MoveForGuardianPlacement(FamilyGuardianWorld, actor, child, destination, reason, range);

    private void PauseGuardianPlacementWork(string actor) =>
        FamilySystem.PauseGuardianPlacementWork(FamilyGuardianWorld, actor);

    private void FollowPlacementGuardian(string child, GuardianTravelActionPlan? originalPlan = null) =>
        FamilySystem.FollowPlacementGuardian(FamilyGuardianWorld, child, originalPlan);

    private void AdvanceGuardianPlacementFollowers(IEnumerable<string> orderActors) =>
        FamilySystem.AdvanceGuardianPlacementFollowers(FamilyGuardianWorld, orderActors);

    private void CompleteGuardianPlacement(string child) =>
        FamilySystem.CompleteGuardianPlacement(FamilyGuardianWorld, child);

    private bool CanEnterGuardianPlacementHouse(string child, PlacedBuilding house) =>
        FamilySystem.CanEnterGuardianPlacementHouse(FamilyGuardianWorld, child, house);

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
        return NonLandExcerpt(note, 256);
    }
}
