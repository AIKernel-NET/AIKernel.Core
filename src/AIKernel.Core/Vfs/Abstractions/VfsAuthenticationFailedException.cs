namespace AIKernel.Core.Vfs.Abstractions;

public sealed class VfsAuthenticationFailedException : UnauthorizedAccessException
{
    public VfsAuthenticationFailedException(string providerId)
        : base($"VFS authentication failed for provider '{providerId}'.")
    {
        ProviderId = providerId;
    }

    public string ProviderId { get; }
}
