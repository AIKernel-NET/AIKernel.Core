namespace AIKernel.Core.Vfs.VfsGit;

/// <summary>
/// [EN] Core-owned descriptor for the VFS Git capability boundary.
/// [JA] VFS Git capability 境界の Core 所有 descriptor です。
/// </summary>
/// <param name="CapabilityId">[EN] Stable VFS Git capability identifier. [JA] 安定した VFS Git capability identifier です。</param>
/// <param name="RepositoryMode">[EN] Repository access mode exposed by the capability. [JA] capability が公開する repository access mode です。</param>
/// <param name="Metadata">[EN] Deterministic descriptor metadata. [JA] 決定論的な descriptor metadata です。</param>
public sealed record VfsGitCapabilityDescriptor(
    string CapabilityId,
    string RepositoryMode,
    IReadOnlyDictionary<string, string> Metadata);
