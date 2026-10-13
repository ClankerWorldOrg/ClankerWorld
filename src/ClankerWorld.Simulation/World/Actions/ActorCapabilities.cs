using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public enum ActionLifeStages { Children, Adults, NonInfants, Dependents }

public sealed record ActorCapabilityState(SocietyAgeBand Age, bool Aboard, bool Mounted,
    bool UrgentFood, bool UrgentWarmth, bool ActiveOrder);

public readonly record struct ActorCapabilityPolicy(ActionLifeStages LifeStages,
    bool AllowBoat = false, bool AllowMounted = true, bool AllowUrgentFood = true,
    bool AllowUrgentWarmth = false, bool AllowActiveOrder = false);

public static class ActorCapabilities
{
    public static ActionCheck Check(ActorCapabilityState? state, string candidate, ActorCapabilityPolicy policy)
    {
        if (state is null) return ActionCheck.Refuse(ActionRefusalReason.ActorUnavailable, "the person is no longer here");
        var ageAllowed = policy.LifeStages switch
        {
            ActionLifeStages.Children => LifeStageRules.IsChild(state.Age),
            ActionLifeStages.Adults => LifeStageRules.IsAdult(state.Age),
            ActionLifeStages.Dependents => LifeStageRules.IsDependent(state.Age),
            _ => !LifeStageRules.IsInfant(state.Age),
        };
        if (!ageAllowed || !LifeStageRules.Permits(state.Age, candidate))
            return ActionCheck.Refuse(ActionRefusalReason.LifeStage, policy.LifeStages == ActionLifeStages.Children
                ? "this activity is for children" : "this activity is not available at their age");
        if (state.Aboard && !policy.AllowBoat)
            return ActionCheck.Refuse(ActionRefusalReason.Boat, "they must stay aboard the boat");
        if (state.Mounted && !policy.AllowMounted)
            return ActionCheck.Refuse(ActionRefusalReason.Mounted, "they need to get off their mount first");
        if (state.UrgentWarmth && !policy.AllowUrgentWarmth)
            return ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "getting warm comes first");
        if (state.UrgentFood && !policy.AllowUrgentFood)
            return ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "finding food comes first");
        if (state.ActiveOrder && !policy.AllowActiveOrder)
            return ActionCheck.Refuse(ActionRefusalReason.ActiveOrder, "their current order comes first");
        return new();
    }
}
