using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;
using Microsoft.Extensions.DependencyInjection;
using Client = ClankerWorld.GodotClient.UI;
using ClientNetworking = ClankerWorld.GodotClient.Networking;
using ClientProtocol = ClankerWorld.GodotClient.Protocol;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData("Élodie Lake")]
    [InlineData("  e\u0301LODIE\u00a0  Lake  ")]
    public async Task SignedPlayerRenameExplainsFirstNameCollisionAndAllowsAnotherName(string duplicate)
    {
        var directory = Directory.CreateTempSubdirectory("player-rename-http-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true, legacyPace: false);
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var signer = new ActionCompatibilitySigner(key);
            var device = await StartAndActivateAsync(host, client, key);
            var identity = host.Services.GetRequiredService<OwnerAuthorityStore>().Identity;
            var authority = new ClankerWorld.GodotClient.Pairing.OwnerAuthorityIdentity(identity.ServerAuthorityId, identity.WorldId);
            var api = new ClientNetworking.OwnerWorldApi(client);
            var uri = new UriBuilder(client.BaseAddress!) { Host = "127.0.0.1" }.Uri;
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            const string first = "founder:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string second = "founder:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            runtime.PlaceFounder(first, new(0, 0));
            runtime.PlaceFounder(second, new(1, 2));
            await api.RenameAgentAsync(uri, authority, device.DeviceId,
                new ClientProtocol.OwnerAgentRenameAction(first, "Élodie Vale"), signer, default);
            var before = WorldRuntimeCodec.Encode(runtime.ExportState());
            var file = host.Services.GetRequiredService<WorldStateFile>();
            var diskBefore = File.ReadAllBytes(file.Path);
            var failure = await Assert.ThrowsAsync<OwnerAgentNameTakenException>(() => api.RenameAgentAsync(
                uri, authority, device.DeviceId, new ClientProtocol.OwnerAgentRenameAction(second, duplicate), signer, default));
            Assert.Equal(HttpStatusCode.Conflict, failure.StatusCode);
            Assert.Contains("another agent", Client.GameUiText.FriendlyFailure(failure), StringComparison.Ordinal);
            Assert.Contains("Choose a different first name", Client.GameUiText.FriendlyFailure(failure), StringComparison.Ordinal);
            Assert.Equal(before, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(diskBefore, File.ReadAllBytes(file.Path));
            var renamed = await api.RenameAgentAsync(uri, authority, device.DeviceId,
                new ClientProtocol.OwnerAgentRenameAction(second, "Marin Lake"), signer, default);
            Assert.True(renamed.Changed);
            var remembered = Assert.Single(runtime.Society.Memories, item => item.OwnerId == second);
            Assert.True(remembered.Permanent);
            Assert.Equal("I was renamed on day 1 to Marin Lake.", remembered.Summary);
            var retry = await api.RenameAgentAsync(uri, authority, device.DeviceId,
                new ClientProtocol.OwnerAgentRenameAction(second, "Marin Lake"), signer, default);
            Assert.False(retry.Changed);
            using var restored = file.LoadOrCreate(runtime.ExportState().WorldSeed);
            Assert.Equal("Marin Lake", restored.Society.GetAgent(second).Name);
            Assert.True(restored.Society.GetAgent(second).HasChosenName);
            Assert.Equal(second, restored.Society.GetAgent(second).Id);
            Assert.Equal(remembered, Assert.Single(restored.Society.Memories, item => item.OwnerId == second));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task ConcurrentSignedRenamesCannotClaimTheSameFirstNameWithDifferentSurnames()
    {
        var directory = Directory.CreateTempSubdirectory("player-rename-race-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true, legacyPace: false);
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            var ids = new[] { "founder:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "founder:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" };
            var names = new[] { "Shared Vale", "Shared Lake" };
            runtime.PlaceFounder(ids[0], new(0, 0));
            runtime.PlaceFounder(ids[1], new(1, 2));
            const string path = "/api/v1/owner/agents/rename";
            var envelopes = new List<OwnerSignedHttpRequest<OwnerAgentRenameAction>>();
            for (var index = 0; index < ids.Length; index++)
            {
                var action = new OwnerAgentRenameAction(ids[index], names[index]);
                envelopes.Add(await CreateSignedRequestAsync(host, client, key, device.DeviceId,
                    path, action, OwnerHttpBinding.AgentRenamePayload(action)));
            }
            var responses = await Task.WhenAll(envelopes.Select(envelope => client.PostAsJsonAsync(path, envelope)));
            try
            {
                Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
                var rejected = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
                Assert.Equal("name_taken", (await rejected.Content.ReadFromJsonAsync<OwnerControlFailure>())!.Code);
                var winner = Array.FindIndex(responses, response => response.StatusCode == HttpStatusCode.OK);
                var chosen = Assert.Single(runtime.Society.Agents, person => person.HasChosenName);
                Assert.Equal(ids[winner], chosen.Id);
                Assert.Equal(names[winner], chosen.Name);
                Assert.False(runtime.Society.GetAgent(ids[1 - winner]).HasChosenName);
                Assert.True(runtime.Society.GetAgent(ids[1 - winner]).NeedsName);
                using var restored = host.Services.GetRequiredService<WorldStateFile>()
                    .LoadOrCreate(runtime.ExportState().WorldSeed);
                Assert.Equal(chosen, Assert.Single(restored.Society.Agents, person => person.HasChosenName));
                Assert.False(restored.Society.GetAgent(ids[1 - winner]).HasChosenName);
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        finally { directory.Delete(recursive: true); }
    }
}
