namespace AIKernel.Core.Rom;

using AIKernel.Abstractions.Rom;
using AIKernel.Core.Time;
using AIKernel.Dtos.Rom;
using AIKernel.Vfs;
using System.Collections.Immutable;
using System.Text;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomLoader']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomLoader']" />
public sealed class RomLoader : IRomLoader
{
    private readonly IMarkdownFrontMatterParser _frontMatterParser;
    private readonly IRomSignatureVerifier _signatureVerifier;
    private readonly IKernelClock _clock;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoader.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoader.#ctor']" />
    public RomLoader(
        IMarkdownFrontMatterParser frontMatterParser,
        IRomSignatureVerifier signatureVerifier,
        IKernelClock? clock = null)
    {
        _frontMatterParser = frontMatterParser
            ?? throw new ArgumentNullException(nameof(frontMatterParser));

        _signatureVerifier = signatureVerifier
            ?? throw new ArgumentNullException(nameof(signatureVerifier));

        _clock = clock ?? KernelClock.System();
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoader.LoadAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoader.LoadAsync']" />
    public async Task<RomSnapshot> LoadAsync(
        IVfsSession session,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("ROM path is required.", nameof(path));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Side effect boundary:
        // VFS read is the only physical I/O performed by RomLoader.
        var file = await session.ReadFileAsync(path).ConfigureAwait(false);
        var bytes = await file.ReadAsync().ConfigureAwait(false);
        var markdown = Encoding.UTF8.GetString(bytes);

        // Pure boundary:
        // From here, parsing and materialization are deterministic for the same input.
        var parsed = _frontMatterParser.Parse(markdown, path);

        var candidate = CreateCandidate(parsed);

        // Verification boundary:
        // Fail-closed. No RomSnapshot is returned unless signature is valid.
        var verification = await _signatureVerifier
            .VerifyAsync(candidate, cancellationToken)
            .ConfigureAwait(false);

        if (!verification.IsVerified)
        {
            throw new RomSignatureVerificationException(
                candidate.SourcePath,
                verification.ExpectedHash,
                verification.ActualHash);
        }

        return new RomSnapshot
        {
            RomId = candidate.RomId,
            SourcePath = candidate.SourcePath,
            Body = candidate.Body,
            SecurityTags = candidate.SecurityTags,
            Relations = candidate.Relations,
            AdditionalMetadata = candidate.AdditionalMetadata,
            LoadedAtUtc = _clock.Now,
            Signature = new RomSignatureSnapshot(
                verification.Algorithm,
                verification.ExpectedHash,
                verification.ActualHash,
                IsVerified: true)
        };
    }

    private static RomSnapshotCandidate CreateCandidate(
        MarkdownFrontMatterDocument document)
    {
        var frontMatter = document.FrontMatter;

        var romId = GetRequiredString(frontMatter, "rom_id", document.SourcePath);
        var expectedHash = GetRequiredNestedString(
            frontMatter,
            "signature",
            "hash",
            document.SourcePath);

        return new RomSnapshotCandidate
        {
            RomId = RomIdFactory.Create(romId, "rom_id"),
            SourcePath = document.SourcePath,
            Body = document.Body,
            SecurityTags = ExtractSecurityTags(frontMatter),
            Relations = ExtractRelations(frontMatter),
            ExpectedHash = expectedHash,
            AdditionalMetadata = ExtractAdditionalMetadata(frontMatter)
        };
    }

    private static string GetRequiredString(
        IReadOnlyDictionary<string, object?> frontMatter,
        string key,
        string sourcePath)
    {
        if (!frontMatter.TryGetValue(key, out var value) || value is null)
        {
            throw new RomRequiredMetadataMissingException(key, sourcePath);
        }

        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new RomRequiredMetadataMissingException(key, sourcePath);
        }

        return text.Trim();
    }

    private static string GetRequiredNestedString(
        IReadOnlyDictionary<string, object?> frontMatter,
        string section,
        string key,
        string sourcePath)
    {
        if (!frontMatter.TryGetValue(section, out var sectionValue)
            || sectionValue is not IReadOnlyDictionary<string, object?> map)
        {
            throw new RomRequiredMetadataMissingException($"{section}.{key}", sourcePath);
        }

        return GetRequiredString(map, key, sourcePath);
    }

    private static ImmutableArray<string> ExtractSecurityTags(
        IReadOnlyDictionary<string, object?> frontMatter)
    {
        if (!frontMatter.TryGetValue("security", out var securityValue)
            || securityValue is not IReadOnlyDictionary<string, object?> security)
        {
            return ImmutableArray<string>.Empty;
        }

        if (!security.TryGetValue("tags", out var tagsValue))
        {
            return ImmutableArray<string>.Empty;
        }

        return ToStringArray(tagsValue)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static ImmutableArray<RomRelationSnapshot> ExtractRelations(
        IReadOnlyDictionary<string, object?> frontMatter)
    {
        if (!frontMatter.TryGetValue("relations", out var relationsValue)
            || relationsValue is not IEnumerable<object?> relationItems)
        {
            return ImmutableArray<RomRelationSnapshot>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<RomRelationSnapshot>();

        foreach (var item in relationItems)
        {
            if (item is not IReadOnlyDictionary<string, object?> map)
            {
                continue;
            }

            if (!map.TryGetValue("target", out var targetValue))
            {
                continue;
            }

            var target = Convert.ToString(
                targetValue,
                System.Globalization.CultureInfo.InvariantCulture);

            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            var kind = map.TryGetValue("kind", out var kindValue)
                ? Convert.ToString(kindValue, System.Globalization.CultureInfo.InvariantCulture)
                : "related";

            builder.Add(new RomRelationSnapshot(
                TargetRomId: target.Trim(),
                Kind: string.IsNullOrWhiteSpace(kind) ? "related" : kind.Trim()));
        }

        return builder
            .Distinct()
            .OrderBy(x => x.TargetRomId, StringComparer.Ordinal)
            .ThenBy(x => x.Kind, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static ImmutableDictionary<string, string> ExtractAdditionalMetadata(
        IReadOnlyDictionary<string, object?> frontMatter)
    {
        var canonicalKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "rom_id",
            "security",
            "relations",
            "signature"
        };

        return frontMatter
            .Where(x => !canonicalKeys.Contains(x.Key))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToImmutableDictionary(
                x => x.Key,
                x => Convert.ToString(
                    x.Value,
                    System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static IEnumerable<string> ToStringArray(object? value)
    {
        if (value is null)
        {
            yield break;
        }

        if (value is string single)
        {
            yield return single;
            yield break;
        }

        if (value is IEnumerable<object?> items)
        {
            foreach (var item in items)
            {
                var text = Convert.ToString(
                    item,
                    System.Globalization.CultureInfo.InvariantCulture);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }
            }
        }
    }
}
