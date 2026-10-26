
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
    [LoggerMessage(EventId = 2265, Level = LogLevel.Information,
        Message = "town_transition tick={WorldTick} town={TownId} transition={Transition} residents={ResidentCount} buildings={BuildingCount} border_tiles={BorderTileCount}")]
    public static partial void LogTownTransition(ILogger logger, long worldTick, string townId, TownTransitionKind transition,
        int residentCount, int buildingCount, int borderTileCount);

    [LoggerMessage(EventId = 2290, Level = LogLevel.Information,
        Message = "town_civic tick={WorldTick} town={TownId} transition={Transition} representative={Representative} members={Members} status={Status} yes={Yes} no={No} ballots={Ballots}")]
    public static partial void LogTownCivic(ILogger logger, long worldTick, string townId, TownCivicTransitionKind transition,
        bool representative, int members, string status, int yes, int no, int ballots);

    /// <summary>A passed admission's outcome. IDs and counts only; never names or proposal text.</summary>
    [LoggerMessage(EventId = 2295, Level = LogLevel.Information,
        Message = "town_admission tick={WorldTick} town={TownId} outcome={Outcome} previous_town={PreviousTownId} members={Members} residents={Residents}")]
    public static partial void LogTownAdmission(ILogger logger, long worldTick, string townId, string outcome,
        string previousTownId, int members, int residents);

    /// <summary>Public case identity and bounded outcomes only; no statements, evidence text or model payloads.</summary>
    [LoggerMessage(EventId = 2296, Level = LogLevel.Information,
        Message = "town_land_hearing tick={WorldTick} town={TownId} case={CaseId} transition={Transition} status={Status} revision={Revision} parties={Parties} evidence={Evidence} responses={Responses} rulings={Rulings}")]
    public static partial void LogLandHearing(ILogger logger, long worldTick, string townId, string caseId,
        TownLandHearingTransitionKind transition, string status, int revision, int parties, int evidence,
        int responses, int rulings);

    /// <summary>Recorded transfer outcome counts only; no public statements or private model payloads.</summary>
    [LoggerMessage(EventId = 2297, Level = LogLevel.Information,
        Message = "town_land_transfer tick={WorldTick} town={TownId} transfer={TransferId} transition={Transition} status={Status} parties={Parties} responses={Responses} tiles={Tiles}")]
    public static partial void LogLandTransfer(ILogger logger, long worldTick, string townId, string transferId,
        TownLandTransferTransitionKind transition, string status, int parties, int responses, int tiles);

    [LoggerMessage(EventId = 2266, Level = LogLevel.Information,
        Message = "town_site_rejected tick={WorldTick} town={TownId} agent={AgentId} building={BuildingId} x={X} y={Y} reason={Reason}")]
    public static partial void LogTownSiteRejected(ILogger logger, long worldTick, string townId, string agentId,
        string buildingId, int x, int y, string reason);

    [LoggerMessage(EventId = 2267, Level = LogLevel.Information,
        Message = "first_town_layout outcome={Outcome} world_tick={WorldTick} x={X} y={Y} buildings={Buildings} roads={RoadTiles}")]
    public static partial void LogTownLayoutAccepted(ILogger logger, long worldTick, string outcome, int x, int y,
        int buildings, int roadTiles);

    [LoggerMessage(EventId = 2272, Level = LogLevel.Information,
        Message = "bridge outcome={Outcome} world_tick={WorldTick} bridge={BridgeId} trigger={Trigger} reason={Reason}")]
    public static partial void LogBridge(ILogger logger, long worldTick, string bridgeId, string trigger,
        string outcome, string reason);

    [LoggerMessage(EventId = 2273, Level = LogLevel.Information,
        Message = "road_route outcome=unconnected world_tick={WorldTick} town={TownId} building={BuildingId} reason={Reason}")]
    public static partial void LogRoadUnconnected(ILogger logger, long worldTick, string townId, string buildingId,
        string reason);

    [LoggerMessage(EventId = 2268, Level = LogLevel.Information,
        Message = "founder_setup outcome=moved world_tick={WorldTick} founder={FounderId} x={X} y={Y}")]
    public static partial void LogFounderMoved(ILogger logger, long worldTick, string founderId, int x, int y);

    [LoggerMessage(EventId = 2269, Level = LogLevel.Information,
        Message = "founder_setup outcome=undone world_tick={WorldTick} founder={FounderId} placed={Placed}")]
    public static partial void LogFounderUndone(ILogger logger, long worldTick, string founderId, int placed);
}
