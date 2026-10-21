using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using ClankerWorld.Simulation.World;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ClankerWorld.Tests;

public sealed class NativeTickProbeTests
{
    [Theory]
    [InlineData(31, true)]
    [InlineData(61, false)]
    public void CompleteNativeFramesUseOneBoundedScenarioBudget(int delayedSeconds, bool completes)
    {
        var source = File.ReadAllText(Path.Combine(RuntimeWriterScan.RepositoryRoot, "scripts/compare-tick-equivalence.cs"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(WorldRuntime).Assembly.Location).Append(typeof(NativeTickProbeTests).Assembly.Location)
            .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path));
        // Run the actual console probe with virtual time; the native world and all frame writes stay real.
        var program = CSharpSyntaxTree.ParseText(
            "using CancellationTokenSource = ClankerWorld.Tests.NativeTickProbeTests.SimulatedDeadline;\n" + source);
        var imports = CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; " +
            "global using System.IO; global using System.Linq; global using System.Threading; global using System.Threading.Tasks;");
        var compilation = CSharpCompilation.Create("TickProbe_" + Guid.NewGuid().ToString("N"), [program, imports], references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        using var compiled = new MemoryStream();
        var emitted = compilation.Emit(compiled);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        compiled.Position = 0;
        var context = new AssemblyLoadContext(compilation.AssemblyName!, isCollectible: true);
        var directory = Directory.CreateTempSubdirectory("native-tick-budget-");
        SimulatedDeadline.Current.Value = new VirtualClock(TimeSpan.FromSeconds(delayedSeconds));
        try
        {
            var assembly = context.LoadFromStream(compiled);
            string[] arguments = [directory.FullName, "town-project-real-donation", "2", "legacy", "test", ""];
            void Run() => assembly.EntryPoint!.Invoke(null, [arguments]);
            if (completes)
            {
                Run();
                var frames = Path.Combine(directory.FullName, "legacy-00");
                Assert.Equal(3, Directory.GetFiles(frames, "*.checkpoint.gz").Length);
                Assert.Equal(3, Directory.GetFiles(frames, "*.events.json").Length);
                Assert.Equal(3, Directory.GetFiles(frames, "*.digests.json").Length);
                for (var tick = 0; tick <= 2; tick++)
                {
                    using var file = File.OpenRead(Path.Combine(frames, $"tick-{tick:D6}.checkpoint.gz"));
                    using var compressed = new GZipStream(file, CompressionMode.Decompress);
                    using var bytes = new MemoryStream();
                    compressed.CopyTo(bytes);
                    Assert.Equal(tick, WorldRuntimeCodec.Decode(bytes.ToArray()).Society.Society.WorldTick);
                }
            }
            else
            {
                var failure = Assert.Throws<TargetInvocationException>(Run);
                Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);
                Assert.Single(Directory.GetFiles(directory.FullName, "*.checkpoint.gz", SearchOption.AllDirectories));
            }
        }
        finally
        {
            SimulatedDeadline.Current.Value = null;
            context.Unload();
            directory.Delete(recursive: true);
        }
    }

    internal sealed class VirtualClock(TimeSpan delay)
    {
        internal TimeSpan Elapsed { get; private set; }
        internal List<SimulatedDeadline> Deadlines { get; } = [];
        private bool advanced;
        internal void DelayFirstStep()
        {
            if (advanced) return;
            advanced = true;
            Elapsed += delay;
            foreach (var deadline in Deadlines.Where(item => item.Due <= Elapsed)) deadline.Cancellation.Cancel();
        }
    }

    public sealed class SimulatedDeadline : IDisposable
    {
        internal static readonly AsyncLocal<VirtualClock?> Current = new();
        private readonly VirtualClock clock = Current.Value!;
        internal CancellationTokenSource Cancellation { get; } = new();
        internal TimeSpan Due { get; }
        public SimulatedDeadline(TimeSpan timeout)
        {
            Due = clock.Elapsed + timeout;
            clock.Deadlines.Add(this);
        }
        public CancellationToken Token { get { clock.DelayFirstStep(); return Cancellation.Token; } }
        public void Dispose() { clock.Deadlines.Remove(this); Cancellation.Dispose(); }
    }
}
