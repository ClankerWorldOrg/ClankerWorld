using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IInventoryKnowledgeWorld
{
    long WorldTick { get; }
    string WritingMaterialTransferId(long tick, string actor, string kind);
}
