using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private (TownGovernanceState Council, TownNonviolentState Nonviolent) OpenNonviolentCase(TownRuntimeState town, TownGovernanceState council, TownNonviolentState state, TownViolationAllegation allegation, TownViolationFiling filing, TownCaseEvidence? evidence) => CivicSystem.OpenNonviolentCase(civicWorld, town, council, state, allegation, filing, evidence);
    private TownNonviolentState AddNonviolentEvidence(TownNonviolentState state, TownViolationCase item, TownCaseEvidence evidence, string actor, TownGovernanceState council) => CivicSystem.AddNonviolentEvidence(civicWorld, state, item, evidence, actor, council);
    private (TownGovernanceState Council, TownNonviolentState Nonviolent) ApplyTownNonviolentAction(TownRuntimeState town, string actor, string action, string subject, string choice, CognitionNonviolentChoice? payload, TownGovernanceState council, TownGovernmentState government) => CivicSystem.ApplyTownNonviolentAction(civicWorld, town, actor, action, subject, choice, payload, council, government);
    private TownRemedyTerm[] NonviolentTerms(TownRuntimeState town, string actor, string caseId, CognitionNonviolentChoice? payload, string? replacingAgreementId = null) => CivicSystem.NonviolentTerms(civicWorld, town, actor, caseId, payload, replacingAgreementId);
    private TownGovernanceState PostNonviolentOffer(TownGovernanceState council, TownRemedyOffer offer) => CivicSystem.PostNonviolentOffer(civicWorld, council, offer);
}
