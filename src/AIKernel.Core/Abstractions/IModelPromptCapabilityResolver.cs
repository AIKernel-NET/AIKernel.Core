#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;

public interface IModelPromptCapabilityResolver
{
    ModelPromptCapability Resolve(
        IModelProvider provider,
        KernelExecutionRequest request);
}
