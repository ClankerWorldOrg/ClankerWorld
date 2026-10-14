using System.Text.Json.Serialization;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Server.Observation;

public sealed record ProtocolVersion(int Major, int Minor);

public sealed record HandshakeView(
    ProtocolVersion Protocol,
    IReadOnlyList<string> ServerCapabilities,
    IReadOnlyList<string> ClientCapabilities);

public sealed record PositionView(int X, int Y);

public sealed record TileView(int X, int Y, string Terrain);

/// <summary>Row-major terrain-kind bytes, base64-encoded for owner JSON.</summary>
public sealed record PackedTerrainView(int Width, int Height, string Encoding, string Data);
/// <summary>Independent row-major byte layers; terrain remains a compatibility projection.</summary>
public sealed record PackedMapLayersView(int Width, int Height, string Encoding,
    string Climate, string Elevation, string Hydrology, string Surface, string Vegetation)
{
    public string? Fertility { get; init; }
}
public sealed record FarmFieldView(PositionView Position, string HouseholdId, string Stage, string? Crop,
    int Fertility, string? WorkerId, int? WorkRemaining);
public sealed record HandcartView(string Id, string OwnerId, string OwnerName, PositionView Position,
    int Capacity, int ConditionPercent, string? PullerId, string? PullerName,
    IReadOnlyList<InventoryEntryView> Cargo);

public sealed record AnimalView(string Id, string Name, string Species, string Sex, int AgeDays, string LifeStage,
    PositionView Position, string? HouseholdId, string? HouseholdName, string CareStatus, string? ProductKind,
    int ProductQuantity, double? BirthDaysRemaining, string? RiderId, string? RiderName, string? LeaderId,
    bool Saddled, IReadOnlyList<string> CarePermissions, IReadOnlyList<string> RidingPermissions,
    int? ProductProgressPercent = null);

/// <summary>
/// A building under construction: a household's planned building or an
/// approved Town project, where it will stand and how far the work has got.
/// </summary>
public sealed record ConstructionSiteView(string Id, string DefinitionId, string DisplayName, IReadOnlyList<string> Tags,
    PositionView Site, int Width, int Height, PositionView? Entrance, int WorkDone, int WorkRequired, string Stage,
    string? TownId, string? HouseholdId);

public sealed record BoatView(string Id, string TownId, string TownName, PositionView Position,
    string? DockedPortId, string? PassengerId, string? PassengerName, string? DestinationPortId,
    string Status, PositionView? ReservedDock, IReadOnlyList<InventoryEntryView> Cargo);
public sealed record BoatTripRequestView(string Id, long Sequence, string PassengerId, string PassengerName,
    string BoatTownId, string OriginPortId, string DestinationPortId, string Status, string? BoatId);

public sealed record GroundStockView(PositionView Position, string OwnerId, string Kind, int Quantity)
{
    public string OwnerName { get; init; } = "Unknown owner";
}
public sealed record WorldPreviewView(PackedTerrainView Terrain, PositionView StartingSite,
    string ManifestDigest, int ResourceSites = 0)
{
    public PackedMapLayersView? PackedMapLayers { get; init; }
    public string? MapLayersDigest { get; init; }
    public GeographyCandidateReport? Coverage { get; init; }
    public IReadOnlyList<GeographyCandidateReport> Candidates { get; init; } = [];
    public IReadOnlyList<GeographyCandidateFailure> FailedCandidates { get; init; } = [];
}

public sealed record MapObjectView(string Id, string Kind, PositionView Position);

public sealed record ResourceView(string Id, string Kind, PositionView Position, bool IsRenewable, string State,
    int? Quantity = null, int? Capacity = null, int? RegenerationAmount = null,
    int? RegenerationIntervalDays = null, string? RegenerationSeason = null, string? TreeKind = null,
    bool IsPlanted = false, string? TreeStage = null, string? NaturalObjectKind = null);

public sealed record ActorView(
    string Id,
    PositionView Position,
    int FullnessBasisPoints,
    int FoodItems,
    int WoodItems);

public sealed record InventoryEntryView(string Kind, int Quantity);

public sealed record DecisionFactorView(string Key, string Detail)
{
    /// <summary>Derived deadline for the sponsored admission approval shown in this row.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? AcceptanceDeadlineTick { get; init; }
}

public sealed record RouteView(
    string Status,
    string? DestinationId,
    PositionView? Destination,
    IReadOnlyList<PositionView> Steps,
    string TopologyManifestDigest);

/// <summary>
/// Developer tools: the route an agent is walking, as the server planned it on
/// its latest step. <see cref="Steps"/> holds at most the first
/// <see cref="StepLimit"/> tiles still ahead; <see cref="StepCount"/> counts all of them.
/// </summary>
public sealed record PlannedRouteView(
    string Reason,
    PositionView Destination,
    IReadOnlyList<PositionView> Steps,
    int StepCount)
{
    public const int StepLimit = 256;
}

public sealed record SpatialKnowledgeView(
    PositionView CurrentTile,
    IReadOnlyList<PositionView> PerceivedTiles,
    IReadOnlyList<PositionView> KnownTiles);

