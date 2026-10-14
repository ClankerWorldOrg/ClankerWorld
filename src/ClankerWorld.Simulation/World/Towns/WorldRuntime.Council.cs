using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static TownSystem.CivicHistoryIndex CivicHistory(TownRuntimeState town) => TownSystem.CivicHistory(town);
    private string[] TownAdults(TownRuntimeState town) => TownSystem.TownAdults(civicWorld, town);
    private int CivicDay => TownSystem.CivicDay(civicWorld);

    /// <summary>
    /// The calendar day a tick falls on, counted from 1 as agents are told it.
    /// A world that starts in the morning reaches its next day before a full
    /// day has elapsed, so this is not the tick divided by a day's length.
    /// </summary>
    internal long CivicDayNumber(long tick) => TownSystem.CivicDayNumber(civicWorld, tick);
    private string? CivicNote(string actor) => TownSystem.CivicNote(civicWorld, actor);
    private static Regex CivicTickText() => TownSystem.CivicTickText();
    private string ReadableCivicNotice(string text) => TownSystem.ReadableCivicNotice(civicWorld, text);
    private static string CivicExcerpt(string text, int limit) => TownSystem.CivicExcerpt(text, limit);
    private void AdvanceTownCouncil() => townSystem.AdvanceTownCouncil(civicWorld);
    private void SaveTownCouncil(TownRuntimeState town, TownCouncilState updated, TownGovernmentState? government = null) => townSystem.SaveTownCouncil(civicWorld, town, updated, government);
    private GridPoint? CivicBoard(TownRuntimeState town) => TownSystem.CivicBoard(civicWorld, town);
    private bool NearCivicBoard(string actor, TownRuntimeState town) => TownSystem.NearCivicBoard(civicWorld, actor, town);
    private static string CivicAgentToken(string id) => TownSystem.CivicAgentToken(id);
    private string ResolveCivicAgentToken(string token) => TownSystem.ResolveCivicAgentToken(civicWorld, token);
    private string CivicAction(string town, string kind, string subject = "", string choice = "") => TownSystem.CivicAction(civicWorld, town, kind, subject, choice);
    private static string CivicRoundToken(TownElection election) => TownSystem.CivicRoundToken(election);
    private void AddTownCivicCandidates(List<CognitionCandidate> candidates, string actor, IReadOnlyList<string>? buildingsWithoutSites = null) => townSystem.AddTownCivicCandidates(civicWorld, candidates, actor, buildingsWithoutSites);
    private void ContinueTownCivicVisit(string actor, string candidate) => townSystem.ContinueTownCivicVisit(civicWorld, actor, candidate);
    private void ApplyTownCivicCandidate(string actor, string candidate, string? proposalText = null, IReadOnlyList<string>? ballot = null, IReadOnlyList<CognitionLandTile>? landTiles = null, CognitionLandHearingChoice? hearingChoice = null, CognitionNonLandChoice? nonLandChoice = null) => townSystem.ApplyTownCivicCandidate(civicWorld, actor, candidate, proposalText, ballot, landTiles, hearingChoice, nonLandChoice);
    private void ApplyPlannedTownCivicCandidate(string actor, string candidate, string? proposalText = null, IReadOnlyList<string>? ballot = null, IReadOnlyList<CognitionLandTile>? landTiles = null, CognitionLandHearingChoice? hearingChoice = null, CognitionNonLandChoice? nonLandChoice = null, ActionPlan? originalPlan = null) => townSystem.ApplyTownCivicCandidate(civicWorld, actor, candidate, proposalText, ballot, landTiles, hearingChoice, nonLandChoice, originalPlan);
}
