
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void CancelProductionJobs(WorldProductionJob[] cancelled, string reason) =>
        productionSystem.CancelProductionJobs(ProductionWorld, cancelled, reason);
}
