using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void SyncEcologyResourceStates() => environmentSystem.SyncEcologyResourceStates(environmentWorld);

    private void AdvanceEnvironmentWeather() => environmentSystem.AdvanceWeather(environmentWorld);

    private static SocietyRuntime CreateSociety(string worldSeed, string worldId,
        Func<string, IDecisionProvider>? providerFactory, int maxCognitionQueueLength,
        int maxCognitionDispatchPerCycle, WorldStartPace startPace) =>
        EnvironmentSystem.CreateSociety(worldSeed, worldId, providerFactory, maxCognitionQueueLength,
            maxCognitionDispatchPerCycle, startPace, FoodLotId, HouseholdId, SecondHouseholdId);

    private void CreatePhysicalState() => EnvironmentSystem.CreatePhysicalState(environmentWorld);

    private void DrainNeeds() => EnvironmentSystem.DrainNeeds(environmentWorld);

    private void RemoveDeadPhysicalState() => EnvironmentSystem.RemoveDeadPhysicalState(environmentWorld);

}
