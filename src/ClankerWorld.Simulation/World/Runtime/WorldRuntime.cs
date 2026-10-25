using PendingModelDecision = ClankerWorld.Simulation.World.CognitionSystem.PendingModelDecision;
using PendingConversationTurn = ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// The live world composition for the first complete single-player
/// alpha. Society owns identity, lifecycle, relationships, and inventory;
/// this composition owns the map, physical positions/needs, cognition cadence,
/// and the world-facing event stream.
/// </summary>
public sealed partial class WorldRuntime : IDisposable
{
    public const int StateSchemaVersion = 139;
    // Founded Towns save laws, protected government changes and the mayor's office from this schema.
    public const int TownGovernmentSchemaVersion = 55;
    public const int ObserverGuidanceSchemaVersion = 41;
    public const int OrderLifecycleSchemaVersion = 49;
    public const int ChildModelSelectionSchemaVersion = 33;
    public const int ConversationSchemaVersion = 35;
    public const int PersonalEquipmentSchemaVersion = 37;
    private const int OrnamentEquipmentSchemaVersion = 52;
    public const int ReusableContainerSchemaVersion = 39;
    // Wills with several heirs, a Town heir, final words and the dead agent's Town.
    public const int WillHeirsSchemaVersion = 53;
    public const int DependentGuardianSearchSchemaVersion = 50;
    public const int GuardianPlacementSchemaVersion = 71;
    public const int ToolProgressionSchemaVersion = 42;
    public const int LifeMomentIdentitySchemaVersion = 43;
    public const int ContinuitySchemaVersion = 46;
    public const int TownAdmissionSchemaVersion = 54;
    internal const int MinimumSupportedStateSchemaVersion = StateSchemaVersion;
    private const string HouseholdId = "household:first";
    private const string SecondHouseholdId = "household:second";
    public const int RequiredFounders = 4;
    private const string FoodLotId = "food:first";
    private const long CognitionReevaluationIntervalTicks = 30;
    private const int ResourceInteractionRange = 1;
    private const int HarvestFoodYield = 4;

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly SemaphoreSlim tickGate = new(1, 1);
    private string worldSeed;
    private GeographyOptions? geographyOptions;
    private readonly CognitionSystem cognitionSystem;
    private Func<string, IDecisionProvider>? providerFactory => cognitionSystem.ProviderFactory;
    private int maxCognitionDispatchPerCycle => cognitionSystem.MaximumDispatch;
    private WorldMap map;
    private FarmingSystem farmingSystem;
    private readonly IFarmingWorld farmingWorld;
    private LandFertility fertility => farmingSystem.Fertility(map);
    private IReadOnlyList<FarmFieldState> fields => farmingSystem.State.Fields;
    private SocietyRuntime society;
    private ContentPackageRegistry contentRegistry;
    private EnvironmentSystem environmentSystem;
    private WorldSystemsState worldSystems => environmentSystem.State;
    private DeclarativeWorldContentState worldContent;
    private ConstructionSystem constructionSystem;
    private ProductionSystem productionSystem;
    private WorldContentSimulationState worldSimulation => productionSystem.Snapshot(constructionSystem.State);
    private WorldAssetReservationLedger assetReservations;
    private AgentStore agentStore = new();
    private IReadOnlyDictionary<string, AgentState> agents => agentStore.Living;
    private IReadOnlyDictionary<string, DeceasedAgentState> deceasedAgents => agentStore.Deceased;
    private Dictionary<string, ResourceState> resources = new(StringComparer.Ordinal);
    private CommunitySystem communitySystem = new(WorldKnowledgeState.Empty);
    private WorldKnowledgeState knowledge => communitySystem.State;
    private OrderSystem orderSystem = new();
    private OrderStore orderStore => orderSystem.Store;
    private IReadOnlyDictionary<string, OwnerQueuedInstruction> instructionsByIdempotency => orderStore.Instructions;
    private IReadOnlyDictionary<string, RecordedInstructionReceipt> instructionReceipts => orderStore.Receipts;
    private IReadOnlyDictionary<string, OwnerOrderCancellation> orderCancellations => orderStore.Cancellations;
    private IReadOnlySet<string> completedSuggestionIds => orderStore.CompletedSuggestions;
    private WorldEventLog worldEventLog = new([]);
    private readonly IAgentWorld agentWorld;
    private readonly IEnvironmentAgentWorld environmentWorld;
    private ReadOnlyCollection<WorldEventEntry> events => worldEventLog.Events;
    private long nextEventId => worldEventLog.NextEventId;
    private long eventHistoryFloor => worldEventLog.HistoryFloor;
    private string? historyArchiveHead => worldEventLog.ArchiveHead;
    private int checkpointSchemaVersion = StateSchemaVersion;
    private bool jevEnabled = true;
    private RoutineHelperSettings routineHelper = RoutineHelperSettings.Jev;
    private long jevPolicyRevision;
    private FounderSetupState? founderSetup { get => townSystem.State.FounderSetup; set => townSystem.RestoreFounderSetup(value); }
    private IReadOnlyList<TownRuntimeState> towns { get => townSystem.State.Towns; set => townSystem.RestoreTowns(value); }
    private IReadOnlyList<TownLandTitleRecord> townLandTitles { get => townSystem.State.TownLandTitles; set => townSystem.RestoreTownLandTitles(value); }
    private IReadOnlyList<HouseholdLandUseRight> householdLandUseRights { get => townSystem.State.HouseholdLandUseRights; set => townSystem.RestoreHouseholdLandUseRights(value); }
    private IReadOnlyList<HouseholdLandUseRequest> householdLandUseRequests { get => townSystem.State.HouseholdLandUseRequests; set => townSystem.RestoreHouseholdLandUseRequests(value); }
    private IReadOnlySet<GridPoint> roadTiles => transportSystem.Roads;
    private AnimalSystem animalSystem = new(AnimalWorldState.Empty);
    private AnimalWorldState animalWorld => animalSystem.State;
    private readonly IAnimalWorld animalWorldAdapter;
    public IReadOnlyList<AnimalState> Animals => animalWorld.Animals;
    private TransportSystem transportSystem = new();
    private IReadOnlyList<HandcartHitch> handcartHitches => transportSystem.Hitches;
    private ConversationSystem conversationSystem = new(new([], [], []));
    private readonly IConversationWorld conversationWorld;
    private TownSystem townSystem = new(CivicState.Empty);
    private readonly ITownWorld civicWorld;
    private ReadOnlyCollection<AgentConversation> conversations => conversationSystem.State.Conversations;
    private ReadOnlyCollection<AgentMarriage> marriages => conversationSystem.State.Marriages;
    private IReadOnlyList<AgentConversationDailyBudget> conversationBudgets => conversationSystem.State.Budgets;
    private GridPoint SharedStoragePosition => StartingStoragePosition(map, worldSimulation, towns);
    private static GridPoint StartingStoragePosition(WorldMap sourceMap, WorldContentSimulationState? simulation,
        IReadOnlyList<TownRuntimeState>? sourceTowns) =>
        sourceMap.MapObjects.FirstOrDefault(item => item.Id == "storage")?.Position ??
        simulation?.Buildings.FirstOrDefault(item => item.InstanceId == "first-town-warehouse")?.Position ??
        sourceTowns?.FirstOrDefault(item => item.OriginSite is not null)?.OriginSite ??
        sourceMap.Resources.First(item => item.Id == "berry-patch").Position;
    private GridPoint WeatherAnchor =>
        map.MapObjects.FirstOrDefault(item => item is { Id: "cooking", Kind: "cooking" })?.Position ??
        towns.FirstOrDefault(item => item.OriginSite is not null)?.OriginSite ??
        SharedStoragePosition;
    private readonly IReadOnlyDictionary<string, PendingModelDecision> pendingModelCalls;
    private readonly IReadOnlyDictionary<string, PendingConversationTurn> pendingConversationTurns;
    private IReadOnlyList<WorldMemoryCompactionTransition> memoryCompactionTransitions => cognitionSystem.MemoryCompactions;

