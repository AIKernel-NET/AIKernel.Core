namespace AIKernel.Core.Tests.Support;

using AIKernel.Dtos.Execution;
using AIKernel.Enums;

internal static class TestExecutionDefaults
{
    public static PromptGenerationOptions PromptOptions { get; } = new()
    {
        OverflowPolicy = PromptOverflowPolicy.FailClosed,
        IncludeContextHash = true,
        IncludeSourceMetadata = true
    };

    public static ExecutionOptions ExecutionOptions { get; } = new()
    {
        Temperature = 0,
        TopP = 1,
        MaxOutputTokens = 128,
        StopSequences = []
    };
}
