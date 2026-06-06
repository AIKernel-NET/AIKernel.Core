namespace AIKernel.Core.Security;

public sealed class SecureCredentialExpiredException(string key, DateTimeOffset expiresAtUtc) : SecureCredentialException($"Secret has expired. Key='{key}', ExpiresAtUtc='{expiresAtUtc:O}'.")
{
    public string Key { get; } = key;

    public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
}
