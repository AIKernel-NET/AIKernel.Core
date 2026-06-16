namespace AIKernel.Core.Rom;

using System.Collections.Immutable;
using AIKernel.Abstractions.Rom;
using AIKernel.Common.Results;
using AIKernel.Dtos.Rom;
using YamlDotNet.Serialization;

/// <summary>EN: Documentation for public API. JA: MarkdownFrontMatterParser を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.MarkdownFrontMatterParser']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.MarkdownFrontMatterParser']/summary" />
public sealed class MarkdownFrontMatterParser : IMarkdownFrontMatterParser
{
    private readonly IDeserializer _deserializer;

    /// <summary>EN: Documentation for public API. JA: MarkdownFrontMatterParser を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.#ctor']/summary" />
    public MarkdownFrontMatterParser()
        : this(new DeserializerBuilder().Build())
    {
    }

    /// <summary>EN: Documentation for public API. JA: MarkdownFrontMatterParser を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.#ctor']/summary" />
    public MarkdownFrontMatterParser(IDeserializer deserializer)
    {
        _deserializer = deserializer
            ?? throw new ArgumentNullException(nameof(deserializer));
    }

    /// <summary>EN: Documentation for public API. JA: Parse を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.Parse']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.Parse']/summary" />
    public MarkdownFrontMatterDocument Parse(
        string markdown,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        if (!markdown.StartsWith("---", StringComparison.Ordinal))
        {
            throw new RomLoadException(
                $"YAML front matter is required. SourcePath='{sourcePath}'.");
        }

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        var secondDelimiter = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);

        if (secondDelimiter < 0)
        {
            throw new RomLoadException(
                $"YAML front matter closing delimiter was not found. SourcePath='{sourcePath}'.");
        }

        var yaml = normalized[4..secondDelimiter];
        var body = normalized[(secondDelimiter + "\n---\n".Length)..];

        var frontMatter = Try
            .Run(() =>
            {
                var raw = _deserializer.Deserialize<Dictionary<object, object?>>(yaml)
                          ?? new Dictionary<object, object?>();

                return NormalizeYamlMap(raw);
            })
            .Match(
                error => throw new RomLoadException(
                    $"YAML front matter could not be parsed. SourcePath='{sourcePath}'.",
                    new InvalidOperationException(error.Message)),
                value => value);

        return new MarkdownFrontMatterDocument(
            SourcePath: sourcePath,
            Body: body,
            FrontMatter: frontMatter.ToImmutableDictionary(StringComparer.Ordinal));
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

            IDictionary<object, object?> map
                => NormalizeYamlMap(map),

            IEnumerable<object?> list when value is not string
                => list.Select(NormalizeYamlValue).ToArray(),

            _ => value
        };
    }
}
