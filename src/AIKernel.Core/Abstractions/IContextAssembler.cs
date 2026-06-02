#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Context;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Dtos.Rom;
using AIKernel.Vfs;

public interface IContextAssembler
{
    Task<IContextSnapshot> AssembleAsync(
        IVfsSession session,
        ContextAssemblyRequest request,
        CancellationToken cancellationToken = default);
}