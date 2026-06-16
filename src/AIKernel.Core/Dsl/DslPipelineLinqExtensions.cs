namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal static class DslPipelineLinqExtensions
{
    /// <summary>
    /// EN: Gets Select.
    /// EN: Documentation for public API. JA: Select を取得します。
    /// </summary>
    public static IKernelPipeline Select(
        this IKernelPipeline pipeline,
        Func<DslPipelineValue, DslPipelineValue> selector)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(selector);

        return new DelegateKernelPipeline(context =>
            pipeline.Execute(context).Map(selector));
    }
    /// <summary>
    /// EN: Gets Map.
    /// EN: Documentation for public API. JA: Map を取得します。
    /// </summary>

    public static IKernelPipeline Map(
        this IKernelPipeline pipeline,
        Func<DslPipelineValue, DslPipelineValue> mapper)
        => Select(pipeline, mapper);
    /// <summary>
    /// EN: Gets Bind.
    /// EN: Documentation for public API. JA: Bind を取得します。
    /// </summary>

    public static IKernelPipeline Bind(
        this IKernelPipeline pipeline,
        Func<DslPipelineValue, IKernelPipeline> binder)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(binder);

        return new DelegateKernelPipeline(context =>
            pipeline.Execute(context).Bind(value =>
            {
                var nextPipeline = binder(value);
                if (nextPipeline is null)
                {
                    return NullPipelineResult();
                }

                return nextPipeline.Execute(CreateContinuationContext(context, value));
            }));
    }
    /// <summary>
    /// EN: Gets SelectMany.
    /// EN: Documentation for public API. JA: SelectMany を取得します。
    /// </summary>

    public static IKernelPipeline SelectMany(
        this IKernelPipeline pipeline,
        Func<DslPipelineValue, IKernelPipeline> binder,
        Func<DslPipelineValue, DslPipelineValue, DslPipelineValue> projector)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(projector);

        return new DelegateKernelPipeline(context =>
            pipeline.Execute(context).Bind(value =>
            {
                var nextPipeline = binder(value);
                if (nextPipeline is null)
                {
                    return NullPipelineResult();
                }

                return nextPipeline
                    .Execute(CreateContinuationContext(context, value))
                    .Map(next => projector(value, next));
            }));
    }
    /// <summary>
    /// EN: Gets Where.
    /// EN: Documentation for public API. JA: Where を取得します。
    /// </summary>

    public static IKernelPipeline Where(
        this IKernelPipeline pipeline,
        Func<DslPipelineValue, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(predicate);

        return new DelegateKernelPipeline(context =>
            pipeline.Execute(context).Bind(value =>
            {
                return Try
                    .Run(() => predicate(value))
                    .Match(
                        PredicateExceptionResult,
                        accepted => accepted
                            ? ResultStep<DslPipelineState, DslPipelineValue>
                                .Success(
                                    DslPipelineState.Initial("dsl.pipeline.linq"),
                                    value)
                            : PredicateRejectedResult());
            }));
    }

    private static DslPipelineExecutionContext CreateContinuationContext(
        DslPipelineExecutionContext context,
        DslPipelineValue value)
        => new(
            value,
            context?.StartedAtUtc ?? DateTimeOffset.UnixEpoch);

    private static ResultStep<DslPipelineState, DslPipelineValue> NullPipelineResult()
        => ResultStep<DslPipelineState, DslPipelineValue>
            .Fail(
                DslPipelineState.Initial("dsl.pipeline.linq"),
                DslExecutionErrors.InvalidRuntime(
                    "DSL LINQ binder returned a null pipeline."))
            .WithSemanticDelta(DslSemanticDeltaFactory.CreateNodeDelta(
                "dsl.pipeline.linq",
                "fail_closed",
                "linq",
                "select_many"));

    private static ResultStep<DslPipelineState, DslPipelineValue> PredicateRejectedResult()
        => ResultStep<DslPipelineState, DslPipelineValue>
            .Fail(
                DslPipelineState.Initial("dsl.pipeline.linq"),
                DslExecutionErrors.PredicateRejected(
                    "DSL LINQ predicate rejected the pipeline value."))
            .WithSemanticDelta(DslSemanticDeltaFactory.CreateNodeDelta(
                "dsl.pipeline.linq",
                "reject",
                "linq",
                "where"));

    private static ResultStep<DslPipelineState, DslPipelineValue> PredicateExceptionResult(
        ErrorContext error)
        => ResultStep<DslPipelineState, DslPipelineValue>
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
                "where"));
}
