namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static void ValidateCompletedGovernmentAuthority(TownCouncilState council, TownGovernmentState state)
    {
        var leader = TownGovernmentRules.GoverningOffice(state)?.HolderId;
        if (state.Arrangement.Ordinary == TownArrangementRules.Mayor &&
            (leader is not null ? council.Form != "leader" || !council.Members.SequenceEqual([leader]) : council.Form != "all_adult") ||
            state.Arrangement.Ordinary != TownArrangementRules.Mayor && council.Form == "leader" ||
            state.Arrangement.Ordinary == TownArrangementRules.AllAdultCouncil && council.Form != "all_adult")
            throw new InvalidDataException("Ordinary decision authority does not match the approved arrangement and living officeholder.");
    }
}
