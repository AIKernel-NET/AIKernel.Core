namespace AIKernel.Core.Rom;

using System.Collections.Immutable;
using AIKernel.Abstractions.Rom;
using AIKernel.Dtos.Rom;
using YamlDotNet.Serialization;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.MarkdownFrontMatterParser']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.MarkdownFrontMatterParser']" />
public sealed class MarkdownFrontMatterParser : IMarkdownFrontMatterParser
{
    private readonly IDeserializer _deserializer;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.#ctor']" />
    public MarkdownFrontMatterParser()
        : this(new DeserializerBuilder().Build())
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.#ctor']" />
    public MarkdownFrontMatterParser(IDeserializer deserializer)
    {
        _deserializer = deserializer
            ?? throw new ArgumentNullException(nameof(deserializer));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.Parse']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.MarkdownFrontMatterParser.Parse']" />
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

        IReadOnlyDictionary<string, object?> frontMatter;

        try
        {
            var raw = _deserializer.Deserialize<Dictionary<object, object?>>(yaml)
                      ?? new Dictionary<object, object?>();

            frontMatter = NormalizeYamlMap(raw);
        }
        catch (Exception ex)
        {
            throw new RomLoadException(
                $"YAML front matter could not be parsed. SourcePath='{sourcePath}'.",
                ex);
        }

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