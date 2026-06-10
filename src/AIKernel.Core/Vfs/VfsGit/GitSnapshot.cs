namespace AIKernel.Core.Vfs.VfsGit;

/// <summary>
/// [EN] Deterministic VFS Git snapshot descriptor.
/// [JA] 決定論的 VFS Git snapshot descriptor です。
/// </summary>
/// <param name="RepositoryPath">[EN] Repository path captured by the snapshot. [JA] snapshot が保持する repository path です。</param>
/// <param name="Commit">[EN] Commit identity captured by the snapshot. [JA] snapshot が保持する commit identity です。</param>
/// <param name="Paths">[EN] Deterministic paths included in the snapshot. [JA] snapshot に含まれる決定論的 path です。</param>
public sealed record GitSnapshot(
    string RepositoryPath,
    GitCommit Commit,
    IReadOnlyList<string> Paths);
