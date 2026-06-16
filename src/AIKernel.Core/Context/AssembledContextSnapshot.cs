namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;

/// <summary>[EN] Documents this public package API member. [JA] AssembledContextSnapshot を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.AssembledContextSnapshot']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.AssembledContextSnapshot']/summary" />
public sealed class AssembledContextSnapshot : IContextSnapshot
{
    /// <summary>[EN] Documents this public package API member. [JA] AssembledContextSnapshot を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.AssembledContextSnapshot.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.AssembledContextSnapshot.#ctor']/summary" />
    public AssembledContextSnapshot(
        string snapshotId,
        string? parentSnapshotId,
        DateTimeOffset createdAtUtc,
        string contextHash,
        IContextCollection context)
    {
        SnapshotId = string.IsNullOrWhiteSpace(snapshotId)
            ? throw new ArgumentException("SnapshotId is required.", nameof(snapshotId))
            : snapshotId;

        ParentSnapshotId = parentSnapshotId;
        CreatedAtUtc = createdAtUtc.Offset == TimeSpan.Zero
            ? createdAtUtc
            : createdAtUtc.ToUniversalTime();
        ContextHash = string.IsNullOrWhiteSpace(contextHash)
            ? throw new ArgumentException("ContextHash is required.", nameof(contextHash))
            : contextHash;
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>[EN] Documents this public package API member. [JA] SnapshotId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.SnapshotId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.SnapshotId']/summary" />
    public string SnapshotId { get; }

    /// <summary>[EN] Documents this public package API member. [JA] ParentSnapshotId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.ParentSnapshotId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.ParentSnapshotId']/summary" />
    public string? ParentSnapshotId { get; }

    /// <summary>[EN] Documents this public package API member. [JA] CreatedAtUtc を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.CreatedAtUtc']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.CreatedAtUtc']/summary" />
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>[EN] Documents this public package API member. [JA] ContextHash を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.ContextHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.ContextHash']/summary" />
    public string ContextHash { get; }

    /// <summary>[EN] Documents this public package API member. [JA] Context を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.Context']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.Context']/summary" />
    public IContextCollection Context { get; }
}
