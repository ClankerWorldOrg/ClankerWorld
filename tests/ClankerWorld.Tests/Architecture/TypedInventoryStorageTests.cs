using ClankerWorld.Simulation.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ClankerWorld.Tests;

public sealed class TypedInventoryStorageTests
{
    [Fact]
    public void RawInterpolatedStringsCannotEnterReservationOfferReceiptOrInputStorage()
    {
        const string source = """
            using ClankerWorld.Simulation;
            using ClankerWorld.Simulation.Kernel;
            using ClankerWorld.Simulation.World;
            class StorageControl
            {
                object Reservation(string owner) => new InventoryReservation($"reserved:{owner}", owner, "lot", 1, "work", 20, true, InventoryReservationState.Reserved);
                object Offer(string owner) => new DirectBarterProposal($"offer:{owner}", 1, owner, "other", "lot", 1, "payment", 1, 20);
                object Receipt(string owner) => new OwnerInstructionReceipt($"receipt:{owner}", "key", 0, 0, 1);
                object Inputs(string owner) => new WorldProductionJob("job", "recipe", "building", owner, 0, 20, WorldProductionJobState.Running, [$"input:{owner}"]);
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(InventoryReservation).Assembly.Location).Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path));
        var invalid = CSharpCompilation.Create("RawInventoryStorage", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = invalid.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Equal(4, errors.Length);
        Assert.All(errors, error => Assert.True(error.Id is "CS1503" or "CS0029", error.ToString()));
        var validSource = source;
        foreach (var prefix in new[] { "reserved", "offer", "receipt", "input" })
            validSource = validSource.Replace("$\"" + prefix + ":{owner}\"", "Identity.Create($\"" + prefix + ":{owner}\")", StringComparison.Ordinal);
        var valid = invalid.RemoveAllSyntaxTrees().AddSyntaxTrees(CSharpSyntaxTree.ParseText(validSource));
        Assert.Empty(valid.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error));
    }
}
