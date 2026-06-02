#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public sealed class UnsupportedPromptCapabilityException : PromptGenerationException
{
    public UnsupportedPromptCapabilityException(string message)
        : base(message)
    {
    }
}
