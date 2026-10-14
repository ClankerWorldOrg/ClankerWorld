using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed record OrnamentChangeResult(bool Applied, string? LotId = null, string? Failure = null);

public sealed partial class WorldRuntime
{
    private const string WearOrnamentPrefix = OrnamentChoice.WearPrefix;
    private const string RemoveOrnamentCandidate = OrnamentChoice.RemoveId;
    private const string GiftOrnamentPrefix = OrnamentChoice.GiftPrefix;

    private static bool IsOrnamentCandidate(string candidate) => candidate == RemoveOrnamentCandidate ||
        candidate.StartsWith(WearOrnamentPrefix, StringComparison.Ordinal) ||
        candidate.StartsWith(GiftOrnamentPrefix, StringComparison.Ordinal);

    private bool LivingOrnamentAdult(string actor) => agents.ContainsKey(actor) && IsAdult(actor);

    private bool BusyWithOrnamentIncompatibleWork(string actor) => agents[actor].Equipment?.Repair is not null ||
        agents[actor].Project is { Stage: not ("completed" or "cancelled") } ||
        fields.Any(field => field.Work?.WorkerId == actor);

    // Carrying is not owning: a borrowed or in-transit ornament is never the carrier's own to wear or give.
    private bool IsPersonallyCarriedOrnament(InventoryLot lot, string actor) =>
        PersonalEquipmentRules.IsOrnament(lot.ItemKind) && lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor) &&
        lot.ContainerLotId is null && lot.DeliveryBuildingId is null && AvailableLotQuantity(lot) > 0;

    private IEnumerable<InventoryLot> PersonalOrnamentsToGive(string actor) =>
        SpendingGoods(actor, GoodsUse.Spend, [actor]).Select(match => match.Lot)
            .Where(lot => IsPersonallyCarriedOrnament(lot, actor));

    private static GoodsRequest OrnamentCollectionRequest(string actor, string owner) => new(
        GoodsUse.Collect, actor, GoodsOwners.One(owner), new GoodsKinds(["gold_ornament", "diamond_ornament"]));

    private IEnumerable<InventoryLot> OrnamentSources(string actor)
    {
        if (!LivingOrnamentAdult(actor)) return [];
        var household = society.Checkpoint.GetAgent(actor).HouseholdId;
        var owners = household is not null ? new[] { actor, household } : [actor];
        return owners.SelectMany(owner => FindGoods(OrnamentCollectionRequest(actor, owner)).Matches)
            .Select(match => match.Lot).Where(lot => IsOrnamentSource(actor, lot))
            .OrderBy(lot => lot.OwnerId == actor ? 0 : 1).ThenBy(lot => lot.Id, StringComparer.Ordinal);
    }

    private IEnumerable<AgentState> LocalOrnamentRecipients(string actor) => agents.Values
        .Where(person => IsLocalOrnamentRecipient(actor, person.AgentId))
        .OrderBy(person => person.AgentId, StringComparer.Ordinal).Take(8);

    private void AddOrnamentCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!LivingOrnamentAdult(actor) || BusyWithOrnamentIncompatibleWork(actor)) return;
        foreach (var plan in OrnamentSources(actor).Where(lot => lot.Id != agents[actor].Equipment?.OrnamentLotId)
            .Select(lot => PlanOrnamentWear(actor, lot)).Where(plan => CheckOrnamentPlan(plan).Allowed).Take(8))
            candidates.Add(new(plan.CandidateId,
                $"Choose and wear an accessible {plan.ItemKind.Replace('_', ' ')}. It stays one carried item.", 180,
                society.Checkpoint.Inventory.GetLot(plan.Selected.StockId).StorageBuildingId)
            { ActionPlan = plan });
        if (agents[actor].Equipment?.OrnamentLotId is { } worn)
        {
            var plan = new RemoveOrnamentActionPlan(actor, worn);
            if (CheckOrnamentPlan(plan).Allowed)
                candidates.Add(new(RemoveOrnamentCandidate, "Remove your worn ornament and keep it among your carried goods.", 181) { ActionPlan = plan });
        }
        var recipients = LocalOrnamentRecipients(actor).ToArray();
        if (recipients.Length == 0) return;
        var gifts = 0;
        foreach (var plans in PersonalOrnamentsToGive(actor).OrderBy(lot => lot.Id, StringComparer.Ordinal)
            .Select(lot => recipients.Select(recipient => PlanOrnamentGift(actor, lot, recipient.AgentId))
                .Where(plan => CheckOrnamentPlan(plan).Allowed).ToArray())
            .Where(plans => plans.Length > 0).Take(4))
            foreach (var plan in plans)
            {
                if (gifts++ >= 16) return;
                var recipient = plan.Selected.RecipientId;
                var name = society.Checkpoint.GetAgent(recipient).Name;
                candidates.Add(new(plan.CandidateId,
                    $"Give one personally carried {plan.ItemKind.Replace('_', ' ')} to {name} nearby.", 190,
                    recipient, name)
                { ActionPlan = plan });
            }
    }

    // These aesthetic/property choices execute only through fresh personal admission.
    // Generic continuing intentions have no wear/remove/gift authority.
    private bool ApplyOrnamentDecision(SocietyCognitionDispatchResult decision)
    {
        var selected = decision.Admission.Intention;
        if (selected is null || !IsOrnamentCandidate(selected.CandidateId)) return false;
        if (PendingInstructionFor(decision.AgentId)?.Kind == OwnerInstructionKind.Order) return false;
        if (!decision.Admission.Accepted || decision.Admission.FellBack ||
            selected.Provider != DecisionProviderKind.LargeLanguageModel || selected.AgentId != decision.AgentId ||
            !LivingOrnamentAdult(decision.AgentId) || BusyWithOrnamentIncompatibleWork(decision.AgentId)) return true;
        var actor = decision.AgentId;
        var plan = decision.ActionPlan as OrnamentActionPlan ?? CurrentOrnamentPlan(actor, selected.CandidateId);
        if (plan is not null) ApplyOrnamentPlan(plan);
        return true;
    }

    private OrnamentActionPlan? CurrentOrnamentPlan(string actor, string candidate)
    {
        if (candidate == RemoveOrnamentCandidate && agents[actor].Equipment?.OrnamentLotId is { } worn)
            return new RemoveOrnamentActionPlan(actor, worn);
        if (candidate.StartsWith(WearOrnamentPrefix, StringComparison.Ordinal))
        {
            var stock = OrnamentSources(actor).FirstOrDefault(lot => new WearOrnamentChoice(lot.Id).Encode() == candidate);
            return stock is null ? null : PlanOrnamentWear(actor, stock);
        }
        foreach (var lot in PersonalOrnamentsToGive(actor))
            foreach (var recipient in LocalOrnamentRecipients(actor))
                if (new GiftOrnamentChoice(lot.Id, recipient.AgentId).Encode() == candidate)
                    return PlanOrnamentGift(actor, lot, recipient.AgentId);
        return null;
    }

    /// <summary>
    /// Putting an ornament on needs a fresh admitted choice; only the walk toward the household
    /// stock that holds it continues between model turns.
    /// </summary>
    private void ContinueOrnamentWalk(string actor, string candidate)
    {
        if (!candidate.StartsWith(WearOrnamentPrefix, StringComparison.Ordinal) || !LivingOrnamentAdult(actor) ||
            BusyWithOrnamentIncompatibleWork(actor) || NeedsUrgentFood(agents[actor]) ||
            NeedsUrgentWarmth(agents[actor])) return;
        if (CurrentOrnamentPlan(actor, candidate) is not WearOrnamentActionPlan plan) return;
        if (CheckOrnamentPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        var person = agents[actor];
        var stock = society.Checkpoint.Inventory.GetLot(plan.Selected.StockId);
        if (PersonalEquipmentRules.IsCarried(stock, actor) || IsWithinInteractionRange(person.Position, plan.Position, plan.InteractionRange)) return;
        MoveToward(actor, person, plan.Position, "ornament", plan.InteractionRange);
    }

    public OrnamentChangeResult WearOrnament(string actor, string lotId)
    {
        gate.Wait();
        try { return WearOrnamentCore(actor, lotId); }
        finally { gate.Release(); }
    }

    private OrnamentChangeResult WearOrnamentCore(string actor, string lotId)
    {
        if (!LivingOrnamentAdult(actor)) return new(false, Failure: "A living adult must choose the ornament.");
        var person = agents[actor];
        if (person.Equipment?.OrnamentLotId == lotId) return new(false, Failure: "This ornament is already worn.");
        var lot = OrnamentSources(actor).FirstOrDefault(item => item.Id == lotId);
        if (lot is null) return new(false, Failure: "Choose a usable, unreserved ornament you carry or your household holds.");
        if (lot.OwnerId != actor && !IsWithinInteractionRange(person.Position, HouseholdStockPosition(lot),
                HouseholdStockInteractionRange(lot)))
            return new(false, Failure: "Walk to the ornament's actual household stock first.");
        if (RecheckGoods(OrnamentCollectionRequest(actor, lot.OwnerId), lot.Id) is not { } match)
            return new(false, Failure: "Choose a usable, unreserved ornament you carry or your household holds.");
        lot = match.Lot;
        return CompleteOrnamentWear(actor, lot);
    }

    private OrnamentChangeResult CompleteOrnamentWear(string actor, InventoryLot lot)
    {
        var person = agents[actor];
        var previous = person.Equipment;
        var equippedId = lot.Id;
        try
        {
            ApplyInventoryTransition(inventory =>
            {
                if (lot.OwnerId != actor)
                {
                    var operation = $"ornament-wear:{WorldTick}:{nextEventId}:{actor}:{lot.Id}";
                    inventory = InventoryAuthority.Transfer(inventory, operation, lot.OwnerId, actor, lot.Id, 1, "ornament_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor));
                    equippedId = lot.Quantity == 1 ? lot.Id : DerivedId.Create(lot.Id + "#transfer:" + operation);
                }
                else if (lot.Quantity > 1)
                {
                    equippedId = OrnamentUnitId(WorldTick, nextEventId, actor, lot.Id);
                    inventory = InventoryAuthority.SplitLot(inventory, lot.Id, 1, equippedId, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                }
                agents[actor] = person with
                { Equipment = (previous ?? new PersonalEquipment()) with { OrnamentLotId = equippedId } };
                return inventory;
            });
        }
        catch
        {
            agents[actor] = person;
            throw;
        }
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new OrnamentWornEventPayload(actor, equippedId));
        return new(true, equippedId);
    }

    public OrnamentChangeResult RemoveOrnament(string actor)
    {
        gate.Wait();
        try { return RemoveOrnamentCore(actor); }
        finally { gate.Release(); }
    }

    private OrnamentChangeResult RemoveOrnamentCore(string actor)
    {
        if (!LivingOrnamentAdult(actor) || agents[actor].Equipment?.OrnamentLotId is not { } id)
            return new(false, Failure: "This adult is not wearing an ornament.");
        return CompleteOrnamentRemoval(actor, id);
    }

    private OrnamentChangeResult CompleteOrnamentRemoval(string actor, string id)
    {
        var person = agents[actor];
        agents[actor] = person with { Equipment = person.Equipment! with { OrnamentLotId = null } };
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new OrnamentRemovedEventPayload(actor, id));
        return new(true, id);
    }

    public OrnamentChangeResult GiveOrnament(string actor, string recipientId, string lotId)
    {
        gate.Wait();
        try { return GiveOrnamentCore(actor, recipientId, lotId); }
        finally { gate.Release(); }
    }

    private OrnamentChangeResult GiveOrnamentCore(string actor, string recipientId, string lotId)
    {
        if (!LivingOrnamentAdult(actor) || !LocalOrnamentRecipients(actor).Any(person => person.AgentId == recipientId))
            return new(false, Failure: "Choose a nearby living adult with room to carry the gift.");
        var lot = RecheckGoods(new(GoodsUse.Spend, actor, GoodsOwners.One(actor),
            new GoodsKinds(["gold_ornament", "diamond_ornament"])), lotId)?.Lot;
        if (lot is not null && !IsPersonallyCarriedOrnament(lot, actor)) lot = null;
        if (lot is null) return new(false, Failure: "The giver must personally carry one usable, unreserved ornament.");
        if (FreeCarryCapacity(recipientId) < 1) return new(false, Failure: "The recipient has no room to carry the gift.");
        return CompleteOrnamentGift(actor, recipientId, lot);
    }

    private OrnamentChangeResult CompleteOrnamentGift(string actor, string recipientId, InventoryLot lot)
    {
        var person = agents[actor];
        var operation = $"ornament-gift:{WorldTick}:{nextEventId}:{actor}:{recipientId}:{lot.Id}";
        var receivedId = lot.Quantity == 1 ? lot.Id : DerivedId.Create(lot.Id + "#transfer:" + operation);
        try
        {
            ApplyInventoryTransition(inventory =>
            {
                var updated = InventoryAuthority.Transfer(inventory, operation, actor, recipientId, lot.Id, 1, "ornament_given", holdAccess: InventoryHoldRules.BorrowedBy(actor));
                if (person.Equipment?.OrnamentLotId == lot.Id)
                    agents[actor] = person with { Equipment = person.Equipment with { OrnamentLotId = null } };
                return updated;
            });
        }
        catch
        {
            agents[actor] = person;
            throw;
        }
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new OrnamentGivenEventPayload(actor, recipientId, lot.Id, receivedId));
        return new(true, receivedId);
    }
}