/// <summary>
/// A safe owner-facing summary of what an agent is currently trying to
/// do. This is an intention label, never private model chain-of-thought.
/// </summary>
public sealed record PublicIntentionView(
    string CandidateId,
    string Summary,
    string Provider,
    long WorldTick);

public sealed record AgentRelationshipView(
    string RelationshipId,
    string OtherPartyId,
    string Type,
    string State,
    string PrivacyClass,
    long EffectiveTick,
    string? Direction = null);

public sealed record PrivateThoughtView(long WorldTick, string Text);
public sealed record AgentMemoryView(long WorldTick, string SubjectId, string SubjectName, string Summary, string Visibility, bool Permanent = false);
public sealed record AgentBeliefView(
    long WorldTick,
    string Statement,
    string Provenance,
    int ConfidenceBasisPoints,
    string? SourceAgentId,
    string? SourceAgentName,
    long? SourceEventId,
    string? AboutAgentId,
    bool IsCorrected,
    long? CorrectedTick);
public sealed record AgentKnowledgeFactView(
    long WorldTick,
    int X,
    int Y,
    string Terrain,
    IReadOnlyList<string> ResourceKinds,
    string DiscovererName,
    string Acquisition,
    string? SourceAgentName);
public sealed record AgentRecipeView(long WorldTick, string Name, string Acquisition, string? SourceAgentName);
public sealed record KnowledgeSiteView(int X, int Y, string Terrain, IReadOnlyList<string> ResourceKinds, string DiscovererName);
public sealed record AgentKnowledgeArtifactView(
    string Id,
    string Kind,
    string Title,
    long CreatedTick,
    string CreatorName,
    IReadOnlyList<KnowledgeSiteView> Sites)
{
    public IReadOnlyList<string> RecipeNames { get; init; } = [];
}
/// <summary>
/// The world's saved calendar, including its season lengths and clock offset,
/// so the game names the season and day of any tick the same way the world does.
/// </summary>
public sealed record CalendarPaceView(
    int TicksPerDay,
    int DaysPerYear,
    int SpringDays,
    int SummerDays,
    int AutumnDays,
    int WinterDays,
    int CalendarOffsetTicks = 0);

/// <summary>
/// An inspection projection, never an editable actor record. A founder draft
/// is visible as such but is not a living simulation actor yet.
/// </summary>
public sealed record AgentView(
    string Id,
    string DisplayName,
    string Lifecycle,
    PositionView Position,
    int FullnessBasisPoints,
    IReadOnlyList<InventoryEntryView> Inventory,
    IReadOnlyList<DecisionFactorView> DecisionFactors,
    RouteView Route,
    SpatialKnowledgeView SpatialKnowledge,
    bool IsDraft)
{
    public PublicIntentionView? PublicIntention { get; init; }

    /// <summary>Developer tools only; null when the agent is not walking anywhere.</summary>
    public PlannedRouteView? PlannedRoute { get; init; }

    public ProjectView? Project { get; init; }
    public SurvivalView? Survival { get; init; }
    public EquipmentView? Equipment { get; init; }
    public string? MedicalCareNote { get; init; }
    public string? ToolMakingRequestNote { get; init; }
    public LessonView? Lesson { get; init; }
    public ProficiencyView? Proficiency { get; init; }
    public IReadOnlyList<SkillView> Skills { get; init; } = [];
    public IReadOnlyList<SocialStandingView> SocialStanding { get; init; } = [];

    public IReadOnlyList<string> SocialNotes { get; init; } = [];

    public IReadOnlyList<AgentRelationshipView> Relationships { get; init; } = [];

    public IReadOnlyList<PrivateThoughtView> RecentPrivateThoughts { get; init; } = [];

    public IReadOnlyList<AgentMemoryView> RecentMemories { get; init; } = [];

    public IReadOnlyList<AgentBeliefView> RecentBeliefs { get; init; } = [];

    public IReadOnlyList<AgentKnowledgeFactView> RecentKnowledgeFacts { get; init; } = [];
    public IReadOnlyList<AgentRecipeView> KnownRecipes { get; init; } = [];

    public IReadOnlyList<AgentKnowledgeArtifactView> KnowledgeArtifacts { get; init; } = [];

    /// <summary>A dead agent's will on their historical profile; null for the living.</summary>
    public FinalWillView? FinalWill { get; init; }
}

/// <param name="Status">pending, accepted or default (household inheritance).</param>
/// <param name="Split">equal or items for an accepted will; otherwise null.</param>
/// <param name="Heirs">Each named heir and the goods the will leaves them, in the will's order.</param>
/// <param name="FinalWords">Words the agent left for the people who inherit, if any.</param>
public sealed record FinalWillView(string Status, string? Split, IReadOnlyList<WillHeirView> Heirs, string? FinalWords);
public sealed record WillHeirView(string Id, string Name, bool IsTown, IReadOnlyList<InventoryEntryView> Items);

public sealed record ProjectView(string Label, string Stage, int WorkDone, int WorkRequired, string? Blocker, long StartedTick);
public sealed record SurvivalView(int WarmthBasisPoints, int IllnessBasisPoints, bool HasClothing, bool HasTool,
    int NutritionBasisPoints, string? LastMealKind);
