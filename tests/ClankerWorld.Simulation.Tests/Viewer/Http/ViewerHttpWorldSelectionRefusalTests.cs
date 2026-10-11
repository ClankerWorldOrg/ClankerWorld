using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Observation;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class ViewerHttpTests
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
            using var host = new ViewerWebApplicationFactory(directory.FullName, privateWorld: true);
            using var client = host.CreateClient();
            var runtime = host.Services.GetRequiredService<PrivateWorldRuntime>();
            runtime.Pause();
            var catalog = host.Services.GetRequiredService<WorldCatalogStore>();
            var activeId = catalog.Active().Id;
            var checkpointPath = Path.Combine(directory.FullName, "runtime.json");
            var catalogPath = Path.Combine(checkpointPath + ".worlds", "catalog.json");
            var liveBefore = PrivateWorldRuntimeCodec.Encode(runtime.ExportState());
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
            Assert.Equal(liveBefore, PrivateWorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(checkpointBefore, File.ReadAllBytes(checkpointPath));
            Assert.Equal(catalogBefore, File.ReadAllBytes(catalogPath));

            // A usable ID still requires authorization; the malformed refusal is no exemption.
            using var unsigned = await client.PostAsJsonAsync("/api/v1/owner/worlds/select",
                new { action = new { operation = "select-world", value = activeId } });
            Assert.Equal(HttpStatusCode.BadRequest, unsigned.StatusCode);
            Assert.Equal(liveBefore, PrivateWorldRuntimeCodec.Encode(runtime.ExportState()));
            Assert.Equal(checkpointBefore, File.ReadAllBytes(checkpointPath));
            Assert.Equal(catalogBefore, File.ReadAllBytes(catalogPath));
        }
        finally { directory.Delete(recursive: true); }
    }
}
