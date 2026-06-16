namespace AIKernel.Core.Dsl;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using AIKernel.Common.Results;

internal interface IDslRomRegistry
{
    Result<DslRomMetadata> Register(DslRomSnapshot snapshot);

    bool Contains(string capabilityName);

    Result<DslRomSnapshot> Resolve(string capabilityName);
}

internal sealed class DslRomRegistry :
    IDslRomRegistry,
    AIKernel.Abstractions.Dsl.IDslRomRegistry
{
    private readonly ConcurrentDictionary<string, DslRomSnapshot> _snapshots =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AIKernel.Dtos.Dsl.DslRomSnapshot>
        _contractSnapshots = new(StringComparer.Ordinal);
    /// <summary>
    /// EN: Executes Register.
    /// [EN] Documents this public package API member. [JA] Register を実行します。
    /// </summary>

    public Result<DslRomMetadata> Register(DslRomSnapshot snapshot)
    {
        return ValidateSnapshotForRegister(snapshot)
            .Bind(valid =>
            {
                var existing = _snapshots.GetOrAdd(
                    valid.Metadata.CapabilityName,
                    valid);

                if (!string.Equals(
                        existing.Metadata.RomHash,
                        valid.Metadata.RomHash,
                        StringComparison.Ordinal))
                {
                    return Result<DslRomMetadata>.Fail(Error(
                        "DSL ROM is immutable; registering different content for the same capability is not allowed."));
                }

                return Result<DslRomMetadata>.Success(existing.Metadata);
            });
    }

    private static Result<DslRomSnapshot> ValidateSnapshotForRegister(
        DslRomSnapshot? snapshot)
    {
        return
            from validSnapshot in RequireSnapshot(snapshot)
            from metadata in RequireMetadata(validSnapshot)
            from _ in ValidateMetadata(metadata)
            from __ in RequireJsonDsl(validSnapshot)
            from ___ in RequirePipeline(validSnapshot)
            from ____ in ValidateHash(validSnapshot)
            select validSnapshot;
    }

    private static Result<DslRomSnapshot> RequireSnapshot(
        DslRomSnapshot? snapshot)
        => snapshot is null
            ? Result<DslRomSnapshot>.Fail(Error("DSL ROM snapshot is required."))
            : Result<DslRomSnapshot>.Success(snapshot);

    private static Result<DslRomMetadata> RequireMetadata(
        DslRomSnapshot snapshot)
        => snapshot.Metadata is null
            ? Result<DslRomMetadata>.Fail(Error("DSL ROM metadata is required."))
            : Result<DslRomMetadata>.Success(snapshot.Metadata);

    private static Result<bool> ValidateMetadata(
        DslRomMetadata metadata)
    {
        return
            from _ in RequireMetadataValue(
                metadata.CapabilityName,
                "DSL ROM capability name is required.")
            from __ in RequireMetadataValue(
                metadata.RomHash,
                "DSL ROM hash is required.")
            from ___ in RequireMetadataValue(
                metadata.Namespace,
                "DSL ROM namespace is required.")
            from ____ in RequireMetadataValue(
                metadata.Name,
                "DSL ROM name is required.")
            from _____ in RequireMetadataValue(
                metadata.Path,
                "DSL ROM path is required.")
            from ______ in ValidateCanonicalIdentity(metadata)
            select true;
    }

    private static Result<bool> RequireMetadataValue(
        string value,
        string message)
        => RequireNonEmpty(value, message)
            .Map(_ => true)
            .ToRomResult();

    private static Result<bool> ValidateCanonicalIdentity(
        DslRomMetadata metadata)
    {
        var metadataError = DslRomMetadataValidator.ValidateCanonicalIdentity(metadata);
        return metadataError is null
            ? Result<bool>.Success(true)
            : Result<bool>.Fail(metadataError);
    }

    private static Result<bool> RequireJsonDsl(
        DslRomSnapshot snapshot)
        => RequireNonEmpty(snapshot.JsonDsl, "DSL ROM JSON is required.")
            .Map(_ => true)
            .ToRomResult();

    private static Result<bool> RequirePipeline(
        DslRomSnapshot snapshot)
        => snapshot.Pipeline is null
            ? Result<bool>.Fail(Error("DSL ROM pipeline is required."))
            : Result<bool>.Success(true);

    private static Result<bool> ValidateHash(
        DslRomSnapshot snapshot)
    {
        var actualHash = DslRomHasher.ComputeHash(snapshot.JsonDsl);
        return string.Equals(
            actualHash,
            snapshot.Metadata.RomHash,
            StringComparison.Ordinal)
            ? Result<bool>.Success(true)
            : Result<bool>.Fail(Error("DSL ROM hash does not match JSON content."));
    }
    /// <summary>
    /// EN: Executes Contains.
    /// [EN] Documents this public package API member. [JA] Contains を実行します。
    /// </summary>

    public bool Contains(string capabilityName)
        => DslRomPath.ParseCapabilityName(capabilityName)
            .Match(
                _ => false,
                _ => FindSnapshotOption(capabilityName).Match(
                    () => false,
                    __ => true));
    /// <summary>
    /// EN: Executes Resolve.
    /// [EN] Documents this public package API member. [JA] Resolve を実行します。
    /// </summary>

    public Result<DslRomSnapshot> Resolve(string capabilityName)
    {
        return
            from _ in DslRomPath.ParseCapabilityName(capabilityName)
            from snapshot in FindSnapshot(capabilityName)
            select snapshot;
    }

    private Result<DslRomSnapshot> FindSnapshot(
        string capabilityName)
        => FindSnapshotOption(capabilityName)
            .Match(
                () => Result<DslRomSnapshot>.Fail(new ErrorContext(
                $"DSL ROM capability was not registered: {capabilityName}.",
                "DSL_ROM_NOT_FOUND",
                false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.Capability,
                SemanticSlot = SemanticSlot.T,
                Metadata = ImmutableDictionary<string, string>.Empty
                    .Add("dsl.capability_name", capabilityName)
            }),
                Result<DslRomSnapshot>.Success);

    private Option<DslRomSnapshot> FindSnapshotOption(
        string capabilityName)
    {
        if (_snapshots.TryGetValue(capabilityName, out var snapshot))
        {
            return Option<DslRomSnapshot>.Some(snapshot);
        }

        return Option<DslRomSnapshot>.None();
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
        => Contains(capabilityName) ||
           FindContractSnapshotOption(capabilityName).Match(
               () => false,
               _ => true);

    async Task<AIKernel.Dtos.Dsl.DslRomSnapshot>
        AIKernel.Abstractions.Dsl.IDslRomRegistry.ResolveAsync(
            string capabilityName,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var core = Resolve(capabilityName);
        return await Task.FromResult(core.Match(
                error => FindContractSnapshotOption(capabilityName).Match(
                    () => throw new InvalidOperationException(error.Message),
                    value => value),
                DslContractMapper.ToContract))
            .ConfigureAwait(false);
    }

    private Option<AIKernel.Dtos.Dsl.DslRomSnapshot> FindContractSnapshotOption(
        string capabilityName)
    {
        if (_contractSnapshots.TryGetValue(capabilityName, out var snapshot))
        {
            return Option<AIKernel.Dtos.Dsl.DslRomSnapshot>.Some(snapshot);
        }

        return Option<AIKernel.Dtos.Dsl.DslRomSnapshot>.None();
    }

    private static Either<string, string> RequireNonEmpty(
        string value,
        string message)
        => string.IsNullOrWhiteSpace(value)
            ? Either<string, string>.FromLeft(message)
            : Either<string, string>.FromRight(value);

    private static ErrorContext Error(string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };

}

internal static class DslRomRegistryEitherExtensions
{
    /// <summary>
    /// EN: Gets ToRomResult&lt;T&gt;.
    /// [EN] Documents this public package API member. [JA] ToRomResult&lt;T&gt; を取得します。
    /// </summary>
    public static Result<T> ToRomResult<T>(
        this Either<string, T> value)
        => value.Match(
            left => Result<T>.Fail(new ErrorContext(left, "DSL_ROM_ERROR", false)
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.G
            }),
            Result<T>.Success);
}
