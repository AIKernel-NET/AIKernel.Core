#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Vfs;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public sealed class VfsAuthenticationFailedException : UnauthorizedAccessException
{
    public VfsAuthenticationFailedException(string providerId)
        : base($"VFS credentials were rejected. ProviderId='{providerId}'.")
    {
        ProviderId = providerId;
    }

    public string ProviderId { get; }
}
