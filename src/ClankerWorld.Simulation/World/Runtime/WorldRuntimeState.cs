using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed record AgentState(
    string AgentId,
    GridPoint Position,
    int FullnessBasisPoints,
    int MoveWaitTicks,
    string Personality,
    string Aspiration,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LastDecisionContext = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentProject? Project = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SurvivalCondition? Survival = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SkillLesson? Lesson = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ParenthoodPlan? Parenthood = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkProficiency? Proficiency = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SocialStandingEntry>? SocialStanding = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecentThought>? RecentThoughts = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int TravelCooldownTicks = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ExplorationState? Exploration = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IdentityChoicePending = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ChildPersonalModelSelection? ChildModelSelection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ModelAttempt? LastModelAttempt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<LearnedSkill>? Skills = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HousingState? Housing = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<HouseholdDeparture>? Departures = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PersonalEquipment? Equipment = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AgentIdentityMoment>? IdentityMoments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GuardianSearchState? GuardianSearch = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MedicalConsentState? MedicalConsent = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MedicalTreatmentState? MedicalTreatment = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GuardianPlacementState? GuardianPlacement = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MedicalSupplyTrip? MedicalSupplyTrip = null);

/// <summary>A saved, ordered request for an adult to accept primary care of a dependent.</summary>
public sealed record GuardianSearchState(
    string Stage,
    long StartedTick,
    long StageStartedTick,
    IReadOnlyList<string> OfferedAdultIds);

/// <summary>A child's non-secret personal-model choice, kept with the world rather than installation credentials.</summary>
public sealed record ChildPersonalModelSelection(
    string Role,
    string? Provider,
    string? EndpointIdentity,
    string? ModelId,
    string? CredentialSlotId,
    string ChoiceReason);

/// <summary>A birth-bound child model choice prepared by the playable host.</summary>
public sealed record PreparedChildModelSelection(string ChildId, ChildPersonalModelSelection Selection);

public sealed record ModelAttempt(
    string Status,
    long WorldTick,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LastAcceptedCandidateId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? LastAcceptedTick = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SetupBlocker = null);

public sealed record RecentThought(long WorldTick, string Text);

public sealed record ResourceStateEntry(string ResourceId, ResourceState State);

/// <param name="TownId">The Town the agent lived in when they died, which their will may name as an heir.</param>
public sealed record DeceasedAgentState(
    string AgentId,
    long DeathTick,
    int AgeAtDeath,
    AgentState LastPhysical,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TownId = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BoatIdAtDeath { get; init; }
}

public sealed record WorldEventEntry(
    long EventId,
    long WorldTick,
    string Kind,
    string Detail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GridPoint? Position = null);

public sealed record FounderSetupState(IReadOnlyList<string> FounderIds, bool Started);

public sealed record WorldRuntimeState(
    int SchemaVersion,
    string WorldSeed,
    WorldMap Map,
    SocietyWorldRuntimeState Society,
    IReadOnlyList<AgentState> Agents,
    IReadOnlyList<ResourceStateEntry> Resources,
    IReadOnlyList<WorldEventEntry> Events,
    IReadOnlyList<OwnerQueuedInstruction>? Instructions = null,
    IReadOnlyList<string>? CompletedInstructionIds = null,
    ContentRegistryState? Content = null,
    WorldSystemsState? WorldSystems = null,
    DeclarativeWorldContentState? WorldContent = null,
    WorldContentSimulationState? WorldSimulation = null,
    WorldAssetReservationLedgerState? AssetReservations = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] long EventHistoryFloor = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HistoryArchiveHead = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SurvivalState? Survival = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FoodStewardship? FoodStewardship = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DeceasedAgentState>? DeceasedAgents = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? JevEnabled = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] long JevPolicyRevision = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FounderSetupState? FounderSetup = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GeographyOptions? Geography = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<TownRuntimeState>? Towns = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorldKnowledgeState? Knowledge = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<GridPoint>? RoadTiles = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<BridgeState>? Bridges = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BridgeTrafficState? BridgeTraffic = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<FarmFieldState>? Fields = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AgentConversation>? Conversations = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AgentConversationDailyBudget>? ConversationBudgets = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<TownLandTitleRecord>? TownLandTitles = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<HouseholdLandUseRight>? HouseholdLandUseRights = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<HouseholdLandUseRequest>? HouseholdLandUseRequests = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ShopTradeState>? ShopTrades = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PopulationContinuity? Continuity = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<OwnerOrderCancellation>? OrderCancellations = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ToolMakingRequestState>? ToolMakingRequests = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<HandcartHitch>? HandcartHitches = null)
{
    [JsonRequired]
    public IReadOnlyList<RecordedInstructionReceipt> InstructionReceipts { get; init; } = [];

    [JsonRequired]
    public IReadOnlyList<WorldActionRefusal> ActionRefusals { get; init; } = [];
    [JsonRequired]
    public long RefusedActionCount { get; init; }
    [JsonRequired]
    public AnimalWorldState AnimalWorld { get; init; } = AnimalWorldState.Empty;
    [JsonRequired]
    public RoutineHelperSettings RoutineHelper { get; init; } = RoutineHelperSettings.Jev;

    [JsonRequired]
    public BoatTransportState BoatTransport { get; init; } = BoatTransportState.Empty();

    [JsonRequired]
    public IReadOnlyList<AgentMarriage> Marriages { get; init; } = [];
}

public sealed record WorldStepResult(
    bool Advanced,
    string Outcome,
    long WorldTick,
    IReadOnlyList<SocietyCognitionDispatchResult> Decisions,
    IReadOnlyList<WorldEventEntry> Events)
{
    public IReadOnlyList<WorldMemoryCompactionTransition> MemoryCompactionTransitions { get; init; } = [];
}
