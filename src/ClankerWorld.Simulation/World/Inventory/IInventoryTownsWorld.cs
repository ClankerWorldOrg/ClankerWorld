using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryTownsWorld
{
    InventoryCheckpoint ReserveTownProjectDelivery(InventoryCheckpoint inventory, string townId, TownProject project, TownProjectDelivery delivery);
    long WorldTick { get; }
}
