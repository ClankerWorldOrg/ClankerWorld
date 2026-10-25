namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy TownProjectPhysicalCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);
    private static ActorCapabilityPolicy TownProjectRecoveryCapabilities(TownProjectRecoveryActionPlan plan) =>
        new(ActionLifeStages.Adults, AllowUrgentFood: !plan.Choice.Recover && plan.Choice.WarehouseId is null,
            AllowUrgentWarmth: !plan.Choice.Recover && plan.Choice.WarehouseId is null, AllowActiveOrder: true);

    private TPlan? PlanTownProjectRequest<TPlan>(string actor, TownProjectActionRequest request) where TPlan : ActionPlan
    {
        var choices = request.Family == TownProjectActionFamily.Donation ? TownProjectDonationChoices(actor) : TownProjectChoices(actor);
        if (choices.FirstOrDefault(choice => choice.Id == request.CandidateId) is not { } selected) return null;
        ActionPlan? plan = request.Family switch
        {
            TownProjectActionFamily.Load => PlanTownProjectLoad(actor, selected),
            TownProjectActionFamily.Work => PlanTownProjectWork(actor, selected),
            TownProjectActionFamily.Recovery => PlanTownProjectRecovery(actor, selected),
            _ => PlanTownProjectDonation(actor, selected),
        };
        return plan as TPlan;
    }

    private static ActionDefinition<TownProjectActionRequest, TownProjectLoadActionPlan> TownProjectLoadDefinition => TownProjectActions.Load;
    private static ActionDefinition<TownProjectActionRequest, TownProjectWorkActionPlan> TownProjectWorkDefinition => TownProjectActions.Work;
    private static ActionDefinition<TownProjectActionRequest, TownProjectRecoveryActionPlan> TownProjectRecoveryDefinition => TownProjectActions.Recovery;
    private static ActionDefinition<TownProjectActionRequest, TownProjectDonationActionPlan> TownProjectDonationDefinition => TownProjectActions.Donation;
    private static ActionDefinition<TownProjectProposalChoice, TownProjectProposalActionPlan> TownProjectProposalDefinition => TownProjectActions.Proposal;

    private static class TownProjectActions
    {
        internal static readonly ActionDefinition<TownProjectActionRequest, TownProjectLoadActionPlan> Load = new(
            DecodeReferenceChoice(candidate => TownProjectActionRequest.Decode(candidate) is { } request && request.Family == TownProjectActionFamily.Load ? request : null),
            static request => request.Encode(), TownProjectPhysicalCapabilities,
            static (world, actor, request, _) => world.PlanTownProjectRequest<TownProjectLoadActionPlan>(actor, request),
            static (world, plan, policy) => world.CheckTownProjectLoad(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedTownProjectLoad(plan); return true; });

        internal static readonly ActionDefinition<TownProjectActionRequest, TownProjectWorkActionPlan> Work = new(
            DecodeReferenceChoice(candidate => TownProjectActionRequest.Decode(candidate) is { } request && request.Family == TownProjectActionFamily.Work ? request : null),
            static request => request.Encode(), TownProjectPhysicalCapabilities,
            static (world, actor, request, _) => world.PlanTownProjectRequest<TownProjectWorkActionPlan>(actor, request),
            static (world, plan, policy) => world.CheckTownProjectWork(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedTownProjectWork(plan); return true; });

        internal static readonly ActionDefinition<TownProjectActionRequest, TownProjectRecoveryActionPlan> Recovery = new(
            DecodeReferenceChoice(candidate => TownProjectActionRequest.Decode(candidate) is { } request && request.Family == TownProjectActionFamily.Recovery ? request : null),
            static request => request.Encode(), TownProjectPhysicalCapabilities,
            static (world, actor, request, _) => world.PlanTownProjectRequest<TownProjectRecoveryActionPlan>(actor, request),
            static (world, plan, policy) => world.CheckTownProjectRecovery(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedTownProjectRecovery(plan); return true; },
            PlanCapabilities: TownProjectRecoveryCapabilities);

        internal static readonly ActionDefinition<TownProjectActionRequest, TownProjectDonationActionPlan> Donation = new(
            DecodeReferenceChoice(candidate => TownProjectActionRequest.Decode(candidate) is { } request && request.Family == TownProjectActionFamily.Donation ? request : null),
            static request => request.Encode(), TownProjectPhysicalCapabilities,
            static (world, actor, request, _) => world.PlanTownProjectRequest<TownProjectDonationActionPlan>(actor, request),
            static (world, plan, policy) => world.CheckTownProjectDonation(plan, policy),
            static (world, plan, _, _) => { return world.ApplyCheckedTownProjectDonation(plan); });

        internal static readonly ActionDefinition<TownProjectProposalChoice, TownProjectProposalActionPlan> Proposal = new(
            DecodeReferenceChoice(TownProjectProposalChoice.Decode), static choice => choice.Encode(), TownSystem.GovernanceCapabilities,
            static (world, actor, choice, _) => TownSystem.PlanTownProjectProposal(world.civicWorld, actor, choice),
            static (world, plan, policy) => TownSystem.CheckTownProjectProposal(world.civicWorld, plan, policy: policy),
            static (world, plan, _, _) =>
            {
                world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
                return true;
            });
    }
}
