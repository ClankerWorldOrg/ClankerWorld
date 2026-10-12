namespace ClankerWorld.Simulation.World;

internal interface ICivicHistoryWorld : ICivicWorld
{
    IReadOnlyList<TownRuntimeState> Towns { get; }
    WorldSystemsConfig CalendarConfig { get; }
}