public sealed record EquipmentView(int CarriedQuantity, int Capacity, string? ClothingKind,
    int? ClothingConditionPercent, string? CarryAidKind, int? CarryAidConditionPercent,
    string? RepairItemKind, int RepairWorkDone, int RepairWorkRequired, string? OrnamentKind = null);

public sealed record StockpileView(string OwnerId, string Name, IReadOnlyList<InventoryEntryView> Items);
// Keep the existing observation field name so older owner clients can still display a lesson.
public sealed record LessonView(string TeacherName, [property: JsonPropertyName("role")] string Skill,
    string Stage, int Progress, int Required);
public sealed record SkillView(string Kind, long LearnedTick, string? TeacherId, string? TeacherName);
public sealed record ProficiencyView(int Building, int Farming, int Crafting);
public sealed record SocialStandingView(string SubjectId, string SubjectName, int Trust);

public sealed record InstructionView(
    string InstructionId,
    string TargetAgentId,
    string Kind,
    string Text,
    string State,
    long SubmittedTick,
    long RunEpoch,
    long SubmissionSequence,
    long? ObservedTick = null,
    string? ObserverReply = null,
    OrderTaskView? Task = null);

public sealed record OrderTaskView(
    string Action,
    string Status,
    int RequestedUnits,
    int CompletedUnits,
    string ProgressUnit,
    bool RepeatUntilCancelled,
    string? TargetFoodKind = null,
    string? TargetResourceId = null,
    int? TargetX = null,
    int? TargetY = null,
    string? BlockedReason = null,
    string? TargetAgentId = null,
    string? TargetMaterialKind = null,
    string? TargetEquipmentKind = null,
    string? TargetCropKind = null,
    string? TargetOutputKind = null,
    string? TargetItemKind = null,
    string? TargetBuildingKind = null,
    string? TargetAnimalId = null,
    string? TargetKnowledgeKind = null,
    string? TargetCartLotId = null,
    string? TalkConversationId = null,
    string? TalkStatus = null,
    string? TalkOutcome = null,
    string? TargetKnowledgeArtifactId = null);

public sealed record CognitionEventView(long EventId, long WorldTick, string Kind, string Detail);

public sealed record AgentDecisionView(
    string AgentId, string Provider, string CandidateId, long WorldTick,
    double Confidence, string? Model, int? InputTokens, int? OutputTokens,
    string? Role = null, long? LatencyMilliseconds = null, bool FellBack = false);

public sealed record CognitionView(
    string Provider,
    bool IsPaused,
    string? InFlightRequestId,
    string? CurrentCandidateId,
    string? CurrentDecisionProvider,
    IReadOnlyList<CognitionEventView> Events,
    IReadOnlyList<AgentDecisionView>? Decisions = null);

public sealed record ContentPackageView(
    string PackageId,
    string Version,
    string PackageDigest,
    string Lifecycle,
    string? LockDigest,
    long? ValidationTick,
    long? StagedTick,
    long? ActivationTick,
    string? ManifestDigest = null,
    string? DisplayName = null,
    string? ProposedByAgentId = null);

public sealed record ContentGovernanceEventView(
    long EventId,
    long WorldTick,
    string PackageId,
    string Kind,
    string Detail);

public sealed record WorldSystemsSummaryView(
    string Season,
    string Weather,
    int EcologyResourceCount,
    int ChunkCount,
    int BuildingDefinitionCount = 0,
    int RecipeDefinitionCount = 0,
    int PlacedBuildingCount = 0,
    int ProductionJobCount = 0,
    int DistinctAssetReservationCount = 0,
    long DurableAssetReservationBytes = 0,
    long DecodedAssetCacheBytes = 0,
    long GpuAssetBytes = 0,
    int AssetRenderUnits = 0);

public sealed record PlacedBuildingView(
    string InstanceId,
    string DefinitionId,
    PositionView Position,
    long PlacedTick,
    string? DisplayName = null,
    IReadOnlyList<string>? Tags = null,
    int Width = 1,
    int Height = 1,
    string? TownId = null,
    string? HouseholdId = null,
    IReadOnlyList<InventoryEntryView>? StoredItems = null,
    PositionView? Entrance = null,
    int? StorageCapacity = null,
    int StoredQuantity = 0,
    int FootprintRevision = 0,
    IReadOnlyList<string>? InvitedGuests = null,
    string? ExpansionState = null,
    string? ExpansionFailure = null,
    int? ResidentLimit = null,
    int PermanentResidentCount = 0,
    bool HasDominantFamily = false,
    bool IsOvercrowded = false)
{
    public IReadOnlyList<ShopTradeView> Trades { get; init; } = [];
    public IReadOnlyList<ToolMakingRequestView> ToolMakingRequests { get; init; } = [];
    public bool AllowsHouseholdOwner { get; init; }
    public IReadOnlyList<BuildingStorageChangeView>? RecentStorageChanges { get; init; }
    public IReadOnlyList<ProductionRecipeView>? AvailableRecipes { get; init; }
}

