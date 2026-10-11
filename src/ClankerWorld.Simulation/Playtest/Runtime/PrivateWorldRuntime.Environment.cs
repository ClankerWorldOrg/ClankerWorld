using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static WorldSystemsState CreateWorldSystems(string worldSeed, SeededMap map,
        WorldStartPace startPace = WorldStartPace.Legacy, bool regionalWeather = true) =>
        EnvironmentSystem.CreateWorldSystems(worldSeed, map, startPace, regionalWeather);

    private static WorldSystemsState AdvanceWorldSystemsTo(WorldSystemsState state, long targetTick) =>
        EnvironmentSystem.AdvanceWorldSystemsTo(state, targetTick);

    private void SyncEcologyResourceStates() => environmentSystem.SyncEcologyResourceStates(environmentWorld);

    private void AdvanceEnvironmentWeather() => environmentSystem.AdvanceWeather(environmentWorld);

    private static SocietyWorldRuntime CreateSociety(string worldSeed, string worldId,
        Func<string, IDecisionProvider>? providerFactory, int maxCognitionQueueLength,
        int maxCognitionDispatchPerCycle, WorldStartPace startPace) =>
        EnvironmentSystem.CreateSociety(worldSeed, worldId, providerFactory, maxCognitionQueueLength,
            maxCognitionDispatchPerCycle, startPace, FoodLotId, HouseholdId, SecondHouseholdId);

    private void CreatePhysicalState() => EnvironmentSystem.CreatePhysicalState(environmentWorld);

    private void DrainNeeds() => EnvironmentSystem.DrainNeeds(environmentWorld);

    private void RemoveDeadPhysicalState() => EnvironmentSystem.RemoveDeadPhysicalState(environmentWorld);

}
