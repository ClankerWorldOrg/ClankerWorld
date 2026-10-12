using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Saves;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClankerWorld.Tests;

public sealed class ServerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string stateDirectory;
    private readonly bool ownsStateDirectory;
    private readonly string? approvedAssetCatalogPath;
    private readonly bool gameWorld;
    private readonly bool legacyPace;
    private readonly bool configureProviderUsagePath;
    private readonly bool runClock;

    public ServerWebApplicationFactory()
        : this(null)
    {
    }

    internal ServerWebApplicationFactory(
        string? persistedStateDirectory,
        string? approvedAssetCatalogPath = null,
        bool gameWorld = true,
        bool legacyPace = true,
        bool configureProviderUsagePath = true,
        bool runClock = false)
    {
        ownsStateDirectory = persistedStateDirectory is null;
        stateDirectory = persistedStateDirectory ?? System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"clankerworld-server-http-{Guid.NewGuid():N}");
        this.approvedAssetCatalogPath = approvedAssetCatalogPath;
        this.gameWorld = gameWorld;
        this.legacyPace = legacyPace;
        this.configureProviderUsagePath = configureProviderUsagePath;
        this.runClock = runClock;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(stateDirectory);
        // Existing protocol tests exercise a populated world. New-world setup has its
        // own integration test; seed the older fixture explicitly instead of quietly
        // depending on the production host's default genesis.
        if (gameWorld && legacyPace && !File.Exists(System.IO.Path.Combine(stateDirectory, "runtime.json")))
        {
            using var seeded = new WorldStateFile(System.IO.Path.Combine(stateDirectory, "runtime.json"),
                newWorldPace: WorldStartPace.Legacy).LoadOrCreate("camp-alpha");
        }
        // All protocol tests cross the actual world host boundary.
        builder.UseSetting("ClankerWorld:World:Mode", gameWorld ? "game" : "fixture");
        builder.UseSetting("ClankerWorld:Clock:Run", runClock ? "true" : "false");
        builder.UseSetting("ClankerWorld:Clock:StrictTickFailures", "true");
        builder.UseSetting("ClankerWorld:Pairing:AuthorityPath", System.IO.Path.Combine(stateDirectory, "authority.json"));
        builder.UseSetting("ClankerWorld:World:SavePath", System.IO.Path.Combine(stateDirectory, "runtime.json"));
        builder.UseSetting(
            "ClankerWorld:Providers:ConfigurationPath",
            System.IO.Path.Combine(stateDirectory, "provider-configuration.json"));
        if (configureProviderUsagePath)
            builder.UseSetting("ClankerWorld:Providers:UsagePath",
                System.IO.Path.Combine(stateDirectory, "provider-usage.json"));
        builder.UseSetting("ClankerWorld:Pairing:ServerAuthorityId", "authority-http-tests");
        if (!string.IsNullOrWhiteSpace(approvedAssetCatalogPath))
        {
            builder.UseSetting("ClankerWorld:Assets:CatalogPath", approvedAssetCatalogPath);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && ownsStateDirectory && Directory.Exists(stateDirectory))
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }
}
