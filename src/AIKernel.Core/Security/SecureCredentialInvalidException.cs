namespace AIKernel.Core.Security;

public sealed class SecureCredentialInvalidException(string key, string reason) : SecureCredentialException($"Secret is invalid. Key='{key}', Reason='{reason}'.")
{
    public string Key { get; } = key;

    public string Reason { get; } = reason;
}
