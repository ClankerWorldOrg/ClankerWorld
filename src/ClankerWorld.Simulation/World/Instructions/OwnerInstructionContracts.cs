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
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrderTask(
    OrderTaskDefinition Definition,
    string Status,
    int RequestedUnits,
    int CompletedUnits,
    string ProgressUnit,
    bool RepeatUntilCancelled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool QuantityIsExplicit = false,
    string? BlockedReason = null,
    Identity? LastEffectId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool WaitForDecisionAfterFailure = false)
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

    public OrderTask UpdateDefinition(Func<OrderTaskDefinition, OrderTaskDefinition> update) =>
        this with { Definition = update(Definition) };

    internal OrderTask UpdateTypedDefinition<TDefinition>(
        Func<TDefinition, TDefinition> update) where TDefinition : OrderTaskDefinition =>
        this with
        {
            Definition = Definition is TDefinition task ? update(task) :
                throw new InvalidOperationException("This order kind does not own the requested fields."),
        };

    [JsonIgnore] public string Action => Definition.Action;

    [JsonIgnore]
    public string? TargetFoodKind => Definition switch
    {
        ConsumeFoodOrderDefinition task => task.TargetFoodKind,
        SeekFoodOrderDefinition task => task.TargetFoodKind,
        HarvestFoodOrderDefinition task => task.TargetFoodKind,
        CollectFoodOrderDefinition task => task.TargetFoodKind,
        _ => null,
    };

    [JsonIgnore]
    public string? TargetResourceId => Definition switch
    {
        SeekFoodOrderDefinition task => task.TargetResourceId,
        HarvestFoodOrderDefinition task => task.TargetResourceId,
        GatherMaterialOrderDefinition task => task.TargetResourceId,
        _ => null,
    };

    [JsonIgnore]
    public GridPoint? TargetPosition => Definition switch
    {
        SeekFoodOrderDefinition task => task.TargetPosition,
        HarvestFoodOrderDefinition task => task.TargetPosition,
        GatherMaterialOrderDefinition task => task.TargetPosition,
        MoveToOrderDefinition task => task.TargetPosition,
        SeekShelterOrderDefinition task => task.TargetPosition,
        TendFireOrderDefinition task => task.TargetPosition,
        ConstructBuildingOrderDefinition task => task.TargetPosition,
        ExpandBuildingOrderDefinition task => task.TargetPosition,
        DeliverStockOrderDefinition task => task.TargetPosition,
        CollectGoodsOrderDefinition task => task.TargetPosition,
        StoreGoodsOrderDefinition task => task.TargetPosition,
        ReturnBorrowedOrderDefinition task => task.TargetPosition,
        ProduceItemOrderDefinition task => task.TargetPosition,
        PlantTreeOrderDefinition task => task.TargetPosition,
        PlantBroadleafOrderDefinition task => task.TargetPosition,
        PlantConiferOrderDefinition task => task.TargetPosition,
        PlantOrchardOrderDefinition task => task.TargetPosition,
        TillFieldOrderDefinition task => task.TargetPosition,
        PlantFieldOrderDefinition task => task.TargetPosition,
        TendFieldOrderDefinition task => task.TargetPosition,
        HarvestFieldOrderDefinition task => task.TargetPosition,
        CollectMaterialOrderDefinition task => task.TargetPosition,
        CollectEquipmentOrderDefinition task => task.TargetPosition,
        CollectFoodOrderDefinition task => task.TargetPosition,
        StoreMaterialOrderDefinition task => task.TargetPosition,
        StoreEquipmentOrderDefinition task => task.TargetPosition,
        TravelByBoatOrderDefinition task => task.TargetPosition,
        _ => null,
    };

    [JsonIgnore]
    public string? TargetAgentId => Definition switch
    {
        AcceptGuardianshipOrderDefinition task => task.TargetAgentId,
        TalkToOrderDefinition task => task.TargetAgentId,
        ProposeMarriageOrderDefinition task => task.TargetAgentId,
        _ => null,
    };

    [JsonIgnore]
    public string? TargetMaterialKind => Definition switch
    {
        GatherMaterialOrderDefinition task => task.TargetMaterialKind,
        CollectMaterialOrderDefinition task => task.TargetMaterialKind,
        StoreMaterialOrderDefinition task => task.TargetMaterialKind,
        _ => null,
    };

    [JsonIgnore]
    public string? TargetEquipmentKind => Definition switch
    {
        RepairEquipmentOrderDefinition task => task.TargetEquipmentKind,
        RepairToolOrderDefinition task => task.TargetEquipmentKind,
        CollectEquipmentOrderDefinition task => task.TargetEquipmentKind,
        StoreEquipmentOrderDefinition task => task.TargetEquipmentKind,
        _ => null,
    };

    [JsonIgnore]
    public string? TargetCropKind => Definition switch
    {
        PlantFieldOrderDefinition task => task.TargetCropKind,
        TendFieldOrderDefinition task => task.TargetCropKind,
        HarvestFieldOrderDefinition task => task.TargetCropKind,
        _ => null,
    };

    [JsonIgnore] public string? TargetRecipeId => (Definition as ProduceItemOrderDefinition)?.TargetRecipeId;

    [JsonIgnore] public string? TargetOutputKind => (Definition as ProduceItemOrderDefinition)?.TargetOutputKind;

    [JsonIgnore] public string? ProductionBuildingId => (Definition as ProduceItemOrderDefinition)?.ProductionBuildingId;

    [JsonIgnore] public string? ProductionJobId => (Definition as ProduceItemOrderDefinition)?.ProductionJobId;

    [JsonIgnore] public long? ProductionProjectStartedTick => (Definition as ProduceItemOrderDefinition)?.ProductionProjectStartedTick;

    [JsonIgnore]
    public string? TargetItemKind => Definition switch
    {
        ReadKnowledgeOrderDefinition task => task.TargetItemKind,
        LoadHandcartOrderDefinition task => task.TargetItemKind,
        UnloadHandcartOrderDefinition task => task.TargetItemKind,
        UnloadHandcartGroundOrderDefinition task => task.TargetItemKind,
        DeliverStockOrderDefinition task => task.TargetItemKind,
        CollectGoodsOrderDefinition task => task.TargetItemKind,
        StoreGoodsOrderDefinition task => task.TargetItemKind,
        ReturnBorrowedOrderDefinition task => task.TargetItemKind,
        _ => null,
    };

    [JsonIgnore]
    public string? TargetStorageBuildingId => Definition switch
    {
        DeliverStockOrderDefinition task => task.TargetStorageBuildingId,
        StoreGoodsOrderDefinition task => task.TargetStorageBuildingId,
        ReturnBorrowedOrderDefinition task => task.TargetStorageBuildingId,
        StoreMaterialOrderDefinition task => task.TargetStorageBuildingId,
        StoreEquipmentOrderDefinition task => task.TargetStorageBuildingId,
        _ => null,
    };

    [JsonIgnore]
    public string? TargetStorageOwnerId => Definition switch
    {
        DeliverStockOrderDefinition task => task.TargetStorageOwnerId,
        StoreGoodsOrderDefinition task => task.TargetStorageOwnerId,
        ReturnBorrowedOrderDefinition task => task.TargetStorageOwnerId,
        StoreMaterialOrderDefinition task => task.TargetStorageOwnerId,
        StoreEquipmentOrderDefinition task => task.TargetStorageOwnerId,
        _ => null,
    };

    [JsonIgnore]
    public GridPoint? TargetStoragePosition => Definition switch
    {
        DeliverStockOrderDefinition task => task.TargetStoragePosition,
        StoreGoodsOrderDefinition task => task.TargetStoragePosition,
        ReturnBorrowedOrderDefinition task => task.TargetStoragePosition,
        StoreMaterialOrderDefinition task => task.TargetStoragePosition,
        StoreEquipmentOrderDefinition task => task.TargetStoragePosition,
        _ => null,
    };

    [JsonIgnore]
    public string? TargetLotId => Definition switch
    {
        LoadHandcartOrderDefinition task => task.TargetLotId,
        UnloadHandcartOrderDefinition task => task.TargetLotId,
        UnloadHandcartGroundOrderDefinition task => task.TargetLotId,
        ReturnBorrowedOrderDefinition task => task.TargetLotId,
        _ => null,
    };

    [JsonIgnore] public string? DeliveryPurpose => (Definition as DeliverStockOrderDefinition)?.DeliveryPurpose;

    [JsonIgnore]
    public string? TargetBuildingKind => Definition switch
    {
        SeekShelterOrderDefinition task => task.TargetBuildingKind,
        TendFireOrderDefinition task => task.TargetBuildingKind,
        ConstructBuildingOrderDefinition task => task.TargetBuildingKind,
        ExpandBuildingOrderDefinition task => task.TargetBuildingKind,
        DeliverStockOrderDefinition task => task.TargetBuildingKind,
        _ => null,
    };

    [JsonIgnore] public string? DeliveryRoute => (Definition as DeliverStockOrderDefinition)?.DeliveryRoute;

    [JsonIgnore] public string? DeliveryLotId => (Definition as DeliverStockOrderDefinition)?.DeliveryLotId;

    [JsonIgnore] public int? DeliveryQuantity => (Definition as DeliverStockOrderDefinition)?.DeliveryQuantity;

    [JsonIgnore] public string? TargetDefinitionId => (Definition as ConstructBuildingOrderDefinition)?.TargetDefinitionId;

    [JsonIgnore] public string? ConstructionOwnerId => (Definition as ConstructBuildingOrderDefinition)?.ConstructionOwnerId;

    [JsonIgnore] public GridPoint? ConstructionPosition => (Definition as ConstructBuildingOrderDefinition)?.ConstructionPosition;

    [JsonIgnore] public long? ConstructionStartedTick => (Definition as ConstructBuildingOrderDefinition)?.ConstructionStartedTick;

    [JsonIgnore] public string? ConstructionInstanceId => (Definition as ConstructBuildingOrderDefinition)?.ConstructionInstanceId;

    [JsonIgnore] public OwnerBuildingExpansionBinding? ExpansionBinding => (Definition as ExpandBuildingOrderDefinition)?.ExpansionBinding;

    [JsonIgnore]
    public OwnerShelterBinding? ShelterBinding => Definition switch
    {
        SeekShelterOrderDefinition task => task.ShelterBinding,
        TendFireOrderDefinition task => task.ShelterBinding,
        _ => null,
    };

    [JsonIgnore]
    public OwnerShelterCompletion? ShelterCompletion => Definition switch
    {
        SeekShelterOrderDefinition task => task.ShelterCompletion,
        TendFireOrderDefinition task => task.ShelterCompletion,
        _ => null,
    };

    [JsonIgnore]
    public string? TargetAnimalId => Definition switch
    {
        AnimalCareOrderDefinition task => task.TargetAnimalId,
        AnimalCollectOrderDefinition task => task.TargetAnimalId,
        AnimalTameOrderDefinition task => task.TargetAnimalId,
        AnimalLeadHomeOrderDefinition task => task.TargetAnimalId,
        AnimalSaddleOrderDefinition task => task.TargetAnimalId,
        AnimalMountOrderDefinition task => task.TargetAnimalId,
        AnimalDismountOrderDefinition task => task.TargetAnimalId,
        _ => null,
    };

    [JsonIgnore] public OwnerBoatTravelBinding? BoatTravel => (Definition as TravelByBoatOrderDefinition)?.BoatTravel;

    [JsonIgnore]
    public string? TargetKnowledgeKind => Definition switch
    {
        WriteKnowledgeOrderDefinition task => task.TargetKnowledgeKind,
        CopyKnowledgeOrderDefinition task => task.TargetKnowledgeKind,
        _ => null,
    };

    [JsonIgnore]
    public string? KnowledgeWritingProjectId => Definition switch
    {
        WriteKnowledgeOrderDefinition task => task.KnowledgeWritingProjectId,
        CopyKnowledgeOrderDefinition task => task.KnowledgeWritingProjectId,
        _ => null,
    };

    [JsonIgnore] public string? KnowledgeCopySourceArtifactId => (Definition as CopyKnowledgeOrderDefinition)?.KnowledgeCopySourceArtifactId;

    [JsonIgnore]
    public string? TargetCartLotId => Definition switch
    {
        AttachHandcartOrderDefinition task => task.TargetCartLotId,
        ParkHandcartOrderDefinition task => task.TargetCartLotId,
        RepairHandcartOrderDefinition task => task.TargetCartLotId,
        LoadHandcartOrderDefinition task => task.TargetCartLotId,
        UnloadHandcartOrderDefinition task => task.TargetCartLotId,
        UnloadHandcartGroundOrderDefinition task => task.TargetCartLotId,
        _ => null,
    };

    [JsonIgnore]
    public string? TalkConversationId => Definition switch
    {
        TalkToOrderDefinition task => task.TalkConversationId,
        ProposeMarriageOrderDefinition task => task.TalkConversationId,
        _ => null,
    };

    [JsonIgnore]
    public string? TalkOutcome => Definition switch
    {
        TalkToOrderDefinition task => task.TalkOutcome,
        ProposeMarriageOrderDefinition task => task.TalkOutcome,
        _ => null,
    };

    [JsonIgnore] public string? TargetKnowledgeArtifactId => (Definition as ReadKnowledgeOrderDefinition)?.TargetKnowledgeArtifactId;

    [JsonIgnore] public OwnerKnowledgeReadCompletion? KnowledgeReadCompletion => (Definition as ReadKnowledgeOrderDefinition)?.KnowledgeReadCompletion;
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Identity? FuelReservationId = null);

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
    Identity OrderId,
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
    Identity InstructionId,
    string IdempotencyKey,
    long SubmittedTick,
    long RunEpoch,
    long Revision);

/// <summary>Retains submission idempotency without retaining a closed task or its text.</summary>
public sealed record RecordedInstructionReceipt(OwnerInstructionReceipt Receipt, string RequestDigest);
