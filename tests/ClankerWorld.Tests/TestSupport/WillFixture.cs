using System.Collections;
using System.Reflection;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class NativeWillFixture
{
    internal static Task PendingTask(WorldRuntime world)
    {
        // Await the full runtime handoff between ticks, before admitting its reply.
        var field = typeof(WorldRuntime).GetField("pendingWills",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pending = Assert.Single(Assert.IsAssignableFrom<IDictionary>(field.GetValue(world)).Values.Cast<object>());
        return Assert.IsAssignableFrom<Task>(pending.GetType().GetProperty("Task")!.GetValue(pending));
    }
}