    public WorldRuntime(
        string worldSeed,
        Func<string, IDecisionProvider>? providerFactory = null,
        int maxCognitionQueueLength = 64,
        int maxCognitionDispatchPerCycle = 4,
        WorldStartPace startPace = WorldStartPace.Legacy,
        GeographyOptions? geographyOptions = null,
        IModelWorkScheduler? modelWorkScheduler = null,
        IModelWorkObserver? modelWorkObserver = null,
        TimeProvider? timeProvider = null)
        : this(worldSeed, providerFactory, maxCognitionQueueLength, maxCognitionDispatchPerCycle, startPace, geographyOptions, preparedMap: null)
    {
        ConfigureModelWork(modelWorkScheduler ?? ThreadPoolModelWorkScheduler.Instance, modelWorkObserver, timeProvider);
    }

    private WorldRuntime(
        string worldSeed,
        Func<string, IDecisionProvider>? providerFactory,
        int maxCognitionQueueLength,
        int maxCognitionDispatchPerCycle,
        WorldStartPace startPace,
        GeographyOptions? geographyOptions,
        WorldMap? preparedMap,
        bool includeLegacyBedroll = false,
        WorldRuntimeState? restoringState = null,
        bool trustedPreparedState = false)
    {
        this.worldSeed = NormalizeRequiredText(worldSeed, nameof(worldSeed));
        conversationWorld = new ConversationWorldAdapter(this);
        agentWorld = new AgentWorldAdapter(this);
        environmentWorld = new EnvironmentWorldAdapter(this);
        civicWorld = new CivicWorldAdapter(this);
        farmingWorld = new FarmingWorldAdapter(this);
        animalWorldAdapter = new AnimalWorldAdapter(this);
        if (geographyOptions is not null &&
            (startPace != WorldStartPace.FounderSetup ||
             !string.Equals(geographyOptions.Seed, this.worldSeed, StringComparison.Ordinal)))
            throw new ArgumentException("Generated geography requires founder setup and the world seed.", nameof(geographyOptions));
        this.geographyOptions = geographyOptions;
        if (maxCognitionQueueLength <= 0 || maxCognitionDispatchPerCycle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCognitionQueueLength));
        }

        cognitionSystem = new(providerFactory, maxCognitionDispatchPerCycle);
        pendingModelCalls = cognitionSystem.ModelCalls;
        pendingConversationTurns = cognitionSystem.ConversationTurns;
        pendingIdentityMoments = cognitionSystem.IdentityMoments;
        contentRegistry = restoringState is null ? new ContentPackageRegistry() :
            ContentPackageRegistry.Restore(restoringState.Content);
        worldContent = restoringState?.WorldContent ?? new DeclarativeWorldContentState([], []);
        var simulation = restoringState is null ? WorldContentSimulationState.Empty :
            trustedPreparedState ? restoringState.WorldSimulation! :
            restoringState.WorldSimulation! with { CropBuilds = restoringState.WorldSimulation.CropBuilds ?? [] };
        constructionSystem = new(WorldConstructionState.From(simulation));
        productionSystem = new(simulation, restoringState?.ToolMakingRequests ?? []);
        assetReservations = new WorldAssetReservationLedger();
        map = preparedMap ?? (startPace == WorldStartPace.FounderSetup
            ? geographyOptions is null
                ? StartingSiteMapGenerator.Generate(this.worldSeed, includeLegacyBedroll)
                : GeographyCandidateSelector.GenerateCandidate(geographyOptions, includeLegacyBedroll: includeLegacyBedroll)
            : SmallTemperateMapGenerator.Generate(this.worldSeed, includeLegacyBedroll));
        farmingSystem = new(FarmingState.Empty, this.worldSeed);
        // Restore supplies an already validated saved state or a trusted tick
        // snapshot. Only a new world needs genesis ecology, chunks and weather.
        environmentSystem = new(restoringState?.WorldSystems ?? EnvironmentSystem.CreateWorldSystems(this.worldSeed, map, startPace, true));
        if (restoringState is not null)
        {
            // Restore never creates founders, physical state or genesis events.
            // Trusted tick snapshots retain their immutable prepared objects.
            society = SocietyRuntime.Restore(restoringState.Society, providerFactory);
            return;
        }

        // New-world genesis begins only after common runtime services exist.
        society = CreateSociety(
            this.worldSeed,
            geographyOptions is null ? this.worldSeed : GeneratedWorldId(this.worldSeed, geographyOptions, map),
            providerFactory,
            maxCognitionQueueLength,
            maxCognitionDispatchPerCycle,
            startPace);
        if (startPace is WorldStartPace.PlacedFounders or WorldStartPace.FounderSetup)
        {
            society.Pause();
        }
        if (startPace == WorldStartPace.FounderSetup)
        {
            founderSetup = new FounderSetupState([], false);
            if (geographyOptions is null)
                towns = [TownBorderRules.CreateFirstTown(map)];
        }
        else
            CreatePhysicalState();
        foreach (var resource in map.Resources)
        {
            resources.Add(resource.Id, ResourceState.Available);
        }
        // Out-of-season orchard trees start without fruit.
        SyncEcologyResourceStates();

        AppendEvent(new WorldCreatedEventPayload(null, this.worldSeed, agents.Count));
        if (towns.Count > 0) AppendEvent(new TownFoundingStartedEventPayload(null, TownBorderRules.FirstTownId));
        this.familySystem.StartContinuityRule(this.FamilyContinuityWorld);
    }

    private static string GeneratedWorldId(string seed, GeographyOptions options, WorldMap generatedMap)
    {
        // The seed controls generation; identity also binds the selected options
        // and exact accepted map. A name change must not create a duplicate.
        var identity = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = 1,
            Seed = seed,
            Geography = options,
            Map = generatedMap.ManifestDigest,
            Layers = MapLayerManifestCodec.Digest(generatedMap),
        });
        return "generated:" + Convert.ToHexStringLower(SHA256.HashData(identity));
    }

    /// <summary>Creates a world from the already previewed deterministic map.</summary>
    public static WorldRuntime CreateFromGeneratedGeography(string worldSeed,
        GeographyOptions geographyOptions, WorldMap preparedMap,
        Func<string, IDecisionProvider>? providerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(geographyOptions);
        ArgumentNullException.ThrowIfNull(preparedMap);
        if (!MatchesGeneratedCandidate(preparedMap, geographyOptions))
            throw new ArgumentException("The prepared map does not match the selected geography candidate.", nameof(preparedMap));

        return new WorldRuntime(worldSeed, providerFactory, 64, 4,
            WorldStartPace.FounderSetup, geographyOptions, preparedMap);
    }

    private static bool MatchesGeneratedCandidate(WorldMap map, GeographyOptions options)
    {
        var (width, height) = GeographyGenerator.Dimensions(options.Size);
        return map.Width == width && map.Height == height &&
            map.WrapsEastWest == options.WrapEastWest &&
            map.GenerationAttempt == options.CandidateAttempt &&
            string.Equals(MapManifestCodec.Digest(map), map.ManifestDigest, StringComparison.Ordinal);
    }

    public long WorldTick => society.Checkpoint.WorldTick;

    public SocietyCheckpoint Society => society.Checkpoint;

    public ContentRegistryState Content => contentRegistry.ExportState();

    public WorldSystemsState WorldSystems => worldSystems;

    public bool JevEnabled => jevEnabled;

    public RoutineHelperSettings RoutineHelper => routineHelper;

    public long JevPolicyRevision => jevPolicyRevision;

    public FounderSetupState? FounderSetup => founderSetup;

    public IReadOnlyList<TownRuntimeState> Towns => towns
        .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();

    public IReadOnlyList<TownLandTitleRecord> TownLandTitles => townLandTitles
        .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();

    public IReadOnlyList<HouseholdLandUseRight> HouseholdLandUseRights => householdLandUseRights
        .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();

    public IReadOnlyList<HouseholdLandUseRequest> HouseholdLandUseRequests => householdLandUseRequests
        .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();

    public IReadOnlyList<GridPoint> RoadTiles => roadTiles.OrderBy(item => item.Y).ThenBy(item => item.X).ToArray();

    public WorldContentSimulationState WorldSimulation => worldSimulation;

    public WorldAssetReservationLedgerState AssetReservations => assetReservations.ExportState();

    public WorldKnowledgeState Knowledge => knowledge;

    public IReadOnlyList<AgentState> Agents => agents.Values
        .OrderBy(item => item.AgentId, StringComparer.Ordinal)
        .ToArray();

    public WorldRuntimeState ExportState()
    {
        using var gateScope = EnterGate();
        return CaptureState();
    }

    public static WorldRuntime Restore(
        WorldRuntimeState state,
        Func<string, IDecisionProvider>? providerFactory = null,
        int maxCognitionDispatchPerCycle = 4,
        IModelWorkScheduler? modelWorkScheduler = null,
        IModelWorkObserver? modelWorkObserver = null,
        TimeProvider? timeProvider = null)
    {
        var runtime = RestoreCore(state, providerFactory, maxCognitionDispatchPerCycle, trustedPreparedState: false);
        runtime.ConfigureModelWork(modelWorkScheduler ?? ThreadPoolModelWorkScheduler.Instance, modelWorkObserver, timeProvider);
        return runtime;
    }

    private static WorldRuntime RestoreCore(
        WorldRuntimeState state,
        Func<string, IDecisionProvider>? providerFactory,
        int maxCognitionDispatchPerCycle,
        bool trustedPreparedState,
        bool applyLoadTransitions = true,
        ConversationState? preparedConversations = null,
        CivicState? preparedCivic = null,
        FarmingSystem? preparedFarming = null)
    {
        if (!trustedPreparedState) ValidateStateForCodec(state);
        var runtime = new WorldRuntime(
            state.WorldSeed,
            providerFactory,
            state.Society.Cognition.MaxQueueLength,
            maxCognitionDispatchPerCycle,
            state.FounderSetup is null ? WorldStartPace.Legacy : WorldStartPace.FounderSetup,
            state.Geography,
            trustedPreparedState ? state.Map : null,
            includeLegacyBedroll: state.Map.MapObjects.Any(item => item.Id == "bedroll" && item.Kind == "bedroll"),
            restoringState: state,
            trustedPreparedState: trustedPreparedState);
        if (!trustedPreparedState && !IsCompatibleSavedMap(runtime.map, state))
        {
            runtime.Dispose();
            throw new InvalidDataException("The world map does not match deterministic regeneration.");
        }

        runtime.map = state.Map;
        runtime.mapAuthoring = state.MapAuthoring;
        runtime.actionRefusals = state.ActionRefusals.ToList();
        runtime.refusedActionCount = state.RefusedActionCount;
        runtime.jevEnabled = state.JevEnabled ?? true;
        runtime.routineHelper = state.RoutineHelper;
        runtime.jevPolicyRevision = state.JevPolicyRevision;
        runtime.transportSystem.RestoreVehicles(state.HandcartHitches!, state.BoatTransport);
        runtime.animalSystem = new(state.AnimalWorld);
        runtime.farmingSystem = trustedPreparedState && preparedFarming is not null
            ? preparedFarming.Fork() : new(new(state.Fields!.OrderBy(field => field.Position.Y)
                .ThenBy(field => field.Position.X)), state.WorldSeed);
        if (trustedPreparedState && preparedCivic is not null)
            runtime.townSystem = new(preparedCivic);
        else
        {
            runtime.founderSetup = state.FounderSetup;
            runtime.towns = state.Towns!.OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
            runtime.townLandTitles = state.TownLandTitles!.OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
            runtime.householdLandUseRights = state.HouseholdLandUseRights!
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
            runtime.householdLandUseRequests = state.HouseholdLandUseRequests!
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
        }
        runtime.transportSystem.RestoreRoads(state.RoadTiles!);
        runtime.transportSystem.RestoreBridges(state.Bridges!.OrderBy(item => item.Id, StringComparer.Ordinal), state.BridgeTraffic!);
        runtime.conversationSystem = new(trustedPreparedState && preparedConversations is not null
            ? preparedConversations : new(state.Conversations!, state.ConversationBudgets!, state.Marriages));
        runtime.marketSystem.RestoreTrades(state.ShopTrades!);
        runtime.ApplyBridgeDecks();
        runtime.assetReservations = WorldAssetReservationLedger.Restore(state.AssetReservations);
        runtime.survivalState = state.Survival;
        runtime.foodStewardship = state.FoodStewardship;
        runtime.familySystem = new(state.Continuity!);
        RegionalWeatherRules.ValidateMap(runtime.worldSystems, runtime.map);
        runtime.agentStore = new(state.Agents, state.DeceasedAgents ?? []);

        runtime.resources.Clear();
        foreach (var resource in state.Resources)
        {
            runtime.resources.Add(resource.ResourceId, resource.State);
        }
        runtime.communitySystem = new(state.Knowledge!);

        try
        {
            runtime.orderStore.Restore(state.Instructions ?? [], state.CompletedInstructionIds ?? [],
                state.OrderCancellations ?? [], state.InstructionReceipts);
        }
        catch
        {
            runtime.Dispose();
            throw;
        }


        runtime.worldEventLog = new(state.Events, state.EventHistoryFloor, state.HistoryArchiveHead);
        if (!trustedPreparedState)
        {
            if (applyLoadTransitions)
            {
                runtime.SuspendRestoredConversations();
                TransportSystem.RepairSavedRoadFootprints(runtime.RoadWorld);
            }
            runtime.Validate();
        }
        return runtime;
    }

    public void Dispose()
    {
        // Disposal does not hold the runtime gate; its provider callbacks can
        // still apply their usage effects synchronously before the gate closes.
        foreach (var id in pendingModelCalls.Keys.ToArray()) CancelPendingModelCall(id, underRuntimeGate: false);
        foreach (var id in pendingWills.Keys.ToArray()) this.familyWills.CancelPendingWill(this.FamilyWillWorld, id, "interrupted", false);
        CancelIdentityMoments(underRuntimeGate: false);
        foreach (var id in pendingConversationTurns.Keys.ToArray()) CancelPendingConversationTurn(id,
            AgentConversationInterruption.Disconnected, underRuntimeGate: false);
        society.Dispose();
        ReleaseRouteSearches();
        gate.Dispose();
        tickGate.Dispose();
    }

    public void PersistCheckpoint(Func<WorldRuntimeState, WorldRuntimeState> persist, bool resumeOnSuccess = false)
    {
        ArgumentNullException.ThrowIfNull(persist);
        using var gateScope = EnterGate();
        if (resumeOnSuccess && society.Checkpoint.IsPaused)
        {
            // Keep the live world paused throughout the write. A failed write
            // discards this proposal, including its resume event and epoch.
            using var proposed = RestoreCore(CaptureState(), providerFactory, maxCognitionDispatchPerCycle,
                trustedPreparedState: true, preparedConversations: conversationSystem.CheckpointState,
                preparedCivic: townSystem.CheckpointState, preparedFarming: farmingSystem);
            proposed.Resume();
            var persisted = persist(proposed.CaptureState());
            if (persisted.HistoryArchiveHead != proposed.historyArchiveHead)
            {
                using var compacted = RestoreCore(persisted, providerFactory, maxCognitionDispatchPerCycle,
                    trustedPreparedState: false, applyLoadTransitions: false);
                CommitPreparedTick(compacted);
            }
            else CommitPreparedTick(proposed);
            return;
        }
        var saved = persist(CaptureState());
        if (saved.HistoryArchiveHead != historyArchiveHead)
        {
            // Compaction still validates the persisted state, but it is not
            // a load: live conversations and pending turn identities survive.
            using var compacted = RestoreCore(saved, providerFactory, maxCognitionDispatchPerCycle,
                trustedPreparedState: false, applyLoadTransitions: false);
            CommitPreparedTick(compacted);
        }
    }

    private WorldRuntimeState CaptureState() => new(
        checkpointSchemaVersion,
        worldSeed,
        map,
        society.ExportState(),
        agents.Values.OrderBy(item => item.AgentId, StringComparer.Ordinal).ToArray(),
        resources.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new ResourceStateEntry(item.Key, item.Value)).ToArray(),
        events.ToArray(),
        instructionsByIdempotency.Values
            .OrderBy(item => item.SubmissionSequence)
            .ToArray(),
        CompletedInstructionIds().OrderBy(item => item, StringComparer.Ordinal).ToArray(),
        contentRegistry.ExportState(),
        worldSystems,
        worldContent,
        worldSimulation,
        assetReservations.ExportState(), eventHistoryFloor, historyArchiveHead, survivalState, foodStewardship,
        deceasedAgents.Count == 0 ? null : deceasedAgents.Values.OrderBy(item => item.AgentId, StringComparer.Ordinal).ToArray(),
        jevPolicyRevision == 0 && jevEnabled ? null : jevEnabled, jevPolicyRevision, founderSetup,
        geographyOptions, towns.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(), knowledge,
        RoadTiles, Bridges, bridgeTraffic, fields.ToArray(),
        conversationSystem.CheckpointState.Conversations,
        conversationSystem.CheckpointState.Budgets,
        TownLandTitles, HouseholdLandUseRights, HouseholdLandUseRequests, ShopTrades, continuity,
        orderCancellations.Values.OrderBy(item => item.Receipt.WorldTick)
            .ThenBy(item => item.IdempotencyKey, StringComparer.Ordinal).ToArray(), ToolMakingRequests,
        handcartHitches.OrderBy(item => item.CartLotId, StringComparer.Ordinal).ToArray())
    {
        InstructionReceipts = instructionReceipts.Values.OrderBy(item => item.Receipt.Revision).ToArray(),
        Marriages = conversationSystem.CheckpointState.Marriages,
        BoatTransport = boatTransport,
        AnimalWorld = animalWorld,
        RoutineHelper = routineHelper,
        ActionRefusals = actionRefusals.ToArray(),
        RefusedActionCount = refusedActionCount,
        MapAuthoring = mapAuthoring,
    };

    private void AppendEvent(WorldEventPayload payload, GridPoint? eventPosition = null) =>
        worldEventLog.AppendEvent(agentWorld, payload, eventPosition);

}
