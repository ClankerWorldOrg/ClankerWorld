namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy MedicalCareCapabilities => FamilySystem.MedicalCareCapabilities;
    private static ActorCapabilityPolicy MedicalTreatmentCapabilities(bool allowBoat) => FamilySystem.MedicalTreatmentCapabilities(allowBoat);

    private static ActionDefinition<MedicalSupplyChoice, MedicalSupplyActionPlan> MedicalSupplyDefinition => MedicalActions.Supply;
    private static ActionDefinition<MedicalTreatmentChoice, MedicalTreatmentActionPlan> MedicalTreatmentDefinition => MedicalActions.Treatment;
    private static ActionDefinition<MedicalConsentChoice, MedicalConsentActionPlan> MedicalConsentDefinition => MedicalActions.Consent;

    private static class MedicalActions
    {
        internal static readonly ActionDefinition<MedicalSupplyChoice, MedicalSupplyActionPlan> Supply = new(
            DecodeReferenceChoice(MedicalSupplyChoice.Decode), static choice => choice.Encode(), MedicalCareCapabilities,
            static (world, actor, _, _) => world.PlanMedicalSupply(actor),
            static (world, plan, policy) => world.CheckMedicalSupplyPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedMedicalSupply(plan); return true; });

        internal static readonly ActionDefinition<MedicalTreatmentChoice, MedicalTreatmentActionPlan> Treatment = new(
            DecodeReferenceChoice(MedicalTreatmentChoice.Decode), static choice => choice.Encode(), MedicalTreatmentCapabilities(false),
            static (world, actor, choice, _) => world.PlanMedicalTreatment(actor, choice),
            static (world, plan, policy) => world.CheckMedicalTreatmentPlan(plan, policy),
            static (world, plan, _, _) =>
            {
                world.CompleteMedicalTreatment(plan.Actor, plan.Choice.PatientId, plan.Choice.ItemKind,
                    world.society.Checkpoint.Inventory.GetLot(plan.StockId));
                return true;
            },
            PlanCapabilities: static plan => MedicalTreatmentCapabilities(plan.AllowBoat));

        internal static readonly ActionDefinition<MedicalConsentChoice, MedicalConsentActionPlan> Consent = new(
            DecodeReferenceChoice(MedicalConsentChoice.Decode), static choice => choice.Encode(), MedicalCareCapabilities,
            static (world, actor, choice, _) => world.PlanMedicalConsent(actor, choice),
            static (world, plan, policy) => world.CheckMedicalConsentPlan(plan, policy),
            static (world, plan, _, _) => { world.CompleteMedicalConsent(plan); return true; });
    }
}
