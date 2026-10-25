using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.AgentPlacement;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Fact]
    public async Task LateAgentPlacementRefusalRestoresItsNewKeyAndAssignments()
    {
        var directory = Directory.CreateTempSubdirectory("agent-placement-transaction-");
        try
        {
            using var baseHost = new ServerWebApplicationFactory(directory.FullName, legacyPace: false);
            using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton<IDecisionProvider>(new DeterministicDecisionProvider())));
            using var client = host.CreateClient();
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, signer);
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            var providers = host.Services.GetRequiredService<ProviderConfigurationStore>();
            var file = host.Services.GetRequiredService<WorldStateFile>();
            var positions = new[] { new GridPoint(0, 0), new(1, 2), new(2, 2), new(3, 2) };
            var founders = positions.Select(_ => "founder:" + Guid.NewGuid().ToString("N")).ToArray();
            var founderSlot = Guid.NewGuid().ToString("N");
            for (var index = 0; index < positions.Length; index++)
            {
                runtime.PlaceFounder(founders[index], positions[index]);
                providers.Configure(new("personal", "openai", "founder-model",
                    index == 0 ? "synthetic-founder-key" : null, false, founders[index], founderSlot,
                    index == 0 ? "Founders" : null));
            }
            runtime.StartWorld();
            var actor = founders[^1];
            runtime.SubmitInstruction(new("placement-race-move", "owner:test", actor,
                OwnerInstructionKind.Order, "Move to tile (4, 2)"));
            var agentId = "agent:" + Guid.NewGuid().ToString("N");
            var slotId = Guid.NewGuid().ToString("N");
            var preview = host.Services.GetRequiredService<OwnerWorldObservationStore>().GetSnapshot();
            var membership = AgentPlacementRules.Resolve(
                preview.PlacedBuildings.Where(building => building.HouseholdId is not null &&
                    4 >= building.Position.X && 4 < building.Position.X + building.Width &&
                    2 >= building.Position.Y && 2 < building.Position.Y + building.Height).Select(building => building.HouseholdId),
                preview.Towns.Where(town => town.BorderTiles.Any(tile => tile.X == 4 && tile.Y == 2)).Select(town => town.Id));
            var cognition = new OwnerProviderConfigurationAction("personal", "openai", "test-model",
                "synthetic-placement-key", false, agentId, slotId, "Placement key");
            var action = new OwnerAgentPlacementAction(agentId, 4, 2, cognition, membership.HouseholdIdFor(agentId), membership.TownId);
            const string endpoint = "/api/v1/owner/agents/place";
            var request = await CreateSignedRequestAsync(host, client, signer, device.DeviceId,
                endpoint, action, OwnerHttpBinding.AgentPlacementPayload(action));
            runtime.ValidateAgentPlacement(agentId, new(4, 2), action.ExpectedHouseholdId, action.ExpectedTownId);
            file.Save(runtime);
            var checkpoint = File.ReadAllBytes(file.Path);
            var configuration = JsonSerializer.Serialize(providers.CaptureRuntimeConfiguration());
            byte[]? afterMovement = null;
            providers.AfterAgentConfiguration = () =>
            {
                providers.AfterAgentConfiguration = null;
                Assert.Contains(providers.CaptureStatus().CredentialSlots!, item => item.Id == slotId);
                // Admit one real movement tick after the key write and before the endpoint's second placement check.
                Assert.True(runtime.AdvanceOneTickAsync().AsTask().GetAwaiter().GetResult().Advanced);
                Assert.Equal(new GridPoint(4, 2), runtime.Agents.Single(item => item.AgentId == actor).Position);
                afterMovement = WorldRuntimeCodec.Encode(runtime.ExportState());
            };
            using var refused = await client.PostAsJsonAsync(endpoint, request);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.NotNull(afterMovement);
            Assert.DoesNotContain(runtime.Agents, item => item.AgentId == agentId);
            Assert.Equal(afterMovement, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(checkpoint, File.ReadAllBytes(file.Path));
            Assert.Equal(configuration, JsonSerializer.Serialize(providers.CaptureRuntimeConfiguration()));
            var reopened = new ProviderConfigurationStore(providers.Path, new("deterministic", null, null, null, null, null, null));
            Assert.Equal(configuration, JsonSerializer.Serialize(reopened.CaptureRuntimeConfiguration()));
            // The same new slot ID must remain usable for an ordinary retry on a free tile.
            action = action with { X = 5 };
            using var placed = await SendSignedAsync(host, client, signer, device.DeviceId,
                endpoint, action, OwnerHttpBinding.AgentPlacementPayload(action));
            Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
            Assert.Contains(runtime.Agents, item => item.AgentId == agentId);
            Assert.Equal(2, providers.CaptureRuntimeConfiguration().Assignments!.Count(item => item.AgentId == agentId));
            Assert.Single(providers.CaptureStatus().CredentialSlots!, item => item.Id == slotId);
        }
        finally { directory.Delete(recursive: true); }
    }
}
