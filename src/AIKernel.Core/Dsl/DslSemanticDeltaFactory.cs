namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using System.Globalization;
using AIKernel.Common.Results;

internal static class DslSemanticDeltaFactory
{
    /// <summary>
    /// EN: Gets CreateLoopDelta.
    /// EN: Documentation for public API. JA: CreateLoopDelta を取得します。
    /// </summary>
    public static SemanticDelta CreateLoopDelta(
        int iteration,
        string decision)
    {
        return new SemanticDelta(
            "dsl.loop.iteration",
            OriginStep.KernelFacade,
            SemanticSlot.T,
            ImmutableDictionary<string, string>.Empty
                .Add(PipelineStepMetadataKeys.DeltaKind, "loop")
                .Add(PipelineStepMetadataKeys.LoopIteration, iteration.ToString(CultureInfo.InvariantCulture))
                .Add(PipelineStepMetadataKeys.LoopDecision, decision),
            Kind: "loop");
    }
    /// <summary>
    /// EN: Gets CreateLoopUntilDelta.
    /// EN: Documentation for public API. JA: CreateLoopUntilDelta を取得します。
    /// </summary>

    public static SemanticDelta CreateLoopUntilDelta(
        int iteration,
        DateTimeOffset? timestamp,
        string decision)
    {
        var metadata = ImmutableDictionary<string, string>.Empty
            .Add(PipelineStepMetadataKeys.DeltaKind, "loop")
            .Add(PipelineStepMetadataKeys.LoopIteration, iteration.ToString(CultureInfo.InvariantCulture))
            .Add(PipelineStepMetadataKeys.LoopDecision, decision);

        if (timestamp is { } value)
        {
            metadata = metadata.Add(
                PipelineStepMetadataKeys.LoopTimestamp,
                value.ToString("O", CultureInfo.InvariantCulture));
        }

        return new SemanticDelta(
            "dsl.loop_until.iteration",
            OriginStep.KernelFacade,
            SemanticSlot.T,
            metadata,
            Kind: "loop");
    }
    /// <summary>
    /// EN: Gets CreateNodeDelta.
    /// EN: Documentation for public API. JA: CreateNodeDelta を取得します。
    /// </summary>

    public static SemanticDelta CreateNodeDelta(
        string label,
        string kind,
        string nodeType,
        string nodeName,
        IReadOnlyDictionary<string, string>? args = null,
        IReadOnlyDictionary<string, string>? extraMetadata = null)
    {
        var metadata = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);
        metadata[PipelineStepMetadataKeys.DeltaKind] = kind;
        metadata["dsl.node_type"] = nodeType;
        metadata["dsl.node_name"] = nodeName;

        if (args is not null)
        {
            foreach (var item in args.OrderBy(
                item => item.Key,
                StringComparer.Ordinal))
            {
                metadata[$"dsl.arg.{item.Key}"] = item.Value;
            }
        }

        if (extraMetadata is not null)
        {
            foreach (var item in extraMetadata.OrderBy(
                item => item.Key,
                StringComparer.Ordinal))
            {
                metadata[item.Key] = item.Value;
            }
        }

        return new SemanticDelta(
            label,
            OriginStep.KernelFacade,
            SemanticSlot.T,
            metadata.ToImmutable(),
            Kind: kind);
    }
}
