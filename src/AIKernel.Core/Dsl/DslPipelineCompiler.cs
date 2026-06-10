namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using AIKernel.Common.Results;
using AIKernel.Core.Time;

internal sealed class DslPipelineCompiler :
    IDslPipelineCompiler,
    AIKernel.Abstractions.Dsl.IDslPipelineCompiler
{
    private readonly IDslCapabilityRegistry _capabilityRegistry;
    private readonly IKernelClock _clock;

    public DslPipelineCompiler(
        IDslCapabilityRegistry capabilityRegistry,
        IKernelClock? clock = null)
    {
        _capabilityRegistry = capabilityRegistry ?? throw new ArgumentNullException(nameof(capabilityRegistry));
        _clock = clock ?? KernelClock.Replay(DateTimeOffset.UnixEpoch);
    }

    public Result<IKernelPipeline> Compile(DslDocument document)
    {
        if (document is null)
        {
            return Result<IKernelPipeline>.Fail(CompileBoundaryFailure(
                "DSL document is required."));
        }

        return ValidateRoot(document.Root)
            .Map<IKernelPipeline>(root => new CompiledDslPipeline(
                root,
                _capabilityRegistry,
                _clock));
    }

    async Task<AIKernel.Abstractions.Dsl.IKernelPipeline>
        AIKernel.Abstractions.Dsl.IDslPipelineCompiler.CompileAsync(
            AIKernel.Dtos.Dsl.DslDocument document,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pipeline = Compile(DslContractMapper.ToCore(document))
            .Match(
                error => throw new InvalidOperationException(error.Message),
                value => value as AIKernel.Abstractions.Dsl.IKernelPipeline
                    ?? throw new InvalidOperationException(
                        "DSL compiler produced a pipeline that does not implement the contract interface."));

        return await Task.FromResult(pipeline).ConfigureAwait(false);
    }

    private Result<PipelineRootNode> ValidateRoot(PipelineNode root)
    {
        return root switch
        {
            null => Result<PipelineRootNode>.Fail(
                CompileBoundaryFailure("Pipeline root is required.")),
            PipelineRootNode pipeline => ValidateNodes(pipeline.Steps)
                .Map(_ => pipeline),
            _ => Result<PipelineRootNode>.Fail(
                InvalidError("DSL root node must be a Pipeline."))
        };
    }

    private Result<bool> ValidateNode(PipelineNode node)
    {
        return node switch
        {
            PipelineRootNode pipeline => ValidateNodes(pipeline.Steps),
            StepNode step => ValidateName(step.Name, "Step name"),
            CallCapabilityNode call => ValidateCapability(call),
            LoopNode loop => ValidateLoop(loop.MaxIterations, loop.BodyNodes),
            LoopUntilNode loopUntil => ValidateLoopUntil(loopUntil),
            SuspendNode suspend => ValidateName(suspend.Reason, "Suspend reason"),
            null => CompileBoundaryFailureResult("Pipeline node is required."),
            _ => Invalid($"Unsupported pipeline node: {node.GetType().Name}.")
        };
    }

    private Result<bool> ValidateNodes(IReadOnlyList<PipelineNode> nodes)
    {
        if (nodes is null)
        {
            return CompileBoundaryFailureResult("Pipeline node list is required.");
        }

        return nodes.Aggregate(
            Result<bool>.Success(true),
            (current, node) => current.Bind(_ => ValidateNode(node)));
    }

    private Result<bool> ValidateLoop(
        int maxIterations,
        IReadOnlyList<PipelineNode> bodyNodes)
    {
        return
            from _ in ValidateNonNegative(
                maxIterations,
                "maxIterations must be greater than or equal to zero.")
            from __ in ValidateNodes(bodyNodes)
            select true;
    }

    private Result<bool> ValidateLoopUntil(LoopUntilNode node)
    {
        return
            from _ in ValidateNonNegative(
                node.Timeout,
                "timeout must be greater than or equal to zero.")
            from __ in ValidateLoop(node.MaxIterations, node.BodyNodes)
            select true;
    }

    private Result<bool> ValidateName(string value, string fieldName)
        => RequireNonEmpty(value, $"{fieldName} must not be empty.")
            .Map(_ => true)
            .ToCompileResult();

    private Result<bool> ValidateCapability(CallCapabilityNode node)
    {
        return
            from _ in ValidateName(node.Name, "Capability name")
            from __ in ValidateCapabilityArgs(node.Args)
            from ___ in ValidateCapabilityExists(node.Name)
            select true;
    }

    private Result<bool> ValidateCapabilityExists(
        string name)
        => Try
            .Run(() => _capabilityRegistry.Contains(name))
            .Match(
                error => Result<bool>.Fail(CapabilityResolutionFailure(name, error)),
                exists => exists
                    ? Result<bool>.Success(true)
                    : Invalid($"Unknown capability: {name}."));

    private static Result<bool> ValidateCapabilityArgs(
        IReadOnlyDictionary<string, string> args)
    {
        if (args is null)
        {
            return CompileBoundaryFailureResult("Capability args are required.");
        }

        return args.Aggregate(
            Result<bool>.Success(true),
            (current, item) =>
                from _ in current
                from __ in ValidateCapabilityArg(item)
                select true);
    }

    private static Result<bool> ValidateCapabilityArg(
        KeyValuePair<string, string> item)
    {
        return
            from _ in ValidateCapabilityArgKey(item.Key)
            from __ in ValidateCapabilityArgValue(item.Value)
            select true;
    }

    private static Result<bool> ValidateCapabilityArgKey(
        string key)
    {
        if (key is null)
        {
            return CompileBoundaryFailureResult("Capability arg keys must not be null.");
        }

        return RequireNonEmpty(key, "Capability arg keys must not be empty.")
            .Map(_ => true)
            .ToCompileResult();
    }

    private static Result<bool> ValidateCapabilityArgValue(
        string value)
        => value is null
            ? CompileBoundaryFailureResult("Capability arg values must not be null.")
            : Result<bool>.Success(true);

    private static Result<bool> ValidateNonNegative(
        int value,
        string message)
        => RequireNonNegative(value, message)
            .Map(_ => true)
            .ToCompileResult();

    private static Result<bool> ValidateNonNegative(
        TimeSpan value,
        string message)
        => RequireNonNegative(value, message)
            .Map(_ => true)
            .ToCompileResult();

    private static Either<string, string> RequireNonEmpty(
        string value,
        string message)
        => string.IsNullOrWhiteSpace(value)
            ? Either<string, string>.FromLeft(message)
            : Either<string, string>.FromRight(value);

    private static Either<string, int> RequireNonNegative(
        int value,
        string message)
        => value < 0
            ? Either<string, int>.FromLeft(message)
            : Either<string, int>.FromRight(value);

    private static Either<string, TimeSpan> RequireNonNegative(
        TimeSpan value,
        string message)
        => value < TimeSpan.Zero
            ? Either<string, TimeSpan>.FromLeft(message)
            : Either<string, TimeSpan>.FromRight(value);

    private static Result<bool> Invalid(string message)
        => Result<bool>.Fail(InvalidError(message));

    private static ErrorContext InvalidError(string message)
        => new(message, "DSL_COMPILE_ERROR", false)
        {
            FailureKind = FailureKind.Reject,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        };

    private static Result<bool> CompileBoundaryFailureResult(string message)
        => Result<bool>.Fail(CompileBoundaryFailure(message));

    private static ErrorContext CompileBoundaryFailure(string message)
        => new(message, "DSL_COMPILE_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        };

    private static ErrorContext CapabilityResolutionFailure(
        string capabilityName,
        ErrorContext source)
    {
        var metadata = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        if (source.Metadata is not null)
        {
            foreach (var item in source.Metadata)
            {
                metadata[item.Key] = item.Value;
            }
        }

        metadata["dsl.capability_name"] = capabilityName;

        return source with
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.Capability,
            SemanticSlot = SemanticSlot.T,
            Metadata = metadata.ToImmutable()
        };
    }
}

internal static class DslPipelineCompilerEitherExtensions
{
    public static Result<T> ToCompileResult<T>(
        this Either<string, T> value)
        => value.Match(
            left => Result<T>.Fail(new ErrorContext(left, "DSL_COMPILE_ERROR", false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.T
            }),
            Result<T>.Success);
}
