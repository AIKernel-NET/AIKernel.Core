using AIKernel.Core.Security;
using AIKernel.Core.Time;

#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Security;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public sealed class EnvironmentCredentialProvider(IKernelClock? clock = null) : ISecureCredentialProvider
{
    private readonly IKernelClock _clock = clock ?? KernelClock.System();

    public ValueTask<string> GetSecretAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new SecureCredentialInvalidException(
                "<empty>",
                "Environment variable name is required.");
        }

        var value = Environment.GetEnvironmentVariable(key);

        SecureCredentialGuard.ValidateSecret(
            key,
            value,
            expiresAtUtc: null,
            timeProvider: _clock.Logical);

        return ValueTask.FromResult(value!);
    }
}
