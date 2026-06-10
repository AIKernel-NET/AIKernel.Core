namespace AIKernel.Core.Providers.SkillProvider;

using AIKernel.Common.Results;
using AIKernel.Dtos.Dsl;

/// <summary>
/// [EN] Describes one Skill.MD-backed capability exposed by the built-in Skill Provider.
/// [JA] 組み込み Skill Provider が公開する Skill.MD 由来 capability を表します。
/// </summary>
/// <param name="CapabilityId">[EN] Stable skill capability identifier. [JA] 安定した skill capability identifier です。</param>
/// <param name="Name">[EN] Human-readable skill name. [JA] 人が読める skill name です。</param>
/// <param name="Version">[EN] Skill capability version. [JA] skill capability version です。</param>
/// <param name="Description">[EN] Skill description text. [JA] skill description text です。</param>
/// <param name="SourcePath">[EN] Source Skill.MD path. [JA] source Skill.MD path です。</param>
/// <param name="ProvidedOperations">[EN] Operations exposed by the skill. [JA] skill が公開する operation です。</param>
/// <param name="RequiredPermissions">[EN] Permissions required to invoke the skill. [JA] skill invocation に必要な permission です。</param>
/// <param name="Metadata">[EN] Deterministic skill metadata. [JA] 決定論的な skill metadata です。</param>
public sealed record SkillCapabilityDescriptor(
    string CapabilityId,
    string Name,
    string Version,
    string Description,
    string SourcePath,
    IReadOnlyList<string> ProvidedOperations,
    IReadOnlyList<string> RequiredPermissions,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// [EN] Structured representation of an OpenAI-compatible SKILL.md document.
/// [JA] OpenAI 互換 SKILL.md ドキュメントの構造化表現です。
/// </summary>
/// <param name="Name">[EN] Parsed skill name. [JA] 解析済み skill name です。</param>
/// <param name="Description">[EN] Parsed skill description. [JA] 解析済み skill description です。</param>
/// <param name="Body">[EN] Skill instruction body. [JA] skill instruction body です。</param>
/// <param name="SourcePath">[EN] Source Skill.MD path. [JA] source Skill.MD path です。</param>
/// <param name="Metadata">[EN] Parsed frontmatter metadata. [JA] 解析済み frontmatter metadata です。</param>
/// <param name="Dsl">[EN] Deterministic DSL projection for the skill. [JA] skill の決定論的 DSL projection です。</param>
public sealed record SkillManifest(
    string Name,
    string Description,
    string Body,
    string SourcePath,
    IReadOnlyDictionary<string, string> Metadata,
    DslDocument Dsl)
{
    /// <summary>
    /// [EN] Converts the parsed manifest into the AIKernel Skill capability descriptor.
    /// [JA] 解析済み manifest を AIKernel Skill capability descriptor に変換します。
    /// </summary>
    public SkillCapabilityDescriptor ToDescriptor()
    {
        var metadata = new Dictionary<string, string>(Metadata, StringComparer.Ordinal)
        {
            ["skill.name"] = Name,
            ["skill.description"] = Description,
            ["skill.source_path"] = SourcePath,
            ["skill.dsl_node_count"] = CountDslNodes(Dsl.Root).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        var capabilityId = GetMetadataValue(metadata, "capabilityId")
            ?? GetMetadataValue(metadata, "capability_id")
            ?? $"skill.{Slug(Name)}";

        var version = GetMetadataValue(metadata, "version") ?? "1.0.0";
        var operations = SplitList(
            GetMetadataValue(metadata, "operations")
            ?? GetMetadataValue(metadata, "operation")
            ?? $"skill.{Slug(Name)}");
        var permissions = SplitList(
            GetMetadataValue(metadata, "permissions")
            ?? GetMetadataValue(metadata, "permission")
            ?? "skill.read,skill.execute");

        metadata["capabilityId"] = capabilityId;
        metadata["version"] = version;
        metadata["operations"] = string.Join(",", operations);
        metadata["permissions"] = string.Join(",", permissions);

        return new SkillCapabilityDescriptor(
            capabilityId,
            Name,
            version,
            Description,
            SourcePath,
            operations,
            permissions,
            metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }

    private static string? GetMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string key)
        => ReadMetadata(metadata, key)
            .Match<string?>(_ => null, value => value);

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

    private static IReadOnlyList<string> SplitList(
        string value)
    {
        return value
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Slug(
        string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => SelectSlugCharacter(ch).Match(_ => '-', value => value))
            .ToArray();

        var compact = string.Join(
            '-',
            new string(chars)
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return RequireSlug(compact)
            .Match(_ => "unnamed", value => value);
    }

    private static Either<string, char> SelectSlugCharacter(
        char value)
    {
        if (char.IsLetterOrDigit(value))
        {
            return Either<string, char>.FromRight(value);
        }

        return Either<string, char>.FromLeft("Character is not valid in a slug.");
    }

    private static Either<string, string> RequireSlug(
        string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Either<string, string>.FromRight(value);
        }

        return Either<string, string>.FromLeft("Slug is empty.");
    }

    private static int CountDslNodes(
        PipelineNode node)
    {
        return node switch
        {
            PipelineRootNode pipeline => 1 + pipeline.Steps.Sum(CountDslNodes),
            LoopNode loop => 1 + loop.BodyNodes.Sum(CountDslNodes),
            LoopUntilNode loopUntil => 1 + loopUntil.BodyNodes.Sum(CountDslNodes),
            _ => 1
        };
    }
}
