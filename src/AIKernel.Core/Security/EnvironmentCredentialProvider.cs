namespace AIKernel.Core.Security;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Time;

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
