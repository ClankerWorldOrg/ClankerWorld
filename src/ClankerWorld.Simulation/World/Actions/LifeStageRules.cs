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
    public static bool IsElder(SocietyAgeBand age) => age == SocietyAgeBand.Elder;

}