public sealed record BuildingStorageChangeView(long EventId, long WorldTick, string ItemKind, long QuantityChange);

public sealed record ToolMakingRequestView(string Id, string RequesterName, string RecipeId,
    string RecipeName, string ItemKind, string Status, string? Blocker, string? OfferId = null);

public sealed record ShopTradeView(string OfferId, string BuyerName, string GoodsKind, int GoodsQuantity,
    string PaymentKind, int PaymentQuantity, string Status, string? CancellationReason);

public sealed record ProductionJobView(
    string JobId,
    string RecipeId,
    string BuildingInstanceId,
    string WorkerId,
    long StartedTick,
    long CompletionTick,
    string State)
{
    public ProductionRecipeView? Recipe { get; init; }
    public IReadOnlyList<MaterialQuantityView>? HeldInputs { get; init; }
}

public sealed record MaterialQuantityView(string Kind, int Quantity);

public sealed record ProductionRecipeView(string Id, string Name,
    IReadOnlyList<MaterialQuantityView> Inputs, IReadOnlyList<MaterialQuantityView> Outputs);

public sealed record AuthoringStateView(
    bool IsPaused,
    long RunEpoch,
    long Revision,
    long TopologyRevision,
    string InitialMapManifestDigest,
    string CurrentMapManifestDigest,
    string Weather,
    string Season,
    IReadOnlyList<string> ApprovedAssetReferences);

public sealed record EventView(long EventId, long WorldTick, string Kind, string Detail, PositionView? Position = null)
{
    public string Text { get; init; } = string.Empty;
}

public sealed record FounderSetupView(int Required, int Placed, bool Started)
{
    /// <summary>An untouched bootstrap world should open New World instead of being shown.</summary>
    public bool RequiresWorldCreation { get; init; }
    public bool CanChooseTownSite { get; init; }
    public bool HasAcceptedTownSite { get; init; }
    public string? LastFounderId { get; init; }
}

public sealed record TownView(
    string Id,
    string Name,
    string FoundingState,
    long FoundedTick,
    IReadOnlyList<string> ResidentIds,
    IReadOnlyList<string> AssignedBuildingIds,
    IReadOnlyList<PositionView> BorderTiles)
{
    public bool IsAbandoned => FoundingState == "founded" && ResidentIds.Count == 0;

    /// <summary>An abandoned Town that has stood empty for a full season; its buildings look falling apart.</summary>
    public bool FallingApart { get; init; }

    public TownCouncilView? Council { get; init; }
    public IReadOnlyList<TownProjectView> Projects { get; init; } = [];
    public TownGovernmentView? Government { get; init; }
    public IReadOnlyList<MarketView> Markets { get; init; } = [];
    public IReadOnlyList<TownLandHearingView> LandHearings { get; init; } = [];
    public int LandHearingCount { get; init; }
    public IReadOnlyList<LandTransferView> LandTransfers { get; init; } = [];
    public int LandTransferCount { get; init; }
    public IReadOnlyList<TownNonLandCaseView> NonLandCases { get; init; } = [];
    public int NonLandCaseCount { get; init; }
}

public sealed record LandTransferPartyView(string HouseholdId, string Kind, string HouseholdName, string RosterKind,
    IReadOnlyList<string> AdultIds, IReadOnlyList<string> AdultNames, IReadOnlyList<string> AcceptedAdultIds,
    IReadOnlyList<string> NoticeAwareAdultIds);
public sealed record LandTransferResponseView(string HouseholdId, string HouseholdName, string AgentId,
    string AgentName, string Kind, long Tick, IReadOnlyList<string> PartyAdults);
public sealed record LandTransferView(string Id, string FilerId, string FilerName, string TargetHouseholdId,
    string TargetHouseholdName, IReadOnlyList<PositionView> Tiles, IReadOnlyList<LandHearingRightVersionView> RightVersions,
    IReadOnlyList<LandTransferPartyView> Parties, string NoticeId, long ProposedTick,
    IReadOnlyList<LandTransferResponseView> Responses, string Status, long? SettledTick, string? Reason,
    string? ReceiptAdjustmentId)
{
    public LandSalePriceView? Price { get; init; }
    public LandSalePaymentView? Payment { get; init; }
}
public sealed record LandSalePriceView(string SellerHouseholdId, string SellerHouseholdName, string ItemKind, int Quantity)
{
    public string ItemName { get; init; } = "goods";
}
public sealed record LandSalePaymentView(string BuyerAgentId, string BuyerName, string SellerAgentId, string SellerName,
    PositionView Position, long Tick);

public sealed record LandHearingOutcomeView(string Kind, string? HouseholdId, string? HouseholdName, long? AgreedEndTick);
public sealed record LandHearingProposalView(IReadOnlyList<PositionView> Tiles,
    LandHearingOutcomeView RequestedOutcome, string Statement);
public sealed record LandHearingRightVersionView(string Id, string Version, HouseholdLandUseRightView Right);
public sealed record LandHearingFilingView(string? AgentId, string? AgentName, string Kind, string Text,
    LandHearingOutcomeView RequestedOutcome, long Tick, string? AuthorityId);
