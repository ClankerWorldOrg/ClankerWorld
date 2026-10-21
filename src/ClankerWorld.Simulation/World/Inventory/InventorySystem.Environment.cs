using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command DropDeceasedCarrierGoods(string id, GridPoint position) => new PreparedCommand(inventory => InventoryAuthority.DropCarrierGoods(inventory, id,
                        new InventoryGroundPosition(position.X, position.Y), holdAccess: inventory.Lots.Where(lot => lot.CarrierId == id)
                            .SelectMany(lot => lot.Holds ?? []).Distinct().ToArray()));

    internal static Command PlaceEstateCustodyGoods(string id, InventoryLot lot, SocietyEstate estate, GridPoint position) => new PreparedCommand(inventory => InventoryAuthority.Relocate(inventory,
                            IdentityRules.PrefixedReference("death:", $"{id}:{lot.Id}"), lot.Id, estate.Id, lot.Quantity,
                            groundPosition: new InventoryGroundPosition(position.X, position.Y), holdAccess: lot.Holds));
}
