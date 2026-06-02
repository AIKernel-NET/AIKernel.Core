namespace AIKernel.Core.Security;

public sealed class SecureCredentialAmbiguousException(string message) : SecureCredentialException(message)
{
}