using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The owner's game client. It renders only signed,
/// server-issued world projections; all control buttons submit a one-use
/// device-key proof to the server and never mutate a local simulation copy.
/// </summary>
public partial class Main : Control
{
    private const int DefaultTileSize = 96;
    private const int TileGap = 0;
    private const int RefreshSeconds = 1;
    /// <summary>Space between the settings boxes and the scrollbar, in interface pixels.</summary>
    private const int SettingsScrollGap = 10;
    private const long StatusToastMilliseconds = 6_000;
    private const int SettingCaptionWidth = 135;
    private const string AppIconPath = "res://icon.ico";

    private readonly System.Net.Http.HttpClient httpClient = new();
    private readonly OwnerWorldApi ownerApi;
    private readonly OwnerWorldObservationSession observationSession = new();
    private readonly OwnerDeviceRegistrationStore registrationStore;
    private readonly OwnerPendingSubmissionStore pendingSubmissionStore;
    private readonly GameDisplayPreferencesStore displayPreferencesStore = new(
        GameUserPath("game-display-preferences.json"));
    private readonly Dictionary<long, OwnerWorldEvent> knownEvents = [];
    private readonly Dictionary<string, AgentMarker> agentVisuals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> mapObjectVisuals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> agentCanonicalXs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> mapObjectCanonicalXs = new(StringComparer.Ordinal);
    // Panels over the map, and the full-screen menus, both drawn at UI Scale.
    private readonly ScaledLayer uiLayer = new() { Name = "Interface" };
    private readonly ScaledLayer menuLayer = new() { Name = "Menus" };
    private readonly List<Label> settingCaptionLabels = [];

    private readonly Label statusLabel = new();
    private readonly PanelContainer statusToast = new();
    private readonly Button settingsButton = new();
    private readonly Button modLibraryButton = new();
    private readonly Button gameSettingsCategoryButton = new();
    private readonly Button worldSettingsCategoryButton = new();
    private readonly VBoxContainer gameSettingsContent = new();
    private readonly VBoxContainer worldSettingsContent = new();
    private readonly ScrollContainer settingsScroll = new();
    private readonly LineEdit serverUrlInput = new();
    private readonly PanelContainer agentModelPanel = new();
    private readonly PanelContainer usageLimitPanel = new();
    private readonly OptionButton agentModelRoleChoice = new();
    private readonly OptionButton agentModelTargetChoice = new();
    private readonly OptionButton agentModelProviderChoice = new();
    private readonly OptionButton agentModelCredentialChoice = new();
    private readonly ModelPicker agentModelPicker = new();
    private readonly OptionButton agentModelThinkingChoice = new();
    private readonly LineEdit agentModelApiKeyInput = new();
    private readonly LineEdit agentModelCredentialLabelInput = new();
    private readonly Label agentModelConfigurationStatus = new();
    private readonly Label agentModelCredentialHint = new();
    private readonly Label usageMeterStatus = new();
    private readonly LineEdit usageAttemptLimitInput = new();
    private readonly Button applyUsageLimitButton = new();
    private readonly Button grantUsageCallsButton = new();
    private readonly Button refreshUsageButton = new();
    private readonly Button saveAgentModelProviderButton = new();
    private readonly Button forgetAgentModelCredentialButton = new();
    private readonly Button deleteAgentModelCredentialSlotButton = new();
    private readonly Button openModelSettingsButton = new();
    private readonly Button refreshAgentModelProviderButton = new();

