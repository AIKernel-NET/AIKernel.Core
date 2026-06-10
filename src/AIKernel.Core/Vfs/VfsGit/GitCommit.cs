namespace AIKernel.Core.Vfs.VfsGit;

/// <summary>
/// [EN] Immutable Git commit identity used by VFS Git boundaries.
/// [JA] VFS Git 境界で利用する immutable Git commit identity です。
/// </summary>
/// <param name="Sha">[EN] Git commit SHA. [JA] Git commit SHA です。</param>
/// <param name="Message">[EN] Optional commit message. [JA] 任意の commit message です。</param>
/// <param name="Timestamp">[EN] Optional commit timestamp. [JA] 任意の commit timestamp です。</param>
public sealed record GitCommit(
    string Sha,
    string? Message = null,
    DateTimeOffset? Timestamp = null);
