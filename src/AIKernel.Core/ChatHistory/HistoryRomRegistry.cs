namespace AIKernel.Core.ChatHistory;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using AIKernel.Common.Results;

public interface IHistoryRomRegistry
{
    Result<HistoryRomMetadata> Register(HistoryRomSnapshot snapshot);

    bool Contains(string romId);

    Result<HistoryRomSnapshot> Resolve(string romId);
}

public sealed class HistoryRomRegistry :
    IHistoryRomRegistry,
    AIKernel.Abstractions.History.IHistoryRomRegistry
{
    private readonly ConcurrentDictionary<string, HistoryRomSnapshot> _snapshots =
        new(StringComparer.Ordinal);

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

    public bool Contains(string romId)
        => HistoryRomPath.ParseRomId(romId).IsSuccess &&
           _snapshots.ContainsKey(romId);

    public Result<HistoryRomSnapshot> Resolve(string romId)
    {
        var parsed = HistoryRomPath.ParseRomId(romId);
        if (parsed.IsFailure)
        {
            return Result<HistoryRomSnapshot>.Fail(parsed.Error!);
        }

        return _snapshots.TryGetValue(romId, out var snapshot)
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

        var result = Register(new HistoryRomSnapshot(
                    HistoryRomContractMapper.ToCore(snapshot.Metadata),
                    snapshot.Markdown,
                    snapshot.Rom));

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        return Task.FromResult(HistoryRomContractMapper.ToContract(result.Value!));
    }

    Task<AIKernel.Dtos.History.HistoryRomSnapshot>
        AIKernel.Abstractions.History.IHistoryRomRegistry.ResolveAsync(
            string romId,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = Resolve(romId);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        return Task.FromResult(HistoryRomContractMapper.ToContract(result.Value!));
    }
}
