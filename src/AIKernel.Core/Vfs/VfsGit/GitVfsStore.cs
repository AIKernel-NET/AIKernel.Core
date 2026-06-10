namespace AIKernel.Core.Vfs.VfsGit;

/// <summary>
/// [EN] Minimal deterministic Git-backed VFS store descriptor.
/// [JA] Git-backed VFS store の最小決定論的 descriptor です。
/// </summary>
public sealed class GitVfsStore(
    string repositoryPath,
    string repositoryMode = "readonly")
{
    /// <summary>[EN] Repository path. [JA] repository path です。</summary>
    public string RepositoryPath { get; } = repositoryPath;

    /// <summary>[EN] Repository access mode. [JA] repository access mode です。</summary>
    public string RepositoryMode { get; } = repositoryMode;

    /// <summary>
    /// [EN] Creates a deterministic snapshot descriptor.
    /// [JA] 決定論的 snapshot descriptor を作成します。
    /// </summary>
    public GitSnapshot CreateSnapshot(
        GitCommit commit,
        IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(paths);

        return new GitSnapshot(
            RepositoryPath,
            commit,
            paths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray());
    }
}
