namespace ClankerWorld.Server.Observation;

/// <summary>Explain recorded judge and election reasons without changing the hearing's saved facts.</summary>
internal static class HearingReasonText
{
    public static string? Describe(string? reason) => reason switch
    {
        "land_mandate_ended" => "The Town ended the authority to appoint a land judge, so this election was cancelled and the case is waiting for a judge.",
        "non_land_mandate_ended" => "The Town ended the authority to appoint a judge for non-land cases, so this election was cancelled and the case is waiting for a judge.",
        "authority_or_eligibility_ended" => "This judge's authority ended or they no longer qualify to hear the case; another eligible judge must be assigned before it can continue.",
        "willingness_withdrawn" => "This judge is no longer willing to hear the case; another eligible judge must be assigned before it can continue.",
        "resigned" => "This judge resigned from the case; another eligible judge must be assigned before it can continue.",
        "authorized_judge_available" => "An eligible judge with authority to hear the case is now available, so this election was cancelled.",
        "case_closed" => "The case was settled.",
        "reopening_rejected" => "The judge rejected reopening the case; the existing ruling stays in effect.",
        _ => reason,
    };
}
