using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Server.Runtime;

internal enum TownTransitionKind
{
    StateLoaded,
    FoundingStarted,
    ResidentJoined,
    ResidentLeft,
    ResidentUnaffiliated,
    Founded,
    BuildingAssigned,
    BuildingUnassigned,
    BorderExpanded,
    Abandoned,
    Revived,
    Resettled,
    StockSalvaged,
}

internal enum TownCivicTransitionKind
{
    CouncilChanged,
    ElectionOpened,
    RunoffOpened,
    ProposalOpened,
    DecisionRecorded,
    ElectionCancelled,
    LawRecorded,
    GovernmentRecorded,
    MayorRecorded,
}

internal enum TownLandHearingTransitionKind
{
    Opened, NoticePublished, EvidenceAdded, ResponseRecorded, JudgeConsent,
    JudgeElection, JudgeAssigned, Ruling, ReopenRequested, Reopened, Inspected, Relayed, Rejected,
}

internal enum TownLandTransferTransitionKind
{
    Proposed, Read, Consent, Withdrawn, Settled, Blocked,
}

/// <summary>Bounded operational outcomes for authoritative Town state changes.</summary>
internal static partial class TownTelemetry
{
    public static void Transition(ILogger logger, long worldTick, string townId,
        TownTransitionKind transition, int residentCount, int buildingCount, int borderTileCount)
    {
        LogTownTransition(logger, worldTick, townId, transition, residentCount, buildingCount, borderTileCount);
    }

    [LoggerMessage(EventId = 2265, Level = LogLevel.Information,
        Message = "town_transition tick={WorldTick} town={TownId} transition={Transition} residents={ResidentCount} buildings={BuildingCount} border_tiles={BorderTileCount}")]
    private static partial void LogTownTransition(ILogger logger, long worldTick, string townId, TownTransitionKind transition,
        int residentCount, int buildingCount, int borderTileCount);

    public static void Civic(ILogger logger, long worldTick, string townId, TownCivicTransitionKind transition,
        bool representative, int members, string status, int yes, int no, int ballots) =>
        LogTownCivic(logger, worldTick, townId, transition, representative, members, status, yes, no, ballots);

    [LoggerMessage(EventId = 2290, Level = LogLevel.Information,
        Message = "town_civic tick={WorldTick} town={TownId} transition={Transition} representative={Representative} members={Members} status={Status} yes={Yes} no={No} ballots={Ballots}")]
    private static partial void LogTownCivic(ILogger logger, long worldTick, string townId, TownCivicTransitionKind transition,
        bool representative, int members, string status, int yes, int no, int ballots);

    /// <summary>A passed admission's outcome. IDs and counts only; never names or proposal text.</summary>
    public static void Admission(ILogger logger, long worldTick, string townId, string outcome, string previousTownId,
        int members, int residents) =>
        LogTownAdmission(logger, worldTick, townId, outcome, previousTownId, members, residents);

    [LoggerMessage(EventId = 2295, Level = LogLevel.Information,
        Message = "town_admission tick={WorldTick} town={TownId} outcome={Outcome} previous_town={PreviousTownId} members={Members} residents={Residents}")]
    private static partial void LogTownAdmission(ILogger logger, long worldTick, string townId, string outcome,
        string previousTownId, int members, int residents);

    /// <summary>Public case identity and bounded outcomes only; no statements, evidence text or model payloads.</summary>
    public static void LandHearing(ILogger logger, long worldTick, string townId, string caseId,
        TownLandHearingTransitionKind transition, string status, int revision, int parties, int evidence,
        int responses, int rulings) =>
        LogLandHearing(logger, worldTick, townId, caseId, transition, status, revision, parties, evidence, responses, rulings);

