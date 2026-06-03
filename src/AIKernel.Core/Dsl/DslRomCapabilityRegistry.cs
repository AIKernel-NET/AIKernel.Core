namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using System.Globalization;
using AIKernel.Common.Results;

public sealed class DslRomCapabilityRegistry : IDslCapabilityRegistry
{
    private readonly IDslCapabilityRegistry _inner;
    private readonly IDslRomRegistry _romRegistry;

    public DslRomCapabilityRegistry(
        IDslCapabilityRegistry inner,
        IDslRomRegistry romRegistry)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _romRegistry = romRegistry ?? throw new ArgumentNullException(nameof(romRegistry));
    }

    public bool Contains(string name)
        => DslRomPath.IsDslCapability(name)
            ? _romRegistry.Contains(name)
            : _inner.Contains(name);

    public Result<DslPipelineValue> Invoke(
        string name,
        DslPipelineValue input,
        IReadOnlyDictionary<string, string> args)
    {
        if (!DslRomPath.IsDslCapability(name))
        {
            try
            {
                return _inner.Invoke(name, input, args);
            }
            catch (Exception ex)
            {
                return Result<DslPipelineValue>.Fail(CapabilityException(name, ex));
            }
        }

        Result<DslRomSnapshot> snapshot;
        try
        {
            snapshot = _romRegistry.Resolve(name);
        }
        catch (Exception ex)
        {
            return Result<DslPipelineValue>.Fail(CapabilityException(name, ex));
        }

        if (snapshot.IsFailure)
        {
            return Result<DslPipelineValue>.Fail(snapshot.Error!);
        }

        var validated = ValidateSnapshot(name, snapshot.Value);
        if (validated.IsFailure)
        {
            return Result<DslPipelineValue>.Fail(validated.Error!);
        }

        ResultStep<DslPipelineState, DslPipelineValue> result;
        try
        {
            result = validated.Value!.Pipeline.Execute(
                DslPipelineExecutionContext.Create(input));
        }
        catch (Exception ex)
        {
            return Result<DslPipelineValue>.Fail(AttachRomMetadata(
                ErrorContext.FromException(ex) with
                {
                    FailureKind = FailureKind.FailClosed,
                    OriginStep = OriginStep.Capability,
                    SemanticSlot = SemanticSlot.T
                },
                validated.Value!.Metadata));
        }

        if (result.IsFailure)
        {
            return Result<DslPipelineValue>.Fail(AttachRomMetadata(
                result.Error!,
                validated.Value.Metadata,
                result.ReplayLog.Count,
                result.ReplayLogHash));
        }

        if (result.Value is null)
        {
            return Result<DslPipelineValue>.Fail(AttachRomMetadata(
                Error("DSL ROM pipeline returned a successful null value."),
                validated.Value.Metadata,
                result.ReplayLog.Count,
                result.ReplayLogHash));
        }

        return Result<DslPipelineValue>.Success(AttachRomData(
            result.Value,
            validated.Value.Metadata,
            result.ReplayLog.Count,
            result.ReplayLogHash));
    }

    private static Result<DslRomSnapshot> ValidateSnapshot(
        string requestedCapabilityName,
        DslRomSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return Result<DslRomSnapshot>.Fail(Error("DSL ROM snapshot is required."));
        }

        if (snapshot.Metadata is null)
        {
            return Result<DslRomSnapshot>.Fail(Error("DSL ROM metadata is required."));
        }

        if (snapshot.Pipeline is null)
        {
            return Result<DslRomSnapshot>.Fail(AttachRomMetadata(
                Error("DSL ROM pipeline is required."),
                snapshot.Metadata));
        }

        var metadataError = ValidateMetadata(snapshot.Metadata);
        if (metadataError is not null)
        {
            return Result<DslRomSnapshot>.Fail(metadataError);
        }

        if (string.IsNullOrWhiteSpace(snapshot.JsonDsl))
        {
            return Result<DslRomSnapshot>.Fail(AttachRomMetadata(
                Error("DSL ROM JSON is required."),
                snapshot.Metadata));
        }

        if (!string.Equals(
                snapshot.Metadata.CapabilityName,
                requestedCapabilityName,
                StringComparison.Ordinal))
        {
            return Result<DslRomSnapshot>.Fail(AttachRomMetadata(
                Error("Resolved DSL ROM capability name does not match the requested capability."),
                snapshot.Metadata));
        }

        var actualHash = DslRomHasher.ComputeHash(snapshot.JsonDsl);
        if (!string.Equals(
                actualHash,
                snapshot.Metadata.RomHash,
                StringComparison.Ordinal))
        {
            return Result<DslRomSnapshot>.Fail(AttachRomMetadata(
                Error("DSL ROM hash does not match JSON content."),
                snapshot.Metadata));
        }

        return Result<DslRomSnapshot>.Success(snapshot);
    }

    private static DslPipelineValue AttachRomData(
        DslPipelineValue value,
        DslRomMetadata metadata,
        int replayLogCount,
        string replayLogHash)
    {
        return value
            .With(DslRomMetadataKeys.RomCall, metadata.CapabilityName)
            .With(DslRomMetadataKeys.RomHash, metadata.RomHash)
            .With(DslRomMetadataKeys.RomPath, metadata.Path)
            .With(DslRomMetadataKeys.RomNamespace, metadata.Namespace)
            .With(DslRomMetadataKeys.RomName, metadata.Name)
            .With(
                DslRomMetadataKeys.RomReplayLogCount,
                replayLogCount.ToString(CultureInfo.InvariantCulture))
            .With(DslRomMetadataKeys.RomReplayLogHash, replayLogHash);
    }

    private static ErrorContext AttachRomMetadata(
        ErrorContext error,
        DslRomMetadata metadata,
        int? replayLogCount = null,
        string? replayLogHash = null)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        if (error.Metadata is not null)
        {
            foreach (var item in error.Metadata)
            {
                builder[item.Key] = item.Value;
            }
        }

        AddIfValue(builder, DslRomMetadataKeys.RomCall, metadata.CapabilityName);
        AddIfValue(builder, DslRomMetadataKeys.RomHash, metadata.RomHash);
        AddIfValue(builder, DslRomMetadataKeys.RomPath, metadata.Path);
        AddIfValue(builder, DslRomMetadataKeys.RomNamespace, metadata.Namespace);
        AddIfValue(builder, DslRomMetadataKeys.RomName, metadata.Name);
        if (replayLogCount is { } count)
        {
            builder[DslRomMetadataKeys.RomReplayLogCount] =
                count.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(replayLogHash))
        {
            builder[DslRomMetadataKeys.RomReplayLogHash] = replayLogHash;
        }

        return error with
        {
            Metadata = builder.ToImmutable()
        };
    }

    private static ErrorContext Error(string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.Capability,
            SemanticSlot = SemanticSlot.T
        };

    private static ErrorContext? ValidateMetadata(DslRomMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.CapabilityName))
        {
            return Error("DSL ROM capability name is required.");
        }

        if (string.IsNullOrWhiteSpace(metadata.RomHash))
        {
            return AttachRomMetadata(Error("DSL ROM hash is required."), metadata);
        }

        if (string.IsNullOrWhiteSpace(metadata.Path))
        {
            return AttachRomMetadata(Error("DSL ROM path is required."), metadata);
        }

        if (string.IsNullOrWhiteSpace(metadata.Namespace))
        {
            return AttachRomMetadata(Error("DSL ROM namespace is required."), metadata);
        }

        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            return AttachRomMetadata(Error("DSL ROM name is required."), metadata);
        }

        var identityError = DslRomMetadataValidator.ValidateCanonicalIdentity(metadata);
        if (identityError is not null)
        {
            return AttachRomMetadata(Error(identityError.Message), metadata);
        }

        return null;
    }

    private static void AddIfValue(
        ImmutableDictionary<string, string>.Builder builder,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder[key] = value;
        }
    }

    private static ErrorContext CapabilityException(
        string capabilityName,
        Exception exception)
    {
        var source = ErrorContext.FromException(exception);
        var builder = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        if (source.Metadata is not null)
        {
            foreach (var item in source.Metadata)
            {
                builder[item.Key] = item.Value;
            }
        }

        builder["dsl.capability_name"] = capabilityName;

        return source with
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.Capability,
            SemanticSlot = SemanticSlot.T,
            Metadata = builder.ToImmutable()
        };
    }
}
