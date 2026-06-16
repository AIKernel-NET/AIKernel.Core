namespace AIKernel.Core.Rom;

using System.Text.Json;
using AIKernel.Abstractions.Rom;
using AIKernel.Dtos.Rom;

/// <summary>[EN] Documents this public package API member. [JA] DefaultRomCanonicalizer を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.DefaultRomCanonicalizer']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.DefaultRomCanonicalizer']/summary" />
public sealed class DefaultRomCanonicalizer : IRomCanonicalizer
{
    /// <summary>[EN] Documents this public package API member. [JA] Canonicalize を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.DefaultRomCanonicalizer.Canonicalize']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.DefaultRomCanonicalizer.Canonicalize']/summary" />
    public CanonicalizedRomDto Canonicalize(IRomDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var normalizedBody = NormalizeBody(document.Body);

        var canonicalPayload = new
        {
            entity_id = document.EntityId,
            entity_type = document.EntityType,
            version = document.Version,
            body = normalizedBody,
            relations = document.RelationReferences
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray(),
            metadata = document.Metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
        };

        var canonicalJson = JsonSerializer.Serialize(
            canonicalPayload,
            new JsonSerializerOptions
            {
                WriteIndented = false
            });

        return new CanonicalizedRomDto
        {
            CanonicalBody = canonicalJson,
            CanonicalizationVersion = "aikernel-rom-canonical-json-v1",
            Entities =
            [
                new RomEntityMetadataDto
                {
                    EntityId = document.EntityId,
                    EntityType = document.EntityType,
                    Version = document.Version,
                    AdditionalMetadata = document.Metadata
                        .OrderBy(x => x.Key, StringComparer.Ordinal)
                        .ToDictionary(
                            x => x.Key,
                            x => (object)x.Value,
                            StringComparer.Ordinal)
                }
            ],
            Relations = document.RelationReferences
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .Select(x => new ResolvedRomRelationDto
                {
                    OriginalReference = x,
                    FullyQualifiedId = x,
                    RelationType = "related"
                })
                .ToArray()
        };
    }

    /// <summary>[EN] Documents this public package API member. [JA] CanonicalizeAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.DefaultRomCanonicalizer.CanonicalizeAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.DefaultRomCanonicalizer.CanonicalizeAsync']/summary" />
    public Task<CanonicalizedRomDto> CanonicalizeAsync(
        IRomDocument document,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Canonicalize(document));
    }

    private static string NormalizeBody(string body)
    {
        return body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }
}
