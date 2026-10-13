using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public static class LifeStageRules
{
    public static bool IsAdult(SocietyAgeBand age) => age is SocietyAgeBand.Adult or SocietyAgeBand.Elder;
    public static bool CanHaveChildren(SocietyAgeBand age) => age == SocietyAgeBand.Adult;
    public static bool IsYoungChild(SocietyAgeBand age) => IsInfant(age) || age == SocietyAgeBand.Child;
    public static bool IsDependent(SocietyAgeBand age) => IsInfant(age) || IsChild(age);
    public static bool IsChild(SocietyAgeBand age) => age is SocietyAgeBand.Child or SocietyAgeBand.Adolescent;
    public static bool IsInfant(SocietyAgeBand age) => age == SocietyAgeBand.Infant;

    public static bool Permits(SocietyAgeBand age, string candidate)
    {
        if (IsInfant(age)) return false;
        if (candidate == "make_room_for_food" && IsChild(age)) return true;
        if (IsAdult(age)) return !candidate.StartsWith("child_", StringComparison.Ordinal);
        return candidate is "safe_idle" or "consume_food" or "drink_milk" or "collect_shared_food" or
            "seek_food" or "harvest_food" or "talk_to" or "move_to" or "wear_clothing" or "seek_warmth" or "seek_shelter" or "inspect_shelter_site" ||
            candidate.StartsWith("guardian_accept:", StringComparison.Ordinal) ||
            candidate.StartsWith("guardian_refuse:", StringComparison.Ordinal) ||
            candidate.StartsWith("guardian_end:", StringComparison.Ordinal) ||
            candidate.StartsWith("child_converse:", StringComparison.Ordinal) ||
            candidate.StartsWith("child_play:", StringComparison.Ordinal) ||
            candidate.StartsWith("child_learn:", StringComparison.Ordinal) ||
            candidate.StartsWith("talk:", StringComparison.Ordinal) ||
            candidate.StartsWith("conversation_", StringComparison.Ordinal) ||
            candidate.StartsWith("town_salvage:", StringComparison.Ordinal) ||
            candidate == "child_help_food" || candidate.StartsWith("child_gather:", StringComparison.Ordinal) ||
            candidate.StartsWith("child_carry:", StringComparison.Ordinal);
    }
}
