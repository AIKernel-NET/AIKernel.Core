namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.AssembledContextSnapshot']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.AssembledContextSnapshot']" />
public sealed class AssembledContextSnapshot : IContextSnapshot
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.AssembledContextSnapshot.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.AssembledContextSnapshot.#ctor']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.SnapshotId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.SnapshotId']" />
    public string SnapshotId { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.ParentSnapshotId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.ParentSnapshotId']" />
    public string? ParentSnapshotId { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.CreatedAtUtc']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.CreatedAtUtc']" />
    public DateTimeOffset CreatedAtUtc { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.ContextHash']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.ContextHash']" />
    public string ContextHash { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.Context']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.AssembledContextSnapshot.Context']" />
    public IContextCollection Context { get; }
}
