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
    int HungerBasisPoints,
    int MoveWaitTicks,
    string Personality,
    string Aspiration,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LastDecisionContext = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementProject? Project = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SurvivalCondition? Survival = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementLesson? Lesson = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementParenthood? Parenthood = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementProficiency? Proficiency = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SettlementSocialStanding>? SocialStanding = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AgentThought>? RecentThoughts = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int TravelCooldownTicks = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementExploration? Exploration = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IdentityChoicePending = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ChildPersonalModelSelection? ChildModelSelection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ModelAttempt? LastModelAttempt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SettlementSkill>? Skills = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementHousing? Housing = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SettlementDeparture>? Departures = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PersonalEquipment? Equipment = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AgentIdentityMoment>? IdentityMoments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementGuardianSearch? GuardianSearch = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MedicalConsentState? MedicalConsent = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MedicalTreatmentState? MedicalTreatment = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementGuardianPlacement? GuardianPlacement = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MedicalSupplyTrip? MedicalSupplyTrip = null);

/// <summary>A saved, ordered request for an adult to accept primary care of a dependent.</summary>
public sealed record SettlementGuardianSearch(
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

public sealed record AgentThought(long WorldTick, string Text);

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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementSurvivalState? Survival = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementCouncil? Council = null,
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<BusinessTradeState>? BusinessTrades = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SettlementContinuity? Continuity = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<OwnerOrderCancellation>? OrderCancellations = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ToolMakingRequestState>? ToolMakingRequests = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<HandcartHitch>? HandcartHitches = null)
{
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
