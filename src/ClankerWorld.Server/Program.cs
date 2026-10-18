using System.Text.Json;
using System.Net;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Content;
using ClankerWorld.Server.Endpoints;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;

var builder = WebApplication.CreateBuilder(args);
var runtimeSeed = builder.Configuration["ClankerWorld:World:Seed"] ?? "world-a";
var configuredWorldMode = builder.Configuration["ClankerWorld:World:Mode"] ?? "game";
if (!string.Equals(configuredWorldMode, "game", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Unsupported ClankerWorld:World:Mode '{configuredWorldMode}'. Only the game world is supported; fixture world mode has been removed.");
}

var runClock = builder.Configuration.GetValue("ClankerWorld:Clock:Run", true);
var clientPresenceTimeoutSeconds = builder.Configuration.GetValue(
    "ClankerWorld:Clock:ClientPresenceTimeoutSeconds",
    5);
if (clientPresenceTimeoutSeconds is < 2 or > 60)
{
    throw new InvalidOperationException(
        "ClankerWorld:Clock:ClientPresenceTimeoutSeconds must be between 2 and 60 seconds.");
}
var configuredDecisionProvider = builder.Configuration["ClankerWorld:Providers:DecisionProvider"] ?? "deterministic";
var configuredJevModel = builder.Configuration["ClankerWorld:Providers:JevModel"] ?? PlayerDecisionProviders.DefaultJevModel;
var configuredModel = builder.Configuration["ClankerWorld:Providers:Model"];
var configuredOpenAiModel = builder.Configuration["ClankerWorld:Providers:OpenAiModel"] ??
    (configuredDecisionProvider.StartsWith("openai", StringComparison.OrdinalIgnoreCase) ? configuredModel : null);
var configuredOllamaCloudModel = builder.Configuration["ClankerWorld:Providers:OllamaCloudModel"] ??
    (configuredDecisionProvider.StartsWith("ollama", StringComparison.OrdinalIgnoreCase) ? configuredModel : null);
var configuredAnthropicModel = builder.Configuration["ClankerWorld:Providers:AnthropicModel"] ??
    (configuredDecisionProvider.StartsWith("anthropic", StringComparison.OrdinalIgnoreCase) ||
     configuredDecisionProvider.StartsWith("claude", StringComparison.OrdinalIgnoreCase) ? configuredModel : null);
var publicPort = builder.Configuration.GetValue("ClankerWorld:Http:Port", 5188);
var approvalPort = builder.Configuration.GetValue<int?>("ClankerWorld:Pairing:ApprovalPort") ?? 0;
if (publicPort is <= 0 or > 65535 || approvalPort is < 0 or > 65535 || approvalPort == publicPort)
{
    throw new InvalidOperationException("ClankerWorld HTTP ports must be distinct values between 1 and 65535.");
}

builder.WebHost.ConfigureKestrel(options =>
{
    // The general limit is the listener's default. RequestBodyLimits sets each
    // request's own limit before any endpoint reads the body, raising it only
    // for signed owner routes.
    options.Limits.MaxRequestBodySize = RequestBodyLimits.Unauthenticated;
    options.Listen(IPAddress.Loopback, publicPort);
    if (approvalPort > 0)
    {
        options.Listen(IPAddress.Loopback, approvalPort);
    }
});

var authorityStatePath = builder.Configuration["ClankerWorld:Pairing:AuthorityPath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "saves", "owner-authority.json");
var configuredRuntimeStatePath = builder.Configuration["ClankerWorld:World:SavePath"];
var runtimeStatePath = configuredRuntimeStatePath ??
    Path.Combine(
        builder.Environment.ContentRootPath,
        "saves",
        "world.json");
var worldStatePath = runtimeStatePath;
var providerConfigurationPath = builder.Configuration["ClankerWorld:Providers:ConfigurationPath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "saves", "provider-configuration.json");
var providerUsagePath = builder.Configuration["ClankerWorld:Providers:UsagePath"] ??
    Path.Combine(Path.GetDirectoryName(Path.GetFullPath(providerConfigurationPath))!, "provider-usage.json");
var approvedAssetCatalogPath = builder.Configuration["ClankerWorld:Assets:CatalogPath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "approved-assets.json");
// Unset, the authority file's own ID is kept, or a new server gets a random one.
var configuredAuthorityId = builder.Configuration["ClankerWorld:Pairing:ServerAuthorityId"];

// The catalog is loaded only from a host-owned path at startup. Its absence
// intentionally yields an empty, deny-all allow-list; malformed existing
// catalog files stop startup rather than becoming a partial approval set.
var approvedAssetCatalog = ApprovedAssetCatalog.LoadOrDeny(approvedAssetCatalogPath);
builder.Services.AddSingleton<IOwnerApprovedAssetReferencePolicy>(approvedAssetCatalog);
builder.Services.AddSingleton(approvedAssetCatalog);
builder.Services.AddHttpClient("typesafe");
builder.Services.AddHttpClient("model");
builder.Services.AddSingleton<WorldJevPolicy>();
builder.Services.AddSingleton(new ProviderUsageStore(providerUsagePath));
builder.Services.AddSingleton(new ProviderConfigurationStore(
    providerConfigurationPath,
    new ProviderConfigurationSeed(
        configuredDecisionProvider,
        configuredJevModel,
        Environment.GetEnvironmentVariable("TYPESAFE_API_KEY"),
        configuredOpenAiModel,
        Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        configuredOllamaCloudModel,
        Environment.GetEnvironmentVariable("OLLAMA_API_KEY"),
        configuredAnthropicModel,
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"))));
builder.Services.AddSingleton(services => new ProviderModelCatalog(
    services.GetRequiredService<ProviderConfigurationStore>(),
    services.GetRequiredService<IHttpClientFactory>()));
builder.Services.AddSingleton<ProviderSetupCheckService>();
builder.Services.AddSingleton<ConfigurableDecisionProvider>();
builder.Services.AddSingleton<IDecisionProvider>(services =>
    services.GetRequiredService<ConfigurableDecisionProvider>());
builder.Services.AddSingleton<WorldStateFile>(services => new WorldStateFile(
    worldStatePath,
    _ => services.GetRequiredService<IDecisionProvider>(),
    WorldStartPace.FounderSetup,
    allowDifferentSavedSeed: true));
builder.Services.AddSingleton(services => new ManualWorldSaveStore(worldStatePath,
    services.GetRequiredService<ILogger<ManualWorldSaveStore>>(),
    services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate));
builder.Services.AddSingleton<ISaveDiskSpaceProbe, SaveDiskSpaceProbe>();
builder.Services.AddSingleton(services => new SaveDiskSpaceMonitor(worldStatePath,
    services.GetRequiredService<ISaveDiskSpaceProbe>(), services.GetRequiredService<ILogger<SaveDiskSpaceMonitor>>()));
builder.Services.AddHostedService(services => services.GetRequiredService<SaveDiskSpaceMonitor>());
builder.Services.AddSingleton(services => new WorldStartupRecovery(
        services.GetRequiredService<WorldStateFile>(), services.GetRequiredService<ManualWorldSaveStore>(),
        services.GetRequiredService<ProviderConfigurationStore>(), _ => services.GetRequiredService<IDecisionProvider>(),
        runtimeSeed, authorityStatePath, services.GetRequiredService<ILogger<WorldStartupRecovery>>()));
builder.Services.AddSingleton<WorldRuntime>(services =>
{
    var runtime = services.GetRequiredService<WorldStartupRecovery>().Runtime;
    services.GetRequiredService<WorldJevPolicy>().Initialize(runtime.JevEnabled, runtime.JevPolicyRevision, runtime.RoutineHelper);
    return runtime;
});
builder.Services.AddSingleton<WorldAutosaveStore>(services => new WorldAutosaveStore(
    worldStatePath, services.GetRequiredService<WorldStartupRecovery>().Pending
        ? services.GetRequiredService<WorldStartupRecovery>().WorldId
        : services.GetRequiredService<WorldRuntime>().Society.WorldId,
    allowWorldSwitch: true,
    mutationGate: services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate));

builder.Services.AddSingleton<WorldCatalogStore>(services =>
{
    var providers = services.GetRequiredService<ProviderConfigurationStore>();
    var autosave = services.GetRequiredService<WorldAutosaveStore>();
    var catalog = new WorldCatalogStore(worldStatePath,
        services.GetRequiredService<WorldRuntime>().ExportState(),
        providers.CaptureRuntimeConfiguration().Assignments ?? [], autosave.Capture(), providers.WorldMutationGate);
    var active = catalog.Active();
    if (catalog.RecoveredSelection)
    {
        if (!(providers.CaptureRuntimeConfiguration().Assignments ?? [])
            .SequenceEqual(active.Assignments))
            providers.RestoreWorldAssignments(active.Assignments);
        if (active.AutosaveSettings is { } settings && autosave.Capture() != settings)
            autosave.SelectWorld(active.WorldId, settings);
    }
    lock (providers.WorldMutationGate)
    {
        try
        {
            var saves = services.GetRequiredService<ManualWorldSaveStore>();
            saves.RecoverDeletions();
            catalog.RecoverDeletions(saves.DeleteWorldSnapshots);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            ManualWorldSaveTelemetry.DeletionPending(services.GetRequiredService<ILogger<WorldCatalogStore>>(),
                "startup", exception.GetType().Name);
        }
    }
    return catalog;
});
builder.Services.AddSingleton<WorldSelectionCoordinator>(services => new WorldSelectionCoordinator(
    services.GetRequiredService<WorldCatalogStore>(),
    services.GetRequiredService<WorldRuntime>(),
    services.GetRequiredService<WorldStateFile>(),
    services.GetRequiredService<ProviderConfigurationStore>(),
    services.GetRequiredService<WorldAutosaveStore>(),
    services.GetRequiredService<WorldJevPolicy>(),
    services.GetRequiredService<ILogger<WorldSelectionCoordinator>>(),
    _ => services.GetRequiredService<IDecisionProvider>(),
    services.GetRequiredService<ManualWorldSaveStore>()));

builder.Services.AddSingleton<OwnerWorldObservationStore>(services =>
    new OwnerWorldObservationStore(services.GetRequiredService<WorldRuntime>()));
builder.Services.AddSingleton(new OwnerAuthorityStateFile(authorityStatePath));
// Set only when the game starts this host on the player's own PC.
var launchSecretPath = builder.Configuration["ClankerWorld:LocalServer:LaunchSecretPath"];
if (launchSecretPath is not null && approvalPort == 0)
{
    throw new InvalidOperationException(
        "ClankerWorld:LocalServer:LaunchSecretPath needs ClankerWorld:Pairing:ApprovalPort.");
}
var approvalListenerOptions = new ApprovalListenerOptions(approvalPort,
    launchSecretPath is null ? null : ApprovalListenerOptions.ReadLaunchSecret(launchSecretPath),
    launchSecretPath is null && approvalPort > 0
        ? ApprovalSecretFile.LoadOrCreate(builder.Configuration["ClankerWorld:Pairing:ApprovalSecretPath"] ??
            authorityStatePath + ".approval-header") : null);
builder.Services.AddSingleton(approvalListenerOptions);
builder.Services.AddSingleton<PairingRequestBudget>();
builder.Services.AddSingleton<RequestRefusals>();
builder.Services.AddSingleton<OwnerAuthorityStore>(services =>
{
    var worldId = services.GetRequiredService<WorldStartupRecovery>().Pending
        ? services.GetRequiredService<WorldStartupRecovery>().WorldId
        : services.GetRequiredService<WorldRuntime>().Society.WorldId;
    var stateFile = services.GetRequiredService<OwnerAuthorityStateFile>();
    return stateFile.LoadOrCreate(
        new OwnerAuthorityIdentity(configuredAuthorityId ?? stateFile.StoredOrNewServerAuthorityId(), worldId),
        allowWorldSwitch: true);
});
builder.Services.AddSingleton<OwnerRequestAuthorizer>();
builder.Services.AddSingleton(new OwnerClientPresenceLease(
    TimeSpan.FromSeconds(clientPresenceTimeoutSeconds)));
builder.Services.AddSingleton(services => ActivatorUtilities.CreateInstance<WorldRuntimeService>(services,
    builder.Configuration.GetValue<bool>("ClankerWorld:Clock:StrictTickFailures")
        ? WorldTickFailurePolicy.Strict : WorldTickFailurePolicy.RefuseSingleAction));
if (runClock)
{
    builder.Services.AddHostedService<WorldStartupService>();
    builder.Services.AddHostedService(services => new WorldCatalogWarmUpService(services,
        services.GetRequiredService<WorldStartupRecovery>()));
}

var app = builder.Build();
// Recover an interrupted selection before hosted services, requests or model dispatch.
if (!app.Services.GetRequiredService<WorldStartupRecovery>().Pending)
    _ = app.Services.GetRequiredService<WorldCatalogStore>();
if (app.Services.GetRequiredService<ProviderUsageStore>().Capture().AccountingError is not null)
    ProviderUsageTelemetry.AccountingBlocked(app.Logger);
ProviderCredentialTelemetry.Ready(app.Logger, OperatingSystem.IsWindows() ? "windows_current_user" : "private_file_permissions");
var providerUsage = app.Services.GetRequiredService<ProviderUsageStore>();

var usageEffects = new Lazy<ProviderUsageWorldEffects>(() => new ProviderUsageWorldEffects(
    app.Services.GetRequiredService<WorldRuntime>(),
    app.Services.GetRequiredService<WorldStateFile>(),
    providerUsage,
    app.Services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate,
    app.Logger));
providerUsage.LimitReached += () => usageEffects.Value.PauseAtLimit();
providerUsage.WarningReached += warning => usageEffects.Value.RecordWarning(warning);

app.UseRequestBodyLimits();
app.UseDefaultFiles();
app.UseStaticFiles();


var recovery = app.Services.GetRequiredService<WorldStartupRecovery>();
app.Use(async (context, next) =>
{
    // Run before endpoint parameter binding: no runtime or world-mutating
    // service may initialize while the refused checkpoint is still active.
    if (recovery.Pending && context.Request.Path.StartsWithSegments("/api/v1/owner") &&
        context.Request.Path != "/api/v1/owner/challenges" &&
        context.Request.Path != "/api/v1/owner/saves/disk-status" &&
        context.Request.Path != "/api/v1/owner/recovery/status" &&
        context.Request.Path != "/api/v1/owner/recovery/restore")
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new
        {
            code = "startup_recovery_required",
            error = "The latest saved state could not be opened. Choose recovery before opening the world."
        });
        return;
    }
    await next(context);
});


app.MapOwnerEndpoints();

app.Run();

public partial class Program;
