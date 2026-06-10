namespace AIKernel.Core.Vfs.Abstractions;

using AIKernel.Vfs;

/// <summary>
/// [EN] Validates credentials supplied to a VFS provider.
/// [JA] VFS provider に渡される credentials を検証します。
/// </summary>
public delegate bool VfsCredentialValidator(IVfsCredentials credentials);
