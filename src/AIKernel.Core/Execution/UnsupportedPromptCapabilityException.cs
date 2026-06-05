namespace AIKernel.Core.Execution;

public sealed class UnsupportedPromptCapabilityException : PromptGenerationException
{
    public UnsupportedPromptCapabilityException(string message)
        : base(message)
    {
    }
}
