using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.Contracts;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Launcher;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ClientPairingState = ClankerWorld.GodotClient.Pairing.OwnerPairingState;
using ClientPairingStatus = ClankerWorld.GodotClient.Pairing.OwnerPairingStatus;
using ClientProtocol = ClankerWorld.GodotClient.Pairing.OwnerPairingProtocol;

namespace ClankerWorld.Tests;

public sealed class DeviceJoinSessionTests
{
    [Theory]
    [InlineData("launcher", false)]
    [InlineData("terminal", true)]
    public async Task ApprovedJoinAndLostActivationReplyReconnectWithTheSameSavedKey(string kind, bool loseReply)
    {
        using var directory = new ClientTestDirectory("launcher-join-");
        using var baseHost = new ServerWebApplicationFactory(null, legacyPace: false);
        var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), new ManualTimeProvider());
        using var host = baseHost.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ClankerWorld:Hosting:Kind", kind);
            builder.ConfigureServices(services => services.AddSingleton(presence));
        });
        using var handler = new ObservePairing(host.Server.CreateHandler()) { LoseActivationReply = loseReply };
        using var http = new HttpClient(handler);
        using var signer = new TestSigner();
        var origin = new Uri("http://127.0.0.1");
        var path = Path.Combine(directory.FullName, "registration.json");
        var store = new OwnerDeviceRegistrationStore(path);
        var session = new DeviceJoinSession(new OwnerWorldApi(http), signer, store);
        await session.StartAsync(origin, CancellationToken.None);
        var pending = Assert.IsType<ClankerWorld.GodotClient.Pairing.OwnerPairingStart>(session.Pending);
        Assert.Equal(kind, pending.Host!.Kind);
        Assert.False(await session.PollAsync(CancellationToken.None));
        Assert.Null(session.Registration);
        Assert.False(File.Exists(path));
        var authority = host.Services.GetRequiredService<OwnerAuthorityStore>();
        Assert.True(authority.ApprovePendingPairingLocally(pending.PairingId, pending.PairingCode).IsSuccess);
        if (loseReply)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => session.PollAsync(CancellationToken.None));
            Assert.Same(pending, session.Pending);
            Assert.Null(session.Registration);
            Assert.False(File.Exists(path));
        }
        Assert.True(await session.PollAsync(CancellationToken.None));
        Assert.Null(session.Pending);
        Assert.Null(session.PendingOrigin);
        Assert.Equal(new OwnerDeviceRegistration(pending.Authority, pending.DeviceId,
            signer.PublicKeyFingerprint, origin.AbsoluteUri, pending.Host), session.Registration);
        Assert.Equal(1, handler.Activations);
        var saved = File.ReadAllText(path);
        Assert.DoesNotContain(pending.PairingCode, saved, StringComparison.Ordinal);
        Assert.DoesNotContain(pending.ActivationCanonicalProof, saved, StringComparison.Ordinal);
        // Restart the launcher: its signed access check uses the saved origin,
        // authority, device and durable signer, without starting pairing again.
        var restarted = new DeviceJoinSession(new OwnerWorldApi(http), signer, store);
        Assert.False(presence.HasActiveClient);
        await restarted.CheckConnectionAsync(CancellationToken.None);
        Assert.False(presence.HasActiveClient);
        Assert.Equal(1, handler.Pairings);
        Assert.Equal(0, handler.Reconnects);
        Assert.Equal(1, handler.RecoveryChecks);
        Assert.Equal(session.Registration, restarted.Registration);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.StartAsync(
            new Uri("https://different-host.test"), CancellationToken.None));
        Assert.Equal(1, handler.Pairings);
        Assert.Null(new OwnerDeviceRegistrationStore(path).TryLoad("different-key"));
        restarted.Forget();
        Assert.False(File.Exists(path));
        Assert.Null(restarted.Registration);
    }

    [Fact]
    public async Task ARefusedCheckpointKeepsItsRecoveryScreenReachableThroughTheLauncher()
    {
        using var directory = new ClientTestDirectory("launcher-join-recovery-");
        using var signer = new TestSigner();
        var store = new OwnerDeviceRegistrationStore(Path.Combine(directory.FullName, "registration.json"));
        var origin = new Uri("http://127.0.0.1");
        using (var original = new ServerWebApplicationFactory(directory.FullName, legacyPace: false))
        using (var client = original.CreateClient())
        {
            var session = new DeviceJoinSession(new OwnerWorldApi(client), signer, store);
            await session.StartAsync(origin, CancellationToken.None);
            var pending = session.Pending!;
            Assert.True(original.Services.GetRequiredService<OwnerAuthorityStore>()
                .ApprovePendingPairingLocally(pending.PairingId, pending.PairingCode).IsSuccess);
            Assert.True(await session.PollAsync(CancellationToken.None));
        }
        var checkpoint = Path.Combine(directory.FullName, "runtime.json");
        var damaged = System.Text.Encoding.UTF8.GetBytes("{damaged latest");
        File.WriteAllBytes(checkpoint, damaged);
        using var restarted = new ServerWebApplicationFactory(directory.FullName, legacyPace: false);
        using var http = restarted.CreateClient();
        var joined = new DeviceJoinSession(new OwnerWorldApi(http), signer, store);
        Assert.True(restarted.Services.GetRequiredService<WorldStartupRecovery>().Pending);
        await joined.CheckConnectionAsync(CancellationToken.None);
        Assert.False(restarted.Services.GetRequiredService<OwnerClientPresenceLease>().HasActiveClient);
        Assert.Equal(damaged, File.ReadAllBytes(checkpoint));
    }

    [Theory]
    [InlineData("authority")]
    [InlineData("device")]
    [InlineData("fingerprint")]
    [InlineData("expired")]
    public async Task WrongOrExpiredPairingCannotActivateOrWriteRegistration(string mismatch)
    {
        using var directory = new ClientTestDirectory("launcher-join-refuse-");
        using var host = new ServerWebApplicationFactory(null, legacyPace: false);
        using var handler = new ObservePairing(host.Server.CreateHandler());
        using var http = new HttpClient(handler);
        using var signer = new TestSigner();
        var path = Path.Combine(directory.FullName, "registration.json");
        var session = new DeviceJoinSession(new OwnerWorldApi(http), signer, new(path));
        await session.StartAsync(new Uri("http://127.0.0.1"), CancellationToken.None);
        var pending = session.Pending!;
        var status = new ClientPairingStatus(pending.Authority, pending.PairingId, pending.DeviceId,
            pending.PublicKeyFingerprint, ClientPairingState.Active, pending.ExpiresAtUtc);
        handler.Status = mismatch switch
        {
            "authority" => status with { Authority = new("different-host", pending.Authority.WorldId) },
            "device" => status with { DeviceId = "another-device" },
            "fingerprint" => status with { PublicKeyFingerprint = "another-key" },
            _ => status with { State = ClientPairingState.Expired },
        };
        if (mismatch == "expired")
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.PollAsync(CancellationToken.None));
        else await Assert.ThrowsAsync<InvalidDataException>(() => session.PollAsync(CancellationToken.None));
        Assert.Null(session.Pending);
        Assert.Null(session.PendingOrigin);
        Assert.Null(session.Registration);
        Assert.False(File.Exists(path));
        Assert.Equal(0, handler.Activations);
    }

    [Theory]
    [InlineData("launcher", "Start hosting")]
    [InlineData("terminal", "sudo systemctl start clankerworld-server.service")]
    [InlineData("future-kind", "private network")]
    public void ConnectionFailureUsesConditionalHostHintButAuthenticationAndTlsHaveTheirOwnMessages(string kind, string hint)
    {
        using var signer = new TestSigner();
        using var http = new HttpClient();
        using var directory = new ClientTestDirectory("launcher-join-hint-");
        var store = new OwnerDeviceRegistrationStore(Path.Combine(directory.FullName, "registration.json"));
        store.Save(new(new("host", "world"), "device", signer.PublicKeyFingerprint, "https://host.test/", new(kind)));
        var session = new DeviceJoinSession(new OwnerWorldApi(http), signer, store);
        var unavailable = session.FailureMessage(new HttpRequestException(HttpRequestError.ConnectionError, "private diagnostic"));
        Assert.Contains(hint, unavailable, StringComparison.Ordinal);
        Assert.StartsWith("Cannot connect to the host.", unavailable);
        Assert.DoesNotContain("private diagnostic", unavailable, StringComparison.Ordinal);
        if (kind != "future-kind") Assert.Contains("If it is stopped", unavailable, StringComparison.Ordinal);
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError })
            Assert.DoesNotContain(hint, session.FailureMessage(new HttpRequestException("private diagnostic", null, status)), StringComparison.Ordinal);
        Assert.Contains("certificate", session.FailureMessage(new HttpRequestException(HttpRequestError.SecureConnectionError, "private diagnostic")), StringComparison.Ordinal);
    }

    [Fact]
    public void RemoteStartArgumentsPinTheOriginAndKeepLauncherPathsSeparate()
    {
        var arguments = GameStarter.Arguments("launcher with spaces.exe", false, serverUrl: "https://host.test:443");
        Assert.Equal(["--", "--launcher=launcher with spaces.exe", "--server-url=https://host.test/"], arguments);
        Assert.Throws<GameInstallException>(() => GameStarter.Arguments("launcher.exe", false, serverUrl: "http://remote.test"));
        Assert.Throws<GameInstallException>(() => GameStarter.Arguments("launcher.exe", false, serverUrl: "https://host.test/other"));
    }

    private sealed class TestSigner : IOwnerDeviceSigner
    {
        private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public string PublicKeySpkiBase64 => ClientProtocol.ExportP256PublicKeySpkiBase64(key);
        public string PublicKeyFingerprint => ClientProtocol.CreatePublicKeyFingerprint(PublicKeySpkiBase64);
        public string SignCanonicalProof(string canonicalProof) => ClientProtocol.SignP256Sha256P1363(key, canonicalProof);
        public void Dispose() => key.Dispose();
    }

    private sealed class ObservePairing(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public bool LoseActivationReply { get; set; }
        public int Pairings { get; private set; }
        public int Activations { get; private set; }
        public int Reconnects { get; private set; }
        public int RecoveryChecks { get; private set; }
        public ClientPairingStatus? Status { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/v1/pairings") Pairings++;
            if (path.StartsWith("/api/v1/pairings/", StringComparison.Ordinal) && request.Method == HttpMethod.Get && Status is not null)
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Status) };
            if (path == OwnerPairingEndpoints.OwnerReconnect) Reconnects++;
            if (path == "/api/v1/owner/recovery/status") RecoveryChecks++;
            var response = await base.SendAsync(request, cancellationToken);
            if (path == OwnerPairingEndpoints.PairingActivation)
            {
                Activations++;
                if (LoseActivationReply)
                {
                    LoseActivationReply = false;
                    response.Dispose();
                    throw new HttpRequestException(HttpRequestError.ConnectionError, "synthetic lost activation reply");
                }
            }
            return response;
        }
    }
}