    private readonly HBoxContainer topBar = new();
    private readonly Button mapButton = new();
    private readonly Button worldInfoButton = new();
    private readonly Label clockLabel = new();
    private readonly Button agentsButton = new();
    private readonly Button eventLogButton = new();
    private readonly Button menuButton = new();
    private readonly ColorRect topBarShade = new();
    private readonly WorldTerrainLayer terrainLayer = new();
    private readonly WeatherLayer weatherLayer = new();
    private readonly NightLayer nightLayer = new();
    private readonly BuildingCompletionLayer buildingCompletionLayer = new();
    private readonly GoldenHourLayer goldenHourLayer = new();
    private readonly GraveLayer graveLayer = new();
    private readonly NightLightsLayer nightLightsLayer = new();
    private readonly SmokeLayer smokeLayer = new();
    private readonly StoredStockLayer storedStockLayer = new();
    private WorldTerrainMap? terrainMap;
    private string? terrainWorldId;
    private OwnerWorldPackedTerrain? terrainPackedTerrain;
    private OwnerWorldPackedMapLayers? terrainPackedLayers;
    private IReadOnlyList<OwnerWorldTile> terrainTiles = [];
    private readonly Control mapCanvas = new();
    private readonly Control mapStage = new();
    private readonly PanelContainer worldMapPanel = new();
    private readonly WorldMapAtlas worldMapAtlas = new();
    private readonly Control objectLayer = new();
    private readonly Control entityLayer = new();
    private readonly Label agentsSummaryLabel = new();
    private readonly PanelContainer agentQuickCard = new();
    private readonly VBoxContainer selectedAgentOverview = new();
    private readonly ScrollContainer selectedAgentModelScroll = new();
    private readonly VBoxContainer selectedAgentModelContent = new();
    private readonly Button modelSettingsButton = new();
    private readonly FittedLabel selectedActorNameLabel = new();
    private readonly LineEdit renameAgentInput = new();
    private readonly Button renameAgentButton = new();
    private readonly Label selectedActorSummaryLabel = new();
    private readonly Label selectedActorConditionLabel = new();
    private readonly Button clearSelectionButton = new();
    private readonly Button findAgentButton = new();
    private readonly Button familyTreeButton = new();
    private readonly PanelContainer familyTreePanel = new();
    private readonly FamilyTreeView familyTreeView = new();
    private readonly ScrollContainer familyTreeScroll = new();
    private readonly Label familyTreeStatus = new();
    private readonly ItemList agentList = new();
    private readonly RichTextLabel agentDetails = new();
    private readonly Label privateThoughtHistory = new();
    private readonly Button memoriesButton = new();
    private readonly PanelContainer memoriesPanel = new();
    private readonly RichTextLabel eventLog = new();
    private readonly PanelContainer agentsPanel = new();
    private readonly PanelContainer eventLogPanel = new();
    private readonly PanelContainer worldInfoPanel = new();
    private readonly PanelContainer selectedTilePanel = new();
    private readonly RichTextLabel selectedTileText = new();
    private Vector2I? selectedTile;
    private readonly PanelContainer pauseMenuPanel = new();
    private readonly VBoxContainer menuActions = new();
    private readonly PanelContainer settingsPanel = new();
    private readonly ColorRect menuShade = new();
    private readonly ColorRect appBackdrop = new();
    private readonly ColorRect worldBackdrop = new();
    private readonly OptionButton themeChoice = new();
    private readonly CheckButton cloudHazeToggle = new();
    private readonly CheckButton lightningToggle = new();
    private readonly Label menuHeadingLabel = new();
    private readonly Button menuCloseButton = new();
    private readonly Button menuResumeButton = new();
    private readonly Button quitGameButton = new();
    private readonly ConfirmationDialog quitGameConfirmation = new();
    private readonly CheckButton fullscreenToggle = new();
    private readonly OptionButton windowSizeChoice = new();
    private static readonly Vector2I[] DisplaySizePresets =
    [
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
    ];
    private readonly OptionButton clockFormatChoice = new();
    private readonly OptionButton dateFormatChoice = new();
    private readonly OptionButton agingOverrideChoice = new();
    private readonly OptionButton routineHelperChoice = new();
    private readonly ModelPicker routineHelperModelPicker = new();
    private readonly OptionButton routineHelperKeyChoice = new();
    private readonly Button applyRoutineHelperButton = new();
    private string? observedRoutineHelperContext;
    private readonly Button applyAgingOverrideButton = new();
    private int? lastObservedAgingOverride;
    private string? lastAgingOverrideWorldId;

    private readonly BoldTextButton pauseButton = new() { Captions = ["Pause", "Paused"] };
    private readonly LineEdit instructionText = new();
    private readonly Button submitInstructionButton = new();
    private readonly Label pendingSubmissionLabel = new();
    private readonly Button retryPendingSubmissionButton = new();
    private readonly Button forgetPendingSubmissionButton = new();
    private readonly OptionButton worldEditKind = new();
    private readonly LineEdit worldEditId = new();
    private readonly LineEdit worldEditValue = new();
    private readonly LineEdit worldEditSecondaryValue = new();
    private readonly SpinBox worldEditX = new();
    private readonly SpinBox worldEditY = new();
    private readonly CheckBox worldEditRenewable = new();
    private readonly Button submitWorldEditButton = new();
    private readonly Label worldEditHintLabel = new();
    private readonly LineEdit pairingApprovalId = new();
    private readonly LineEdit pairingApprovalCode = new();
    private readonly Button approvePairingButton = new();
    private readonly Button refreshDevicesButton = new();
    private readonly ItemList pairedDeviceList = new();
    private readonly LineEdit revokeDeviceId = new();
    private readonly Button revokeDeviceButton = new();