    [LoggerMessage(EventId = 2296, Level = LogLevel.Information,
        Message = "town_land_hearing tick={WorldTick} town={TownId} case={CaseId} transition={Transition} status={Status} revision={Revision} parties={Parties} evidence={Evidence} responses={Responses} rulings={Rulings}")]
    private static partial void LogLandHearing(ILogger logger, long worldTick, string townId, string caseId,
        TownLandHearingTransitionKind transition, string status, int revision, int parties, int evidence,
        int responses, int rulings);

    /// <summary>Recorded transfer outcome counts only; no public statements or private model payloads.</summary>
    public static void LandTransfer(ILogger logger, long worldTick, string townId, string transferId,
        TownLandTransferTransitionKind transition, string status, int parties, int responses, int tiles) =>
        LogLandTransfer(logger, worldTick, townId, transferId, transition, status, parties, responses, tiles);

    [LoggerMessage(EventId = 2297, Level = LogLevel.Information,
        Message = "town_land_transfer tick={WorldTick} town={TownId} transfer={TransferId} transition={Transition} status={Status} parties={Parties} responses={Responses} tiles={Tiles}")]
    private static partial void LogLandTransfer(ILogger logger, long worldTick, string townId, string transferId,
        TownLandTransferTransitionKind transition, string status, int parties, int responses, int tiles);

    public static void SiteRejected(ILogger logger, long worldTick, string townId, string agentId,
        string buildingId, int x, int y, string reason)
    {
        LogTownSiteRejected(logger, worldTick, townId, agentId, buildingId, x, y, reason);
    }

    [LoggerMessage(EventId = 2266, Level = LogLevel.Information,
        Message = "town_site_rejected tick={WorldTick} town={TownId} agent={AgentId} building={BuildingId} x={X} y={Y} reason={Reason}")]
    private static partial void LogTownSiteRejected(ILogger logger, long worldTick, string townId, string agentId,
        string buildingId, int x, int y, string reason);

    public static void LayoutAccepted(ILogger logger, long worldTick, string outcome, int x, int y,
        int buildings, int roadTiles) =>
        LogTownLayoutAccepted(logger, worldTick, outcome, x, y, buildings, roadTiles);

    [LoggerMessage(EventId = 2267, Level = LogLevel.Information,
        Message = "first_town_layout outcome={Outcome} world_tick={WorldTick} x={X} y={Y} buildings={Buildings} roads={RoadTiles}")]
    private static partial void LogTownLayoutAccepted(ILogger logger, long worldTick, string outcome, int x, int y,
        int buildings, int roadTiles);

    public static void Bridge(ILogger logger, long worldTick, string bridgeId, string trigger,
        string outcome, string reason) =>
        LogBridge(logger, worldTick, bridgeId, trigger, outcome, reason);

    [LoggerMessage(EventId = 2272, Level = LogLevel.Information,
        Message = "bridge outcome={Outcome} world_tick={WorldTick} bridge={BridgeId} trigger={Trigger} reason={Reason}")]
    private static partial void LogBridge(ILogger logger, long worldTick, string bridgeId, string trigger,
        string outcome, string reason);

    public static void RoadUnconnected(ILogger logger, long worldTick, string townId, string buildingId, string reason) =>
        LogRoadUnconnected(logger, worldTick, townId, buildingId, reason);

    [LoggerMessage(EventId = 2273, Level = LogLevel.Information,
        Message = "road_route outcome=unconnected world_tick={WorldTick} town={TownId} building={BuildingId} reason={Reason}")]
    private static partial void LogRoadUnconnected(ILogger logger, long worldTick, string townId, string buildingId,
        string reason);

    public static void FounderMoved(ILogger logger, long worldTick, string founderId, int x, int y) =>
        LogFounderMoved(logger, worldTick, founderId, x, y);

    [LoggerMessage(EventId = 2268, Level = LogLevel.Information,
        Message = "founder_setup outcome=moved world_tick={WorldTick} founder={FounderId} x={X} y={Y}")]
    private static partial void LogFounderMoved(ILogger logger, long worldTick, string founderId, int x, int y);

