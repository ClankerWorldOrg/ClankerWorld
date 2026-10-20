using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void CancelUnavailableWorkers() => productionSystem.CancelUnavailableWorkers(ProductionWorld);

    private void CancelProductionJobs(WorldProductionJob[] cancelled, string reason) =>
        productionSystem.CancelProductionJobs(ProductionWorld, cancelled, reason);
}
