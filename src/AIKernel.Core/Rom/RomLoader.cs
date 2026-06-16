namespace AIKernel.Core.Rom;

using AIKernel.Abstractions.Rom;
using AIKernel.Common.Results;
using AIKernel.Core.Time;
using AIKernel.Dtos.Rom;
using AIKernel.Vfs;
using System.Collections.Immutable;
using System.Text;

/// <summary>[EN] Documents this public package API member. [JA] RomLoader を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomLoader']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomLoader']/summary" />
public sealed class RomLoader : IRomLoader
{
    private readonly IMarkdownFrontMatterParser _frontMatterParser;
    private readonly IRomSignatureVerifier _signatureVerifier;
    private readonly IKernelClock _clock;

    /// <summary>[EN] Documents this public package API member. [JA] RomLoader を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoader.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoader.#ctor']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] LoadAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoader.LoadAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomLoader.LoadAsync']/summary" />
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
        => RequireString(frontMatter, key)
            .Match(
                _ => throw new RomRequiredMetadataMissingException(key, sourcePath),
                value => value);

    private static string GetRequiredNestedString(
        IReadOnlyDictionary<string, object?> frontMatter,
        string section,
        string key,
        string sourcePath)
        => ReadMap(frontMatter, section)
            .Match(
                () => throw new RomRequiredMetadataMissingException($"{section}.{key}", sourcePath),
                map => GetRequiredString(map, key, sourcePath));

    private static ImmutableArray<string> ExtractSecurityTags(
        IReadOnlyDictionary<string, object?> frontMatter)
        => ReadMap(frontMatter, "security")
            .Bind(security => ReadObject(security, "tags"))
            .Match(
                () => ImmutableArray<string>.Empty,
                tags => ToStringArray(tags)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToImmutableArray());

    private static ImmutableArray<RomRelationSnapshot> ExtractRelations(
        IReadOnlyDictionary<string, object?> frontMatter)
        => ReadItems(frontMatter, "relations")
            .Match(
                () => ImmutableArray<RomRelationSnapshot>.Empty,
                relations => relations
                    .OfType<IReadOnlyDictionary<string, object?>>()
                    .Select(ReadRelation)
                    .SelectMany(relation => relation.Match(
                        Enumerable.Empty<RomRelationSnapshot>,
                        value => [value]))
                    .Distinct()
                    .OrderBy(x => x.TargetRomId, StringComparer.Ordinal)
                    .ThenBy(x => x.Kind, StringComparer.Ordinal)
                    .ToImmutableArray());

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

    private static Either<string, string> RequireString(
        IReadOnlyDictionary<string, object?> source,
        string key)
        => ReadOptionalString(source, key)
            .Match(
                () => Either<string, string>.FromLeft($"Metadata field '{key}' is missing."),
                Either<string, string>.FromRight);

    private static Option<RomRelationSnapshot> ReadRelation(
        IReadOnlyDictionary<string, object?> map)
        => ReadOptionalString(map, "target")
            .Map(target => new RomRelationSnapshot(
                TargetRomId: target,
                Kind: ReadOptionalString(map, "kind").OrElse("related")));

    private static Option<string> ReadOptionalString(
        IReadOnlyDictionary<string, object?> source,
        string key)
        => ReadObject(source, key)
            .Map(value => Convert.ToString(
                value,
                System.Globalization.CultureInfo.InvariantCulture))
            .Bind(ReadNonEmpty);

    private static Option<object> ReadObject(
        IReadOnlyDictionary<string, object?> source,
        string key)
    {
        if (source.TryGetValue(key, out var value) &&
            value is not null)
        {
            return Option<object>.Some(value);
        }

        return Option<object>.None();
    }

    private static Option<IReadOnlyDictionary<string, object?>> ReadMap(
        IReadOnlyDictionary<string, object?> source,
        string key)
    {
        if (source.TryGetValue(key, out var value) &&
            value is IReadOnlyDictionary<string, object?> map)
        {
            return Option<IReadOnlyDictionary<string, object?>>.Some(map);
        }

        return Option<IReadOnlyDictionary<string, object?>>.None();
    }

    private static Option<IEnumerable<object?>> ReadItems(
        IReadOnlyDictionary<string, object?> source,
        string key)
    {
        if (source.TryGetValue(key, out var value) &&
            value is IEnumerable<object?> items)
        {
            return Option<IEnumerable<object?>>.Some(items);
        }

        return Option<IEnumerable<object?>>.None();
    }

    private static Option<string> ReadNonEmpty(
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Option<string>.Some(value.Trim());
        }

        return Option<string>.None();
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
