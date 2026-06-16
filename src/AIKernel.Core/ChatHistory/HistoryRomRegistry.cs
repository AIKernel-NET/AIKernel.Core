namespace AIKernel.Core.ChatHistory;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using AIKernel.Common.Results;

internal interface IHistoryRomRegistry
{
    Result<HistoryRomMetadata> Register(HistoryRomSnapshot snapshot);

    bool Contains(string romId);

    Result<HistoryRomSnapshot> Resolve(string romId);
}

internal sealed class HistoryRomRegistry :
    IHistoryRomRegistry,
    AIKernel.Abstractions.History.IHistoryRomRegistry
{
    private readonly ConcurrentDictionary<string, HistoryRomSnapshot> _snapshots =
        new(StringComparer.Ordinal);
    /// <summary>
    /// EN: Executes Register.
    /// EN: Documentation for public API. JA: Register を実行します。
    /// </summary>

    public Result<HistoryRomMetadata> Register(HistoryRomSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM snapshot is required."));
        }

        if (snapshot.Metadata is null)
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM metadata is required."));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.RomId))
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM id is required."));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.RomHash))
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM hash is required."));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.Namespace))
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM namespace is required."));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.Name))
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM name is required."));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.Path))
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM path is required."));
        }

        var metadataError = HistoryRomMetadataValidator.ValidateCanonicalIdentity(
            snapshot.Metadata);
        if (metadataError is not null)
        {
            return Result<HistoryRomMetadata>.Fail(metadataError);
        }

        if (string.IsNullOrWhiteSpace(snapshot.Markdown))
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM markdown is required."));
        }

        if (snapshot.Rom is null)
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM loaded snapshot is required."));
        }

        if (!string.Equals(
                snapshot.Rom.RomId.Value,
                snapshot.Metadata.RomId,
                StringComparison.Ordinal))
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM metadata id does not match loaded ROM."));
        }

        if (snapshot.Rom.Signature is null || !snapshot.Rom.Signature.IsVerified)
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM signature is not verified."));
        }

        if (!string.Equals(
                snapshot.Rom.Signature.ActualHash,
                snapshot.Metadata.RomHash,
                StringComparison.Ordinal))
        {
            return Result<HistoryRomMetadata>.Fail(
                HistoryRomErrors.Error("History ROM hash does not match verified ROM content."));
        }

        var existing = _snapshots.GetOrAdd(
            snapshot.Metadata.RomId,
            snapshot);

        if (!string.Equals(
                existing.Metadata.RomHash,
                snapshot.Metadata.RomHash,
                StringComparison.Ordinal))
        {
            return Result<HistoryRomMetadata>.Fail(HistoryRomErrors.Error(
                "History ROM is immutable; registering different content for the same id is not allowed."));
        }

        return Result<HistoryRomMetadata>.Success(existing.Metadata);
    }
    /// <summary>
    /// EN: Executes Contains.
    /// EN: Documentation for public API. JA: Contains を実行します。
    /// </summary>

    public bool Contains(string romId)
        => HistoryRomPath.ParseRomId(romId)
            .Match(
                _ => false,
                _ => _snapshots.ContainsKey(romId));
    /// <summary>
    /// EN: Executes Resolve.
    /// EN: Documentation for public API. JA: Resolve を実行します。
    /// </summary>

    public Result<HistoryRomSnapshot> Resolve(string romId)
        => from _ in HistoryRomPath.ParseRomId(romId)
           from snapshot in ResolveSnapshot(romId)
           select snapshot;

    Task<AIKernel.Dtos.History.HistoryRomMetadata>
        AIKernel.Abstractions.History.IHistoryRomRegistry.RegisterAsync(
            AIKernel.Dtos.History.HistoryRomSnapshot snapshot,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        return Task.FromResult(Register(new HistoryRomSnapshot(
                HistoryRomContractMapper.ToCore(snapshot.Metadata),
                snapshot.Markdown,
                snapshot.Rom))
            .Match(
                error => throw new InvalidOperationException(error.Message),
                HistoryRomContractMapper.ToContract));
    }

    Task<AIKernel.Dtos.History.HistoryRomSnapshot>
        AIKernel.Abstractions.History.IHistoryRomRegistry.ResolveAsync(
            string romId,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Resolve(romId)
            .Match(
                error => throw new InvalidOperationException(error.Message),
                HistoryRomContractMapper.ToContract));
    }

    private Result<HistoryRomSnapshot> ResolveSnapshot(string romId)
        => _snapshots.TryGetValue(romId, out var snapshot)
            ? Result<HistoryRomSnapshot>.Success(snapshot)
            : Result<HistoryRomSnapshot>.Fail(new ErrorContext(
                $"History ROM was not registered: {romId}.",
                "HISTORY_ROM_NOT_FOUND",
                false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.C,
                Metadata = ImmutableDictionary<string, string>.Empty
                    .Add(HistoryRomMetadataKeys.RomId, romId)
            });
}
