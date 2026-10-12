using System.Text.Json;
using System.Net;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Control;
using ClankerWorld.Server.Endpoints;
using ClankerWorld.Server.Observation;

var builder = WebApplication.CreateBuilder(args);
var runtimeSeed = builder.Configuration["ClankerWorld:Runtime:Seed"] ?? SeededWorldObservationStore.SampleSeed;
var configuredWorldMode = builder.Configuration["ClankerWorld:Runtime:WorldMode"] ?? "game";
if (!string.Equals(configuredWorldMode, "game", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(configuredWorldMode, "fixture", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Unsupported ClankerWorld:Runtime:WorldMode '{configuredWorldMode}'. Expected game or fixture.");
}

var isGameWorld = string.Equals(configuredWorldMode, "game", StringComparison.OrdinalIgnoreCase);
var advanceFixture = builder.Configuration.GetValue<bool>("ClankerWorld:Runtime:AdvanceScript");
var advanceRuntime = isGameWorld
    ? builder.Configuration.GetValue("ClankerWorld:Runtime:AdvanceScript", true)
    : advanceFixture;
var clientPresenceTimeoutSeconds = builder.Configuration.GetValue(
    "ClankerWorld:Runtime:ClientPresenceTimeoutSeconds",
    5);
if (clientPresenceTimeoutSeconds is < 2 or > 60)
{
    throw new InvalidOperationException(
        "ClankerWorld:Runtime:ClientPresenceTimeoutSeconds must be between 2 and 60 seconds.");
}
var configuredDecisionProvider = builder.Configuration["ClankerWorld:Runtime:DecisionProvider"] ?? "deterministic";
var configuredJevModel = builder.Configuration["ClankerWorld:Runtime:JevModel"] ?? "jev-1.13.0";
var configuredModel = builder.Configuration["ClankerWorld:Runtime:Model"];
var configuredOpenAiModel = builder.Configuration["ClankerWorld:Runtime:OpenAiModel"] ??
    (configuredDecisionProvider.StartsWith("openai", StringComparison.OrdinalIgnoreCase) ? configuredModel : null);
var configuredOllamaCloudModel = builder.Configuration["ClankerWorld:Runtime:OllamaCloudModel"] ??
    (configuredDecisionProvider.StartsWith("ollama", StringComparison.OrdinalIgnoreCase) ? configuredModel : null);
var configuredAnthropicModel = builder.Configuration["ClankerWorld:Runtime:AnthropicModel"] ??
    (configuredDecisionProvider.StartsWith("anthropic", StringComparison.OrdinalIgnoreCase) ||
     configuredDecisionProvider.StartsWith("claude", StringComparison.OrdinalIgnoreCase) ? configuredModel : null);
var publicPort = builder.Configuration.GetValue("ClankerWorld:Http:Port", 5188);
var localApprovalPort = builder.Configuration.GetValue<int?>("ClankerWorld:Pairing:LocalApprovalPort") ?? 0;
if (publicPort is <= 0 or > 65535 || localApprovalPort is < 0 or > 65535 || localApprovalPort == publicPort)
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
    if (localApprovalPort > 0)
    {
        options.Listen(IPAddress.Loopback, localApprovalPort);
    }
});

