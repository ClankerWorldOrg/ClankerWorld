namespace ClankerWorld.Simulation.World;

internal interface IEnvironmentWorld
{
    WorldMap Map { get; }
    GridPoint WeatherAnchor { get; }
    void SetResourceAvailability(string id, bool available);
    void AppendEvent(string kind, string detail);
}
