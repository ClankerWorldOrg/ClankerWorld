using System.Collections;
using System.Reflection;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

internal static class NativeWillFixture
{
    internal static Task PendingTask(PrivateWorldRuntime world)
    {
        // Await the full runtime handoff between ticks, before admitting its reply.
        var field = typeof(PrivateWorldRuntime).GetField("pendingWills",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pending = Assert.Single(Assert.IsAssignableFrom<IDictionary>(field.GetValue(world)).Values.Cast<object>());
        return Assert.IsAssignableFrom<Task>(pending.GetType().GetProperty("Task")!.GetValue(pending));
    }
}
