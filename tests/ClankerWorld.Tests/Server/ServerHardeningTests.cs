using System.Collections.Concurrent;
using System.Net;
using ClankerWorld.Server;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClankerWorld.Tests;

public sealed class ServerHardeningTests
{
    [Fact]
    public void CredentialRecordsOmitAllSecretsFromPrinting()
    {
        const string secret = "synthetic-record-secret";
        var credential = new StoredProviderCredential("model", secret);
        var slot = new ProviderCredentialSlot("slot", "openai", "Test", secret);
        object[] records =
        [
            credential, slot,
            new ProviderConfigurationSeed("deterministic", null, secret, null, secret, null, secret, null, secret),
            new ProviderConfigurationState(4, 1, "deterministic", "deterministic", credential, credential, credential,
                CredentialSlots: [slot], Anthropic: credential),
            new RuntimeProviderConfiguration("deterministic", "deterministic", credential, credential, credential, 1,
                CredentialSlots: [slot], Anthropic: credential),
            new FrozenChildModelBinding(new ChildPersonalModelSelection("planning", "openai", null, "model", null, "test"), secret),
        ];
        foreach (var record in records) Assert.DoesNotContain(secret, record.ToString());
    }

    [Fact]
    public void TemporaryCredentialCleanupKeepsLiveFilesAndOtherState()
    {
        var directory = Directory.CreateTempSubdirectory("credential-cleanup-");
        try
        {
            var config = Path.Combine(directory.FullName, "provider.json");
            File.WriteAllText(config, "keep current state");
            var stale = Path.Combine(directory.FullName, $".provider.json.{Guid.NewGuid():N}.tmp");
            var busy = Path.Combine(directory.FullName, $".provider.json.{Guid.NewGuid():N}.tmp");
            var unrelated = Path.Combine(directory.FullName, ".provider.json.not-a-guid.tmp");
            File.WriteAllText(stale, "synthetic temporary credential data");
            File.WriteAllText(unrelated, "keep unrelated state");
            using (var writer = new FileStream(busy, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Assert.Equal((1, 1), ProviderConfigurationStore.RemoveTemporaryFiles(config));
                Assert.False(File.Exists(stale));
                Assert.True(File.Exists(busy));
            }
            Assert.Equal((1, 0), ProviderConfigurationStore.RemoveTemporaryFiles(config));
            Assert.False(File.Exists(busy));
            Assert.Equal("keep current state", File.ReadAllText(config));
            Assert.Equal("keep unrelated state", File.ReadAllText(unrelated));
            if (!OperatingSystem.IsWindows())
            {
                var link = Path.Combine(directory.FullName, $".provider.json.{Guid.NewGuid():N}.tmp");
                File.CreateSymbolicLink(link, config);
                Assert.Equal((0, 1), ProviderConfigurationStore.RemoveTemporaryFiles(config));
                Assert.Equal("keep current state", File.ReadAllText(config));
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void PrivateStorageProtectsTheFirstWriteAndSavedMetadata()
    {
        var root = Directory.CreateTempSubdirectory("private-storage-");
        try
        {
            var directory = Path.Combine(root.FullName, "saves");
            PrivateStorage.EnsureDirectory(directory);
            var path = Path.Combine(directory, "checkpoint");
            using (var stream = PrivateStorage.CreateNew(path))
            {
                if (!OperatingSystem.IsWindows())
                    Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
                stream.Write([1, 2, 3]);
            }
            Assert.Throws<IOException>(() => PrivateStorage.CreateNew(path));
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
            Assert.True(SavedBuild.TryWrite(path));
            _ = new WorldAutosaveStore(path, "world-private");
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(directory));
                foreach (var file in Directory.GetFiles(directory))
                    Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                PrivateStorage.EnsureDirectory(directory);
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task ProviderClientsRefuseRedirectsAndOversizedBufferedResponses()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        ProviderHttpClients.Configure(builder.Services);
        await using var app = builder.Build();
        var followed = 0;
        app.MapGet("/redirect", () => Results.Redirect("/target"));
        app.MapGet("/target", () => { Interlocked.Increment(ref followed); return Results.NoContent(); });
        app.MapGet("/large", () => Results.Bytes(new byte[ProviderResponseBody.MaximumBytes + 1]));
        await app.StartAsync();
        try
        {
            var origin = new Uri(app.Urls.Single());
            foreach (var name in new[] { "model", "typesafe" })
            {
                using var client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient(name);
                Assert.Equal(ProviderResponseBody.MaximumBytes, client.MaxResponseContentBufferSize);
                using var redirected = await client.GetAsync(new Uri(origin, "/redirect"));
                Assert.Equal(HttpStatusCode.Found, redirected.StatusCode);
                await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri(origin, "/large")));
            }
            Assert.Equal(0, followed);
        }
        finally { await app.StopAsync(); }
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(413)]
    [InlineData(500)]
    public async Task RequestErrorsKeepTheirStatusAndPrivateDetails(int status)
    {
        const string detail = "synthetic-private-exception-detail";
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        using var log = new ExceptionLog();
        builder.Logging.AddProvider(log);
        await using var app = builder.Build();
        SafeServerErrors.Configure(app, app.Logger);
        Exception failure = status == 500 ? new InvalidOperationException(detail) : new BadHttpRequestException(detail, status);
        app.MapGet("/failure/{id}", IResult (string id) => throw failure);
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/failure/public-device");
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain(detail, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(log.Messages, message => message.Contains(detail, StringComparison.Ordinal));
        if (status == 500)
        {
            Assert.Contains(log.Messages, message => message.Contains("server_request outcome=failed error_type=InvalidOperationException", StringComparison.Ordinal));
            Assert.Contains(log.Messages, message => message.Contains("route=/failure/{id}", StringComparison.Ordinal) &&
                !message.Contains("public-device", StringComparison.Ordinal));
        }
        else Assert.DoesNotContain(log.Messages, message => message.Contains("server_request outcome=failed", StringComparison.Ordinal));
        await app.StopAsync();
    }

    private sealed class ExceptionLog : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(Messages);
        public void Dispose() { }

        private sealed class Capture(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception) + exception);
        }
    }
}