var authorityStatePath = builder.Configuration["ClankerWorld:Pairing:StatePath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "saves", "owner-authority.json");
var configuredRuntimeStatePath = builder.Configuration["ClankerWorld:Runtime:StatePath"];
var runtimeStatePath = configuredRuntimeStatePath ??
    Path.Combine(
        builder.Environment.ContentRootPath,
        "saves",
        isGameWorld ? "world.json" : "fixture-runtime.json");
var legacyRuntimeStatePath = builder.Configuration["ClankerWorld:Runtime:LegacyStatePath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "saves", "fixture-runtime.json");
var worldStatePath = isGameWorld
    ? runtimeStatePath
    : builder.Configuration["ClankerWorld:Runtime:WorldStatePath"] ??
        Path.Combine(builder.Environment.ContentRootPath, "saves", "world.json");
var providerConfigurationPath = builder.Configuration["ClankerWorld:Runtime:ProviderStatePath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "saves", "provider-configuration.json");
var providerUsagePath = builder.Configuration["ClankerWorld:Runtime:ProviderUsagePath"] ??
    Path.Combine(Path.GetDirectoryName(Path.GetFullPath(providerConfigurationPath))!, "provider-usage.json");
var approvedAssetCatalogPath = builder.Configuration["ClankerWorld:Assets:CatalogPath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "approved-assets.json");
var configuredAuthorityId = builder.Configuration["ClankerWorld:Pairing:ServerAuthorityId"] ??
    $"clankerworld-host:{Environment.MachineName}";

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
builder.Services.AddSingleton<OwnerWorldStateFile>(services => new OwnerWorldStateFile(
    isGameWorld ? legacyRuntimeStatePath : runtimeStatePath,
    approvedAssetCatalog,
    services.GetRequiredService<IDecisionProvider>()));
builder.Services.AddSingleton<OwnerWorldRuntime>(services => services
    .GetRequiredService<OwnerWorldStateFile>()
    .LoadOrCreate(runtimeSeed));
builder.Services.AddSingleton<WorldStateFile>(services => new WorldStateFile(
    worldStatePath,
    _ => services.GetRequiredService<IDecisionProvider>(),
    WorldStartPace.FounderSetup,
    allowDifferentSavedSeed: isGameWorld));
builder.Services.AddSingleton(services => new ManualWorldSaveStore(worldStatePath,
    services.GetRequiredService<ILogger<ManualWorldSaveStore>>(),
    services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate));
builder.Services.AddSingleton<ISaveDiskSpaceProbe, SaveDiskSpaceProbe>();
builder.Services.AddSingleton(services => new SaveDiskSpaceMonitor(worldStatePath,
    services.GetRequiredService<ISaveDiskSpaceProbe>(), services.GetRequiredService<ILogger<SaveDiskSpaceMonitor>>()));
if (isGameWorld)
    builder.Services.AddHostedService(services => services.GetRequiredService<SaveDiskSpaceMonitor>());
if (isGameWorld)
    builder.Services.AddSingleton(services => new WorldStartupRecovery(
        services.GetRequiredService<WorldStateFile>(), services.GetRequiredService<ManualWorldSaveStore>(),
        services.GetRequiredService<ProviderConfigurationStore>(), _ => services.GetRequiredService<IDecisionProvider>(),
        runtimeSeed, authorityStatePath, services.GetRequiredService<ILogger<WorldStartupRecovery>>()));
builder.Services.AddSingleton<WorldRuntime>(services =>
{
    var runtime = isGameWorld ? services.GetRequiredService<WorldStartupRecovery>().Runtime
        : services.GetRequiredService<WorldStateFile>().LoadOrCreate(runtimeSeed);
    services.GetRequiredService<WorldJevPolicy>().Initialize(runtime.JevEnabled, runtime.JevPolicyRevision, runtime.RoutineHelper);
    return runtime;
});
builder.Services.AddSingleton<WorldAutosaveStore>(services => new WorldAutosaveStore(
    worldStatePath, isGameWorld && services.GetRequiredService<WorldStartupRecovery>().Pending
        ? services.GetRequiredService<WorldStartupRecovery>().WorldId
        : services.GetRequiredService<WorldRuntime>().Society.WorldId,
    allowWorldSwitch: isGameWorld,
    mutationGate: services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate));
if (isGameWorld)
{
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
        _ => services.GetRequiredService<IDecisionProvider>()));
}
builder.Services.AddSingleton<OwnerWorldObservationStore>(services => isGameWorld
    ? new OwnerWorldObservationStore(services.GetRequiredService<WorldRuntime>())
    : new OwnerWorldObservationStore(services.GetRequiredService<OwnerWorldRuntime>()));
builder.Services.AddSingleton(new OwnerAuthorityStateFile(authorityStatePath));
// Set only when the game starts this host on the player's own PC.
var companionSecretPath = builder.Configuration["ClankerWorld:Pairing:CompanionSecretPath"];
if (companionSecretPath is not null && localApprovalPort == 0)
{
    throw new InvalidOperationException(
        "ClankerWorld:Pairing:CompanionSecretPath needs ClankerWorld:Pairing:LocalApprovalPort.");
}
var pairingHostOptions = new OwnerPairingHostOptions(localApprovalPort,
    companionSecretPath is null ? null : OwnerPairingHostOptions.ReadCompanionSecret(companionSecretPath));
builder.Services.AddSingleton(pairingHostOptions);
builder.Services.AddSingleton<PairingRequestBudget>();
builder.Services.AddSingleton<HostRequestRefusals>();
builder.Services.AddSingleton<OwnerAuthorityStore>(services =>
{
    var worldId = isGameWorld
        ? services.GetRequiredService<WorldStartupRecovery>().Pending
            ? services.GetRequiredService<WorldStartupRecovery>().WorldId
            : services.GetRequiredService<WorldRuntime>().Society.WorldId
        : services.GetRequiredService<OwnerWorldRuntime>().Capture().Snapshot.World.Identity.WorldId;
    return services.GetRequiredService<OwnerAuthorityStateFile>().LoadOrCreate(
        new OwnerAuthorityIdentity(configuredAuthorityId, worldId),
        allowWorldSwitch: isGameWorld);
});
builder.Services.AddSingleton<OwnerRequestAuthorizer>();
builder.Services.AddSingleton(new OwnerClientPresenceLease(
    TimeSpan.FromSeconds(clientPresenceTimeoutSeconds)));
if (isGameWorld)
    builder.Services.AddSingleton(services => ActivatorUtilities.CreateInstance<WorldRuntimeService>(services,
        builder.Configuration.GetValue<bool>("ClankerWorld:Runtime:StrictTickFailures")
            ? WorldTickFailurePolicy.Strict : WorldTickFailurePolicy.RefuseSingleAction));
if (advanceRuntime)
{
    if (isGameWorld)
    {
        builder.Services.AddHostedService<WorldStartupService>();
        builder.Services.AddHostedService(services => new WorldCatalogWarmUpService(services,
            services.GetRequiredService<WorldStartupRecovery>()));
    }
    else
    {
        builder.Services.AddHostedService<OwnerWorldRuntimeService>();
    }
}

var app = builder.Build();
// Recover an interrupted selection before hosted services, requests or model dispatch.
if (isGameWorld && !app.Services.GetRequiredService<WorldStartupRecovery>().Pending)
    _ = app.Services.GetRequiredService<WorldCatalogStore>();
if (app.Services.GetRequiredService<ProviderUsageStore>().Capture().AccountingError is not null)
    ProviderUsageTelemetry.AccountingBlocked(app.Logger);
ProviderCredentialTelemetry.Ready(app.Logger, OperatingSystem.IsWindows() ? "windows_current_user" : "private_file_permissions");
var providerUsage = app.Services.GetRequiredService<ProviderUsageStore>();
if (isGameWorld)
{
    var usageEffects = new Lazy<ProviderUsageWorldEffects>(() => new ProviderUsageWorldEffects(
        app.Services.GetRequiredService<WorldRuntime>(),
        app.Services.GetRequiredService<WorldStateFile>(),
        providerUsage,
        app.Services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate,
        app.Logger));
    providerUsage.LimitReached += () => usageEffects.Value.PauseAtLimit();
    providerUsage.WarningReached += warning => usageEffects.Value.RecordWarning(warning);
}
else
{
    providerUsage.LimitReached += () =>
    {
        var runtime = app.Services.GetRequiredService<OwnerWorldRuntime>();
        if (runtime.Pause("provider_usage_limit"))
            app.Services.GetRequiredService<OwnerWorldStateFile>().Save(runtime);
        ProviderUsageTelemetry.LimitReached(app.Logger, "paused", 0);
    };
    // The fixture world has no Event Log line for this; the log still records it.
    providerUsage.WarningReached += warning =>
        ProviderUsageTelemetry.WarningReached(app.Logger, "logged_only", warning.Attempts, warning.AttemptLimit, 0);
}
app.UseRequestBodyLimits();
app.UseDefaultFiles();
app.UseStaticFiles();

if (isGameWorld)
{
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
}

app.MapOwnerEndpoints(isGameWorld);

app.Run();

public partial class Program;
