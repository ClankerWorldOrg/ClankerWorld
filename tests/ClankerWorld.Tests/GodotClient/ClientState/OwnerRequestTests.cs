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
        var entered = new TaskCompletionSource();
        var completed = new TaskCompletionSource();
        var applied = false;
        var reply = new TaskCompletionSource<int>();
        var running = request.RunAsync(async context =>
        {
            try
            {
                entered.SetResult();
                var value = await context.AwaitAsync(reply.Task);
                applied = true;
                return value;
            }
            finally { completed.TrySetResult(); }
        }, cancellation.Token);
        await ModelWorkHarness.WaitForSignalAsync(entered.Task);
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
        // The protected callback must finish without applying a late cancelled reply.
        reply.TrySetResult(9);
        await ModelWorkHarness.WaitForSignalAsync(completed.Task);
        Assert.Equal(outcome == "success", applied);
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
        var reply = new TaskCompletionSource<int>();
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
