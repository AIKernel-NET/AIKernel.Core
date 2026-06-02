#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Abstractions.Context;

public sealed record PromptGenerationRequest(
    IContextSnapshot ContextSnapshot,
    string UserInstruction,
    ModelPromptCapability Capability,
    PromptGenerationOptions Options);
