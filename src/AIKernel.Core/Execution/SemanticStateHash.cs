namespace AIKernel.Core.Execution;

/// <summary>EN: Documentation for public API. JA: SemanticStateHash を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.struct']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.struct']/summary" />
public readonly record struct SemanticStateHash(
    string Algorithm,
    string HexDigest)
{
    /// <summary>EN: Documentation for public API. JA: ToString を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToString']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToString']/summary" />
    public override string ToString()
    {
        return $"{Algorithm}:{HexDigest}";
    }

    /// <summary>EN: Documentation for public API. JA: ToExecutionId を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToExecutionId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToExecutionId']/summary" />
    public string ToExecutionId()
    {
        return "exec:" + ToString();
    }
}
