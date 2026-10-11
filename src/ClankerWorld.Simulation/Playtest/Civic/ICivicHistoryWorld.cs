using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

internal interface ICivicHistoryWorld : ICivicWorld
{
    IReadOnlyList<TownRuntimeState> Towns { get; }
    WorldSystemsConfig CalendarConfig { get; }
}
