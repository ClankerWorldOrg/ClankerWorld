using System.Collections;
using System.Reflection;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class NativeWillFixture
{
    internal static Task PendingTask(WorldRuntime world)
    {
        return Assert.Single(world.PendingModelWork, item => item.Transition.Kind == ClankerWorld.Simulation.Cognition.ModelWorkKind.Will).Completion;
    }
}
