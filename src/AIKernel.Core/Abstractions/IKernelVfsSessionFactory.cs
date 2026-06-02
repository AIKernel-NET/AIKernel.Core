#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Kernel;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Dtos.Kernel;
using AIKernel.Vfs;

public interface IKernelVfsSessionFactory
{
    Task<IVfsSession> OpenSessionAsync(
        KernelRequest request,
        CancellationToken cancellationToken = default);
}