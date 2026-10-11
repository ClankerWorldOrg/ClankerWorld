using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.World;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static CivicSystem.CivicHistoryIndex CivicHistory(TownRuntimeState town) => CivicSystem.CivicHistory(town);
    private string[] TownAdults(TownRuntimeState town) => CivicSystem.TownAdults(civicWorld, town);
    private int CivicDay => CivicSystem.CivicDay(civicWorld);

    /// <summary>
    /// The calendar day a tick falls on, counted from 1 as agents are told it.
    /// A world that starts in the morning reaches its next day before a full
    /// day has elapsed, so this is not the tick divided by a day's length.
    /// </summary>
    internal long CivicDayNumber(long tick) => CivicSystem.CivicDayNumber(civicWorld, tick);
    private string? CivicNote(string actor) => CivicSystem.CivicNote(civicWorld, actor);
    private static Regex CivicTickText() => CivicSystem.CivicTickText();
    private string ReadableCivicNotice(string text) => CivicSystem.ReadableCivicNotice(civicWorld, text);
    private static string CivicExcerpt(string text, int limit) => CivicSystem.CivicExcerpt(text, limit);
    private void AdvanceTownGovernance() => civicSystem.AdvanceTownGovernance(civicWorld);
    private void SaveTownGovernance(TownRuntimeState town, TownGovernanceState updated, TownGovernmentState? government = null) => civicSystem.SaveTownGovernance(civicWorld, town, updated, government);
    private GridPoint? CivicBoard(TownRuntimeState town) => CivicSystem.CivicBoard(civicWorld, town);
    private bool NearCivicBoard(string actor, TownRuntimeState town) => CivicSystem.NearCivicBoard(civicWorld, actor, town);
    private static string CivicAgentToken(string id) => CivicSystem.CivicAgentToken(id);
    private string ResolveCivicAgentToken(string token) => CivicSystem.ResolveCivicAgentToken(civicWorld, token);
    private string CivicAction(string town, string kind, string subject = "", string choice = "") => CivicSystem.CivicAction(civicWorld, town, kind, subject, choice);
    private static string CivicRoundToken(TownElection election) => CivicSystem.CivicRoundToken(election);
    private void AddTownCivicCandidates(List<CognitionCandidate> candidates, string actor, IReadOnlyList<string>? buildingsWithoutSites = null) => CivicSystem.AddTownCivicCandidates(civicWorld, candidates, actor, buildingsWithoutSites);
    private void ContinueTownCivicVisit(string actor, string candidate) => CivicSystem.ContinueTownCivicVisit(civicWorld, actor, candidate);
    private void ApplyTownCivicCandidate(string actor, string candidate, string? proposalText = null, IReadOnlyList<string>? ballot = null, IReadOnlyList<CognitionLandTile>? landTiles = null, CognitionLandHearingChoice? hearingChoice = null, CognitionNonviolentChoice? nonviolentChoice = null) => civicSystem.ApplyTownCivicCandidate(civicWorld, actor, candidate, proposalText, ballot, landTiles, hearingChoice, nonviolentChoice);
}
