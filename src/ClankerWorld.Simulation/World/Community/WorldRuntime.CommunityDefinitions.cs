using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy HouseholdCommunityCapabilities => CommunitySystem.HouseholdCommunityCapabilities;
    private static ActorCapabilityPolicy LearningCapabilities => CommunitySystem.LearningCapabilities;

    private static class CommunityActions
    {
        internal static readonly ActionDefinition<HousingChoice, HousingActionPlan> Housing = new(
            DecodeReferenceChoice(HousingChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => CommunitySystem.PlanHousingAction(world.CommunityHousingWorld, actor, choice),
            static (world, plan, policy) => CommunitySystem.CheckHousingActionPlan(world.CommunityHousingWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteHousingAction(world.CommunityHousingWorld, plan); return true; });

        internal static readonly ActionDefinition<LearningChoice, LearningActionPlan> Learning = new(
            DecodeReferenceChoice(LearningChoice.Decode), static choice => choice.Encode(), LearningCapabilities,
            static (world, actor, choice, _) => CommunitySystem.PlanLearningAction(world.CommunityLearningWorld, actor, choice, false),
            static (world, plan, policy) => CommunitySystem.CheckLearningActionPlan(world.CommunityLearningWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteLearningAction(world.CommunityLearningWorld, plan); return true; });

        internal static readonly ActionDefinition<DepartureChoice, DepartureActionPlan> Departure = new(
            DecodeReferenceChoice(DepartureChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => CommunitySystem.PlanDepartureAction(world.CommunityDepartureWorld, actor, choice),
            static (world, plan, policy) => CommunitySystem.CheckDepartureActionPlan(world.CommunityDepartureWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteDepartureAction(world.CommunityDepartureWorld, plan); return true; });

        internal static readonly ActionDefinition<ReplacementCareChoice, ReplacementCareActionPlan> ReplacementCare = new(
            DecodeReferenceChoice(ReplacementCareChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => CommunitySystem.PlanReplacementCare(world.CommunityDepartureWorld, actor, choice),
            static (world, plan, policy) => CommunitySystem.CheckReplacementCarePlan(world.CommunityDepartureWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteReplacementCare(world.CommunityDepartureWorld, plan); return true; });

        internal static readonly ActionDefinition<RelocationChoice, RelocationActionPlan> Relocation = new(
            DecodeReferenceChoice(RelocationChoice.Decode), static _ => RelocationChoice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, _, _) => CommunitySystem.PlanRelocationAction(world.CommunityRelocationWorld, actor),
            static (world, plan, policy) => CommunitySystem.CheckRelocationActionPlan(world.CommunityRelocationWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteRelocationAction(world.CommunityRelocationWorld, plan); return true; });

        internal static readonly ActionDefinition<HouseholdResumeChoice, HouseholdResumeActionPlan> HouseholdResume = new(
            DecodeReferenceChoice(HouseholdResumeChoice.Decode), static choice => choice.Encode(), HouseholdCommunityCapabilities,
            static (world, actor, choice, _) => CommunitySystem.PlanHouseholdResume(world.CommunityWorkWorld, actor, choice),
            static (world, plan, policy) => CommunitySystem.CheckHouseholdResumePlan(world.CommunityWorkWorld, plan, policy),
            static (world, plan, _, _) => { CommunitySystem.CompleteHouseholdResume(world.CommunityWorkWorld, plan); return true; });
    }

    private static ActorCapabilityPolicy HouseGuestCapabilities(bool offeredChoice) =>
        new(ActionLifeStages.Adults, AllowBoat: !offeredChoice,
            AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

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

    private static GoodsRequest DepartureAllowanceRequest(string actor, string household) =>
        new(GoodsUse.DepartureAllowance, actor, GoodsOwners.One(household), EdibleFoodGoods);

    private List<DepartureAllowance> PlanDepartureAllowance(string actor, string household)
    {
        var result = new List<DepartureAllowance>();
        var remaining = 2;
        foreach (var match in FindGoods(DepartureAllowanceRequest(actor, household)).Matches)
        {
            var quantity = Math.Min(remaining, match.Quantity);
            if (quantity <= 0) continue;
            result.Add(new(match.Lot, quantity));
            remaining -= quantity;
            if (remaining == 0) break;
        }
        return result;
    }

    private void AddHousingCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        AddHousingCandidateOptions(options, actor);
        foreach (var candidate in options)
        {
            if (HousingChoice.Decode(candidate.Id) is not { } choice) { candidates.Add(candidate); continue; }
            if (global::ClankerWorld.Simulation.World.WorldRuntime.CommunityActions.Housing.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
        }
    }

    private void AddLearningCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        CommunitySystem.AddLearningCandidateOptions(this.CommunityLearningWorld, options, actor);
        foreach (var candidate in options)
            if (LearningChoice.Decode(candidate.Id) is { } choice && global::ClankerWorld.Simulation.World.WorldRuntime.CommunityActions.Learning.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
    }

    internal sealed record PersonalCollectionEffect(string MoveId, string ItemKind, int Quantity);

    private PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = int.MaxValue)
    {
        var plan = PlanPersonalGoods(actor, new(PersonalGoodsKind.Collect, lotId), maximumQuantity, PersonalGoodsMode.Native);
        return ApplyPersonalGoods(plan) is { } effect ? new(effect.MoveId, effect.ItemKind, effect.Quantity) : null;
    }
    internal sealed record PersonalStorageEffect(string MoveId, string ItemKind, int Quantity);

    private IEnumerable<InventoryLot> PersonalStorageLots(string actor, string houseId) =>
        SpendingGoods(actor, GoodsUse.StorePersonal, [actor]).Select(match => match.Lot)
            .Where(lot => !IsEdibleFood(lot.ItemKind) &&
            VesselFits(lot, DestinationRoom(houseId))).OrderBy(lot => lot.Id, StringComparer.Ordinal);

    private PersonalStorageEffect? StorePersonalGoods(string actor, string lotId, int maximumQuantity = int.MaxValue,
        string? requiredHouseId = null, string? requiredHouseholdId = null, GridPoint? requiredPosition = null)
    {
        var plan = PlanPersonalGoods(actor, new(PersonalGoodsKind.Store, lotId), maximumQuantity,
            requiredHouseId is null ? PersonalGoodsMode.Native : PersonalGoodsMode.BoundOrder,
            requiredHouseId, requiredHouseholdId, requiredPosition);
        return ApplyPersonalGoods(plan) is { } effect ? new(effect.MoveId, effect.ItemKind, effect.Quantity) : null;
    }
    internal sealed record BorrowedReturnEffect(string MoveId, string ItemKind, int Quantity);

    private IEnumerable<InventoryLot> ReturnableBorrowedGoods(string actor) => CarriedBorrowedGoods(actor).Where(lot =>
        lot.DeliveryBuildingId is null && !PersonalEquipmentRules.IsSelected(agents[actor].Equipment, lot.Id) &&
        PhysicalUnreservedQuantity(lot) > 0 &&
        (!InventoryContainerRules.IsContainer(lot.ItemKind) || !HasActiveContainerReservation(society.Checkpoint.Inventory, lot.Id)));

    private BorrowedReturnEffect? ReturnBorrowedGoods(string actor, string lotId, int maximumQuantity = int.MaxValue,
        string? requiredHouseId = null, string? requiredHouseholdId = null, GridPoint? requiredPosition = null)
    {
        var plan = PlanPersonalGoods(actor, new(PersonalGoodsKind.Return, lotId), maximumQuantity,
            requiredHouseId is null ? PersonalGoodsMode.Native : PersonalGoodsMode.BoundOrder,
            requiredHouseId, requiredHouseholdId, requiredPosition);
        return ApplyPersonalGoods(plan) is { } effect ? new(effect.MoveId, effect.ItemKind, effect.Quantity) : null;
    }

    private PersonalGoodsActionPlan? PlanPersonalGoods(string actor, PersonalGoodsChoice choice, int maximum = int.MaxValue,
        PersonalGoodsMode mode = PersonalGoodsMode.Choice, string? requiredHouse = null,
        string? requiredOwner = null, GridPoint? requiredPosition = null) =>
        CommunitySystem.PlanPersonalGoods(CommunityGoodsWorld, actor, choice, maximum, mode, requiredHouse, requiredOwner, requiredPosition);

    private (string MoveId, string ItemKind, int Quantity)? ApplyPersonalGoods(PersonalGoodsActionPlan? plan)
    {
        if (plan is null) return null;
        if (CheckActionPlan(plan).Refusal is { } refusal)
        {
            if (plan.Mode == PersonalGoodsMode.Choice) RefuseChoice(plan, refusal);
            return null;
        }
        return CommunitySystem.ApplyCheckedPersonalGoods(this.CommunityGoodsWorld, plan);
    }
}