    public static void FounderUndone(ILogger logger, long worldTick, string founderId, int placed) =>
        LogFounderUndone(logger, worldTick, founderId, placed);

    [LoggerMessage(EventId = 2269, Level = LogLevel.Information,
        Message = "founder_setup outcome=undone world_tick={WorldTick} founder={FounderId} placed={Placed}")]
    private static partial void LogFounderUndone(ILogger logger, long worldTick, string founderId, int placed);
}

internal static partial class AgentPlacementLog
{
    [LoggerMessage(EventId = 1710, Level = LogLevel.Information,
        Message = "AgentPlaced AgentId={AgentId} HouseholdId={HouseholdId} WorldTick={WorldTick} X={X} Y={Y}")]
    public static partial void Placed(ILogger logger, string agentId, string? householdId, long worldTick, int x, int y);

    [LoggerMessage(EventId = 1711, Level = LogLevel.Warning,
        Message = "AgentPlacementRejected AgentId={AgentId} Reason={Reason}")]
    public static partial void Rejected(ILogger logger, string agentId, string reason);

    [LoggerMessage(EventId = 1712, Level = LogLevel.Information,
        Message = "AgentRenamed AgentId={AgentId} WorldTick={WorldTick}")]
    public static partial void Renamed(ILogger logger, string agentId, long worldTick);
}

/// <summary>Committed cart milestones; names, private text and per-step movement stay out of these logs.</summary>
public static partial class HandcartTelemetry
{
    public static void Record(ILogger logger, WorldEventEntry worldEvent, InventoryCheckpoint inventory,
        IReadOnlyList<string> actorIds)
    {
        if (worldEvent.Kind is not ("handcart_attached" or "handcart_parked" or "handcart_loaded" or
            "handcart_unloaded" or "handcart_repaired" or "handcart_transferred" or "handcart_blocked")) return;
        var actor = actorIds.OrderByDescending(id => id.Length).FirstOrDefault(id =>
            worldEvent.Detail.StartsWith(id + ":", StringComparison.Ordinal));
        if (actor is null) return;
        var cart = inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart)
            .OrderByDescending(lot => lot.Id.Length).FirstOrDefault(lot =>
                worldEvent.Detail.StartsWith(actor + ":" + lot.Id + ":", StringComparison.Ordinal) ||
                worldEvent.Detail == actor + ":" + lot.Id);
        var cargo = cart is null ? 0 : inventory.Lots.Where(lot => lot.ContainerLotId == cart.Id).Sum(lot => lot.Quantity);
        LogMilestone(logger, worldEvent.WorldTick, worldEvent.Kind, actor, cart?.Id ?? "none",
            cart?.ConditionBasisPoints / 100 ?? 0, cargo);
    }

    [LoggerMessage(EventId = 2260, Level = LogLevel.Information,
        Message = "handcart_activity tick={WorldTick} event={EventKind} agent={AgentId} cart={CartId} condition={ConditionPercent} cargo={CargoQuantity}")]
    private static partial void LogMilestone(ILogger logger, long worldTick, string eventKind,
        string agentId, string cartId, int conditionPercent, int cargoQuantity);
}

public static partial class OwnerAgingOverrideTelemetry
{
    [LoggerMessage(EventId = 2211, Level = LogLevel.Information,
        Message = "world_aging_override tick={WorldTick} rate={Rate}")]
    public static partial void Changed(ILogger logger, long worldTick, int rate);
}

public static partial class OwnerJevAssistanceTelemetry
{
    [LoggerMessage(EventId = 2219, Level = LogLevel.Information,
        Message = "world_jev_assistance tick={WorldTick} enabled={Enabled}")]
    public static partial void Changed(ILogger logger, long worldTick, bool enabled);
    [LoggerMessage(EventId = 2220, Level = LogLevel.Information,
        Message = "world_routine_helper tick={WorldTick} provider={Provider} model={Model}")]
    public static partial void HelperChanged(ILogger logger, long worldTick, string provider, string model);
}
