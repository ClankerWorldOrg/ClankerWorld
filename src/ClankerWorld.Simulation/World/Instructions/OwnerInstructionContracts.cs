using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// The two owner-to-agent instruction strengths supported by the world. The request never carries an instruction ID: that ID is minted by
/// the authoritative runtime after validation.
/// </summary>
public enum OwnerInstructionKind
{
    Suggestion,
    Order,
}

/// <summary>
/// The durable ingress state of an owner instruction. Typed order progress
/// records the subsequent task lifecycle.
/// </summary>
public enum OwnerInstructionState
{
    Queued,
}

/// <summary>
/// An untrusted client request to queue a human instruction. The idempotency
/// key is client supplied; every other durable identifier is server minted.
/// </summary>
public sealed record OwnerInstructionRequest(
    string IdempotencyKey,
    string IssuerId,
    string TargetAgentId,
    OwnerInstructionKind Kind,
    string Text,
    bool Queue = false);

/// <summary>
/// Authoritative progress for the small set of order tasks currently supported
/// by the world simulation. The original instruction text remains on
/// its own message record; this typed state is the physical task the host understood.
/// </summary>
public sealed record OrderTask(
    string Action,
    string Status,
    int RequestedUnits,
    int CompletedUnits,
    string ProgressUnit,
    bool RepeatUntilCancelled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool QuantityIsExplicit = false,
    string? TargetFoodKind = null,
    string? TargetResourceId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetPosition = null,
    string? BlockedReason = null,
    string? LastEffectId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool WaitForDecisionAfterFailure = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetAgentId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetMaterialKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetEquipmentKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetCropKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetRecipeId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetOutputKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProductionBuildingId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProductionJobId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? ProductionProjectStartedTick = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetItemKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageBuildingId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetStorageOwnerId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? TargetStoragePosition = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetLotId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DeliveryPurpose = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetBuildingKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DeliveryRoute = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DeliveryLotId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DeliveryQuantity = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetDefinitionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ConstructionOwnerId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? ConstructionPosition = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? ConstructionStartedTick = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ConstructionInstanceId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OwnerBuildingExpansionBinding? ExpansionBinding = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OwnerShelterBinding? ShelterBinding = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OwnerShelterCompletion? ShelterCompletion = null)
{
    private OrderStatus status = OrderStatusRules.Parse(Status);

    // The string is the existing save and observation spelling. The enum is
    // the task's authority, and only Transition can change it after creation.
    public string Status
    {
        get => status.ToWireValue();
        private init => status = OrderStatusRules.Parse(value);
    }

    [JsonIgnore] public OrderStatus LifecycleStatus => status;
    [JsonIgnore] public bool IsTerminal => status.IsTerminal();

    public OrderTask Transition(OrderStatus next, string? reason = null, bool waitForDecision = false)
    {
        if (!Enum.IsDefined(next)) throw new ArgumentOutOfRangeException(nameof(next));
        if (IsTerminal && next != status)
            throw new InvalidOperationException("A closed order cannot be reopened.");
        if (next == OrderStatus.Blocked && string.IsNullOrWhiteSpace(reason) ||
            waitForDecision && next != OrderStatus.Blocked)
            throw new ArgumentException("A blocked order needs a reason; only a blocked order can wait for a new decision.");
        return this with
        {
            Status = next.ToWireValue(),
            BlockedReason = reason,
            WaitForDecisionAfterFailure = waitForDecision,
        };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? TargetAnimalId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public OwnerBoatTravelBinding? BoatTravel { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? TargetKnowledgeKind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? KnowledgeWritingProjectId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? KnowledgeCopySourceArtifactId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? TargetCartLotId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? TalkConversationId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? TalkOutcome { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? TargetKnowledgeArtifactId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public OwnerKnowledgeReadCompletion? KnowledgeReadCompletion { get; init; }
}

public sealed record OwnerBoatTravelBinding(string DestinationPortId, string? RequestId = null);

public sealed record OwnerKnowledgeReadCompletion(long WorldTick, IReadOnlyList<GridPoint> LearnedSites)
{
    [JsonRequired] public IReadOnlyList<string> LearnedRecipes { get; init; } = [];
}

public sealed record OwnerShelterBinding(
    string Kind,
    GridPoint Position,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? BuildingInstanceId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DefinitionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OwnerId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? BuildingPosition = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? BuildingPlacedTick = null);

public sealed record OwnerShelterCompletion(
    long WorldTick,
    GridPoint Position,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FuelReservationId = null);

public sealed record OwnerBuildingExpansionBinding(
    string BuildingInstanceId,
    string DefinitionId,
    string OwnerId,
    GridPoint ExpectedPosition,
    int ExpectedRevision,
    GridPoint TargetPosition,
    BuildingFootprintRevision TargetFootprint,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? JobId = null);

public sealed record OwnerOrderCancelRequest(
    string IdempotencyKey,
    string IssuerId,
    string WorldId,
    string TargetAgentId,
    string OrderId);

public sealed record OwnerOrderControlReceipt(
    string OrderId,
    string Status,
    bool Changed,
    long WorldTick,
    long LatestEventId);

public sealed record OwnerOrderCancellation(
    string IdempotencyKey,
    string IssuerId,
    string WorldId,
    string TargetAgentId,
    string OrderId,
    OwnerOrderControlReceipt Receipt);

/// <summary>
/// The authoritative queued record retained by the world runtime.
/// </summary>
public sealed record OwnerQueuedInstruction(
    string InstructionId,
    string IdempotencyKey,
    string IssuerId,
    string TargetAgentId,
    OwnerInstructionKind Kind,
    string Text,
    long SubmittedTick,
    long RunEpoch,
    long SubmissionSequence,
    OwnerInstructionState State,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? ObservedTick = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ObserverReply = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? GuidancePromptedTick = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Queue = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OrderTask? Task = null)
{
    public const int RecentClosedOrderLimitPerAgent = 6;
    public const int MaximumTextLength = 512;
    public const int MaximumIdentifierLength = DerivedId.MaximumLength;
}

/// <summary>
/// A stable reply to an instruction submission. Repeating the same request
/// with its idempotency key returns this exact receipt and does not append a
/// second event.
/// </summary>
public sealed record OwnerInstructionReceipt(
    string InstructionId,
    string IdempotencyKey,
    long SubmittedTick,
    long RunEpoch,
    long Revision);

/// <summary>Retains submission idempotency without retaining a closed task or its text.</summary>
public sealed record RecordedInstructionReceipt(OwnerInstructionReceipt Receipt, string RequestDigest);
