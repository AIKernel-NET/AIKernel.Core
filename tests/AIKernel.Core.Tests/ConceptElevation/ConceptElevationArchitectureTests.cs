namespace AIKernel.Core.Tests.ConceptElevation;

using System.Text.RegularExpressions;
using AIKernel.Core.Concepts;

/// <summary>
/// EN: Verifies that elevated concept names remain on concept surfaces only.
/// JA: 昇格した概念名が concept surface のみに留まることを検証します。
/// </summary>
public sealed class ConceptElevationArchitectureTests
{
    private static readonly string[] PhilosophicalPrefixes =
    [
        "Ethos",
        "Pathos",
        "Logos",
        "Nomos",
        "Dike",
        "Kratos",
        "Aisthesis",
        "Phantasia",
        "Chronos",
        "Kairos",
        "Dynamis",
        "Energeia",
        "Nous",
        "Telos",
        "Apatheia",
        "Ataraxia",
        "Eidos",
    ];

    private static readonly string[] ForbiddenTechnicalSuffixes =
    [
        "Dto",
        "Request",
        "Result",
        "Mapper",
        "Adapter",
        "Serializer",
        "Converter",
        "HttpClient",
        "JSInterop",
        "JsInterop",
        "NativeBridge",
        "Provider",
    ];

    private static readonly Regex TypeDeclarationPattern = new(
        @"\b(?:public|internal|private|protected)?\s*(?:sealed\s+|abstract\s+|static\s+|partial\s+)*\b(?:class|record|interface|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    /// <summary>
    /// EN: Confirms Core concept facades are available without touching CTG contracts.
    /// JA: CTG contract に触れず Core concept facade が利用可能であることを確認します。
    /// </summary>
    [Fact]
    public void ConceptFacades_WhenConstructed_ReturnStableConceptValues()
    {
        var telos = new TelosObjective("objective.test", "Verify concept elevation");
        var nomos = new NomosCanon("Canon.Test", ["Canon.Test.Rule"]);
        var dike = new DikeSafetyBoundary("Boundary.Test", ["Requirement.Test"]);
        var ethos = new EthosCouncil();
        var pathos = new PathosCouncil();
        var logos = new LogosCouncil();

        Assert.True(telos.Matches("objective.test"));
        Assert.True(nomos.ContainsRule("Canon.Test.Rule"));
        Assert.True(dike.Requires("Requirement.Test"));
        Assert.Equal("Ethics, safety, and norms", ethos.Responsibility);
        Assert.Equal("Risk, anomaly, and danger signals", pathos.Responsibility);
        Assert.Equal("Logic, consistency, and verification", logos.Responsibility);
    }

    /// <summary>
    /// EN: Rejects philosophical prefixes on DTO, adapter, provider, and other low-level names.
    /// JA: DTO / adapter / provider など低レイヤ名への哲学語 prefix を拒否します。
    /// </summary>
    [Fact]
    public void SourceTypes_WhenUsingPhilosophicalPrefix_DoNotUseForbiddenTechnicalSuffix()
    {
        var violations = FindViolations("AIKernel.Core.slnx");

        Assert.Empty(violations);
    }

    private static IReadOnlyList<string> FindViolations(string solutionFileName)
    {
        var repositoryRoot = FindRepositoryRoot(solutionFileName);
        var sourceRoot = Path.Combine(repositoryRoot, "src");

        return Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => FindViolationsInFile(repositoryRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> FindViolationsInFile(string repositoryRoot, string path)
    {
        var source = File.ReadAllText(path);
        foreach (Match match in TypeDeclarationPattern.Matches(source))
        {
            var typeName = match.Groups["name"].Value;
            var hasPhilosophicalPrefix = PhilosophicalPrefixes.Any(prefix => typeName.StartsWith(prefix, StringComparison.Ordinal));
            var hasForbiddenTechnicalSuffix = ForbiddenTechnicalSuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal));
            var isConceptSurface = path.Contains($"{Path.DirectorySeparatorChar}Concepts{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

            if (hasPhilosophicalPrefix && (hasForbiddenTechnicalSuffix || !isConceptSurface))
            {
                yield return $"{Path.GetRelativePath(repositoryRoot, path)}: {typeName}";
            }
        }
    }

    private static string FindRepositoryRoot(string solutionFileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, solutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate {solutionFileName}.");
    }
}
