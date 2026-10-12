using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private (TownCouncilState Council, TownNonLandHearingState NonLandHearings) OpenNonLandCase(TownRuntimeState town, TownCouncilState council, TownNonLandHearingState state, TownViolationAllegation allegation, TownViolationFiling filing, TownCaseEvidence? evidence) => TownSystem.OpenNonLandCase(civicWorld, town, council, state, allegation, filing, evidence);
    private TownNonLandHearingState AddNonLandEvidence(TownNonLandHearingState state, TownNonLandCase item, TownCaseEvidence evidence, string actor, TownCouncilState council) => TownSystem.AddNonLandEvidence(civicWorld, state, item, evidence, actor, council);
    private (TownCouncilState Council, TownNonLandHearingState NonLandHearings) ApplyTownNonLandAction(TownRuntimeState town, string actor, string action, string subject, string choice, CognitionNonLandChoice? payload, TownCouncilState council, TownGovernmentState government) => townSystem.ApplyTownNonLandAction(civicWorld, town, actor, action, subject, choice, payload, council, government);
    private TownRemedyTerm[] NonLandTerms(TownRuntimeState town, string actor, string caseId, CognitionNonLandChoice? payload, string? replacingAgreementId = null) => townSystem.NonLandTerms(civicWorld, town, actor, caseId, payload, replacingAgreementId);
    private TownCouncilState PostNonLandOffer(TownCouncilState council, TownRemedyOffer offer) => TownSystem.PostNonLandOffer(civicWorld, council, offer);
}
