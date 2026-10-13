using System.Net;
using ClankerWorld.GodotClient.ClientState;

namespace ClankerWorld.Tests.GodotClient.ClientState;

public sealed class OwnerRequestTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("refusal")]
    [InlineData("timeout")]
    [InlineData("lost reply")]
    [InlineData("unreadable reply")]
    public async Task EveryOutcomeRestoresControls(string outcome)
    {
        List<bool> controls = [];
        var request = new OwnerRequest<int>(() => true, controls.Add);
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = request.RunAsync(async context =>
        {
            entered.SetResult();
            return await context.AwaitAsync(reply.Task);
        }, cancellation.Token);
        await entered.Task;
        Assert.Equal([true], controls);
        switch (outcome)
        {
            case "success": reply.SetResult(7); Assert.Equal(7, await running); break;
            case "refusal": reply.SetException(new HttpRequestException("refused", null, HttpStatusCode.Conflict)); break;
            case "timeout": cancellation.Cancel(); break;
            case "lost reply": reply.SetException(new HttpRequestException("connection lost")); break;
            case "unreadable reply": reply.SetException(new System.Text.Json.JsonException()); break;
        }
        if (outcome != "success") await Assert.ThrowsAnyAsync<Exception>(() => running);
        Assert.Equal([true, false], controls);
        // A reply that arrives after the deadline cannot leave controls locked.
        reply.TrySetResult(9);
        Assert.Equal([true, false], controls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorldOrPanelChangeDiscardsBothRepliesAndRefusals(bool refuse)
    {
        var current = true;
        var busy = false;
        var applied = false;
        var reply = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new OwnerRequest<int>(() => current, value => busy = value);
        var running = runner.RunAsync(async context =>
        {
            var value = await context.AwaitAsync(reply.Task);
            applied = true;
            return value;
        });
        Assert.True(busy);
        current = false;
        if (refuse) reply.SetException(new HttpRequestException("refused"));
        else reply.SetResult(1);
        await Assert.ThrowsAsync<ObsoleteOwnerRequestException>(() => running);
        Assert.False(applied);
        Assert.False(busy);
    }
}
