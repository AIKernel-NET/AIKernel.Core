#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Security;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public interface ISecureCredentialProvider
{
    ValueTask<string> GetSecretAsync(
        string key,
        CancellationToken cancellationToken = default);
}