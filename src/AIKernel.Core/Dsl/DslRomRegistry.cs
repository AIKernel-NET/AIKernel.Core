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

        if (!DslRomPath.IsDslCapability(snapshot.Metadata.CapabilityName))
        {
            return Result<DslRomMetadata>.Fail(Error("DSL ROM capability name must use the dsl:// scheme."));
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
        => DslRomPath.IsDslCapability(capabilityName) &&
           _snapshots.ContainsKey(capabilityName);

    public Result<DslRomSnapshot> Resolve(string capabilityName)
    {
        if (!DslRomPath.IsDslCapability(capabilityName))
        {
            return Result<DslRomSnapshot>.Fail(Error("Capability is not a DSL ROM capability."));
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
}
