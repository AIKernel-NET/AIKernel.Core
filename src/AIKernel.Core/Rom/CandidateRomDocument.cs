namespace AIKernel.Core.Rom;

using AIKernel.Abstractions.Rom;
using AIKernel.Common.Results;
using AIKernel.Dtos.Rom;

internal sealed class CandidateRomDocument : IRomDocument
{
    private readonly RomSnapshotCandidate _candidate;
    private readonly IRomCanonicalizer _canonicalizer;
    private readonly ISemanticHasher _semanticHasher;
    /// <summary>
    /// EN: Gets CandidateRomDocument.
    /// [EN] Documents this public package API member. [JA] CandidateRomDocument を取得します。
    /// </summary>

    public CandidateRomDocument(
        RomSnapshotCandidate candidate,
        IRomCanonicalizer canonicalizer,
        ISemanticHasher semanticHasher)
    {
        _candidate = candidate;
        _canonicalizer = canonicalizer;
        _semanticHasher = semanticHasher;
    }
    /// <summary>
    /// EN: Gets EntityId.
    /// [EN] Documents this public package API member. [JA] EntityId を取得します。
    /// </summary>

    public string EntityId => _candidate.RomId.Value;
    /// <summary>
    /// EN: Gets EntityType.
    /// [EN] Documents this public package API member. [JA] EntityType を取得します。
    /// </summary>

    public string EntityType =>
        ReadMetadata("entity_type").OrElse("rom");
    /// <summary>
    /// EN: Gets Version.
    /// [EN] Documents this public package API member. [JA] Version を取得します。
    /// </summary>

    public string Version =>
        ReadMetadata("version").OrElse("1");
    /// <summary>
    /// EN: Gets Body.
    /// [EN] Documents this public package API member. [JA] Body を取得します。
    /// </summary>

    public string Body => _candidate.Body;
    /// <summary>
    /// EN: Gets Metadata.
    /// [EN] Documents this public package API member. [JA] Metadata を取得します。
    /// </summary>

    public IReadOnlyDictionary<string, string> Metadata =>
        _candidate.AdditionalMetadata;
    /// <summary>
    /// EN: Gets RelationReferences.
    /// [EN] Documents this public package API member. [JA] RelationReferences を取得します。
    /// </summary>

    public IReadOnlyList<string> RelationReferences =>
        _candidate.Relations
            .Select(x => x.TargetRomId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    /// <summary>
    /// EN: Executes GetSemanticHashAsync.
    /// [EN] Documents this public package API member. [JA] GetSemanticHashAsync を実行します。
    /// </summary>

    public async Task<string> GetSemanticHashAsync()
    {
        var canonicalized = await CanonicalizeAsync().ConfigureAwait(false);
        return await _semanticHasher.ComputeHashAsync(canonicalized).ConfigureAwait(false);
    }
    /// <summary>
    /// EN: Executes CanonicalizeAsync.
    /// [EN] Documents this public package API member. [JA] CanonicalizeAsync を実行します。
    /// </summary>

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
