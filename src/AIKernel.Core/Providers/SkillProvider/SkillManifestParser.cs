namespace AIKernel.Core.Providers.SkillProvider;

using AIKernel.Common.Results;
using AIKernel.Dtos.Dsl;
using YamlDotNet.Serialization;

/// <summary>
/// [EN] Parses OpenAI-compatible SKILL.md files into Skill manifests and DSL pipeline documents.
/// [JA] OpenAI 互換 SKILL.md を Skill manifest と DSL pipeline document に解析します。
/// </summary>
public sealed class SkillManifestParser
{
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// [EN] Creates a parser using the default YAML frontmatter deserializer.
    /// [JA] 既定の YAML frontmatter deserializer を使用する parser を作成します。
    /// </summary>
    public SkillManifestParser()
        : this(new DeserializerBuilder().Build())
    {
    }

    /// <summary>
    /// [EN] Creates a parser with an explicit YAML deserializer.
    /// [JA] 明示された YAML deserializer を使用する parser を作成します。
    /// </summary>
    public SkillManifestParser(
        IDeserializer deserializer)
    {
        _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
    }

    /// <summary>
    /// [EN] Parses a Skill.MD / SKILL.md document.
    /// [JA] Skill.MD / SKILL.md document を解析します。
    /// </summary>
    public SkillManifest Parse(
        string markdown,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        var parsed = ParseHeader(normalized, sourcePath);
        var metadata = parsed.Metadata;

        var name = GetRequired(metadata, "name", sourcePath);
        var description = GetRequired(metadata, "description", sourcePath);
        var dsl = CompileSkillToDsl(parsed.Body, metadata);

        return new SkillManifest(
            name,
            description,
            parsed.Body,
            sourcePath,
            metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            dsl);
    }

    /// <summary>
    /// [EN] Converts Skill.MD instructions into a deterministic DSL document.
    /// [JA] Skill.MD の instructions を決定論的な DSL document に変換します。
    /// </summary>
    public DslDocument CompileSkillToDsl(
        string body,
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(metadata);

        var extractedSteps = ExtractSteps(body)
            .Select(ToDslNode)
            .ToArray();
        var steps = SelectSkillSteps(extractedSteps, metadata);

        return new DslDocument(new PipelineRootNode(steps));
    }

    private ParsedSkillHeader ParseHeader(
        string normalized,
        string sourcePath)
        => RequireOpenAiFrontMatter(normalized)
            .Match(
                _ =>
                {
                    var metadata = ParseLegacyHeader(normalized, out var body);
                    return new ParsedSkillHeader(metadata, body);
                },
                markdown =>
                {
                    var metadata = ParseOpenAiFrontMatter(markdown, sourcePath, out var body);
                    return new ParsedSkillHeader(metadata, body);
                });

    private static Either<string, string> RequireOpenAiFrontMatter(
        string markdown)
    {
        if (markdown.StartsWith("---\n", StringComparison.Ordinal))
        {
            return Either<string, string>.FromRight(markdown);
        }

        return Either<string, string>.FromLeft(
            "SKILL.md does not start with OpenAI-compatible YAML frontmatter.");
    }

    private Dictionary<string, string> ParseOpenAiFrontMatter(
        string markdown,
        string sourcePath,
        out string body)
    {
        var secondDelimiter = markdown.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (secondDelimiter < 0)
        {
            throw new InvalidOperationException(
                $"SKILL.md YAML frontmatter closing delimiter was not found. SourcePath='{sourcePath}'.");
        }

        var yaml = markdown[4..secondDelimiter];
        body = markdown[(secondDelimiter + "\n---\n".Length)..];

        return Try
            .Run(() =>
            {
                var raw = _deserializer.Deserialize<Dictionary<object, object?>>(yaml)
                          ?? new Dictionary<object, object?>();
                return FlattenYaml(raw);
            })
            .Match(
                error => throw new InvalidOperationException(
                    $"SKILL.md YAML frontmatter could not be parsed. SourcePath='{sourcePath}'.",
                    new InvalidOperationException(error.Message)),
                metadata => metadata);
    }

