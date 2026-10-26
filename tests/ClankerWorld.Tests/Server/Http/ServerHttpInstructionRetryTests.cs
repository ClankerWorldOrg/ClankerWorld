using OwnerInstructionReceipt = ClankerWorld.Simulation.World.OwnerInstructionReceipt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData(" durable-retry ")]
    public async Task SignedInstructionRetryReturnsItsReceiptAfterRecipientDeathAndHostRestart(string idempotencyKey)
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-instruction-retry-");
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            const string path = "/api/v1/owner/instructions";
            OwnerInstructionAction action;
            string deviceId;
            OwnerInstructionReceipt receipt;
            using (var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true))
            using (var client = host.CreateClient())
            {
                deviceId = (await StartAndActivateAsync(host, client, key)).DeviceId;
                action = new OwnerInstructionAction(idempotencyKey, "founder-mira", "suggestion", "wait safely",
                    host.Services.GetRequiredService<WorldRuntime>().Society.WorldId);
                using var accepted = await SendSignedAsync(host, client, key, deviceId, path, action,
                    OwnerActionPayload.Instruction(action));
                Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
                receipt = (await accepted.Content.ReadFromJsonAsync<OwnerInstructionReceipt>())!;
                Assert.Equal("durable-retry", receipt.IdempotencyKey);
                using var exactRetry = await SendSignedAsync(host, client, key, deviceId, path, action,
                    OwnerActionPayload.Instruction(action));
                Assert.Equal(HttpStatusCode.OK, exactRetry.StatusCode);
                Assert.Equal(receipt, await exactRetry.Content.ReadFromJsonAsync<OwnerInstructionReceipt>());
                var runtime = host.Services.GetRequiredService<WorldRuntime>();
                using var deceased = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
                    WorldRuntimeCodec.Encode(InstructionRecipientFixture.AfterDeath(runtime.ExportState(), "founder-mira"))));
                deceased.Pause();
                host.Services.GetRequiredService<WorldStateFile>().Save(deceased);
            }

            for (var restart = 0; restart < 2; restart++)
            {
                using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
                using var client = host.CreateClient();
                var runtime = host.Services.GetRequiredService<WorldRuntime>();
                var before = WorldRuntimeCodec.Encode(runtime.ExportState());
                using var retried = await SendSignedAsync(host, client, key, deviceId, path, action,
                    OwnerActionPayload.Instruction(action));
                Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
                Assert.Equal(receipt, await retried.Content.ReadFromJsonAsync<OwnerInstructionReceipt>());
                var conflict = action with { Text = "gather food" };
                using var conflicting = await SendSignedAsync(host, client, key, deviceId, path, conflict,
                    OwnerActionPayload.Instruction(conflict));
                Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);
                var newDead = action with { IdempotencyKey = "new-dead-request" };
                using var rejected = await SendSignedAsync(host, client, key, deviceId, path, newDead,
                    OwnerActionPayload.Instruction(newDead));
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
                Assert.Equal(before, WorldRuntimeCodec.Encode(runtime.ExportState()));
                Assert.Single(runtime.ExportState().Instructions!);
                Assert.Single(runtime.ExportState().Events, item => item.Kind == "instruction_queued");
                runtime.Validate();
            }
        }
        finally { directory.Delete(recursive: true); }
    }
}
