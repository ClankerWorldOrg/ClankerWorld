namespace ClankerWorld.Simulation.Society;

public static partial class SocietyRules
{
    public static SocietyOperationResult SetAgingOverride(SocietyCheckpoint checkpoint, int rate)
    {
        Validate(checkpoint);
        if (!checkpoint.IsPaused)
        {
            throw new InvalidOperationException("Pause the world before changing life pace.");
        }
        if (rate is not (1 or 365 or 1_460))
        {
            throw new ArgumentOutOfRangeException(nameof(rate));
        }
        if ((checkpoint.LifeClock?.Rate ?? 1) == rate)
        {
            return new(checkpoint);
        }
        var next = checkpoint with
        {
            Config = checkpoint.Config with { ContractVersion = Math.Max(2, checkpoint.Config.ContractVersion) },
            LifeClock = new(rate, checkpoint.WorldTick, checkpoint.LifeTickAt(checkpoint.WorldTick)),
        };
        return Commit(next, "aging_override_changed", rate.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
