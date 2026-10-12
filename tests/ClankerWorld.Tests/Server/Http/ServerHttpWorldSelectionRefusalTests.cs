using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Worlds;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("missing")]
    [InlineData("\"  \"")]
    public async Task WorldSelectionWithoutAnIdReturnsBadRequestAndKeepsCheckpointAndCatalogBytes(string valueJson)
    {
        var directory = Directory.CreateTempSubdirectory("world-selection-refusal-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var client = host.CreateClient();
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            runtime.Pause();
            var catalog = host.Services.GetRequiredService<WorldCatalogStore>();
            var activeId = catalog.Active().Id;
            var checkpointPath = Path.Combine(directory.FullName, "runtime.json");
            var catalogPath = Path.Combine(checkpointPath + ".worlds", "catalog.json");
            var liveBefore = WorldRuntimeCodec.Encode(runtime.ExportState());
            var checkpointBefore = File.ReadAllBytes(checkpointPath);
            var catalogBefore = File.ReadAllBytes(catalogPath);
            var value = valueJson == "missing" ? "" : ",\"value\":" + valueJson;
            using var content = new StringContent("{\"action\":{\"operation\":\"select-world\"" + value + "}}",
                Encoding.UTF8, "application/json");

            using var response = await client.PostAsync("/api/v1/owner/worlds/select", content);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var refusal = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("A world ID is required.", refusal.GetProperty("error").GetString());
            Assert.Equal(activeId, catalog.Active().Id);
            Assert.Equal(liveBefore, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(checkpointBefore, File.ReadAllBytes(checkpointPath));
            Assert.Equal(catalogBefore, File.ReadAllBytes(catalogPath));

            // A usable ID still requires authorization; the malformed refusal is no exemption.
            using var unsigned = await client.PostAsJsonAsync("/api/v1/owner/worlds/select",
                new { action = new { operation = "select-world", value = activeId } });
            Assert.Equal(HttpStatusCode.BadRequest, unsigned.StatusCode);
            Assert.Equal(liveBefore, WorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(checkpointBefore, File.ReadAllBytes(checkpointPath));
            Assert.Equal(catalogBefore, File.ReadAllBytes(catalogPath));
        }
        finally { directory.Delete(recursive: true); }
    }
}