public sealed record LandHearingPartyView(string Id, string Kind, string Name,
    IReadOnlyList<string> AdultIds, IReadOnlyList<string> AdultNames, string? RepresentativeId,
    string? RepresentativeName, IReadOnlyList<string> NoticeAwareAdultIds);
public sealed record LandHearingResponseView(int Revision, string PartyId, string AgentId, string AgentName,
    string Kind, string Text, long Tick);
public sealed record LandHearingJudgeView(string AgentId, string AgentName, string Kind, string AuthorityId, long AssignedTick);
public sealed record LandHearingJudgeTermView(LandHearingJudgeView Judge, long EndedTick, string Reason);
public sealed record LandHearingEvidenceView(string Id, int Revision, string Kind, string Acquisition,
    string SourceAgentId, string SourceAgentName, string? SourceRecordId, string? SourceVersion, long ObservedTick,
    string SubmittedByAgentId, string SubmittedByName, long SubmittedTick, string Text)
{
    public HouseholdLandUseRightView? PermissionRecord { get; init; }
    public TownLandTitleView? TitleRecord { get; init; }
    public string? RecordPartyName { get; init; }
    public int? LawVersion { get; init; }
}
public sealed record LandHearingReadView(int Revision, string AgentId, string AgentName, long ReadTick,
    IReadOnlyList<string> EvidenceIds, string? SourceAgentId, string? SourceAgentName)
{
    public IReadOnlyList<string> ReopenRequestIds { get; init; } = [];
}
public sealed record LandHearingRulingView(string Id, int Revision, LandHearingJudgeView Judge, long Tick,
    LandHearingOutcomeView Outcome, IReadOnlyList<PositionView> Tiles, IReadOnlyList<string> EvidenceIds, IReadOnlyList<string> LawIds,
    string Reasons, IReadOnlyList<string> AdjustmentIds)
{
    public IReadOnlyList<LandHearingPartyView> Parties { get; init; } = [];
}
public sealed record LandHearingReopenRequestView(string Id, string AgentId, string AgentName, long Tick,
    string Kind, IReadOnlyList<string> EvidenceIds, string Reasons, string Status,
    LandHearingJudgeView? AssessedBy, long? AssessedTick, string? Assessment);
public sealed record LandHearingElectionView(string Id, string Stage, int Round, long? DeadlineTick,
    IReadOnlyList<CivicCandidateView> Candidates, string? WinnerName, string? Reason);
public sealed record TownLandHearingView(string Id, string Kind, string Status, long FiledTick, long? SettledTick,
    int Revision, IReadOnlyList<PositionView> Tiles, IReadOnlyList<LandHearingRightVersionView> RightVersions,
    string NoticeId, long PublishedTick, long DeadlineTick,
    LandHearingOutcomeView RequestedOutcome, IReadOnlyList<LandHearingFilingView> Filings, IReadOnlyList<LandHearingPartyView> Parties,
    IReadOnlyList<LandHearingEvidenceView> Evidence, IReadOnlyList<LandHearingResponseView> Responses,
    IReadOnlyList<LandHearingRulingView> Rulings, LandHearingJudgeView? Judge, IReadOnlyList<LandHearingJudgeTermView> JudgeHistory,
    LandHearingElectionView? JudgeElection, LandHearingElectionView? LatestJudgeElection,
    IReadOnlyList<LandHearingReopenRequestView> ReopenRequests)
{
    public IReadOnlyList<string> PropertyDetails { get; init; } = [];
    public IReadOnlyList<LandHearingReadView> Reads { get; init; } = [];
    public IReadOnlyList<LandHearingPartyView> CurrentParties { get; init; } = [];
}

public sealed record TownLawView(string Id, string Subject, string Rule, string Scope, int SiteTiles, int Version,
    long AdoptedTick, long? EndedTick)
{
    public IReadOnlyList<PositionView> Site { get; init; } = [];
}
public sealed record TownOfficeView(string Mandate, string? HolderName, long? TermEndTick, string? VacancyReason);
public sealed record GovernmentChangeView(string Id, string Declaration, string Status, int Yes, int No, int RequiredYes,
    long? DeadlineTick, long? HandoverDeadlineTick, string? Reason)
{
    public NonLandExtensionView? NonLandExtension { get; init; }
}
public sealed record MayoralElectionView(string Id, string Mandates, string Stage, int Round, long? DeadlineTick,
    IReadOnlyList<CivicCandidateView> Candidates, string? WinnerName, string? Reason);
public sealed record TownGovernmentView(string Declaration, IReadOnlyList<TownLawView> Laws, int LawCount,
    IReadOnlyList<TownOfficeView> Offices, IReadOnlyList<GovernmentChangeView> Changes,
    MayoralElectionView? Election, MayoralElectionView? LatestElection, long RetryTick)
{
    public bool NonLandAuthorized { get; init; }
    public NonLandAuthorityView? NonLandAuthority { get; init; }
    public IReadOnlyList<NonLandGrantView> NonLandGrants { get; init; } = [];
}

public sealed record MarketView(string Id, string ProjectId, string HallBuildingId,
    PositionView Site, PositionView PlazaPosition, int PlazaWidth, int PlazaHeight,
    IReadOnlyList<MarketStallView> Stalls, long? RemovedTick = null);
