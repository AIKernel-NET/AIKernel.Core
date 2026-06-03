namespace AIKernel.Common.Results;

public sealed record SemanticDelta(
    string Label,
    OriginStep? OriginStep = null,
    SemanticSlot? SemanticSlot = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public static SemanticDelta Empty { get; } = new("none");
}
