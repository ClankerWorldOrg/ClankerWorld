using OwnerOrderControlReceipt = ClankerWorld.Simulation.World.OwnerOrderControlReceipt;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldOrderCancellationSaveValidationTests
{
    [Fact]
    public void CheckpointRejectsRetainedCancellationForAnActiveOrder()
    {
        var checkpoint = CreateCancellationCheckpoint("gather berries");
        var document = JsonNode.Parse(checkpoint.Bytes)!.AsObject();
        var state = document["state"]!.AsObject();
        var instruction = Assert.Single(state["instructions"]!.AsArray())!.AsObject();
        instruction["task"]!["status"] = "waiting";
        state["completedInstructionIds"] = new JsonArray();
        var cancellation = Assert.Single(state["orderCancellations"]!.AsArray())!.AsObject();
        cancellation["receipt"]!["status"] = "waiting";
        cancellation["receipt"]!["changed"] = false;
        var damaged = JsonSerializer.SerializeToUtf8Bytes(document);

        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(damaged));
        AssertOriginalCheckpointStillLoads(checkpoint.Bytes);
    }

    [Theory]
    [InlineData("idempotencyKey", true)]
    [InlineData("idempotencyKey", false)]
    [InlineData("issuerId", true)]
    [InlineData("issuerId", false)]
    public void CheckpointRejectsNoncanonicalCancellationIdentity(string member, bool leadingSpace)
    {
        var checkpoint = CreateCancellationCheckpoint("gather berries");
        var document = JsonNode.Parse(checkpoint.Bytes)!.AsObject();
        var cancellation = Assert.Single(document["state"]!["orderCancellations"]!.AsArray())!.AsObject();
        var identity = cancellation[member]!.GetValue<string>();
        cancellation[member] = leadingSpace ? " " + identity : identity + " ";
        var damaged = JsonSerializer.SerializeToUtf8Bytes(document);

        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(damaged));
        AssertOriginalCheckpointStillLoads(checkpoint.Bytes);
    }

    [Theory]
    [InlineData("gather berries", "cancelled", true)]
    [InlineData("build a House", "not_understood", false)]
    public void TerminalCancellationReceiptsRetainNormalizedRetriesAfterReload(
        string text, string status, bool changed)
    {
        var checkpoint = CreateCancellationCheckpoint(text);
        Assert.Equal(status, checkpoint.Receipt.Status);
        Assert.Equal(changed, checkpoint.Receipt.Changed);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(checkpoint.Bytes));
        var normalizedRetry = checkpoint.Request with
        {
            IdempotencyKey = " " + checkpoint.Request.IdempotencyKey + " ",
            IssuerId = " " + checkpoint.Request.IssuerId + " ",
            WorldId = " " + checkpoint.Request.WorldId + " ",
            TargetAgentId = " " + checkpoint.Request.TargetAgentId + " ",
            OrderId = " " + checkpoint.Request.OrderId + " ",
        };

        Assert.Equal(checkpoint.Receipt, restored.CancelOrder(normalizedRetry));
        Assert.Equal(checkpoint.Bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static CancellationCheckpoint CreateCancellationCheckpoint(string text)
    {
        using var world = new WorldRuntime("cancellation-checkpoint-validation");
        var instruction = world.SubmitInstruction(new OwnerInstructionRequest(
            "checkpoint-order", "owner:test", "founder-ilya", OwnerInstructionKind.Order, text));
        var request = new OwnerOrderCancelRequest(
            "checkpoint-cancel", "owner:test", world.Society.WorldId, "founder-ilya", instruction.InstructionId);
        var receipt = world.CancelOrder(request);
        return new CancellationCheckpoint(WorldRuntimeCodec.Encode(world.ExportState()), request, receipt);
    }

    private static void AssertOriginalCheckpointStillLoads(byte[] bytes)
    {
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private sealed record CancellationCheckpoint(
        byte[] Bytes, OwnerOrderCancelRequest Request, OwnerOrderControlReceipt Receipt);
}
