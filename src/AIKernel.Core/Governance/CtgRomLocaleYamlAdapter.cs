namespace AIKernel.Core.Governance;

using AIKernel.Core.Time;
using AIKernel.Dtos.Diagnostics;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Diagnostics;
using YamlDotNet.Serialization;

/// <summary>
/// EN: Carries the result of adapting CTG ROM locale YAML into Core governance references. JA: CTG ROM locale YAML を Core 統治参照へ変換した結果を運びます。
/// </summary>
public sealed record CtgRomMergeResult
{
    /// <summary>EN: Gets whether the merge adapter produced no fail-closed diagnostics. JA: merge adapter が fail-closed 診断を生成しなかったかどうかを取得します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>EN: Gets the merged ROM descriptor. JA: merge 済み ROM descriptor を取得します。</summary>
    public CtgMergedRomDescriptor Descriptor { get; init; } = new();

    /// <summary>EN: Gets resolved canon references. JA: 解決済み正典参照を取得します。</summary>
    public IReadOnlyList<CanonReference> CanonReferences { get; init; } = [];

    /// <summary>EN: Gets diagnostics emitted by the adapter. JA: adapter が出力した診断を取得します。</summary>
    public IReadOnlyList<DiagnosticEntry> Diagnostics { get; init; } = [];
}

/// <summary>
/// EN: Adapts merged CTG ROM locale YAML into canon reference carriers. JA: merge 済み CTG ROM locale YAML を正典参照 carrier へ変換します。
/// </summary>
public sealed class CtgRomLocaleYamlAdapter
{
    private readonly IDeserializer _deserializer;
    private readonly CtgCanonReferenceResolver _referenceResolver;
    private readonly IKernelClock _clock;

    /// <summary>
    /// EN: Initializes the adapter with default collaborators. JA: 既定の協調オブジェクトで adapter を初期化します。
    /// </summary>
    public CtgRomLocaleYamlAdapter()
        : this(
            new DeserializerBuilder().Build(),
            new CtgCanonReferenceResolver(),
            KernelClock.System())
    {
    }

    /// <summary>
    /// EN: Initializes the adapter. JA: adapter を初期化します。
    /// </summary>
    /// <param name="deserializer">EN: The YAML deserializer. JA: YAML deserializer です。</param>
    /// <param name="referenceResolver">EN: The canon reference resolver. JA: 正典参照 resolver です。</param>
    /// <param name="clock">EN: The clock used for diagnostics. JA: 診断に使用する clock です。</param>
    public CtgRomLocaleYamlAdapter(
        IDeserializer deserializer,
        CtgCanonReferenceResolver referenceResolver,
        IKernelClock clock)
    {
        _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// EN: Parses locale YAML without content hashes. JA: content hash なしで locale YAML を解析します。
    /// </summary>
    /// <param name="yaml">EN: The locale YAML content. JA: locale YAML 内容です。</param>
    /// <param name="sourcePath">EN: The optional source path. JA: 任意の source path です。</param>
    /// <returns>EN: The merge result. JA: merge 結果を返します。</returns>
    public CtgRomMergeResult Parse(
        string yaml,
        string? sourcePath = null)
    {
        return Parse(
            yaml,
            new Dictionary<string, string>(StringComparer.Ordinal),
            sourcePath);
    }

    /// <summary>
    /// EN: Parses locale YAML and attaches known content hashes by path. JA: locale YAML を解析し、path ごとの既知 content hash を付与します。
    /// </summary>
    /// <param name="yaml">EN: The locale YAML content. JA: locale YAML 内容です。</param>
    /// <param name="contentHashesByPath">EN: Content hashes keyed by ROM path. JA: ROM path を key とする content hash です。</param>
    /// <param name="sourcePath">EN: The optional source path. JA: 任意の source path です。</param>
    /// <returns>EN: The merge result. JA: merge 結果を返します。</returns>
    public CtgRomMergeResult Parse(
        string yaml,
        IReadOnlyDictionary<string, string> contentHashesByPath,
        string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(contentHashesByPath);

        var root = NormalizeYamlMap(
            _deserializer.Deserialize<Dictionary<object, object?>>(yaml)
            ?? new Dictionary<object, object?>());
        var diagnostics = new List<DiagnosticEntry>();
        var canon = GetMap(root, "canon");
        var councils = GetMapList(root, "councils");
        var decisionGate = GetMap(root, "decisionGate");
        var trajectoryGate = GetMap(root, "trajectoryGate");
        var rejectPolicy = GetMap(root, "rejectPolicy");

        var descriptor = new CtgMergedRomDescriptor
        {
            CanonReference = CreateReference(
                canon,
                pathField: "path",
                referenceField: "canonReference",
                location: "canon",
                contentHashesByPath,
                diagnostics,
                sourcePath),
            CouncilReferences = councils
                .Select((council, index) => CreateReference(
                    council,
                    pathField: "rulesPath",
                    referenceField: "canonReference",
                    location: $"councils[{index}]",
                    contentHashesByPath,
                    diagnostics,
                    sourcePath))
                .Where(reference => reference is not null)
                .Select(reference => reference!)
                .ToArray(),
            DecisionGateReference = CreateReference(
                decisionGate,
                pathField: "policyPath",
                referenceField: "canonReference",
                location: "decisionGate",
                contentHashesByPath,
                diagnostics,
                sourcePath),
            TrajectoryGateReference = CreateReference(
                trajectoryGate,
                pathField: "policyPath",
                referenceField: "canonReference",
                location: "trajectoryGate",
                contentHashesByPath,
                diagnostics,
                sourcePath),
            RejectPolicyReference = CreateReference(
                rejectPolicy,
                pathField: "rulesPath",
                referenceField: "canonReference",
                location: "rejectPolicy",
                contentHashesByPath,
                diagnostics,
                sourcePath),
            Metadata = CreateMetadata(root, sourcePath)
        };
        var references = _referenceResolver.Resolve(descriptor);

        return new CtgRomMergeResult
        {
            Succeeded = diagnostics.All(diagnostic => diagnostic.Severity < DiagnosticSeverity.Error),
            Descriptor = descriptor,
            CanonReferences = references,
            Diagnostics = diagnostics
        };
    }

    private CanonReference? CreateReference(
        IReadOnlyDictionary<string, object?> source,
        string pathField,
        string referenceField,
        string location,
        IReadOnlyDictionary<string, string> contentHashesByPath,
        ICollection<DiagnosticEntry> diagnostics,
        string? sourcePath)
    {
        var path = GetString(source, pathField);
        var canonReference = GetString(source, referenceField);

        if (string.IsNullOrWhiteSpace(path))
        {
            diagnostics.Add(CreateDiagnostic(
                code: "ctg.rom.missing_path",
                message: "CTG ROM locale YAML is missing a governance document path.",
                sourcePath,
                location,
                pathField));
        }

        if (string.IsNullOrWhiteSpace(canonReference))
        {
            diagnostics.Add(CreateDiagnostic(
                code: "ctg.rom.missing_canon_reference",
                message: "CTG ROM locale YAML is missing a canon reference.",
                sourcePath,
                location,
                referenceField));
        }

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(canonReference))
        {
            return null;
        }

        contentHashesByPath.TryGetValue(path, out var contentHash);

        return new CanonReference
        {
            CanonId = canonReference,
            Path = path,
            ContentHash = string.IsNullOrWhiteSpace(contentHash) ? null : contentHash
        };
    }

