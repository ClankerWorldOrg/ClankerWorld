using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Server.Pairing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClankerWorld.Tests;

/// <summary>Request bodies are capped per route before any JSON is parsed.</summary>
public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData("/api/v1/owner/reconnect", RequestBodyLimits.SignedOwnerAction)]
    [InlineData("/API/V1/Owner/worlds/create", RequestBodyLimits.SignedOwnerAction)]
    [InlineData("/api/v1/owner/challenges", RequestBodyLimits.Unauthenticated)]
    [InlineData("/api/v1/handshake", RequestBodyLimits.Unauthenticated)]
    [InlineData("/api/v1/approval/devices/device_1/revoke", RequestBodyLimits.Unauthenticated)]
    [InlineData("/api/v1/approval/shutdown", RequestBodyLimits.Unauthenticated)]
    [InlineData("/api/v1/ownerx", RequestBodyLimits.Unauthenticated)]
    [InlineData("/", RequestBodyLimits.Unauthenticated)]
    [InlineData("/api/v1/pairings/activate", RequestBodyLimits.Pairing)]
    [InlineData("/api/v1/approval/pairings/pairing_1/approve", RequestBodyLimits.Pairing)]
    public void OnlySignedOwnerRoutesGetTheLargerBodyLimit(string path, long limit) =>
        Assert.Equal(limit, RequestBodyLimits.For(path));

    [Fact]
    public async Task OversizedUnauthenticatedBodiesAreRefusedBeforeTheyAreParsed()
    {
        using var host = new ServerWebApplicationFactory();
        using var client = host.CreateClient();
        var log = new RecordingLogger<RequestRefusals>();
        host.Services.GetRequiredService<ILoggerFactory>().AddProvider(new RecordingLoggerProvider<RequestRefusals>(log));

        // Not JSON at all: a parsed body would be a 400, so a 413 shows nothing read it.
        using var malformed = await client.PostAsync("/api/v1/owner/challenges", Body(64));
        using var challenge = await client.PostAsync("/api/v1/owner/challenges",
            Body((int)RequestBodyLimits.Unauthenticated + 1));
        using var recovery = await client.PostAsync("/api/v1/approval/devices/device_not_real/revoke",
            Body((int)RequestBodyLimits.Unauthenticated + 1));

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, challenge.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, recovery.StatusCode);
        // One line for the burst, with fixed fields and no request text.
        var refusal = Assert.Single(log.Messages, message => message.StartsWith("request_body_limit", StringComparison.Ordinal));
        Assert.Equal("request_body_limit outcome=refused route_kind=unauthenticated limit_bytes=65536 skipped=0", refusal);
        Assert.DoesNotContain(log.Messages, message => message.Contains("xxxx", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignedOwnerRequestsUpToTheirLimitSucceedAndLargerOnesNeverReachTheSignatureCheck()
    {
        using var host = new ServerWebApplicationFactory();
        using var client = host.CreateClient();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var device = await StartAndActivateAsync(host, client, key);
        var action = new OwnerReconnectAction(0);
        var payload = OwnerActionPayload.Reconnect(action);

        using var normal = await SendSignedAsync(host, client, key, device.DeviceId, "/api/v1/owner/reconnect", action, payload);
        Assert.Equal(HttpStatusCode.OK, normal.StatusCode);

        var atLimit = await CreateSignedRequestAsync(host, client, key, device.DeviceId, "/api/v1/owner/reconnect", action, payload);
        using var fits = await client.PostAsync("/api/v1/owner/reconnect",
            PaddedJson(atLimit, RequestBodyLimits.SignedOwnerAction));
        Assert.Equal(HttpStatusCode.OK, fits.StatusCode);

        var overLimit = await CreateSignedRequestAsync(host, client, key, device.DeviceId, "/api/v1/owner/reconnect", action, payload);
        using var refused = await client.PostAsync("/api/v1/owner/reconnect",
            PaddedJson(overLimit, RequestBodyLimits.SignedOwnerAction + 1));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, refused.StatusCode);
        // The refused request did not use its one-time challenge.
        using var retried = await client.PostAsJsonAsync("/api/v1/owner/reconnect", overLimit);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    /// <summary>
    /// The real server, as the game and an operator reach it: the approval
    /// listener checks the host name, and the listener's small default limit is
    /// raised only for signed owner routes, also for bodies without a length.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KestrelApprovalListenerChecksTheHostNameAndCapsStreamedBodies(bool approveByCode)
    {
        var publicPort = FreeLoopbackPort();
        var approvalPort = FreeLoopbackPort();
        using var baseHost = new ServerWebApplicationFactory();
        using var host = baseHost.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ClankerWorld:Http:Port", publicPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("ClankerWorld:Pairing:ApprovalPort",
                approvalPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });
        host.UseKestrel();
        host.StartServer();
        var log = new RecordingLogger<RequestRefusals>();
        host.Services.GetRequiredService<ILoggerFactory>().AddProvider(new RecordingLoggerProvider<RequestRefusals>(log));
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{publicPort}/") };
        var approvalOrigin = new Uri($"http://127.0.0.1:{approvalPort}/");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var start = await client.PostAsJsonAsync("/api/v1/pairings",
            new StartOwnerPairingHttpRequest(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())));
        var pairing = (await start.Content.ReadFromJsonAsync<OwnerPairingStart>())!;
        var approvePath = new Uri(approvalOrigin, approveByCode
            ? "api/v1/approval/pairings/approve" : $"api/v1/approval/pairings/{pairing.PairingId}/approve");

        using (var foreignName = new HttpRequestMessage(HttpMethod.Post, approvePath)
        {
            Content = JsonContent.Create(new PairingApprovalHttpRequest(pairing.PairingCode)),
        })
        {
            foreignName.Headers.Host = $"example.test:{approvalPort}";
            using var refused = await client.SendAsync(foreignName);
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        }
        var authority = host.Services.GetRequiredService<OwnerAuthorityStore>();
        Assert.Equal(OwnerPairingState.Pending, authority.GetPairingStatus(pairing.PairingId).Value!.State);
        // A fixed line that never repeats the name the request used.
        Assert.Equal("approval_listener outcome=refused reason=host_name skipped=0",
            Assert.Single(log.Messages, message => message.StartsWith("approval_listener", StringComparison.Ordinal)));

        var approvalSecret = ApprovalSecretFile.LoadOrCreate(host.Services.GetRequiredService<IConfiguration>()
            ["ClankerWorld:Pairing:AuthorityPath"]! + ".approval-header");
        using var recoveryKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var recoveryBody = new StartOwnerPairingHttpRequest(Convert.ToBase64String(recoveryKey.ExportSubjectPublicKeyInfo()));
        var recoveryPath = new Uri(approvalOrigin, "api/v1/approval/pairings");
        using var refusedRecovery = await client.PostAsJsonAsync(recoveryPath, recoveryBody);
        Assert.Equal(HttpStatusCode.NotFound, refusedRecovery.StatusCode);
        using var recoveryRequest = new HttpRequestMessage(HttpMethod.Post, recoveryPath)
        {
            Content = JsonContent.Create(recoveryBody),
        };
        recoveryRequest.Headers.Add(ApprovalListenerOptions.ApprovalSecretHeader, approvalSecret);
        using var recoveredPairing = await client.SendAsync(recoveryRequest);
        Assert.Equal(HttpStatusCode.OK, recoveredPairing.StatusCode);
        foreach (var supplied in new[] { null, "wrong-secret" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, approvePath)
            {
                Content = JsonContent.Create(new PairingApprovalHttpRequest(pairing.PairingCode)),
            };
            if (supplied is not null) request.Headers.Add(ApprovalListenerOptions.ApprovalSecretHeader, supplied);
            using var refused = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
            Assert.Equal(OwnerPairingState.Pending, authority.GetPairingStatus(pairing.PairingId).Value!.State);
        }
        using var approval = new HttpRequestMessage(HttpMethod.Post, approvePath)
        {
            Content = JsonContent.Create(new PairingApprovalHttpRequest(pairing.PairingCode)),
        };
        approval.Headers.Add(ApprovalListenerOptions.ApprovalSecretHeader, approvalSecret);
        using var approved = await client.SendAsync(approval);
        Assert.Equal(approveByCode ? HttpStatusCode.NoContent : HttpStatusCode.OK, approved.StatusCode);
        using var activated = await client.PostAsJsonAsync("/api/v1/pairings/activate", new ActivateOwnerPairingHttpRequest(
            pairing.PairingId, pairing.ActivationCanonicalProof, Sign(key, pairing.ActivationCanonicalProof)));
        var device = (await activated.Content.ReadFromJsonAsync<OwnerDevice>())!;

        // A signed request past the listener's default, streamed without a length.
        var action = new OwnerReconnectAction(0);
        var envelope = await CreateSignedRequestAsync(host, client, key, device.DeviceId, "/api/v1/owner/reconnect", action,
            OwnerActionPayload.Reconnect(action));
        using var streamed = new HttpRequestMessage(HttpMethod.Post, "/api/v1/owner/reconnect")
        {
            Content = new StreamedJson(PaddedJsonBytes(envelope, 4 * RequestBodyLimits.Unauthenticated)),
        };
        using var large = await client.SendAsync(streamed);
        Assert.Equal(HttpStatusCode.OK, large.StatusCode);

        var revokePath = new Uri(approvalOrigin, $"api/v1/approval/devices/{device.DeviceId}/revoke");
        using var refusedRevocation = await client.PostAsJsonAsync(revokePath, new DeviceRevokeHttpRequest(null));
        Assert.Equal(HttpStatusCode.NotFound, refusedRevocation.StatusCode);
        Assert.Equal(OwnerDeviceState.Active, authority.GetDevice(device.DeviceId).Value!.State);
        using var revoke = new HttpRequestMessage(HttpMethod.Post, revokePath)
        {
            Content = JsonContent.Create(new DeviceRevokeHttpRequest(null)),
        };
        revoke.Headers.Add(ApprovalListenerOptions.ApprovalSecretHeader, approvalSecret);
        using var revoked = await client.SendAsync(revoke);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(OwnerDeviceState.Revoked, authority.GetDevice(device.DeviceId).Value!.State);
        Assert.Equal("approval_listener outcome=refused reason=proof skipped=0",
            Assert.Single(log.Messages, message => message.Contains("reason=proof", StringComparison.Ordinal)));
        Assert.DoesNotContain(log.Messages, message => message.Contains(approvalSecret, StringComparison.Ordinal));

        // Valid JSON must be read through the body limit; an invalid prefix
        // can be rejected with 400 before the later oversized chunk arrives.
        using var oversized = new HttpRequestMessage(HttpMethod.Post,
            new Uri(approvalOrigin, "api/v1/approval/devices/device_not_real/revoke"))
        {
            Content = new StreamedJson(PaddedJsonBytes(new DeviceRevokeHttpRequest(null), RequestBodyLimits.Unauthenticated + 1024)),
        };
        using var tooLarge = await client.SendAsync(oversized);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
    }

    private static ByteArrayContent Body(int bytes) => Json(Encoding.ASCII.GetBytes(new string('x', bytes)));

    // JSON allows trailing whitespace, and the signature covers the action, not the bytes.
    private static ByteArrayContent PaddedJson<T>(T value, long bytes) => Json(PaddedJsonBytes(value, bytes));

    private static byte[] PaddedJsonBytes<T>(T value, long bytes)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value, WebJsonOptions);
        var padded = new byte[bytes];
        json.CopyTo(padded, 0);
        padded.AsSpan(json.Length).Fill((byte)' ');
        return padded;
    }

    private static ByteArrayContent Json(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new("application/json");
        return content;
    }

    /// <summary>A body sent in chunks with no declared length.</summary>
    private sealed class StreamedJson : HttpContent
    {
        private readonly byte[] bytes;

        public StreamedJson(byte[] bytes)
        {
            this.bytes = bytes;
            Headers.ContentType = new("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private static int FreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
