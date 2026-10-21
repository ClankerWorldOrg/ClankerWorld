using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Society;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryCommunityWorld
{
    long WorldTick { get; }
}
