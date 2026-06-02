namespace AIKernel.Core.Security;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Time;
using Microsoft.Extensions.Configuration;

/// <summary>
/// ConfigurationCredentialProvider
/// </summary>
/// <remarks>
/// UserSecrets / 環境変数 / appsettings を IConfiguration に統合して利用ケースに対応。
/// </remarks>
public sealed class ConfigurationCredentialProvider(
    IConfiguration configuration,
    IKernelClock? clock = null) : ISecureCredentialProvider
{
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
                "Configuration key is required.");
        }

        var value = _configuration[key];

        SecureCredentialGuard.ValidateSecret(
            key,
            value,
            expiresAtUtc: null,
            timeProvider: _clock.Logical);

        return ValueTask.FromResult(value!);
    }
}
