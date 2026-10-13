using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public enum GoodsUse { Holdings, ConsumeAt, Collect, ReachableHoldings, RecoveryHoldings, Recover, Spend, Sell, StorePersonal, Deliver, Eat, VesselFor, ReturnVessel, Cargo, Salvage, CarriedDelivery, SpareCargo, OrchardFoodRecovery, CarriedInputs, FieldPlanting, TreePlanting }

public enum GoodsDeliveryPurpose { Stock, FarmStock, Expansion, TownProject }

public enum GoodsDemandCoverage { None, KnowledgeWriting }

public sealed record GoodsOwners(IReadOnlyList<string> Ids)
{
    public static GoodsOwners One(string id) => new([id]);
}

public sealed record GoodsKinds(IReadOnlyList<string> Ids)
{
    public static GoodsKinds One(string id) => new([id]);
}

public sealed record GoodsRequest(GoodsUse Use, string? Actor, GoodsOwners Owners, GoodsKinds Kinds,
    string? AtBuilding = null, GridPoint? Near = null, string? Destination = null, int ExtraUnits = 0, bool Explain = false,
    GoodsDemandCoverage DemandCoverage = GoodsDemandCoverage.None,
    GoodsDeliveryPurpose DeliveryPurpose = GoodsDeliveryPurpose.Stock, string? ContentsKind = null, string? ContainerId = null, string? ReservationId = null);

public enum GoodsPlaceKind { Stored, Ground, Carried, Unlocated }
public sealed record GoodsPlace(GoodsPlaceKind Kind, string? BuildingId = null,
    GridPoint? Position = null, string? CarrierId = null, string? DeliveryBuildingId = null);

public enum GoodsReason { Owner, Kind, Place, Damaged, Spoiled, Vessel, Empty, Reservation, Delivery, MarketStall, Custody, CarryRoom, Route, DestinationRoom }
public sealed record GoodsMatch(InventoryLot Lot, InventoryLot Root, GoodsPlace Place, int Quantity, int MoveUnits)
{
    public IReadOnlyList<InventoryHold> RequiredHolds => (Lot.Holds ?? []).Concat(Root.Holds ?? []).Distinct().ToArray();
}
public sealed record GoodsAnswer(IReadOnlyList<GoodsMatch> Matches, IReadOnlyList<(string LotId, GoodsReason Reason)> Excluded,
    long CoveredReservations = 0)
{
    public long Total => Matches.Sum(match => (long)match.Quantity);
    public long CoveredTotal => Total + CoveredReservations;
    public GoodsMatch? First => Matches.Count > 0 ? Matches[0] : null;
}
