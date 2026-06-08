namespace AIKernel.Core.Execution;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.struct']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.struct']" />
public readonly record struct SemanticStateHash(
    string Algorithm,
    string HexDigest)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToString']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToString']" />
    public override string ToString()
    {
        return $"{Algorithm}:{HexDigest}";
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToExecutionId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.struct.ToExecutionId']" />
    public string ToExecutionId()
    {
        return "exec:" + ToString();
    }
}
