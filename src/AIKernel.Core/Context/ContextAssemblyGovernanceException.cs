namespace AIKernel.Core.Context;

using AIKernel.Dtos.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextAssemblyGovernanceException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextAssemblyGovernanceException']" />
public sealed class ContextAssemblyGovernanceException : ContextAssemblyException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyGovernanceException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyGovernanceException.#ctor']" />
    public ContextAssemblyGovernanceException(RomId romId, string? reason)
        : base($"Context assembly governance denied ROM. RomId='{romId.Value}'. Reason='{reason ?? "unspecified"}'.")
    {
        RomId = romId;
        Reason = reason;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.ContextAssemblyGovernanceException.RomId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.ContextAssemblyGovernanceException.RomId']" />
    public RomId RomId { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.ContextAssemblyGovernanceException.Reason']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.ContextAssemblyGovernanceException.Reason']" />
    public string? Reason { get; }
}