public sealed record MarketStallView(string BuildingInstanceId, int SlotIndex, PositionView Position,
    string? SellerId, string? SellerName, long? OccupiedTick,
    IReadOnlyList<MarketStockView> Stock, IReadOnlyList<MarketTradeView> Trades);
public sealed record MarketStockView(string LotId, string? ParentLotId, string OwnerId, string OwnerName,
    string Kind, int Quantity, int AvailableQuantity);
public sealed record MarketTradeView(string OfferId, string SellerId, string SellerName,
    string GoodsOwnerId, string GoodsOwnerName, string PaymentOwnerId, string PaymentOwnerName,
    string BuyerId, string BuyerName, string GoodsKind, int GoodsQuantity, string PaymentKind,
    int PaymentQuantity, string Status, string? CancellationReason,
    bool SellerAccepted = false, bool BuyerAccepted = false);

public sealed record CivicProposalView(string Id, string Kind, string Text, string Status, int Yes, int No,
    int RequiredYes, long DeadlineTick)
{
    public LandHearingProposalView? LandHearingRequest { get; init; }
    public TownProjectPlanView? Project { get; init; }
}
public sealed record TownProjectBudgetView(string Kind, int Quantity);
public sealed record TownProjectPlanView(string Name, string ProposerId, string ProposerName,
    string DefinitionId, string DisplayName, PositionView Site, PositionView Entrance,
    int Width, int Height, IReadOnlyList<TownProjectBudgetView> Budget)
{
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? BoatPortId { get; init; }
}
public sealed record TownProjectMaterialView(string Kind, int Budget, int Supplied);
public sealed record TownProjectView(string Id, string ProposalId, string Name,
    string ProposerId, string ProposerName, string DefinitionId, string DisplayName,
    PositionView Site, PositionView Entrance, int Width, int Height,
    IReadOnlyList<TownProjectMaterialView> Materials, int WorkDone, int WorkRequired,
    string Stage, string? Blocker, string? CompletedBuildingId, CivicProposalView Approval)
{
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? CompletedBoatId { get; init; }
    public long? RemovedTick { get; init; }
}
public sealed record CivicCandidateView(string Id, string Name, int Votes);
public sealed record TownElectionView(string Id, string Kind, string Stage, int Seats, long DeadlineTick,
    IReadOnlyList<CivicCandidateView> Candidates, IReadOnlyList<string> SettledNames);
public sealed record TownCouncilView(string Form, string Fallback, IReadOnlyList<string> MemberNames,
    long? TermEndTick, long RetryTick, IReadOnlyList<string> WillingCandidateNames,
    IReadOnlyList<CivicProposalView> Proposals, TownElectionView? Election)
{
    public TownElectionView? LatestElection { get; init; }
}

public sealed record TownLandTitleView(string Id, string TownId, IReadOnlyList<PositionView> Tiles,
    long RecordedTick);

public sealed record HouseholdLandUseRightView(string Id, string TownId, string HouseholdId,
    IReadOnlyList<PositionView> Tiles, long GrantedTick, string GrantSource, long? AgreedEndTick);

public sealed record HouseholdLandUseRequestView(string Id, string TownId, string HouseholdId,
    string RequestedByAgentId, IReadOnlyList<PositionView> Tiles, long RequestedTick,
    long? AgreedEndTick, bool IsDisputed, IReadOnlyList<string> ClaimantHouseholdIds,
    IReadOnlyList<PositionView> DisputedTiles)
{
    public string ApprovalDetail { get; init; } = "Awaiting approval";
}

public sealed record WeatherRegionView(int X, int Y, string Weather, int? SoilMoisture = null);

/// <summary>
/// A saved bridge exactly as movement uses it: its deck tiles are walkable
/// only along <see cref="Axis"/> between the two entrances.
/// </summary>
public sealed record BridgeView(
    string Id,
    string Design,
    string Trigger,
    string Axis,
    IReadOnlyList<PositionView> Entrances,
    IReadOnlyList<PositionView> Span,
    long BuiltTick);

public sealed record ConversationTurnView(
    string Id,
    string SpeakerId,
    string SpeakerName,
    string Text,
    long WorldTick,
    IReadOnlyList<string> ListenerIds,
    bool IsWrapUp,
    string? SurnameChoice = null);

public sealed record ConversationView(
    string Id,
    string InitiatorId,
    string InitiatorName,
    string InviteeId,
    string InviteeName,
    string Status,
    string? Interruption,
    string? Outcome,
    long CreatedTick,
    long LastUpdatedTick,
    IReadOnlyList<ConversationTurnView> Turns)
{
    public string Kind { get; init; } = "ordinary";
    public string? ChosenSurname { get; init; }
}

public sealed record WorldGenerationView(string Seed, string Size, string ClimateMode,
    string SelectedClimate, bool LatitudeCooling, bool WrapEastWest);

