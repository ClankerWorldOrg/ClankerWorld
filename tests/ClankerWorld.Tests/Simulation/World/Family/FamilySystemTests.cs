using ClankerWorld.Simulation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class FamilySystemTests
{
    [Fact]
    public void InvalidBirthCompletionFailsAtItsOwningSystemBeforePublishing()
    {
        var store = new AgentStore();
        store.PlaceFounder("parent", new(1, 1));
        var before = store.Living["parent"];
        var incomplete = new ParenthoodPlan("partner", "completed", 0, 0,
            PrimaryCaregiverId: "parent", IntendedHouseholdId: "household-first");

        var failure = Assert.Throws<InvalidDataException>(() => store.RecordParenthood("parent", before, incomplete));

        Assert.Contains("Family parenthood", failure.Message, StringComparison.Ordinal);
        Assert.Same(before, store.Living["parent"]);
    }

    [Fact]
    public void AConsumedDoseCannotGainTimeWhenItsProgressIsWritten()
    {
        var store = new AgentStore();
        store.PlaceFounder("patient", new(1, 1));
        var before = store.Living["patient"];
        var treatment = new MedicalTreatmentState("caregiver", "medicine-lot", "owner", Identity.Parse("dose-receipt"), "medicine",
            0, 1, MedicalCareRules.TreatmentTicks);

        var failure = Assert.Throws<InvalidDataException>(() => store.RecordMedicalTreatment("patient", before, treatment));

        Assert.Contains("Family medical treatment", failure.Message, StringComparison.Ordinal);
        Assert.Same(before, store.Living["patient"]);
    }

    [Fact]
    public void OneContinuityCoupleCannotHaveTwoDeadlines()
    {
        var state = new PopulationContinuity(true, [new("first", "second", 120), new("first", "second", 121)], []);
        Assert.Throws<InvalidDataException>(() => FamilySystem.ValidateLocalContinuity(state));
    }
}
