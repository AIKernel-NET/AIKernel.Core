namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.SemanticDelta']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.SemanticDelta']/summary" />
public sealed record SemanticDelta(
    string Label,
    OriginStep? OriginStep = null,
    SemanticSlot? SemanticSlot = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? Kind = null)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.SemanticDelta.new']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.SemanticDelta.new']/summary" />
    public static SemanticDelta Empty { get; } = new("none");
}
