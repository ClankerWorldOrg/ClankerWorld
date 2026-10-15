using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed record ConstructionMaterialInput(int QuantityIndex, string LotId, string ItemKind, int Quantity);

public abstract record ConstructionProjectActionPlan(string ActorId, string ProjectCandidateId) : ActionPlan(ActorId, ProjectCandidateId);

public sealed record ConstructionGoalActionPlan(string ActorId, BuildingDefinition Definition)
    : ConstructionProjectActionPlan(ActorId, "build:building:" + Definition.CanonicalId)
{
    public override string Activity => "start that building goal without a selected site";
}

public sealed record ConstructionActionPlan(string ActorId, BuildingDefinition Definition, GridPoint Site,
    string InstanceId, string? ActorHouseholdId, string? HouseholdId, string? TownId, string MaterialOwnerId,
    string? InstructionId, IReadOnlyList<ConstructionMaterialInput>? Inputs, IReadOnlyList<GridPoint> Route)
    : ConstructionProjectActionPlan(ActorId, new ConstructionChoice(Definition.CanonicalId, Site).Encode())
{
    public override string Activity => "build that selected design at its chosen site";
}
