namespace AIKernel.Core.Rom;

using System.Text.Json;
using AIKernel.Abstractions.Rom;
using AIKernel.Dtos.Rom;

public sealed class DefaultRomCanonicalizer : IRomCanonicalizer
{
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