    private DiagnosticEntry CreateDiagnostic(
        string code,
        string message,
        string? sourcePath,
        string location,
        string field)
    {
        return new DiagnosticEntry
        {
            DiagnosticId = code + "." + location,
            Code = code,
            Message = message,
            Severity = DiagnosticSeverity.Error,
            Scope = DiagnosticScope.Governance,
            ObservedAt = _clock.Now,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source_path"] = sourcePath ?? string.Empty,
                ["location"] = location,
                ["field"] = field
            }
        };
    }

    private static IReadOnlyDictionary<string, string> CreateMetadata(
        IReadOnlyDictionary<string, object?> root,
        string? sourcePath)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["source_path"] = sourcePath ?? string.Empty
        };

        AddIfPresent(metadata, "id", GetString(root, "id"));
        AddIfPresent(metadata, "version", GetString(root, "version"));
        AddIfPresent(metadata, "canon_version", GetString(root, "canonVersion"));
        AddIfPresent(metadata, "schema_version", GetString(root, "schemaVersion"));
        AddIfPresent(metadata, "locale", GetString(root, "locale"));

        return metadata;
    }

    private static void AddIfPresent(
        IDictionary<string, string> metadata,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata[key] = value;
        }
    }

    private static IReadOnlyDictionary<string, object?> GetMap(
        IReadOnlyDictionary<string, object?> source,
        string key)
    {
        return source.TryGetValue(key, out var value) && value is IReadOnlyDictionary<string, object?> map
            ? map
            : new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> GetMapList(
        IReadOnlyDictionary<string, object?> source,
        string key)
    {
        if (!source.TryGetValue(key, out var value) ||
            value is not IEnumerable<object?> values ||
            value is string)
        {
            return [];
        }

        return values
            .OfType<IReadOnlyDictionary<string, object?>>()
            .ToArray();
    }

    private static string? GetString(
        IReadOnlyDictionary<string, object?> source,
        string key)
    {
        return source.TryGetValue(key, out var value)
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }

    private static IReadOnlyDictionary<string, object?> NormalizeYamlMap(
        IDictionary<object, object?> raw)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var pair in raw)
        {
            var key = Convert.ToString(
                pair.Key,
                System.Globalization.CultureInfo.InvariantCulture);

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            result[key] = NormalizeYamlValue(pair.Value);
        }

        return result;
    }

    private static object? NormalizeYamlValue(object? value)
    {
        return value switch
        {
            null => null,
            IDictionary<object, object?> map => NormalizeYamlMap(map),
            IEnumerable<object?> list when value is not string => list.Select(NormalizeYamlValue).ToArray(),
            _ => value
        };
    }
}
