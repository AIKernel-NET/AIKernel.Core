namespace AIKernel.Core.Rom;

using AIKernel.Abstractions.Rom;
using AIKernel.Common.Results;
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
        ReadMetadata("entity_type").OrElse("rom");

    public string Version =>
        ReadMetadata("version").OrElse("1");

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

    private Option<string> ReadMetadata(
        string key)
    {
        if (_candidate.AdditionalMetadata.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return Option<string>.Some(value);
        }

        return Option<string>.None();
    }
}