    private static Dictionary<string, string> ParseLegacyHeader(
        string markdown,
        out string body)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        var bodyStart = 0;
        var lines = markdown.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                bodyStart = index + 1;
                break;
            }

            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                bodyStart = index;
                break;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            metadata[NormalizeKey(key)] = value;
            bodyStart = index + 1;
        }

        var title = ReadOptionalMetadata(metadata, "title");
        var name = ReadOptionalMetadata(metadata, "name");
        name.Match(
            () => title.Tap(value => metadata["name"] = value),
            _ => name);

        body = string.Join('\n', lines.Skip(bodyStart));
        return metadata;
    }

    private static Dictionary<string, string> FlattenYaml(
        IDictionary<object, object?> raw)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in raw)
        {
            var key = Convert.ToString(
                pair.Key,
                System.Globalization.CultureInfo.InvariantCulture);

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            FlattenValue(result, NormalizeKey(key), pair.Value);
        }

        return result;
    }

    private static void FlattenValue(
        Dictionary<string, string> result,
        string key,
        object? value)
    {
        switch (value)
        {
            case null:
                result[key] = string.Empty;
                break;
            case IDictionary<object, object?> map:
                foreach (var pair in map)
                {
                    var nestedKey = Convert.ToString(
                        pair.Key,
                        System.Globalization.CultureInfo.InvariantCulture);

                    if (!string.IsNullOrWhiteSpace(nestedKey))
                    {
                        FlattenValue(result, $"{key}.{NormalizeKey(nestedKey)}", pair.Value);
                    }
                }

                break;
            case IEnumerable<object?> list when value is not string:
                result[key] = string.Join(
                    ",",
                    list
                        .Select(x => Convert.ToString(
                            x,
                            System.Globalization.CultureInfo.InvariantCulture))
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                break;
            default:
                result[key] = Convert.ToString(
                                  value,
                                  System.Globalization.CultureInfo.InvariantCulture)
                              ?? string.Empty;
                break;
        }
    }

    private static IReadOnlyList<string> ExtractSteps(
        string body)
    {
        var lines = body.Split('\n');
        var steps = new List<string>();
        var insideSteps = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Equals("steps:", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("## Steps", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("# Steps", StringComparison.OrdinalIgnoreCase))
            {
                insideSteps = true;
                continue;
            }

            if (insideSteps && line.StartsWith('#'))
            {
                break;
            }

            if (insideSteps)
            {
                steps.Add(CleanStepLine(line));
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) ||
                line.StartsWith("* ", StringComparison.Ordinal))
            {
                steps.Add(CleanStepLine(line));
            }
        }

        return steps
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static PipelineNode ToDslNode(
        string step)
    {
        const string callPrefix = "call:";

        if (step.StartsWith(callPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var capability = step[callPrefix.Length..].Trim();
            return new CallCapabilityNode(
                capability,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "skill"
                });
        }

        return new StepNode(step);
    }

    private static string CleanStepLine(
        string line)
    {
        var cleaned = line.Trim();
        while (cleaned.Length > 0 &&
               (cleaned[0] is '-' or '*' ||
                char.IsDigit(cleaned[0]) ||
                cleaned[0] is '.' or ')'))
        {
            cleaned = cleaned[1..].TrimStart();
        }

        return cleaned;
    }

    private static string GetRequired(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        string sourcePath)
    {
        return ReadMetadata(metadata, key)
            .Match(
                _ => throw new InvalidOperationException(
                    $"SKILL.md metadata field '{key}' is required. SourcePath='{sourcePath}'."),
                value => value);
    }

    private static IReadOnlyList<PipelineNode> SelectSkillSteps(
        IReadOnlyList<PipelineNode> steps,
        IReadOnlyDictionary<string, string> metadata)
        => RequireNonEmptySteps(steps)
            .Match(
                _ => [new StepNode($"Use skill instructions: {GetSkillNameOrDefault(metadata)}")],
                value => value);

    private static Either<string, IReadOnlyList<PipelineNode>> RequireNonEmptySteps(
        IReadOnlyList<PipelineNode> steps)
    {
        if (steps.Count > 0)
        {
            return Either<string, IReadOnlyList<PipelineNode>>.FromRight(steps);
        }

        return Either<string, IReadOnlyList<PipelineNode>>.FromLeft(
            "SKILL.md does not contain explicit steps.");
    }

    private static string GetSkillNameOrDefault(
        IReadOnlyDictionary<string, string> metadata)
        => ReadMetadata(metadata, "name")
            .Match(_ => "skill", value => value);

    private static Either<string, string> ReadMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string key)
    {
        if (metadata.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return Either<string, string>.FromRight(value.Trim());
        }

        return Either<string, string>.FromLeft($"Metadata field '{key}' is missing.");
    }

    private static Option<string> ReadOptionalMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string key)
    {
        if (metadata.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return Option<string>.Some(value.Trim());
        }

        return Option<string>.None();
    }

    private static string NormalizeKey(
        string key)
    {
        return key.Trim();
    }

    private readonly record struct ParsedSkillHeader(
        Dictionary<string, string> Metadata,
        string Body);
}
