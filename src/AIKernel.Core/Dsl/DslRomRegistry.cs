namespace AIKernel.Core.Dsl;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using AIKernel.Common.Results;

public interface IDslRomRegistry
{
    Result<DslRomMetadata> Register(DslRomSnapshot snapshot);

    bool Contains(string capabilityName);

    Result<DslRomSnapshot> Resolve(string capabilityName);
}

public sealed class DslRomRegistry :
    IDslRomRegistry,
    AIKernel.Abstractions.Dsl.IDslRomRegistry
{
    private readonly ConcurrentDictionary<string, DslRomSnapshot> _snapshots =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AIKernel.Dtos.Dsl.DslRomSnapshot>
        _contractSnapshots = new(StringComparer.Ordinal);

    public Result<DslRomMetadata> Register(DslRomSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM snapshot is required."));
        }

        if (snapshot.Metadata is null)
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM metadata is required."));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.CapabilityName))
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM capability name is required."));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.RomHash))
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM hash is required."));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.Namespace))
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM namespace is required."));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.Name))
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM name is required."));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.Path))
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM path is required."));
        }

        var metadataError = DslRomMetadataValidator.ValidateCanonicalIdentity(
            snapshot.Metadata);
        if (metadataError is not null)
        {
            return Result<DslRomMetadata>.Fail(metadataError);
        }

        if (string.IsNullOrWhiteSpace(snapshot.JsonDsl))
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM JSON is required."));
        }

        if (snapshot.Pipeline is null)
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM pipeline is required."));
        }

        var actualHash = DslRomHasher.ComputeHash(snapshot.JsonDsl);
        if (!string.Equals(
                actualHash,
                snapshot.Metadata.RomHash,
                StringComparison.Ordinal))
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM hash does not match JSON content."));
        }

        var existing = _snapshots.GetOrAdd(
            snapshot.Metadata.CapabilityName,
            snapshot);

        if (!string.Equals(
                existing.Metadata.RomHash,
                snapshot.Metadata.RomHash,
                StringComparison.Ordinal))
        {
            return Result<DslRomMetadata>.Fail(Error(
                "DSL ROM is immutable; registering different content for the same capability is not allowed."));
        }

        return Result<DslRomMetadata>.Success(existing.Metadata);
    }

    public bool Contains(string capabilityName)
        => DslRomPath.ParseCapabilityName(capabilityName).IsSuccess &&
           _snapshots.ContainsKey(capabilityName);

    public Result<DslRomSnapshot> Resolve(string capabilityName)
    {
        var parsed = DslRomPath.ParseCapabilityName(capabilityName);
        if (parsed.IsFailure)
        {
            return Result<DslRomSnapshot>.Fail(parsed.Error!);
        }

        return _snapshots.TryGetValue(capabilityName, out var snapshot)
            ? Result<DslRomSnapshot>.Success(snapshot)
            : Result<DslRomSnapshot>.Fail(new ErrorContext(
                $"DSL ROM capability was not registered: {capabilityName}.",
                "DSL_ROM_NOT_FOUND",
                false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.Capability,
                SemanticSlot = SemanticSlot.T,
                Metadata = ImmutableDictionary<string, string>.Empty
                    .Add("dsl.capability_name", capabilityName)
            });
    }

    async Task<AIKernel.Dtos.Dsl.DslRomMetadata>
        AIKernel.Abstractions.Dsl.IDslRomRegistry.RegisterAsync(
            AIKernel.Dtos.Dsl.DslRomSnapshot snapshot,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var metadata = DslContractMapper.ToCore(snapshot.Metadata);
        var metadataError = DslRomMetadataValidator.ValidateCanonicalIdentity(metadata);
        if (metadataError is not null)
        {
            throw new InvalidOperationException(metadataError.Message);
        }

        if (string.IsNullOrWhiteSpace(snapshot.JsonDsl))
        {
            throw new InvalidOperationException("DSL ROM JSON is required.");
        }

        var actualHash = DslRomHasher.ComputeHash(snapshot.JsonDsl);
        if (!string.Equals(actualHash, metadata.RomHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "DSL ROM hash does not match JSON content.");
        }

        var existing = _contractSnapshots.GetOrAdd(
            metadata.CapabilityName,
            snapshot);

        if (!string.Equals(
                existing.Metadata.RomHash,
                snapshot.Metadata.RomHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "DSL ROM is immutable; registering different content for the same capability is not allowed.");
        }

        return await Task.FromResult(existing.Metadata).ConfigureAwait(false);
    }

    bool AIKernel.Abstractions.Dsl.IDslRomRegistry.Contains(
        string capabilityName)
        => Contains(capabilityName) || _contractSnapshots.ContainsKey(capabilityName);

    async Task<AIKernel.Dtos.Dsl.DslRomSnapshot>
        AIKernel.Abstractions.Dsl.IDslRomRegistry.ResolveAsync(
            string capabilityName,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var core = Resolve(capabilityName);
        if (core.IsSuccess)
        {
            return await Task.FromResult(DslContractMapper.ToContract(core.Value!))
                .ConfigureAwait(false);
        }

        if (_contractSnapshots.TryGetValue(capabilityName, out var snapshot))
        {
            return await Task.FromResult(snapshot).ConfigureAwait(false);
        }

        throw new InvalidOperationException(core.Error!.Message);
    }

    private static ErrorContext Error(string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };

}
