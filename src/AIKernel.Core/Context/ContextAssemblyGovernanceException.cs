namespace AIKernel.Core.Context;

using AIKernel.Dtos.Rom;

/// <summary>[EN] Documents this public package API member. [JA] ContextAssemblyGovernanceException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextAssemblyGovernanceException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextAssemblyGovernanceException']/summary" />
public sealed class ContextAssemblyGovernanceException : ContextAssemblyException
{
    /// <summary>[EN] Documents this public package API member. [JA] ContextAssemblyGovernanceException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyGovernanceException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssemblyGovernanceException.#ctor']/summary" />
    public ContextAssemblyGovernanceException(RomId romId, string? reason)
        : base($"Context assembly governance denied ROM. RomId='{romId.Value}'. Reason='{reason ?? "unspecified"}'.")
    {
        RomId = romId;
        Reason = reason;
    }

    /// <summary>[EN] Documents this public package API member. [JA] RomId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.ContextAssemblyGovernanceException.RomId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.ContextAssemblyGovernanceException.RomId']/summary" />
    public RomId RomId { get; }

    /// <summary>[EN] Documents this public package API member. [JA] Reason を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.ContextAssemblyGovernanceException.Reason']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.ContextAssemblyGovernanceException.Reason']/summary" />
    public string? Reason { get; }
}
