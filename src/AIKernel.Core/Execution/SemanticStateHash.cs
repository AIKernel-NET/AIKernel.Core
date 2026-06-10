namespace AIKernel.Core.Execution;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.struct']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.struct']/summary" />
public readonly record struct SemanticStateHash(
    string Algorithm,
    string HexDigest)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToString']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToString']/summary" />
    public override string ToString()
    {
        return $"{Algorithm}:{HexDigest}";
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToExecutionId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToExecutionId']/summary" />
    public string ToExecutionId()
    {
        return "exec:" + ToString();
    }
}
