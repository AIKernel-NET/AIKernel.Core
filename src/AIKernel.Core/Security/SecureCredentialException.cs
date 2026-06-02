namespace AIKernel.Core.Security;

public abstract class SecureCredentialException : Exception
{
    protected SecureCredentialException(string message)
        : base(message)
    {
    }

    protected SecureCredentialException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
