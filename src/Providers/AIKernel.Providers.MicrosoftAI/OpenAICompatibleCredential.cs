namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Core.Security;
using AIKernel.Core.Time;

public sealed record OpenAICompatibleCredential
{
    public required string ApiKey { get; init; }

    public DateTimeOffset? ExpiresAtUtc { get; init; }

    public static OpenAICompatibleCredential Create(
        string keyName,
        string apiKey,
        DateTimeOffset? expiresAtUtc,
        IKernelClock? clock = null)
    {
        SecureCredentialGuard.ValidateSecret(
            keyName,
            apiKey,
            expiresAtUtc,
            clock?.Logical);

        return new OpenAICompatibleCredential
        {
            ApiKey = apiKey,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public override string ToString()
    {
        return "OpenAICompatibleCredential { ApiKey = ***REDACTED*** }";
    }
}
