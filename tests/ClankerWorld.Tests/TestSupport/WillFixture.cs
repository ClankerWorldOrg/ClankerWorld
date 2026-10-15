using System.Collections;
using System.Reflection;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class NativeWillFixture
{
    internal static IEnumerable<object> PendingItems(object queue) => queue is FamilyWillService service
        ? service.Pending.Values.Cast<object>() : Assert.IsAssignableFrom<IDictionary>(queue).Values.Cast<object>();

    internal static Task PendingTask(WorldRuntime world)
    {
        // Await the full runtime handoff between ticks, before admitting its reply.
        var field = typeof(WorldRuntime).GetField("familyWills",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pending = Assert.Single(PendingItems(field.GetValue(world)!));
        return Assert.IsAssignableFrom<Task>(pending.GetType().GetProperty("Task")!.GetValue(pending));
    }
}
