#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Kernel;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Kernel;

public interface IKernelModelProviderSelector
{
    Task<IModelProvider> SelectAsync(
        KernelRequest request,
        IContextSnapshot contextSnapshot,
        CancellationToken cancellationToken = default);
}