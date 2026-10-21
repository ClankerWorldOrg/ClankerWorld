using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static void CompletePaidBoatProject(IBoatWorld world, string townId, TownProject project)
    {
        var port = world.Port(project.Plan.BoatPortId!)!;
        var dock = world.FreePortDock(port) ?? throw new InvalidOperationException("The completed boat has no reserved launch space.");
        var id = TownProjectRules.BuildingId(project.Id);
        world.Transport.RecordBoat(new(id, townId, project.Id, dock, port.InstanceId));
        world.SetTownProject(townId, project with
        {
            Stage = "completed",
            CompletedBoatId = id,
            Blocker = null,
            LastTransitionTick = world.WorldTick,
        });
        world.AppendEvent(new CommunalBoatLaunchedEventPayload(null, townId, id, port.InstanceId, project.Plan.Name), dock);
    }
}
