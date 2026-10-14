using ClankerWorld.Simulation.World;

namespace ClankerWorld.Server.Worlds;

/// <summary>
/// A small picture of a world's land and water for the Load World list: the
/// commonest terrain in each block of tiles, at most <see cref="MaximumWidth"/>
/// pixels wide and packed like the world's own terrain. The catalog keeps it,
/// so the list can show it without reading the world's save again.
/// </summary>
