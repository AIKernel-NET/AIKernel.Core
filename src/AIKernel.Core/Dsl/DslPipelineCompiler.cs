namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using AIKernel.Common.Results;
using AIKernel.Core.Time;

public sealed class DslPipelineCompiler :
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

        var result = Compile(DslContractMapper.ToCore(document));
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        if (result.Value is not AIKernel.Abstractions.Dsl.IKernelPipeline pipeline)
        {
            throw new InvalidOperationException(
                "DSL compiler produced a pipeline that does not implement the contract interface.");
        }

        return await Task.FromResult(pipeline).ConfigureAwait(false);
    }

    private Result<PipelineRootNode> ValidateRoot(PipelineNode root)
    {
        if (root is null)
        {
            return Result<PipelineRootNode>.Fail(
                CompileBoundaryFailure("Pipeline root is required."));
        }

        if (root is not PipelineRootNode pipeline)
        {
            return Result<PipelineRootNode>.Fail(
                InvalidError("DSL root node must be a Pipeline."));
        }

        var result = ValidateNodes(pipeline.Steps);
        return result.IsSuccess
            ? Result<PipelineRootNode>.Success(pipeline)
            : Result<PipelineRootNode>.Fail(result.Error!);
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

        foreach (var node in nodes)
        {
            var result = ValidateNode(node);
            if (result.IsFailure)
                return result;
        }

        return Result<bool>.Success(true);
    }

    private Result<bool> ValidateLoop(
        int maxIterations,
        IReadOnlyList<PipelineNode> bodyNodes)
    {
        if (maxIterations < 0)
        {
            return Invalid("maxIterations must be greater than or equal to zero.");
        }

        return ValidateNodes(bodyNodes);
    }

    private Result<bool> ValidateLoopUntil(LoopUntilNode node)
    {
        if (node.Timeout < TimeSpan.Zero)
        {
            return Invalid("timeout must be greater than or equal to zero.");
        }

        return ValidateLoop(node.MaxIterations, node.BodyNodes);
    }

    private Result<bool> ValidateName(string value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Invalid($"{fieldName} must not be empty.")
            : Result<bool>.Success(true);
    }

    private Result<bool> ValidateCapability(CallCapabilityNode node)
    {
        var name = node.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return Invalid("Capability name must not be empty.");
        }

        var args = ValidateCapabilityArgs(node.Args);
        if (args.IsFailure)
        {
            return args;
        }

        bool contains;
        try
        {
            contains = _capabilityRegistry.Contains(name);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(CapabilityResolutionException(name, ex));
        }

        return contains ? Result<bool>.Success(true) : Invalid($"Unknown capability: {name}.");
    }

    private static Result<bool> ValidateCapabilityArgs(
        IReadOnlyDictionary<string, string> args)
    {
        if (args is null)
        {
            return CompileBoundaryFailureResult("Capability args are required.");
        }

        foreach (var item in args)
        {
            if (item.Key is null)
            {
                return CompileBoundaryFailureResult("Capability arg keys must not be null.");
            }

            if (string.IsNullOrWhiteSpace(item.Key))
            {
                return Invalid("Capability arg keys must not be empty.");
            }

            if (item.Value is null)
            {
                return CompileBoundaryFailureResult("Capability arg values must not be null.");
            }
        }

        return Result<bool>.Success(true);
    }

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

    private static ErrorContext CapabilityResolutionException(
        string capabilityName,
        Exception exception)
    {
        var source = ErrorContext.FromException(exception);
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
