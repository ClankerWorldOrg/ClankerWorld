using System.Reflection;
using System.Xml.Linq;
using ClankerWorld.Simulation.World;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClankerWorld.Tests;

internal static class RuntimeWriterScan
{
    public static string RepositoryRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ClankerWorld.sln"))) return directory.FullName;
            throw new DirectoryNotFoundException("Could not locate the ClankerWorld repository root.");
        }
    }

    public static SortedDictionary<string, string[]> FindWriters(string root)
    {
        var properties = typeof(AgentState).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name).ToArray();
        var writers = properties.Append("$collection").ToDictionary(name => name,
            _ => new SortedSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var compilation = CreateCompilation(root);
        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.Length > 0))
        {
            var model = compilation.GetSemanticModel(tree);
            var filename = Path.GetRelativePath(root, tree.FilePath).Replace(Path.DirectorySeparatorChar, '/');
            void Record(string component, SyntaxNode at)
            {
                if (!at.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => type.Identifier.ValueText == "AgentStore"))
                    throw new InvalidDataException($"Agent state may only be written by AgentStore: {filename}: {at}");
                writers[component].Add(filename);
            }
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (node is WithExpressionSyntax with && IsAgent(model.GetTypeInfo(with.Expression).Type))
                    foreach (var assignment in with.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                        if (model.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol property && writers.ContainsKey(property.Name))
                            Record(property.Name, node);
                // Construction writes every component, including defaults; moving construction
                // into a new file must be reviewed just like adding a new component update.
                if (node is BaseObjectCreationExpressionSyntax creation && IsAgent(model.GetTypeInfo(creation).Type))
                    foreach (var property in properties) Record(property, node);
                if (node is AssignmentExpressionSyntax write)
                {
                    if (model.GetSymbolInfo(write.Left).Symbol is IPropertySymbol property && IsAgent(property.ContainingType))
                        Record(property.Name, node);
                    if (write.Left is ElementAccessExpressionSyntax element && IsAgentDictionary(model.GetTypeInfo(element.Expression).Type) ||
                        model.GetSymbolInfo(write.Left).Symbol is IFieldSymbol { Name: "agents", ContainingType.Name: "WorldRuntime" })
                        Record("$collection", node);
                }
                if (node is InvocationExpressionSyntax call && call.Expression is MemberAccessExpressionSyntax access &&
                    access.Name.Identifier.ValueText is "Add" or "Remove" or "Clear" or "TryAdd" &&
                    IsAgentDictionary(model.GetTypeInfo(access.Expression).Type))
                    Record("$collection", node);
            }
        }
        return new SortedDictionary<string, string[]>(writers.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()), StringComparer.Ordinal);
    }

    internal static CSharpCompilation CreateCompilation(string root)
    {
        var source = Path.Combine(root, "src/ClankerWorld.Simulation");
        var linkedPaths = XDocument.Load(Path.Combine(source, "ClankerWorld.Simulation.csproj"))
            .Descendants("Compile").Select(element => element.Attribute("Include")?.Value)
            .Where(include => include is not null).SelectMany(include =>
            {
                var path = Path.GetFullPath(Path.Combine(source, include!.Replace('\\', Path.DirectorySeparatorChar)));
                return Path.GetFileName(path).Contains('*')
                    ? Directory.EnumerateFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path))
                    : new[] { path };
            });
        var paths = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(source, path).Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .Concat(linkedPaths);
        var trees = paths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)).ToList();
        trees.Add(CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; " +
            "global using System.IO; global using System.Linq; global using System.Net.Http; " +
            "global using System.Threading; global using System.Threading.Tasks;"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("RuntimeWriterGuard", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        // GeneratedRegex implementations are emitted by the real build, outside this
        // source scan. All other binding failures must fail closed rather than hide a writer.
        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            !IsGeneratedRegexDeclaration(diagnostic)).Take(10).ToArray();
        if (errors.Length > 0) throw new InvalidDataException(string.Join("\n", errors.Select(error => error.ToString())));
        return compilation;
    }

    private static bool IsAgent(ITypeSymbol? type) => type?.ToDisplayString() == "ClankerWorld.Simulation.World.AgentState";
    private static bool IsGeneratedRegexDeclaration(Diagnostic diagnostic) => diagnostic.Id == "CS8795" &&
        diagnostic.Location.SourceTree?.GetRoot().FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.AttributeLists
            .SelectMany(list => list.Attributes).Any(attribute => attribute.Name.ToString() == "GeneratedRegex") == true;
    private static bool IsAgentDictionary(ITypeSymbol? type) => type is INamedTypeSymbol { Name: "Dictionary", TypeArguments.Length: 2 } dictionary &&
        IsAgent(dictionary.TypeArguments[1]);
}
