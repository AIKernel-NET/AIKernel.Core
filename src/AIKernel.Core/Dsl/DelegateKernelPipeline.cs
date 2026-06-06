namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal sealed class DelegateKernelPipeline : IKernelPipeline
{
    private readonly Func<DslPipelineExecutionContext, ResultStep<DslPipelineState, DslPipelineValue>> _execute;

    public DelegateKernelPipeline(
        Func<DslPipelineExecutionContext, ResultStep<DslPipelineState, DslPipelineValue>> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public ResultStep<DslPipelineState, DslPipelineValue> Execute(
        DslPipelineExecutionContext context)
    {
        try
        {
            return _execute(context);
        }
        catch (Exception ex)
        {
            return ResultStep<DslPipelineState, DslPipelineValue>
                .Fail(
                    DslPipelineState.Initial("dsl.pipeline.linq"),
                    ErrorContext.FromException(ex) with
                    {
                        FailureKind = FailureKind.FailClosed,
                        OriginStep = OriginStep.KernelFacade,
                        SemanticSlot = SemanticSlot.T
                    })
                .WithSemanticDelta(DslSemanticDeltaFactory.CreateNodeDelta(
                    "dsl.pipeline.linq",
                    "fail_closed",
                    "linq",
                    "execute"));
        }
    }
}
