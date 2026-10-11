using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

internal interface IEnvironmentWorld
{
    SeededMap Map { get; }
    GridPoint WeatherAnchor { get; }
    void SetResourceAvailability(string id, bool available);
    void AppendEvent(string kind, string detail);
}
