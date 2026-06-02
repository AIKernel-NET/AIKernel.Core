#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Abstractions.Context;

public sealed record KernelExecutionRequest
{
    public required IContextSnapshot ContextSnapshot { get; init; }

    public required string UserInstruction { get; init; }

    public required PromptGenerationOptions PromptOptions { get; init; }

    public required ExecutionOptions ExecutionOptions { get; init; }

    public string? RequestedModelId { get; init; }
}
