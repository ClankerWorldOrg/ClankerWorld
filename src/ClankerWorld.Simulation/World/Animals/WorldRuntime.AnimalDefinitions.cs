namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<AnimalActionRequest, AnimalWorkActionPlan> AnimalWorkDefinition => AnimalActions.Work;
    private static ActionDefinition<AnimalActionRequest, AnimalProductActionPlan> AnimalProductDefinition => AnimalActions.Product;
    private static ActionDefinition<AnimalActionRequest, AnimalTravelActionPlan> AnimalTravelDefinition => AnimalActions.Travel;
    private static ActionDefinition<AnimalActionRequest, AnimalSupplyActionPlan> AnimalSupplyDefinition => AnimalActions.Supply;
    private static ActionDefinition<AnimalActionRequest, AnimalTripActionPlan> AnimalTripDefinition => AnimalActions.Trip;
    private static ActionDefinition<AnimalActionRequest, AnimalAgreementActionPlan> AnimalAgreementDefinition => AnimalActions.Agreement;

    private static class AnimalActions
    {
        private static ActionDefinition<AnimalActionRequest, TPlan> Define<TPlan>(AnimalActionFamily family,
            Func<WorldRuntime, TPlan, ActorCapabilityPolicy, ActionCheck> check,
            Func<WorldRuntime, TPlan, bool> apply, Func<TPlan, ActorCapabilityPolicy>? capabilities = null,
            Func<WorldRuntime, ActionContext, bool>? noPlan = null) where TPlan : ActionPlan => new(
                DecodeReferenceChoice(candidate => AnimalActionRequest.Decode(candidate) is { } request && request.Family == family ? request : null),
                static request => request.Encode(), AnimalSystem.ActionCapabilities,
                static (world, actor, request, _) => world.animalSystem.PlanAnimalRequest<TPlan>(world.animalWorldAdapter, actor, request),
                check, (world, plan, _, _) => apply(world, plan), PlanCapabilities: capabilities, NoPlanFallback: noPlan);

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalWorkActionPlan> Work = Define<AnimalWorkActionPlan>(
            AnimalActionFamily.Work, static (world, plan, policy) => world.animalSystem.CheckAnimalWorkPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.ApplyCheckedAnimalWork(world.animalWorldAdapter, plan); return true; });

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalProductActionPlan> Product = Define<AnimalProductActionPlan>(
            AnimalActionFamily.Product, static (world, plan, policy) => world.animalSystem.CheckAnimalProductPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.ApplyCheckedAnimalProduct(world.animalWorldAdapter, plan); return true; },
            capabilities: static plan => AnimalSystem.ProductCapabilities(plan.Choice.Kind));

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalTravelActionPlan> Travel = Define<AnimalTravelActionPlan>(
            AnimalActionFamily.Travel, static (world, plan, policy) => world.animalSystem.CheckAnimalTravelPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.ApplyCheckedAnimalTravel(world.animalWorldAdapter, plan); return true; });

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalSupplyActionPlan> Supply = Define<AnimalSupplyActionPlan>(
            AnimalActionFamily.Supply, static (world, plan, policy) => world.animalSystem.CheckAnimalSupplyPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.ApplyCheckedAnimalSupply(world.animalWorldAdapter, plan); return true; });

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalTripActionPlan> Trip = Define<AnimalTripActionPlan>(
            AnimalActionFamily.Trip, static (world, plan, policy) => world.animalSystem.CheckAnimalTripPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.ApplyCheckedAnimalTrip(world.animalWorldAdapter, plan); return true; },
            noPlan: static (world, action) => world.animalSystem.FinishMissingAnimalTrip(world.animalWorldAdapter, action.AgentId));

        internal static readonly ActionDefinition<AnimalActionRequest, AnimalAgreementActionPlan> Agreement = Define<AnimalAgreementActionPlan>(
            AnimalActionFamily.Agreement, static (world, plan, policy) => world.animalSystem.CheckAnimalAgreementPlan(world.animalWorldAdapter, plan, policy),
            static (world, plan) => { world.animalSystem.CompleteAnimalAgreement(world.animalWorldAdapter, plan); return true; });
    }
}
