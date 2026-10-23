namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy HouseGuestCapabilities(bool offeredChoice) =>
        new(ActionLifeStages.Adults, AllowBoat: !offeredChoice,
            AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private static ActionDefinition<PersonalGoodsChoice, PersonalGoodsActionPlan> PersonalGoodsDefinition => CustodyActions.PersonalGoods;
    private static ActionDefinition<HouseGuestChoice, HouseGuestActionPlan> HouseGuestDefinition => CustodyActions.HouseGuest;

    private static class CustodyActions
    {
        internal static readonly ActionDefinition<PersonalGoodsChoice, PersonalGoodsActionPlan> PersonalGoods = new(
            DecodeReferenceChoice(PersonalGoodsChoice.Decode), static choice => choice.Encode(), CommunitySystem.PersonalGoodsCapabilities(PersonalGoodsMode.Choice),
            static (world, actor, choice, _) => world.PlanPersonalGoods(actor, choice),
            static (world, plan, policy) => CommunitySystem.CheckPersonalGoods(world.CommunityGoodsWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.ApplyCheckedPersonalGoods(world.CommunityGoodsWorld, plan); return true; },
            PlanCapabilities: static plan => CommunitySystem.PersonalGoodsCapabilities(plan.Mode));

        internal static readonly ActionDefinition<HouseGuestChoice, HouseGuestActionPlan> HouseGuest = new(
            DecodeReferenceChoice(HouseGuestChoice.Decode), static choice => choice.Encode(), HouseGuestCapabilities(true),
            static (world, actor, choice, _) => world.PlanHouseGuest(actor, choice),
            static (world, plan, policy) => world.CheckHouseGuest(plan, policy),
            static (world, plan, _, _) =>
            {
                world.SetHouseGuestInvitationCore(plan.Actor, plan.House.InstanceId, plan.Choice.GuestId, plan.Choice.Invited, plan);
                return true;
            },
            PlanCapabilities: static plan => HouseGuestCapabilities(plan.OfferedChoice));
    }
}
