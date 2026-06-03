namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;
using AIKernel.Core.Time;

public sealed class DslPipelineCompiler : IDslPipelineCompiler
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
        ArgumentNullException.ThrowIfNull(document);

        return ValidateNode(document.Root)
            .Map<IKernelPipeline>(_ => new CompiledDslPipeline(
                document.Root,
                _capabilityRegistry,
                _clock));
    }

    private Result<bool> ValidateNode(PipelineNode node)
    {
        return node switch
        {
            PipelineRootNode pipeline => ValidateNodes(pipeline.Steps),
            StepNode step => ValidateName(step.Name, "Step name"),
            CallCapabilityNode call => ValidateCapability(call.Name),
            LoopNode loop => ValidateLoop(loop.MaxIterations, loop.BodyNodes),
            LoopUntilNode loopUntil => ValidateLoopUntil(loopUntil),
            SuspendNode suspend => ValidateName(suspend.Reason, "Suspend reason"),
            _ => Invalid($"Unsupported pipeline node: {node.GetType().Name}.")
        };
    }

    private Result<bool> ValidateNodes(IReadOnlyList<PipelineNode> nodes)
    {
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

    private Result<bool> ValidateCapability(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Invalid("Capability name must not be empty.");
        }

        return _capabilityRegistry.Contains(name)
            ? Result<bool>.Success(true)
            : Invalid($"Unknown capability: {name}.");
    }

    private static Result<bool> Invalid(string message)
        => Result<bool>.Fail(new ErrorContext(message, "DSL_COMPILE_ERROR", false)
        {
            FailureKind = FailureKind.Reject,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        });
}
