#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Execution;

public interface IContextPromptProjector
{
    IReadOnlyList<ContextPromptBlock> Project(
        IContextSnapshot snapshot,
        PromptProjectionOptions options);
}
