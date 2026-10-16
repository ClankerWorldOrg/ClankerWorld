namespace ClankerWorld.Simulation.World;

public enum CivicProposalVoteKind { Yes, No, Withdraw }

public sealed record CivicProposalVoteChoice(string TownId, CivicProposalVoteKind Kind, string ProposalId)
{
    public static string Verb(CivicProposalVoteKind kind) => kind switch { CivicProposalVoteKind.Yes => "yes", CivicProposalVoteKind.No => "no", _ => "withdraw_proposal" };
    public string Encode() => $"civic|{TownId}|{Verb(Kind)}|{ProposalId}|";
    public static CivicProposalVoteChoice? Decode(string value)
    {
        if (value.Split('|') is not ["civic", var town, var verb, var proposal, ""] ||
            !IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) ||
            !IdentityRules.Reference(proposal, DerivedId.MaximumLength, requireTrimmed: true)) return null;
        CivicProposalVoteKind? kind = verb switch { "yes" => CivicProposalVoteKind.Yes, "no" => CivicProposalVoteKind.No, "withdraw_proposal" => CivicProposalVoteKind.Withdraw, _ => null };
        return kind is { } selected ? new(town, selected, proposal) : null;
    }
}

public sealed record CivicProposalVoteActionPlan(string ActorId, CivicProposalVoteChoice Choice, TownProposal Proposal)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "give that Town proposal response";
}
