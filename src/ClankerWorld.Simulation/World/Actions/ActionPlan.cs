namespace ClankerWorld.Simulation.World;

/// <summary>A server-only choice plan. It is never part of a prompt or checkpoint.</summary>
public abstract record ActionPlan(string Actor, string CandidateId);

public enum ActionRefusalReason { ActorUnavailable, LifeStage, Boat, UrgentNeed, ActiveOrder, TargetUnavailable, StockUnavailable, Capacity, OutOfReach }

public sealed record ActionRefusal(ActionRefusalReason Reason, string Explanation);

public readonly record struct ActionCheck(ActionRefusal? Refusal = null)
{
    public bool Allowed => Refusal is null;
    public static ActionCheck Refuse(ActionRefusalReason reason, string explanation) => new(new(reason, explanation));
}
