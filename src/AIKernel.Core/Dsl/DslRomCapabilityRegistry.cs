namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using System.Globalization;
using AIKernel.Common.Results;

internal sealed class DslRomCapabilityRegistry :
    IDslCapabilityRegistry,
    AIKernel.Abstractions.Dsl.IDslCapabilityRegistry
{
    private readonly IDslCapabilityRegistry _inner;
    private readonly IDslRomRegistry _romRegistry;
    /// <summary>
    /// EN: Gets DslRomCapabilityRegistry.
    /// [EN] Documents this public package API member. [JA] DslRomCapabilityRegistry を取得します。
    /// </summary>

    public DslRomCapabilityRegistry(
        IDslCapabilityRegistry inner,
        IDslRomRegistry romRegistry)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _romRegistry = romRegistry ?? throw new ArgumentNullException(nameof(romRegistry));
    }
    /// <summary>
    /// EN: Executes Contains.
    /// [EN] Documents this public package API member. [JA] Contains を実行します。
    /// </summary>

    public bool Contains(string name)
        => DslRomPath.IsDslCapability(name)
            ? _romRegistry.Contains(name)
            : _inner.Contains(name);
    /// <summary>
    /// EN: Gets Invoke.
    /// [EN] Documents this public package API member. [JA] Invoke を取得します。
    /// </summary>

    public Result<DslPipelineValue> Invoke(
        string name,
        DslPipelineValue input,
        IReadOnlyDictionary<string, string> args)
    {
        if (!DslRomPath.IsDslCapability(name))
        {
            return Try
                .Run(() => _inner.Invoke(name, input, args))
                .Match(error => Result<DslPipelineValue>.Fail(CapabilityException(name, error)), result => result);
        }

        var snapshot =
            from _ in ParseRequestedCapabilityName(name)
            from resolved in ResolveSnapshot(name)
            from validated in ValidateSnapshot(name, resolved)
            select validated;

        return snapshot.Bind(validated => ExecuteSnapshot(validated, input));
    }

    private Result<DslPipelineValue> ExecuteSnapshot(
        DslRomSnapshot snapshot,
        DslPipelineValue input)
    {
        return
            from result in ExecutePipeline(snapshot, input)
            from value in RequirePipelineValue(snapshot.Metadata, result)
            select AttachRomData(
                value,
                snapshot.Metadata,
                result.ReplayLog.Count,
                result.ReplayLogHash);
    }

    private static Result<bool> ParseRequestedCapabilityName(
        string name)
        => DslRomPath.ParseCapabilityName(name)
            .Match(
                error => Result<bool>.Fail(CapabilityNameError(
                    name,
                    error.Message)),
                _ => Result<bool>.Success(true));

    private Result<DslRomSnapshot> ResolveSnapshot(
        string name)
        => Try
            .Run(() => _romRegistry.Resolve(name))
            .Match(error => Result<DslRomSnapshot>.Fail(CapabilityException(name, error)), result => result);

    private static Result<ResultStep<DslPipelineState, DslPipelineValue>> ExecutePipeline(
        DslRomSnapshot snapshot,
        DslPipelineValue input)
        => Try
            .Run(() => snapshot.Pipeline.Execute(
                DslPipelineExecutionContext.Create(input)))
            .Match(
                error => Result<ResultStep<DslPipelineState, DslPipelineValue>>.Fail(AttachRomMetadata(
                    error with
                    {
                        FailureKind = FailureKind.FailClosed,
                        OriginStep = OriginStep.Capability,
                        SemanticSlot = SemanticSlot.T
                    },
                    snapshot.Metadata)),
                Result<ResultStep<DslPipelineState, DslPipelineValue>>.Success);

    private static Result<DslPipelineValue> RequirePipelineValue(
        DslRomMetadata metadata,
        ResultStep<DslPipelineState, DslPipelineValue> result)
        => result.Match(
            (_, error) => Result<DslPipelineValue>.Fail(AttachRomMetadata(
                error,
                metadata,
                result.ReplayLog.Count,
                result.ReplayLogHash)),
            (_, value) => value is null
                ? Result<DslPipelineValue>.Fail(AttachRomMetadata(
                    Error("DSL ROM pipeline returned a successful null value."),
                    metadata,
                    result.ReplayLog.Count,
                    result.ReplayLogHash))
                : Result<DslPipelineValue>.Success(value));

    private static Result<DslRomSnapshot> ValidateSnapshot(
        string requestedCapabilityName,
        DslRomSnapshot? snapshot)
    {
        return
            from validSnapshot in RequireSnapshot(snapshot)
            from metadata in RequireSnapshotMetadata(validSnapshot)
            from _ in RequireSnapshotPipeline(validSnapshot, metadata)
            from __ in ValidateSnapshotMetadata(metadata)
            from ___ in RequireSnapshotJson(validSnapshot, metadata)
            from ____ in ValidateRequestedCapabilityName(
                requestedCapabilityName,
                metadata)
            from _____ in ValidateSnapshotHash(validSnapshot, metadata)
            select validSnapshot;
    }

    private static Result<DslRomSnapshot> RequireSnapshot(
        DslRomSnapshot? snapshot)
        => snapshot is null
            ? Result<DslRomSnapshot>.Fail(Error("DSL ROM snapshot is required."))
            : Result<DslRomSnapshot>.Success(snapshot);

    private static Result<DslRomMetadata> RequireSnapshotMetadata(
        DslRomSnapshot snapshot)
        => snapshot.Metadata is null
            ? Result<DslRomMetadata>.Fail(Error("DSL ROM metadata is required."))
            : Result<DslRomMetadata>.Success(snapshot.Metadata);

    private static Result<bool> RequireSnapshotPipeline(
        DslRomSnapshot snapshot,
        DslRomMetadata metadata)
        => snapshot.Pipeline is null
            ? Result<bool>.Fail(AttachRomMetadata(
                Error("DSL ROM pipeline is required."),
                metadata))
            : Result<bool>.Success(true);

    private static Result<bool> ValidateSnapshotMetadata(
        DslRomMetadata metadata)
        => ValidateMetadata(metadata)
            .Match(
                () => Result<bool>.Success(true),
                Result<bool>.Fail);

    private static Result<bool> RequireSnapshotJson(
        DslRomSnapshot snapshot,
        DslRomMetadata metadata)
        => RequireNonEmpty(snapshot.JsonDsl, "DSL ROM JSON is required.")
            .Map(_ => true)
            .ToRomCapabilityResult(metadata);

    private static Result<bool> ValidateRequestedCapabilityName(
        string requestedCapabilityName,
        DslRomMetadata metadata)
        => string.Equals(
            metadata.CapabilityName,
            requestedCapabilityName,
            StringComparison.Ordinal)
            ? Result<bool>.Success(true)
            : Result<bool>.Fail(AttachRomMetadata(
                Error("Resolved DSL ROM capability name does not match the requested capability."),
                metadata));

    private static Result<bool> ValidateSnapshotHash(
        DslRomSnapshot snapshot,
        DslRomMetadata metadata)
    {
        var actualHash = DslRomHasher.ComputeHash(snapshot.JsonDsl);
        return string.Equals(
            actualHash,
            metadata.RomHash,
            StringComparison.Ordinal)
            ? Result<bool>.Success(true)
            : Result<bool>.Fail(AttachRomMetadata(
                Error("DSL ROM hash does not match JSON content."),
                metadata));
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

        OptionalText(replayLogHash)
            .Tap(value => builder[DslRomMetadataKeys.RomReplayLogHash] = value);

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

    private static Option<ErrorContext> ValidateMetadata(
        DslRomMetadata metadata)
        => ValidateRequiredMetadata(metadata)
            .Match(
                message => Option<ErrorContext>.Some(
                    AttachRomMetadata(Error(message), metadata)),
                _ => OptionalError(
                        DslRomMetadataValidator.ValidateCanonicalIdentity(metadata))
                    .Map(error => AttachRomMetadata(Error(error.Message), metadata)));

    private static Either<string, bool> ValidateRequiredMetadata(
        DslRomMetadata metadata)
        => from capabilityName in RequireNonEmpty(
                metadata.CapabilityName,
                "DSL ROM capability name is required.")
           from romHash in RequireNonEmpty(
                metadata.RomHash,
                "DSL ROM hash is required.")
           from path in RequireNonEmpty(
                metadata.Path,
                "DSL ROM path is required.")
           from ns in RequireNonEmpty(
                metadata.Namespace,
                "DSL ROM namespace is required.")
           from name in RequireNonEmpty(
                metadata.Name,
                "DSL ROM name is required.")
           select true;

    private static void AddIfValue(
        ImmutableDictionary<string, string>.Builder builder,
        string key,
        string? value)
    {
        OptionalText(value)
            .Tap(text => builder[key] = text);
    }

    private static Either<string, string> RequireNonEmpty(
        string? value,
        string message)
        => string.IsNullOrWhiteSpace(value)
            ? Either<string, string>.FromLeft(message)
            : Either<string, string>.FromRight(value);

    private static Option<string> OptionalText(
        string? value)
        => string.IsNullOrWhiteSpace(value)
            ? Option<string>.None()
            : Option<string>.Some(value);

    private static Option<ErrorContext> OptionalError(
        ErrorContext? value)
        => value is null
            ? Option<ErrorContext>.None()
            : Option<ErrorContext>.Some(value);

    private static ErrorContext CapabilityException(
        string capabilityName,
        ErrorContext source)
    {
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

    private static ErrorContext CapabilityNameError(
        string capabilityName,
        string message)
        => Error(message) with
        {
            Metadata = ImmutableDictionary<string, string>.Empty
                .Add("dsl.capability_name", capabilityName)
        };

    async Task<AIKernel.Dtos.Dsl.DslPipelineValue>
        AIKernel.Abstractions.Dsl.IDslCapabilityRegistry.InvokeAsync(
            string name,
            AIKernel.Dtos.Dsl.DslPipelineValue input,
            IReadOnlyDictionary<string, string> args,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.FromResult(Invoke(name, DslContractMapper.ToCore(input), args)
            .Match(
                error => throw new InvalidOperationException(error.Message),
                DslContractMapper.ToContract))
            .ConfigureAwait(false);
    }
}

internal static class DslRomCapabilityRegistryEitherExtensions
{
    /// <summary>
    /// EN: Gets ToRomCapabilityResult&lt;T&gt;.
    /// [EN] Documents this public package API member. [JA] ToRomCapabilityResult&lt;T&gt; を取得します。
    /// </summary>
    public static Result<T> ToRomCapabilityResult<T>(
        this Either<string, T> value,
        DslRomMetadata metadata)
        => value.Match(
            left => Result<T>.Fail(DslRomCapabilityRegistryError.Attach(
                DslRomCapabilityRegistryError.Create(left),
                metadata)),
            Result<T>.Success);
}

internal static class DslRomCapabilityRegistryError
{
    /// <summary>
    /// EN: Gets Create.
    /// [EN] Documents this public package API member. [JA] Create を取得します。
    /// </summary>
    public static ErrorContext Create(
        string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.Capability,
            SemanticSlot = SemanticSlot.T
        };
    /// <summary>
    /// EN: Gets Attach.
    /// [EN] Documents this public package API member. [JA] Attach を取得します。
    /// </summary>

    public static ErrorContext Attach(
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

        AddIfValue(builder, DslRomMetadataKeys.RomCall, metadata.CapabilityName);
        AddIfValue(builder, DslRomMetadataKeys.RomHash, metadata.RomHash);
        AddIfValue(builder, DslRomMetadataKeys.RomPath, metadata.Path);
        AddIfValue(builder, DslRomMetadataKeys.RomNamespace, metadata.Namespace);
        AddIfValue(builder, DslRomMetadataKeys.RomName, metadata.Name);

        return error with
        {
            Metadata = builder.ToImmutable()
        };
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
}
