using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed record OrnamentChangeResult(bool Applied, string? LotId = null, string? Failure = null);

public sealed partial class WorldRuntime
{
    private const string WearOrnamentPrefix = "wear_ornament:";
    private const string RemoveOrnamentCandidate = "remove_ornament";
    private const string GiftOrnamentPrefix = "gift_ornament:";

    private static bool IsOrnamentCandidate(string candidate) => candidate == RemoveOrnamentCandidate ||
        candidate.StartsWith(WearOrnamentPrefix, StringComparison.Ordinal) ||
        candidate.StartsWith(GiftOrnamentPrefix, StringComparison.Ordinal);

    // Preserve complete lot/agent identities without delimiter ambiguity or unbounded action IDs.
    private static string OrnamentChoiceId(string prefix, params string[] identity) => DerivedId.Hash(JsonSerializer.Serialize(identity), prefix);

    private bool LivingOrnamentAdult(string actor) => agents.ContainsKey(actor) && AdultResident(actor);

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
            .Select(match => match.Lot).Where(lot => lot.ContainerLotId is null &&
                (IsPersonallyCarriedOrnament(lot, actor) || household is not null && lot.OwnerId == household &&
                    (lot.CarrierId is null || lot.CarrierId == actor) && FreeCarryCapacity(actor) > 0 &&
                    (lot.StorageBuildingId is null || worldSimulation.Buildings.Any(building =>
                        building.InstanceId == lot.StorageBuildingId && building.HouseholdId == household))))
            .OrderBy(lot => lot.OwnerId == actor ? 0 : 1).ThenBy(lot => lot.Id, StringComparer.Ordinal);
    }

    private IEnumerable<AgentState> LocalOrnamentRecipients(string actor) => agents.Values
        .Where(person => person.AgentId != actor && LivingOrnamentAdult(person.AgentId) &&
            IsWithinInteractionRange(agents[actor].Position, person.Position, ResourceInteractionRange) &&
            FreeCarryCapacity(person.AgentId) > 0)
        .OrderBy(person => person.AgentId, StringComparer.Ordinal).Take(8);

    private void AddOrnamentCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!LivingOrnamentAdult(actor) || BusyWithOrnamentIncompatibleWork(actor)) return;
        foreach (var lot in OrnamentSources(actor).Where(lot => lot.Id != agents[actor].Equipment?.OrnamentLotId).Take(8))
            candidates.Add(new(OrnamentChoiceId(WearOrnamentPrefix, lot.Id),
                $"Choose and wear an accessible {lot.ItemKind.Replace('_', ' ')}. It stays one carried item.", 180,
                lot.StorageBuildingId));
        if (agents[actor].Equipment?.OrnamentLotId is not null)
            candidates.Add(new(RemoveOrnamentCandidate, "Remove your worn ornament and keep it among your carried goods.", 181));
        var gifts = 0;
        foreach (var lot in PersonalOrnamentsToGive(actor)
                     .OrderBy(lot => lot.Id, StringComparer.Ordinal).Take(4))
            foreach (var recipient in LocalOrnamentRecipients(actor))
            {
                if (gifts++ >= 16) return;
                var name = society.Checkpoint.GetAgent(recipient.AgentId).Name;
                candidates.Add(new(OrnamentChoiceId(GiftOrnamentPrefix, lot.Id, recipient.AgentId),
                    $"Give one personally carried {lot.ItemKind.Replace('_', ' ')} to {name} nearby.", 190,
                    recipient.AgentId, name));
            }
    }

    // These aesthetic/property choices execute only through fresh personal admission.
    // Generic continuing intentions have no wear/remove/gift authority.
    private bool ApplyOrnamentDecision(SocietyCognitionDispatchResult decision)
    {
        var selected = decision.Admission.Intention;
        if (selected is null || !IsOrnamentCandidate(selected.CandidateId)) return false;
        if (PendingInstructionFor(decision.AgentId)?.Kind == OwnerInstructionKind.MustDo) return false;
        if (!decision.Admission.Accepted || decision.Admission.FellBack ||
            selected.Provider != DecisionProviderKind.LargeLanguageModel || selected.AgentId != decision.AgentId ||
            !LivingOrnamentAdult(decision.AgentId) || BusyWithOrnamentIncompatibleWork(decision.AgentId)) return true;
        var actor = decision.AgentId;
        if (selected.CandidateId == RemoveOrnamentCandidate)
        {
            _ = RemoveOrnamentCore(actor);
            return true;
        }
        if (selected.CandidateId.StartsWith(WearOrnamentPrefix, StringComparison.Ordinal))
        {
            var source = OrnamentSources(actor).FirstOrDefault(lot =>
                OrnamentChoiceId(WearOrnamentPrefix, lot.Id) == selected.CandidateId);
            if (source is null) return true;
            var person = agents[actor];
            if (source.OwnerId != actor && !IsWithinInteractionRange(person.Position, HouseholdStockPosition(source),
                    HouseholdStockInteractionRange(source)))
                MoveToward(actor, person, HouseholdStockPosition(source), "ornament", HouseholdStockInteractionRange(source));
            else _ = WearOrnamentCore(actor, source.Id);
            return true;
        }
        foreach (var lot in PersonalOrnamentsToGive(actor))
            foreach (var recipient in LocalOrnamentRecipients(actor))
                if (OrnamentChoiceId(GiftOrnamentPrefix, lot.Id, recipient.AgentId) == selected.CandidateId)
                {
                    _ = GiveOrnamentCore(actor, recipient.AgentId, lot.Id);
                    return true;
                }
        return true;
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
        var source = OrnamentSources(actor).FirstOrDefault(lot => OrnamentChoiceId(WearOrnamentPrefix, lot.Id) == candidate);
        var person = agents[actor];
        if (source is null || source.OwnerId == actor || IsWithinInteractionRange(person.Position,
                HouseholdStockPosition(source), HouseholdStockInteractionRange(source))) return;
        MoveToward(actor, person, HouseholdStockPosition(source), "ornament", HouseholdStockInteractionRange(source));
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
        var previous = person.Equipment;
        var equippedId = lot.Id;
        try
        {
            ApplyInventoryTransition(inventory =>
            {
                if (lot.OwnerId != actor)
                {
                    var operation = $"ornament-wear:{WorldTick}:{nextEventId}:{actor}:{lot.Id}";
                    inventory = InventoryAuthority.Transfer(inventory, operation, lot.OwnerId, actor, lot.Id, 1, "ornament_collected");
                    equippedId = lot.Quantity == 1 ? lot.Id : DerivedId.Create(lot.Id + "#transfer:" + operation);
                }
                else if (lot.Quantity > 1)
                {
                    equippedId = OrnamentUnitId(WorldTick, nextEventId, actor, lot.Id);
                    inventory = InventoryAuthority.SplitLot(inventory, lot.Id, 1, equippedId);
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
        AppendEvent("ornament_worn", $"{actor}:ornament:{equippedId}");
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
        var person = agents[actor];
        agents[actor] = person with { Equipment = person.Equipment! with { OrnamentLotId = null } };
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent("ornament_removed", $"{actor}:ornament:{id}");
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
        var person = agents[actor];
        var operation = $"ornament-gift:{WorldTick}:{nextEventId}:{actor}:{recipientId}:{lot.Id}";
        var receivedId = lot.Quantity == 1 ? lot.Id : DerivedId.Create(lot.Id + "#transfer:" + operation);
        try
        {
            ApplyInventoryTransition(inventory =>
            {
                var updated = InventoryAuthority.Transfer(inventory, operation, actor, recipientId, lot.Id, 1, "ornament_given");
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
        AppendEvent("ornament_given", $"{actor}:ornament_gift:{recipientId}:{lot.Id}:{receivedId}");
        return new(true, receivedId);
    }
}