    private OwnerDeviceKey? deviceKey;
    private OwnerDeviceRegistration? registration
    {
        get => observationSession.Registration;
        set
        {
            // Applies to load, forget, ordinary activation and recovered activation.
            refreshCancellation?.Cancel();
            observationSession.ReplaceRegistration(value);
            knownEvents.Clear();
            usageReads++;
            usageStatus = null;
            usageLimitEdited = false;
            usageAttemptLimitInput.Text = string.Empty;
            RenderUsageStatus();
        }
    }
    private OwnerDevice[] pairedDevices = [];
    private OwnerProviderConfigurationStatus? providerConfiguration;
    private OwnerUsageStatus? usageStatus;
    private string? usagePauseWorldId;
    private bool wasObservedPaused;
    private OwnerPendingSubmission? pendingSubmission;
    private string? selectedAgentId;
    private string? renamingAgentId;
    private readonly RefusedAgentRename refusedAgentRename = new();
    private bool isRefreshing;
    private CancellationTokenSource? refreshCancellation;
    private int successfulRefreshCount;
    private bool isOwnerAction;
    private bool registeredEndpointInvalid;
    private bool menuPausedWorld;
    private bool menuPauseConfirmed;
    private int pendingMenuResumes;
    private long pendingMenuResumeGeneration;
    private OwnerWorldSnapshot? renderedMapSnapshot;
    private int currentTileSize = DefaultTileSize;
    private float cameraZoom = 1;
    private float minimumCameraZoom = 0.65f;
    private float maximumCameraZoom = 4f;
    private Vector2 cameraCenterTiles;
    private string? cameraWorldId;
    private bool draggingMap;
    private GameDisplayPreferences displayPreferences = new();
    private OwnerWorldCalendarPace? observedCalendarPace;
    private string? renderedEventLog;
    private StatusToastKind statusToastKind;
    private long statusToastShownAtMsec;

    public Main()
    {
        ownerApi = new OwnerWorldApi(httpClient);
        localServer = FindBundledLocalServer();
        localServerStarting = localServer is not null;
        // A game with its own local server keeps that pairing apart from any server
        // pairing an older client saved in the same user folder.
        var prefix = localServer is null ? string.Empty : "local-";
        registrationStore = new OwnerDeviceRegistrationStore(
            GameUserPath($"{prefix}owner-device-registration.json"));
        pendingSubmissionStore = new OwnerPendingSubmissionStore(
            GameUserPath($"{prefix}owner-pending-submission.json"));
    }

    public override void _Ready()
    {
        // Developer command: rewrite the Windows program icon from the logo art.
        if (OS.HasFeature("editor") && OS.GetCmdlineUserArgs().Contains("--write-app-icon", StringComparer.Ordinal))
        {
            File.WriteAllBytes(ProjectSettings.GlobalizePath(AppIconPath), MenuLogo.IconFile());
            GetTree().Quit();
            return;
        }
        displayPreferences = displayPreferencesStore.Load();
        ApplySavedDisplaySettings();
        // The logo's robot and planet are the window and taskbar icon.
        if (DisplayServer.GetName() != "headless")
            DisplayServer.SetIcon(MenuLogo.Icon(64));
        // Pixel frames, buttons and icons stay crisp when the picture is scaled.
        TextureFilter = TextureFilterEnum.Nearest;
        UiTheme.Apply(GetTree().Root, UiTheme.Resolve(UiTheme.Parse(displayPreferences.Theme)));
        if (OS.GetCmdlineUserArgs().Contains("--ui-smoke-test", StringComparer.Ordinal))
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        BuildLayout();
        BuildKeyboardNavigation();
        BindOwnerRequestPanels();
        UiTheme.Changed += ApplyThemeColors;
        ApplyThemeColors();
        ApplyUiScale();
        GetWindow().SizeChanged += RefreshRenderSize;
        ShowMainMenu();
        if (OS.GetCmdlineUserArgs().Contains("--profile-open-smoke-test", StringComparer.Ordinal))
        {
            _ = VerifyProfileOpenAsync();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--launcher-options-smoke-test", StringComparer.Ordinal))
        {
            VerifyDeveloperRunOptions();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--measure-map-draw", StringComparer.Ordinal))
        {
            _ = MeasureMapDrawAsync();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--ui-smoke-test", StringComparer.Ordinal))
        {
            _ = VerifyMenuLayoutAsync();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--world-edit-smoke-test", StringComparer.Ordinal))
        {
            _ = VerifyWorldEditSmokeAsync();
            return;
        }
        _ = TryGetCommandLineServerUrl(out var commandLineUrl);
        serverUrlInput.Text = localServer?.Origin.AbsoluteUri ?? commandLineUrl ?? ConfiguredServerUrl();
        _ = localServer is null ? InitializeAsync() : StartLocalServerAsync();

        var timer = new Godot.Timer
        {
            WaitTime = RefreshSeconds,
            Autostart = true,
        };
        timer.Timeout += () => _ = PulseAsync();
        AddChild(timer);
    }

    public override void _ExitTree()
    {
        CancelManualSaveListRead();
        refreshCancellation?.Cancel();
        CancelAutosaveSettingsRead();
        worldListRequest.Dispose();
        UiTheme.Changed -= ApplyThemeColors;
        deviceKey?.Dispose();
        httpClient.Dispose();
        // Failure dialogs join the tree only when shown; the retained address
        // control has no panel. The scene tree cannot free unparented nodes.
        foreach (var node in new Node[] { launcherStartFailure, localServerFailure, localServerStopFailure, serverUrlInput })
            if (node.GetParent() is null) node.Free();
        base._ExitTree();
    }


}
