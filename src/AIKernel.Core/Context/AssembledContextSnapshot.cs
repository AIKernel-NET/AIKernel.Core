namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;

public sealed class AssembledContextSnapshot : IContextSnapshot
{
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

    public string SnapshotId { get; }

    public string? ParentSnapshotId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public string ContextHash { get; }

    public IContextCollection Context { get; }
}
