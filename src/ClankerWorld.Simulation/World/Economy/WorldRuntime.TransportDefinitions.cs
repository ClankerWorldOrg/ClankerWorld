namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy TransportCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    private static ActorCapabilityPolicy BoatTripCapabilities =>
        new(ActionLifeStages.Adults, AllowMounted: false, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    private static ActorCapabilityPolicy HandcartControlCapabilities(HandcartControlKind kind) =>
        new(ActionLifeStages.Adults, AllowMounted: kind != HandcartControlKind.Attach,
            AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private static ActionDefinition<BoatTripRequestChoice, BoatTripActionPlan> BoatTripDefinition => TransportActions.BoatTrip;
    private static ActionDefinition<BoatWaitingChoice, BoatWaitingActionPlan> BoatWaitingDefinition => TransportActions.BoatWaiting;
    private static ActionDefinition<HandcartControlChoice, HandcartControlActionPlan> HandcartControlDefinition => TransportActions.Control;
    private static ActionDefinition<HandcartBuildMaterialChoice, HandcartBuildMaterialActionPlan> HandcartBuildMaterialDefinition => TransportActions.BuildMaterial;
    private static ActionDefinition<HandcartRepairInputChoice, HandcartRepairInputActionPlan> HandcartRepairInputDefinition => TransportActions.RepairInput;
    private static ActionDefinition<HandcartPullChoice, HandcartPullActionPlan> HandcartPullDefinition => TransportActions.Pull;
    private static ActionDefinition<HandcartCargoChoice, HandcartCargoActionPlan> HandcartCargoDefinition => TransportActions.Cargo;
    private static ActionDefinition<HandcartRepairChoice, HandcartRepairActionPlan> HandcartRepairDefinition => TransportActions.Repair;

    private static class TransportActions
    {
        internal static readonly ActionDefinition<BoatTripRequestChoice, BoatTripActionPlan> BoatTrip = new(
            DecodeReferenceChoice(BoatTripRequestChoice.Decode), static choice => choice.Encode(), BoatTripCapabilities,
            static (world, actor, choice, _) => world.BoatTripChoices(actor).FirstOrDefault(item => item.Id == choice.CandidateId)?.Plan,
            static (world, plan, policy) => TransportSystem.CheckBoatTripPlan(world.BoatWorld, plan, policy),
            static (world, plan, _, _) => { TransportSystem.ApplyCheckedBoatTrip(world.BoatWorld, plan); return true; });

        internal static readonly ActionDefinition<BoatWaitingChoice, BoatWaitingActionPlan> BoatWaiting = new(
            DecodeReferenceChoice(BoatWaitingChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => TransportSystem.PlanBoatWaiting(world.BoatWorld, actor, choice),
            static (world, plan, policy) => TransportSystem.CheckBoatWaitingPlan(world.BoatWorld, plan, policy),
            static (world, plan, _, _) => { TransportSystem.ApplyCheckedBoatWaiting(world.BoatWorld, plan); return true; });

        internal static readonly ActionDefinition<HandcartControlChoice, HandcartControlActionPlan> Control = new(
            DecodeReferenceChoice(HandcartControlChoice.Decode), static choice => choice.Encode(), HandcartControlCapabilities(HandcartControlKind.Attach),
            static (world, actor, choice, _) => world.PlanHandcartControl(actor, choice),
            static (world, plan, policy) => world.CheckHandcartControlPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedHandcartControl(plan); return true; },
            PlanCapabilities: static plan => HandcartControlCapabilities(plan.Choice.Kind));

        internal static readonly ActionDefinition<HandcartBuildMaterialChoice, HandcartBuildMaterialActionPlan> BuildMaterial = new(
            DecodeReferenceChoice(HandcartBuildMaterialChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => world.PlanHandcartBuildMaterial(actor, choice),
            static (world, plan, policy) => world.CheckHandcartBuildMaterialPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedHandcartBuildMaterial(plan); return true; });

        internal static readonly ActionDefinition<HandcartRepairInputChoice, HandcartRepairInputActionPlan> RepairInput = new(
            DecodeReferenceChoice(HandcartRepairInputChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => world.PlanHandcartRepairInput(actor, choice),
            static (world, plan, policy) => world.CheckHandcartRepairInputPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedHandcartRepairInput(plan); return true; });

        internal static readonly ActionDefinition<HandcartPullChoice, HandcartPullActionPlan> Pull = new(
            DecodeReferenceChoice(HandcartPullChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => world.PlanHandcartPull(actor, choice),
            static (world, plan, policy) => world.CheckHandcartPullPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedHandcartPull(plan); return true; });

        internal static readonly ActionDefinition<HandcartCargoChoice, HandcartCargoActionPlan> Cargo = new(
            DecodeReferenceChoice(HandcartCargoChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => world.PlanHandcartCargo(actor, choice),
            static (world, plan, policy) => world.CheckHandcartCargoPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedHandcartCargo(plan); return true; });

        internal static readonly ActionDefinition<HandcartRepairChoice, HandcartRepairActionPlan> Repair = new(
            DecodeReferenceChoice(HandcartRepairChoice.Decode), static choice => choice.Encode(), TransportCapabilities,
            static (world, actor, choice, _) => world.PlanHandcartRepair(actor, choice),
            static (world, plan, policy) => world.CheckHandcartRepairPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedHandcartRepair(plan); return true; });
    }
}