public sealed record WorldSnapshotView(
    string WorldId,
    long WorldTick,
    string MapManifestDigest,
    IReadOnlyList<TileView> Tiles,
    IReadOnlyList<MapObjectView> Objects,
    IReadOnlyList<ResourceView> Resources,
    ActorView? Actor,
    long LatestEventId)
{
    public PackedTerrainView? PackedTerrain { get; init; }
    public PackedMapLayersView? PackedMapLayers { get; init; }
    public string? MapLayersDigest { get; init; }
    public bool WrapsEastWest { get; init; }
    public WorldGenerationView? Generation { get; init; }
    public IReadOnlyList<FarmFieldView> Fields { get; init; } = [];
    public IReadOnlyList<GroundStockView> GroundStocks { get; init; } = [];
    public IReadOnlyList<HandcartView> Handcarts { get; init; } = [];
    public IReadOnlyList<AnimalView> Animals { get; init; } = [];
    public IReadOnlyList<ConstructionSiteView> ConstructionSites { get; init; } = [];
    public IReadOnlyList<BoatView> Boats { get; init; } = [];
    public IReadOnlyList<BoatTripRequestView> BoatRequests { get; init; } = [];
    public IReadOnlyList<StockpileView> Stockpiles { get; init; } = [];
    public FoodStewardshipView? FoodStewardship { get; init; }
    public int? AgingOverrideRate { get; init; }
    public CalendarPaceView? CalendarPace { get; init; }
    /// <summary>
    /// How dark the world is now, decided by the host from the world clock:
    /// 0 in daylight, 10,000 at full night, between them at dusk and dawn.
    /// Absent for a world without a calendar.
    /// </summary>
    public int? DarknessBasisPoints { get; init; }
    public bool? JevEnabled { get; init; }
    public string? RoutineHelperProvider { get; init; }
    public string? RoutineHelperModel { get; init; }
    public string? RoutineHelperCredentialSlotId { get; init; }
    /// <summary>The current saved rule state, independent of retained event history.</summary>
    public bool? ContinuityRuleActive { get; init; }
    public FounderSetupView? FounderSetup { get; init; }
    public IReadOnlyList<TownView> Towns { get; init; } = [];
    public IReadOnlyList<TownLandTitleView> TownLandTitles { get; init; } = [];
    public IReadOnlyList<HouseholdLandUseRightView> HouseholdLandUseRights { get; init; } = [];
    public IReadOnlyList<HouseholdLandUseRequestView> HouseholdLandUseRequests { get; init; } = [];
    public IReadOnlyList<PositionView> RoadTiles { get; init; } = [];
    public IReadOnlyList<BridgeView> Bridges { get; init; } = [];
    public int WeatherRegionSize { get; init; } = 32;
    public IReadOnlyList<WeatherRegionView> WeatherRegions { get; init; } = [];
    /// <summary>Developer tools: how long the host took to work out the latest tick; null before one runs.</summary>
    public double? LastTickMilliseconds { get; init; }
    public long RefusedActionCount { get; init; }
    /// <summary>
    /// The inspectable population projection. <see cref="Actor"/> remains for
    /// older diagnostic clients.
    /// </summary>
    public IReadOnlyList<AgentView> Agents { get; init; } = [];

    /// <summary>Recent public dialogue only; private thoughts never enter this projection.</summary>
    public IReadOnlyList<ConversationView> Conversations { get; init; } = [];

    /// <summary>
    /// Native world metadata, including pause state, run epoch, event revision,
    /// map identity, weather and season. The world projection supplies it.
    /// </summary>
    public AuthoringStateView? Authoring { get; init; }

    /// <summary>Current server text for the visible retained events, including renamed people.</summary>
    public IReadOnlyDictionary<long, string> EventTexts { get; init; } = new Dictionary<long, string>();

    public IReadOnlyList<InstructionView> Instructions { get; init; } = [];

    public CognitionView? Cognition { get; init; }

    public IReadOnlyList<ContentPackageView> ContentPackages { get; init; } = [];

    public IReadOnlyList<ContentGovernanceEventView> ContentEvents { get; init; } = [];

    public WorldSystemsSummaryView? WorldSystems { get; init; }

    public IReadOnlyList<PlacedBuildingView> PlacedBuildings { get; init; } = [];

    public IReadOnlyList<ProductionJobView> ProductionJobs { get; init; } = [];
}

public sealed record FoodStewardshipView(string? StewardName, string FoodPolicy, string? ProposedPolicy, int Approvals, int Rejections, int Voters);

public sealed record EventSliceView(long SnapshotTick, long AfterEventId, IReadOnlyList<EventView> Events,
    long EventHistoryFloor = 0, bool ResetRequired = false);

public sealed record ObserverTimelineView(string InstanceId, long Generation);

/// <summary>
/// A reconnect response is one server-side capture, not a race between a
/// client's separate snapshot and event-history requests.
/// </summary>
public sealed record ReconnectBaselineView(WorldSnapshotView Snapshot, EventSliceView Events,
    ObserverTimelineView? Timeline = null);

/// <summary>
/// Owns the static deterministic sample exposed by the first browser slice.
/// It creates protocol DTOs from the core's immutable harness state rather than
/// exposing simulation records to clients.
/// </summary>
public sealed record NonLandAuthorityView(string HolderId, string HolderName, string AuthorityId,
    long EffectiveTick, long TermStartTick, long TermEndTick);
