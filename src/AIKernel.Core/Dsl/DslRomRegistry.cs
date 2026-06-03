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

public sealed class DslRomRegistry : IDslRomRegistry
{
    private readonly ConcurrentDictionary<string, DslRomSnapshot> _snapshots =
        new(StringComparer.Ordinal);

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

        var metadataError = ValidateMetadataIdentity(snapshot.Metadata);
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

    private static ErrorContext Error(string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };

    private static ErrorContext? ValidateMetadataIdentity(DslRomMetadata metadata)
    {
        var parsed = DslRomPath.ParseCapabilityName(metadata.CapabilityName);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        var expectedCapabilityName = DslRomPath.CreateCapabilityName(
            metadata.Namespace,
            metadata.Name);
        if (expectedCapabilityName.IsFailure)
        {
            return expectedCapabilityName.Error!;
        }

        if (!string.Equals(
                expectedCapabilityName.Value,
                metadata.CapabilityName,
                StringComparison.Ordinal))
        {
            return Error("DSL ROM capability name must match dsl://{namespace}/{name}.");
        }

        var expectedPath = DslRomPath.Create(metadata.Namespace, metadata.Name);
        if (expectedPath.IsFailure)
        {
            return expectedPath.Error!;
        }

        return string.Equals(
            expectedPath.Value,
            metadata.Path,
            StringComparison.Ordinal)
            ? null
            : Error("DSL ROM path must match rom/dsl/{namespace}/{name}.json.");
    }
}
