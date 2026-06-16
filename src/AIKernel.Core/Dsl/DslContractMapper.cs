namespace AIKernel.Core.Dsl;

using ContractDsl = AIKernel.Dtos.Dsl;

internal static class DslContractMapper
{
    /// <summary>
    /// EN: Executes ToCore.
    /// EN: Documentation for public API. JA: ToCore を実行します。
    /// </summary>
    public static DslDocument ToCore(ContractDsl.DslDocument document)
        => new(ToCore(document.Root));
    /// <summary>
    /// EN: Executes ToContract.
    /// EN: Documentation for public API. JA: ToContract を実行します。
    /// </summary>

    public static ContractDsl.DslPipelineValue ToContract(DslPipelineValue value)
        => new(value.Data);
    /// <summary>
    /// EN: Executes ToCore.
    /// EN: Documentation for public API. JA: ToCore を実行します。
    /// </summary>

    public static DslPipelineValue ToCore(ContractDsl.DslPipelineValue value)
        => new(value.Data);
    /// <summary>
    /// EN: Executes ToContract.
    /// EN: Documentation for public API. JA: ToContract を実行します。
    /// </summary>

    public static ContractDsl.DslPipelineState ToContract(DslPipelineState state)
        => new(state.PipelineId, state.CurrentNode, state.ExecutedNodeCount);
    /// <summary>
    /// EN: Gets ToCore.
    /// EN: Documentation for public API. JA: ToCore を取得します。
    /// </summary>

    public static ContractDsl.DslPipelineExecutionContext ToCore(
        DslPipelineExecutionContext context)
        => new(ToContract(context.Input), context.StartedAtUtc);
    /// <summary>
    /// EN: Gets ToCore.
    /// EN: Documentation for public API. JA: ToCore を取得します。
    /// </summary>

    public static DslPipelineExecutionContext ToCore(
        ContractDsl.DslPipelineExecutionContext context)
        => new(ToCore(context.Input), context.StartedAtUtc);
    /// <summary>
    /// EN: Executes ToContract.
    /// EN: Documentation for public API. JA: ToContract を実行します。
    /// </summary>

    public static ContractDsl.DslRomMetadata ToContract(DslRomMetadata metadata)
        => new(
            metadata.Namespace,
            metadata.Name,
            metadata.Path,
            metadata.CapabilityName,
            metadata.RomHash,
            metadata.CreatedAtUtc);
    /// <summary>
    /// EN: Executes ToCore.
    /// EN: Documentation for public API. JA: ToCore を実行します。
    /// </summary>

    public static DslRomMetadata ToCore(ContractDsl.DslRomMetadata metadata)
        => new(
            metadata.Namespace,
            metadata.Name,
            metadata.Path,
            metadata.CapabilityName,
            metadata.RomHash,
            metadata.CreatedAtUtc);
    /// <summary>
    /// EN: Executes ToContract.
    /// EN: Documentation for public API. JA: ToContract を実行します。
    /// </summary>

    public static ContractDsl.DslRomSnapshot ToContract(DslRomSnapshot snapshot)
        => new(ToContract(snapshot.Metadata), snapshot.JsonDsl);

    private static PipelineNode ToCore(ContractDsl.PipelineNode node)
        => node switch
        {
            ContractDsl.PipelineRootNode pipeline => new PipelineRootNode(
                pipeline.Steps.Select(ToCore).ToArray()),
            ContractDsl.StepNode step => new StepNode(step.Name),
            ContractDsl.CallCapabilityNode call => new CallCapabilityNode(
                call.Name,
                call.Args),
            ContractDsl.LoopNode loop => new LoopNode(
                loop.MaxIterations,
                loop.BodyNodes.Select(ToCore).ToArray()),
            ContractDsl.LoopUntilNode loopUntil => new LoopUntilNode(
                loopUntil.Timeout,
                loopUntil.MaxIterations,
                loopUntil.BodyNodes.Select(ToCore).ToArray()),
            ContractDsl.SuspendNode suspend => new SuspendNode(suspend.Reason),
            _ => throw new ArgumentException(
                $"Unsupported contract DSL node: {node?.GetType().Name ?? "<null>"}.",
                nameof(node))
        };
}
