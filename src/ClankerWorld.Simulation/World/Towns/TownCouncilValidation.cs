using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public static partial class TownCouncilValidation
{
    public static void Validate(TownRuntimeState town, SocietyCheckpoint society, int day)
    {
        if (town.Council is not { } state)
        {
            if (town.FoundingState == "founded") throw new InvalidDataException("A founded Town is missing its saved governance.");
            return;
        }
        var tick = society.WorldTick;
        var known = society.Agents.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var adults = town.ResidentIds.Where(id => society.Agents.Any(p => p.Id == id &&
            p.Status == SocietyAgentStatus.Active && LifeStageRules.IsAdult(p.AgeBand))).ToHashSet(StringComparer.Ordinal);
        TownSystem.ValidateLocalCouncil(town.Id, state);
        if (state.Members.Any(id => !adults.Contains(id)) || state.Form == "all_adult" && !adults.SetEquals(state.Members) ||
            state.Candidates.Any(candidate => !adults.Contains(candidate.AgentId)))
            throw new InvalidDataException("A Town's saved council, candidates or civic ledger is invalid.");
        foreach (var proposal in state.Proposals)
            if (!known.Contains(proposal.AuthorId) || proposal.Kind == "admission" && !known.Contains(proposal.SubjectId!) ||
                proposal.Kind == "law_case" && !known.Contains(proposal.NonLandRequest!.Allegation.SubjectId) || proposal.OpenedTick > tick ||
                proposal.DeadlineTick != proposal.OpenedTick + day || proposal.Status == "pending" && proposal.DeadlineTick <= tick ||
                proposal.Status != "pending" && proposal.SettledTick > tick)
                throw new InvalidDataException("A Town's saved proposal or final votes are invalid.");
        foreach (var election in state.ElectionHistory.Concat(state.Election is { } current ? [current] : []))
            if (election.OpenedTick > tick || election.DeadlineTick != election.OpenedTick + day ||
                election.Voters.Concat(election.Candidates).Concat(election.Supported).Concat(election.SettledSeats).Any(id => !known.Contains(id)) ||
                state.Election?.Id == election.Id && election.Voters.Concat(election.Candidates).Concat(election.SettledSeats).Any(id => !adults.Contains(id)))
                throw new InvalidDataException("A Town's saved election, consent, ballots or recorded draw is invalid.");
        if (state.Notices.Any(notice => notice.PostedTick > tick))
            throw new InvalidDataException("A saved Town civic notice is invalid.");
        ValidateKnowledge(state, known, tick);
    }

    private static void ValidateKnowledge(TownCouncilState state, HashSet<string> known, long tick)
    {
        foreach (var receipt in state.Knowledge)
            if (!known.Contains(receipt.AgentId) || receipt.LearnedTick > tick || receipt.SourceAgentId is { } source && !known.Contains(source))
                throw new InvalidDataException("Saved civic knowledge requires an actual notice or informed relay.");
    }
}
