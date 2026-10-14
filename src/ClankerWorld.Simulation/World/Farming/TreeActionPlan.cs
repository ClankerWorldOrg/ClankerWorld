namespace ClankerWorld.Simulation.World;

public enum TreeChoice { Plant, Orchard, Replant }

public sealed record PlannedOrchardClaim(string ReservationId, int Quantity);

public sealed record TreeActionPlan(string ActorId, TreeChoice Choice, GridPoint Position,
    string Species, string? StumpId, PlannedEquipmentPickup Seed, PlannedOrchardClaim? Claim,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Candidate(Choice))
{
    public static string Candidate(TreeChoice choice) => choice switch
    {
        TreeChoice.Replant => "replant_tree",
        TreeChoice.Orchard => "plant_orchard",
        _ => "plant_tree",
    };

    public static TreeChoice? Decode(string candidate) => candidate switch
    {
        "replant_tree" => TreeChoice.Replant,
        "plant_orchard" => TreeChoice.Orchard,
        "plant_tree" => TreeChoice.Plant,
        _ => null,
    };

    public override string Activity => Choice switch
    {
        TreeChoice.Replant => "replant the selected stump",
        TreeChoice.Orchard => "plant the orchard seed",
        _ => "plant the tree seed",
    };
}
