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
            return _inner.Invoke(name, input, args);
        }

        var snapshot = _romRegistry.Resolve(name);
        if (snapshot.IsFailure)
        {
            return Result<DslPipelineValue>.Fail(snapshot.Error!);
        }

        var result = snapshot.Value!.Pipeline.Execute(
            DslPipelineExecutionContext.Create(input));

        if (result.IsFailure)
        {
            return Result<DslPipelineValue>.Fail(AttachRomMetadata(
                result.Error!,
                snapshot.Value.Metadata));
        }

        return Result<DslPipelineValue>.Success(AttachRomData(
            result.Value!,
            snapshot.Value.Metadata,
            result.ReplayLog.Count,
            result.ReplayLogHash));
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
        DslRomMetadata metadata)
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

        builder[DslRomMetadataKeys.RomCall] = metadata.CapabilityName;
        builder[DslRomMetadataKeys.RomHash] = metadata.RomHash;
        builder[DslRomMetadataKeys.RomPath] = metadata.Path;
        builder[DslRomMetadataKeys.RomNamespace] = metadata.Namespace;
        builder[DslRomMetadataKeys.RomName] = metadata.Name;

        return error with
        {
            Metadata = builder.ToImmutable()
        };
    }
}