public sealed record NonLandExtensionView(string HolderId, string HolderName, string BaseMandate,
    long TermStartTick, long TermEndTick, long? ConsentTick);
public sealed record NonLandGrantView(string Id, string HolderId, string HolderName, string BaseMandate,
    long ConsentTick, long EffectiveTick, long TermStartTick, long TermEndTick);
public sealed record CasePartyView(string Id, string Role, string SubjectId, string SubjectName,
    string? RespondingAdultId, string? RespondingAdultName, string? HouseholdName, bool NoticeAware);
public sealed record CaseRevisionView(int Number, string NoticeId, long PublishedTick, long DeadlineTick,
    IReadOnlyList<CasePartyView> Parties);
public sealed record CaseFilingView(string AgentId, string AgentName, string Kind, long Tick,
    string Statement, IReadOnlyList<string> EvidenceIds);
public sealed record CaseEvidenceView(string Id, int Revision, string Kind, string Acquisition,
    string SourceAgentId, string SourceAgentName, string? SourceRecordId, string? SourceVersion,
    long ObservedTick, string SubmittedByAgentId, string SubmittedByName, long SubmittedTick, string Text);
public sealed record CaseReadView(int Revision, string AgentId, string AgentName, long ReadTick,
    IReadOnlyList<string> EvidenceIds, IReadOnlyList<string> ReopenRequestIds, string? SourceAgentName);
public sealed record CaseResponseView(int Revision, string PartyId, string AgentId, string AgentName,
    string RepresentedAgentId, string RepresentedAgentName, string Kind, string Text, long Tick);
public sealed record CaseJudgeView(string AgentId, string AgentName, string Kind, string AuthorityId, long AssignedTick);
public sealed record CaseJudgeTermView(CaseJudgeView Judge, long EndedTick, string Reason);
public sealed record CaseElectionView(string Id, string Stage, int Round, long? DeadlineTick,
    IReadOnlyList<CivicCandidateView> Candidates, string? WinnerName, string? Reason);
public sealed record CaseReopenRequestView(string Id, string AgentName, long Tick, string Kind,
    IReadOnlyList<string> EvidenceIds, string Reasons, string Status, CaseJudgeView? AssessedBy,
    long? AssessedTick, string? Assessment);
public sealed record ViolationFindingView(string Id, int Revision, CaseJudgeView Judge, long Tick,
    string Result, string Standard, IReadOnlyList<string> EvidenceIds, string Reasons, string Uncertainty,
    string Consequence, IReadOnlyList<CasePartyView> Parties);
public sealed record RemedyTermView(string Id, string Kind, string ContributorId, string ContributorName,
    string? BeneficiaryId, string? BeneficiaryName, string? ItemKind, int Quantity, string? TargetId, string? TargetName);
public sealed record RemedyResponseView(string AgentId, string AgentName, int Revision, string Kind,
    long Tick, string? Reason);
public sealed record RemedyOfferView(string Id, string FindingId, int Revision,
    IReadOnlyList<RemedyTermView> Terms, string Reason, string NoticeId, long PublishedTick,
    long ResponseDeadlineTick, long CompletionTicks, string Status, IReadOnlyList<RemedyResponseView> Responses,
    IReadOnlyList<string> NoticeAwareContributorIds, string? ReplacesOfferId, string? AgreementId);
public sealed record RemedyEffectView(string Id, string TermId, string ActorId, string ActorName,
    long Tick, string Kind, string? BeneficiaryName, string? ItemKind, int Quantity, string? TargetName,
    string NativeReceiptId);
public sealed record RestorativeAgreementView(string Id, string OfferId, int OfferRevision,
    IReadOnlyList<RemedyTermView> Terms, IReadOnlyList<RemedyResponseView> Consents,
    long AcceptedTick, long DeadlineTick, string Status, string? ReplacesAgreementId,
    IReadOnlyList<RemedyEffectView> Effects)
{
    public bool Superseded { get; init; }
}
public sealed record TownNonLandCaseView(string Id, string Status, long FiledTick, long? SettledTick,
    string SubjectId, string SubjectName, string ConductKind, PositionView Position, long ConductTick,
    string Statement, TownLawView? ApplicableLaw, int AllegedLawVersion,
    IReadOnlyList<CaseRevisionView> Revisions, IReadOnlyList<CaseFilingView> Filings,
    IReadOnlyList<CaseEvidenceView> Evidence, IReadOnlyList<CaseReadView> Reads,
    IReadOnlyList<CaseResponseView> Responses, IReadOnlyList<ViolationFindingView> Findings,
    CaseJudgeView? Judge, IReadOnlyList<CaseJudgeTermView> JudgeHistory,
    CaseElectionView? JudgeElection, CaseElectionView? LatestJudgeElection,
    IReadOnlyList<CaseReopenRequestView> ReopenRequests, IReadOnlyList<RemedyOfferView> Offers,
    IReadOnlyList<RestorativeAgreementView> Agreements)
{
    public IReadOnlyList<CasePartyView> CurrentParties { get; init; } = [];
}
