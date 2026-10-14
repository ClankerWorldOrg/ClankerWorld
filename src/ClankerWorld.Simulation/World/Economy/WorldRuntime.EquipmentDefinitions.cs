using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<PrivateEquipmentKind, PrivateEquipmentActionPlan> GarmentDefinition => EquipmentActions.Garment;
    private static ActionDefinition<PrivateEquipmentKind, PrivateEquipmentActionPlan> CarryAidDefinition => EquipmentActions.CarryAid;
    private static ActionDefinition<EquipmentRepairChoice, EquipmentRepairActionPlan> EquipmentRepairDefinition => EquipmentActions.Repair;
    private static ActionDefinition<VesselChoice, VesselActionPlan> HouseholdVesselDefinition(VesselActionKind kind) => EquipmentActions.Vessels[(int)kind];

    private static class EquipmentActions
    {
        internal static readonly ActionDefinition<PrivateEquipmentKind, PrivateEquipmentActionPlan> Garment = CreateEquipment(PrivateEquipmentKind.Garment);
        internal static readonly ActionDefinition<PrivateEquipmentKind, PrivateEquipmentActionPlan> CarryAid = CreateEquipment(PrivateEquipmentKind.CarryAid);
        internal static readonly ActionDefinition<EquipmentRepairChoice, EquipmentRepairActionPlan> Repair = new(
            EquipmentRepairChoice.TryDecode, static _ => EquipmentRepairChoice.Encode(), EquipmentRepairPolicy,
            static (world, actor, _, _) => world.PlanEquipmentRepair(actor),
            static (world, plan, policy) => world.CheckEquipmentRepairPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedEquipmentRepairPlan(plan); return true; });
        internal static readonly ReadOnlyCollection<ActionDefinition<VesselChoice, VesselActionPlan>> Vessels =
            Array.AsReadOnly(Enum.GetValues<VesselActionKind>().Select(CreateVessel).ToArray());

        private static ActionDefinition<PrivateEquipmentKind, PrivateEquipmentActionPlan> CreateEquipment(PrivateEquipmentKind kind) => new(
            (string candidate, out PrivateEquipmentKind choice) => PrivateEquipmentActionPlan.TryDecode(candidate, out choice) && choice == kind,
            PrivateEquipmentActionPlan.Candidate, PrivateEquipmentPolicy(kind),
            static (world, actor, choice, _) => world.PlanPrivateEquipment(actor, choice == PrivateEquipmentKind.CarryAid),
            static (world, plan, policy) => world.CheckPrivateEquipmentPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedPrivateEquipmentPlan(plan); return true; });

        private static ActionDefinition<VesselChoice, VesselActionPlan> CreateVessel(VesselActionKind kind) => new(
            (string candidate, out VesselChoice choice) =>
            {
                choice = VesselChoice.Decode(candidate)!;
                return choice is not null && choice.Kind == kind;
            },
            static choice => choice.Encode(), VesselPolicy(kind),
            static (world, actor, choice, _) => world.PlanVesselAction(actor, choice),
            static (world, plan, policy) => world.CheckVesselPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedVesselPlan(plan); return true; });
    }
}
