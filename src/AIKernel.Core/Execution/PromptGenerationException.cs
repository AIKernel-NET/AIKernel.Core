namespace AIKernel.Core.Execution;

public class PromptGenerationException : InvalidOperationException
{
    public PromptGenerationException(string message)
        : base(message)
    {
    }
}
