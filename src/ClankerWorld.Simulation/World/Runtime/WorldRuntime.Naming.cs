using System.Globalization;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void ApplyChosenNameOutcome(
        CognitionDecisionRequest request,
        CognitionDecisionResponse response,
        CognitionAdmissionResult admission)
    {
        if (response.Provider != DecisionProviderKind.LargeLanguageModel ||
            !admission.Accepted ||
            admission.FellBack && admission.Outcome != "candidate_not_legal" &&
            !admission.Outcome.StartsWith("low_confidence:", StringComparison.Ordinal))
        {
            return;
        }

        var agentId = request.Observation.AgentId;
        var agent = society.Checkpoint.GetAgent(agentId);
        if (!agent.NeedsName)
        {
            return;
        }

        var chosenName = CognitionDecisionResponse.NormalizeChosenName(response.ChosenName);
        var chosenKey = chosenName is null ? null : AgentNameRules.CanonicalKey(chosenName);
        if (chosenName is null || chosenKey is null)
        {
            CloseNameRequest(agentId);
            AppendEvent(new AgentNameUnavailableEventPayload(agentId));
            return;
        }

        var duplicate = AgentNameRules.IsTaken(society.Checkpoint, agentId, chosenName);
        if (!duplicate)
        {
            if (!AgentNameRules.IsAllowedChildName(society.Checkpoint, agentId, chosenName))
            {
                CloseNameRequest(agentId);
                AppendEvent(new AgentNameUnavailableEventPayload(agentId));
                return;
            }
            society.Apply(checkpoint => SocietyRules.RenameAgent(checkpoint, agentId, chosenName));
            AppendEvent(new AgentNamedEventPayload(agentId));
            return;
        }

        AppendEvent(new AgentNameRejectedEventPayload(agentId));
        if (request.Observation.IsNameRetry)
        {
            CloseNameRequest(agentId);
            AppendEvent(new AgentNameRetryExhaustedEventPayload(agentId));
            return;
        }

        var queued = society.EnqueueCognition(new SocietyCognitionScheduleEntry(
            $"name-retry:{request.RequestId}",
            agentId,
            CognitionSystem.PriorityFor(CognitionCandidatesWorld, agents[agentId]),
            WorldTick,
            [SocietyCognitionScheduler.NameRetryTriggerId],
            request.Observation with { IsNameRetry = false }));
        if (queued)
        {
            AppendEvent(new AgentNameRetryRequestedEventPayload(agentId));
            return;
        }

        // The original queue entry has just been consumed, so this is only a
        // defensive bound for an unexpectedly full or unavailable queue.
        CloseNameRequest(agentId);
        AppendEvent(new AgentNameRetryUnavailableEventPayload(agentId));
    }

    private void CloseNameRequest(string agentId) =>
        society.Apply(checkpoint => SocietyRules.CloseNaming(checkpoint, agentId));

    private void CloseUnresolvedNameRetry(CognitionDecisionRequest request)
    {
        if (!request.Observation.IsNameRetry)
        {
            return;
        }

        var agentId = request.Observation.AgentId;
        var agent = society.Checkpoint.GetAgent(agentId);
        if (!agent.NeedsName)
        {
            return;
        }

        CloseNameRequest(agentId);
        AppendEvent(new AgentNameRetryUnusableEventPayload(agentId));
    }

    private void ApplyPersonalIdentityChoice(string id, CognitionDecisionRequest request,
        CognitionDecisionResponse? response, CognitionAdmissionResult admission) =>
        CognitionSystem.ApplyPersonalIdentityChoice(CognitionPersonalIdentityWorld, id, request, response, admission);

    // Named production factories let the native inventory exercise the actual
    // generators. DerivedId alone owns their bound and hashing policy.
    private static string MedicalDoseId(long tick, long eventId, string caregiver, string patient) =>
        DerivedId.Create($"medical-dose:{tick}:{eventId}:{caregiver}:{patient}");
    private static string FoodHarvestLotId(long tick, string actor) => DerivedId.Create($"food:harvest:{tick:D10}:{actor}", "food:harvest:sha256:");
    private static string OrchardSeedLotId(long tick, string actor) => DerivedId.Create($"orchard-seed:{tick:D10}:{actor}");
    private static string MaterialLotId(long tick, string actor) => DerivedId.Create($"material:{tick}:{actor}");
    private static string TreeSeedLotId(long tick, string actor) => DerivedId.Create($"tree-seed:{tick}:{actor}");
    private static string UsedToolLotId(string lotId, long tick, string actor) => DerivedId.Create($"{lotId}:use:{tick}:{actor}");
    private static string RepairedToolLotId(string lotId, long tick, string actor) => DerivedId.Create($"{lotId}:repair:{tick}:{actor}");
    private static string ToolRepairReservationId(long tick, string actor, string repairedId, string material) =>
        DerivedId.Create($"equipment-repair:{tick}:{actor}:{repairedId}:{material}");
    private static string ToolRepairGiftId(long tick, string actor, string repairedId, string material) =>
        DerivedId.Create($"equipment-repair-gift:{tick}:{actor}:{repairedId}:{material}");
    private static string FieldPlantingReservationId(GridPoint position, int cycle, long tick, string worker) =>
        DerivedId.Create($"{FarmFieldRules.FieldId(position)}:plant:{cycle}:{tick}:{worker}");
    private static string FieldToolLotId(string lotId, GridPoint position, long tick, string worker) =>
        DerivedId.Create($"{lotId}:field:{FarmFieldRules.FieldId(position)}:{tick}:{worker}");
    private static string FieldHarvestId(GridPoint position, int cycle, string kind) =>
        DerivedId.Create($"{FarmFieldRules.FieldId(position)}:harvest:{cycle}:{kind}");
    private static string EquipmentTransferId(long tick, string actor, string lotId) => DerivedId.Create($"equipment:{tick}:{actor}:{lotId}");
    private static string EquippedUnitId(long tick, string actor, string lotId) => DerivedId.Create($"equipped:{tick}:{actor}:{lotId}");
    private static string EquipmentRepairReservationId(long tick, string actor, string material) => DerivedId.Create($"repair:{tick}:{actor}:{material}");
    private static string OrnamentUnitId(long tick, long eventId, string actor, string lotId) =>
        DerivedId.Create($"ornament-unit:{tick}:{eventId}:{actor}:{lotId}");
    private static string ProductionInputReservationId(string purpose, int quantityIndex, string lotId) =>
        DerivedId.Create($"{purpose}:quantity:{quantityIndex}:lot:{lotId}");
    private static string ProductionOutputLotId(string jobId, int outputIndex) =>
        DerivedId.Create($"{jobId}:output:{outputIndex.ToString("D2", CultureInfo.InvariantCulture)}");
    private static string ProductionJobId(long sequence) =>
        DerivedId.Create($"production-{sequence.ToString("D10", CultureInfo.InvariantCulture)}");
    private static string ProductionVesselUnitId(string outputLotId, int unitIndex) =>
        DerivedId.Create($"{outputLotId}:unit:{unitIndex.ToString("D2", CultureInfo.InvariantCulture)}");
    private static string ExpansionMaterialReservationId(string jobId, int ordinal) => DerivedId.Create($"{jobId}:material:{ordinal}");
    private static string ExpansionJobId(long sequence) => DerivedId.Create($"expansion-{sequence:D8}");
    private static string TreePlantingReceipt(string treeId) => DerivedId.Create("tree:plant:" + treeId);
    private static string ParenthoodRequestId(string actor, long requestedTick) => DerivedId.Create($"family:{actor}:{requestedTick}");
    private static string NewPartnershipId(string actor, string target, long tick) => DerivedId.Create($"partnership:{actor}:{target}:{tick}");
    private static string NewCareRelationshipId(string actor, string target, long tick) => DerivedId.Create($"care-relationship:{actor}:{target}:{tick}");
    private static string SoloHouseholdId(string actor, long tick) => DerivedId.Create($"household:solo:{actor}:{tick}");
    private static string DepartureTransferId(string actor, long tick, int ordinal, string lot) => DerivedId.Create($"departure:{actor}:{tick}:{ordinal}:{lot}");
    private static string PersonalMoveId(string actor, long tick, string lot) => DerivedId.Create($"personal:{actor}:{tick}:{lot}");
    private static string ChildSocialMemoryId(string kind, string actor, string target, long tick) =>
        DerivedId.Create($"child-social:{kind}:{actor}:{target}:{tick}", $"child-social:{kind}:sha256:");
    private static string LessonGratitudeMemoryId(string student, string teacher, SkillKind skill) =>
        DerivedId.Create($"lesson-gratitude:{student}:{teacher}:{skill}", "lesson-gratitude:sha256:");
    private static string ProjectGratitudeMemoryId(string requester, string helper) =>
        DerivedId.Create($"project-gratitude:{requester}:{helper}", "project-gratitude:sha256:");
    private static string TrustMemoryId(string owner, string subject) =>
        DerivedId.Create($"trust-memory:{owner}:{subject}", "trust-memory:sha256:");
    private static string CartBuildMaterialTransferId(long tick, string actor) => DerivedId.Create($"cart-material:{tick}:{actor}");
    private static string CartRepairReservationId(long tick, string actor, string material) => DerivedId.Create($"cart-repair:{tick}:{actor}:{material}");
    private static string NativeCartRepairId(long tick, string actor, string cart) => DerivedId.Create($"repair-handcart:{tick}:{actor}:{cart}");
    private static string FoodPotTransferId(long tick, string actor) => DerivedId.Create($"food-pot:{tick}:{actor}");
    private static string RoadRouteId(string town, string building) => DerivedId.Create($"road:{town}:{building}");
    private static string DirectBarterOfferId(long tick, string actor, string other) => DerivedId.Create($"{(global::ClankerWorld.Simulation.World.MarketSystem.DirectBarterPrefix)}{tick}:{actor}:{other}");
    private static string ShopTradeOfferId(long tick, string actor, string building) => DerivedId.Create($"{ShopTradePrefix}{tick}:{actor}:{building}");
    private static string WritingProjectId(string actor, long tick, int reservationCount) =>
        DerivedId.Hash($"{actor}|{tick}|{reservationCount}", "knowledge-work-");
    private static string WritingMaterialReservationId(string project, int ordinal) => DerivedId.Create($"{project}:{ordinal}");
    private static string WritingMaterialTransferId(long tick, string actor, string kind) => DerivedId.Create($"knowledge-material:{tick}:{actor}:{kind}");
    private static string ConductSeenId(string conduct, string observer) => DerivedId.Create($"{conduct}:seen:{observer}");
    private static string ConductInspectionId(string conduct, string file, string actor) => DerivedId.Create($"{conduct}:inspect:{file}:{actor}");
    private static string ConductRelayId(string conduct, long tick, string source, string recipient) => DerivedId.Create($"{conduct}:relay:{tick}:{source}:{recipient}");
    private static string NativeReturnId(long tick, string actor, long eventId) => DerivedId.Create($"remedy-return:{tick}:{actor}:{eventId}");
    private static string WarehouseStockId(long tick, string actor) => DerivedId.Create($"warehouse-stock:{tick}:{actor}");
    private static string RemedyEffectId(string receipt) => DerivedId.Create("effect:" + receipt);
    private static string MaterialGatherReceipt(string lot) => DerivedId.Create("gather:" + lot, "gather:material:sha256:");
    private static string FoodHarvestReceipt(string lot) => DerivedId.Create("harvest:" + lot);
    private static string ConsumptionOrderReceipt(long tick, string actor, string lot) => DerivedId.Hash($"consume:{tick:D10}:{actor}:{lot}", "consume:");
    private static string FieldWorkOrderReceipt(string actor, long tick, GridPoint position, FarmWorkKind kind, int cycle) =>
        DerivedId.Hash($"{actor}:{tick}:{FarmFieldRules.FieldId(position)}:{kind}:{cycle}", "field:work:");
    private static string ToolRepairReceipt(string actor, long tick, string lot) => DerivedId.Hash($"{actor}:{tick}:{lot}", "repair:tool:");
    private static string EquipmentRepairReceipt(string actor, long startedTick, string lot) => DerivedId.Hash($"{actor}:{startedTick}:{lot}", "repair:equipment:");
    private static string PersonalCollectionReceipt(string move) => DerivedId.Hash(move, "collect:personal:");
    private static string PersonalStorageReceipt(string move) => DerivedId.Hash(move, "store:personal:");
    private static string BorrowedReturnReceipt(string move) => DerivedId.Hash(move, "return:borrowed:");
    private static string StockDeliveryReceipt(string operation) => DerivedId.Hash(operation, "delivery:stock:");
    private static string RenameMemoryId(string agent, long eventId) => DerivedId.Hash(agent, "player-rename:" + eventId.ToString("D20", CultureInfo.InvariantCulture) + ":");
    private static string AnimalSupplyId(string lot, string yard, string? animal, string? action) =>
        DerivedId.Hash($"{lot}:{yard}:{animal}:{action}", "animal:supply:", 24);
    private static string AnimalActionId(string action, string animal, string? other) => DerivedId.Hash($"{animal}:{other}", "animal:" + action + ":", 24);
    private static string AnimalAgreementId(string action, string animal, string other, string? lot) =>
        DerivedId.Hash(JsonSerializer.Serialize(new[] { action, animal, other, lot }), "animal:agreement:", 24);
    private static string AnimalHideId(string animal) => DerivedId.Hash(animal, "animal-hide-", 24);
    private static string AnimalProductId(string animal, long tick) => DerivedId.Hash($"{animal}:{tick}", "animal-product-", 24);
    private static string AnimalBornId(string mother, long startedTick) => DerivedId.Hash($"{mother}:{startedTick}", "animal-born-", 24);
    private static string AnimalHerdId(string household) => DerivedId.Create("household:" + household);
    private static string AnimalProductReservationId(string product) => DerivedId.Create(product + "-held");
    private static string AnimalSaddleOperationId(string animal, long tick, long eventId) => DerivedId.Hash($"{animal}:{tick}:{eventId}", "fit-saddle-", 24);
    private static string AnimalSaddleReservationId(string operation) => DerivedId.Create(operation + "-held");
    private static string AnimalSupplyTripId(string actor) => DerivedId.Hash(actor, "animal:supply_trip:", 24);
    private static string AnimalPickupId(string actor, long tick, long eventId) => DerivedId.Hash($"{actor}:{tick}:{eventId}", "animal-pickup-", 24);
    private static string AnimalOrderReceiptId(string instruction, string animal, string? completion) =>
        DerivedId.Hash($"{instruction}:{animal}:{completion}", "animal-order:", 24);
    private static string AnimalHideTransferId(string actor, string animal, long tick, bool carry) =>
        DerivedId.Hash($"{actor}:{animal}:{tick}", carry ? "carry-wild-hide-" : "collect-wild-hide-", 24);
    private static string AnimalInputReservationId(string actor, long tick, long eventId, string purpose, int index) =>
        DerivedId.Hash($"{actor}:{tick}:{eventId}:{purpose}:{index}", "animal-input-", 24);
    private static string AnimalCargoDismountId(string rider, long tick, long eventId, string lot) =>
        DerivedId.Hash($"{rider}:{tick}:{eventId}:{lot}", "dismount-cargo-", 24);
    private static string AnimalOfferId(string actor, string animal, long tick, long eventId) =>
        DerivedId.Hash($"{actor}:{animal}:{tick}:{eventId}", "animal-offer-", 24);
    private static string AutomaticBuildingId(string agent, BuildingDefinition definition) =>
        agent.All(character => char.IsLower(character) || char.IsDigit(character) || character is '.' or '-' or '_')
            ? DerivedId.Create($"build-{agent}-{definition.PackageDigest[7..15]}-{definition.LocalId}", "build-sha256-")
            : DerivedId.Hash(agent + "\n" + definition.CanonicalId, "build-v2-");
    private static string ReplacementBuildingId(string original, int replacement) =>
        DerivedId.Hash(original + "\n" + replacement.ToString(CultureInfo.InvariantCulture), "build-replacement-");
    private static string TownSalvageOperationId(long tick, string actor, string candidate) =>
        DerivedId.Hash(actor + "|" + candidate, $"salvage:{tick}:");
    private static string DeveloperVesselUnitId(string source, int ordinal) => DerivedId.Create(source + ":" + ordinal);
    private static string BoatTripId(string town, string origin, string destination) =>
        new BoatTripActionChoice(town, origin, destination).Encode();
    private static string NonLandRemedyChoiceId(string town, string agreement, string term) =>
        new NonLandWorkChoice(town, agreement, term).Encode();
    private static string NonLandRelayChoiceId(string town, string act, string listener) =>
        new NonLandReportRelayChoice(town, act, listener).Encode();

    /// <summary>The name and its private permanent memory publish together, including for married spouses.</summary>
    private SocietyOperationResult RenameFromPlayer(SocietyCheckpoint checkpoint, string agentId, string name)
    {
        var renamed = SocietyRules.RenameAgent(checkpoint, agentId, name);
        if (renamed.NewEvents is not { Count: > 0 }) return renamed;
        var identity = renamed.Checkpoint.GetAgent(agentId);
        // The saved world-event ordinal distinguishes same-tick renames and survives setup undo and reload.
        var id = RenameMemoryId(agentId, nextEventId);
        var day = CivicDayNumber(checkpoint.WorldTick).ToString(CultureInfo.InvariantCulture);
        var recorded = SocietyRules.RecordSocialMemory(renamed.Checkpoint,
            new(Identity.Parse(id), agentId, agentId, $"I was renamed on day {day} to {identity.Name}.", "private", checkpoint.WorldTick, Permanent: true));
        return new(recorded.Checkpoint, NewEvents: [.. renamed.NewEvents, .. recorded.NewEvents ?? []]);
    }

    public IReadOnlyList<AgentMarriage> Marriages => marriages.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();

    private int FindMarriageIndex(Predicate<AgentMarriage> predicate) => conversationSystem.MarriageIndex(predicate);

    private void SetMarriageAt(int index, AgentMarriage marriage) => conversationSystem.SetMarriageAt(index, marriage);

    private bool CanProposeMarriage(AgentConversation conversation) =>
        conversationSystem.CanProposeMarriage(conversationWorld, conversation);

    private void MaintainMarriages() =>
        conversationSystem.MaintainMarriages(conversationWorld);

    private SocietyOperationResult RenameSpouses(AgentMarriage marriage, string agentId, string name, bool fromPlayer = false) =>
        ConversationSystem.RenameSpouses(conversationWorld, marriage, agentId, name, fromPlayer);
}
