#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Context;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public class ContextAssemblyException : Exception
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
