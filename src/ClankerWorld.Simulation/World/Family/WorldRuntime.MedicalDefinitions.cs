namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy MedicalCareCapabilities => FamilySystem.MedicalCareCapabilities;

    private static ActionDefinition<MedicalSupplyChoice, MedicalSupplyActionPlan> MedicalSupplyDefinition => MedicalActions.Supply;
    private static ActionDefinition<MedicalTreatmentChoice, MedicalTreatmentActionPlan> MedicalTreatmentDefinition => MedicalActions.Treatment;
    private static ActionDefinition<MedicalConsentChoice, MedicalConsentActionPlan> MedicalConsentDefinition => MedicalActions.Consent;

    private static class MedicalActions
    {
        internal static readonly ActionDefinition<MedicalSupplyChoice, MedicalSupplyActionPlan> Supply = new(
            DecodeReferenceChoice(MedicalSupplyChoice.Decode), static choice => choice.Encode(), MedicalCareCapabilities,
            static (world, actor, _, _) => FamilySystem.PlanMedicalSupply(world.FamilySupplyWorld, actor),
            static (world, plan, policy) => FamilySystem.CheckMedicalSupplyPlan(world.FamilySupplyWorld, plan, policy),
            static (world, plan, _, _) => { FamilySystem.ApplyCheckedMedicalSupply(world.FamilySupplyWorld, plan); return true; });

        internal static readonly ActionDefinition<MedicalTreatmentChoice, MedicalTreatmentActionPlan> Treatment = new(
            DecodeReferenceChoice(MedicalTreatmentChoice.Decode), static choice => choice.Encode(), FamilySystem.MedicalTreatmentCapabilities(false),
            static (world, actor, choice, _) => FamilySystem.PlanMedicalTreatment(world.FamilyMedicalWorld, actor, choice, 2500, false),
            static (world, plan, policy) => FamilySystem.CheckMedicalTreatmentPlan(world.FamilyMedicalWorld, plan, policy),
            static (world, plan, _, _) =>
            {
                world.CompleteMedicalTreatment(plan.Actor, plan.Choice.PatientId, plan.Choice.ItemKind,
                    world.society.Checkpoint.Inventory.GetLot(plan.StockId));
                return true;
            },
            PlanCapabilities: static plan => FamilySystem.MedicalTreatmentCapabilities(plan.AllowBoat));

        internal static readonly ActionDefinition<MedicalConsentChoice, MedicalConsentActionPlan> Consent = new(
            DecodeReferenceChoice(MedicalConsentChoice.Decode), static choice => choice.Encode(), MedicalCareCapabilities,
            static (world, actor, choice, _) => FamilySystem.PlanMedicalConsent(world.FamilyMedicalWorld, actor, choice),
            static (world, plan, policy) => FamilySystem.CheckMedicalConsentPlan(world.FamilyMedicalWorld, plan, policy),
            static (world, plan, _, _) => { FamilySystem.CompleteMedicalConsent(world.FamilyTreatmentWorld, plan); return true; });
    }
}
