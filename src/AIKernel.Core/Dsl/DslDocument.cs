namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using AIKernel.Common.Results;

internal sealed record DslDocument(PipelineNode Root)
{
    public static Result<DslDocument> FromJson(string json)
        => DslDocumentParser.Parse(json);
}

internal abstract record PipelineNode(string Type);

internal sealed record PipelineRootNode(
    IReadOnlyList<PipelineNode> Steps) : PipelineNode("Pipeline");

internal sealed record StepNode(
    string Name) : PipelineNode("Step");

internal sealed record CallCapabilityNode(
    string Name,
    IReadOnlyDictionary<string, string> Args) : PipelineNode("CallCapability");

internal sealed record LoopNode(
    int MaxIterations,
    IReadOnlyList<PipelineNode> BodyNodes) : PipelineNode("Loop");

internal sealed record LoopUntilNode(
    TimeSpan Timeout,
    int MaxIterations,
    IReadOnlyList<PipelineNode> BodyNodes) : PipelineNode("LoopUntil");

internal sealed record SuspendNode(
    string Reason) : PipelineNode("Suspend");

internal sealed record DslPipelineValue(
    IReadOnlyDictionary<string, string> Data)
{
    public static DslPipelineValue Empty { get; } = new(
        ImmutableDictionary<string, string>.Empty);

    public DslPipelineValue With(string key, string value)
    {
        if (Data is null)
        {
            throw new InvalidOperationException(
                "DSL pipeline value data is required.");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "DSL pipeline value data keys must not be empty.",
                nameof(key));
        }

        ArgumentNullException.ThrowIfNull(value);

        var builder = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        foreach (var item in Data)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                throw new InvalidOperationException(
                    "DSL pipeline value data keys must not be empty.");
            }

            if (item.Value is null)
            {
                throw new InvalidOperationException(
                    "DSL pipeline value data values must not be null.");
            }

            builder[item.Key] = item.Value;
        }

        builder[key] = value;
        return new DslPipelineValue(builder.ToImmutable());
    }
}

internal sealed record DslPipelineState(
    string PipelineId,
    string CurrentNode,
    int ExecutedNodeCount)
{
    public static DslPipelineState Initial(string pipelineId)
        => new(pipelineId, "start", 0);

    public DslPipelineState Advance(string nodeName)
        => this with
        {
            CurrentNode = nodeName,
            ExecutedNodeCount = ExecutedNodeCount + 1
        };
}

internal sealed record DslPipelineExecutionContext(
    DslPipelineValue Input,
    DateTimeOffset StartedAtUtc)
{
    public static DslPipelineExecutionContext Create(DslPipelineValue? input = null)
        => new(input ?? DslPipelineValue.Empty, DateTimeOffset.UnixEpoch);
}

internal interface IKernelPipeline
{
    ResultStep<DslPipelineState, DslPipelineValue> Execute(
        DslPipelineExecutionContext context);
}

internal interface IDslPipelineCompiler
{
    Result<IKernelPipeline> Compile(DslDocument document);
}

internal interface IDslCapabilityRegistry
{
    bool Contains(string name);

    Result<DslPipelineValue> Invoke(
        string name,
        DslPipelineValue input,
        IReadOnlyDictionary<string, string> args);
}
