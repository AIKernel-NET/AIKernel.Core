namespace AIKernel.Core.Rom;

using AIKernel.Abstractions.Rom;
using AIKernel.Dtos.Rom;

internal sealed class CandidateRomDocument : IRomDocument
{
    private readonly RomSnapshotCandidate _candidate;
    private readonly IRomCanonicalizer _canonicalizer;
    private readonly ISemanticHasher _semanticHasher;

    public CandidateRomDocument(
        RomSnapshotCandidate candidate,
        IRomCanonicalizer canonicalizer,
        ISemanticHasher semanticHasher)
    {
        _candidate = candidate;
        _canonicalizer = canonicalizer;
        _semanticHasher = semanticHasher;
    }

    public string EntityId => _candidate.RomId.Value;

    public string EntityType =>
        _candidate.AdditionalMetadata.TryGetValue("entity_type", out var entityType)
            ? entityType
            : "rom";

    public string Version =>
        _candidate.AdditionalMetadata.TryGetValue("version", out var version)
            ? version
            : "1";

    public string Body => _candidate.Body;

    public IReadOnlyDictionary<string, string> Metadata =>
        _candidate.AdditionalMetadata;

    public IReadOnlyList<string> RelationReferences =>
        _candidate.Relations
            .Select(x => x.TargetRomId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    public async Task<string> GetSemanticHashAsync()
    {
        var canonicalized = await CanonicalizeAsync().ConfigureAwait(false);
        return await _semanticHasher.ComputeHashAsync(canonicalized).ConfigureAwait(false);
    }

    public Task<CanonicalizedRomDto> CanonicalizeAsync()
    {
        return _canonicalizer.CanonicalizeAsync(this);
    }
}
