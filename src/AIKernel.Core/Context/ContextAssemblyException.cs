namespace AIKernel.Core.Context;

public class ContextAssemblyException : InvalidOperationException
{
    public ContextAssemblyException(string message)
        : base(message)
    {
    }

    public ContextAssemblyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
