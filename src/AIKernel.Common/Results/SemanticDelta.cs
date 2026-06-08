namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.SemanticDelta']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.SemanticDelta']" />
public sealed record SemanticDelta(
    string Label,
    OriginStep? OriginStep = null,
    SemanticSlot? SemanticSlot = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? Kind = null)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.SemanticDelta.new']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.SemanticDelta.new']" />
    public static SemanticDelta Empty { get; } = new("none");
}
