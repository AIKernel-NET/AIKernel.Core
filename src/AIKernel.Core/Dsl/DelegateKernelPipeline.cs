namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal sealed class DelegateKernelPipeline : IKernelPipeline
{
    private readonly Func<DslPipelineExecutionContext, ResultStep<DslPipelineState, DslPipelineValue>> _execute;
    /// <summary>
    /// EN: Gets DelegateKernelPipeline.
    /// [EN] Documents this public package API member. [JA] DelegateKernelPipeline を取得します。
    /// </summary>

    public DelegateKernelPipeline(
        Func<DslPipelineExecutionContext, ResultStep<DslPipelineState, DslPipelineValue>> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }
    /// <summary>
    /// EN: Gets Execute.
    /// [EN] Documents this public package API member. [JA] Execute を取得します。
    /// </summary>

    public ResultStep<DslPipelineState, DslPipelineValue> Execute(
        DslPipelineExecutionContext context)
        => Try
            .Run(() => _execute(context))
            .Match(
                error => ResultStep<DslPipelineState, DslPipelineValue>
                    .Fail(
                        DslPipelineState.Initial("dsl.pipeline.linq"),
                        error with
                        {
                            FailureKind = FailureKind.FailClosed,
                            OriginStep = OriginStep.KernelFacade,
                            SemanticSlot = SemanticSlot.T
                        })
                    .WithSemanticDelta(DslSemanticDeltaFactory.CreateNodeDelta(
                        "dsl.pipeline.linq",
                        "fail_closed",
                        "linq",
                        "execute")),
                result => result);
}
