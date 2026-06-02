#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Rom;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Dtos.Rom;
using AIKernel.Vfs;

public interface IRomLoader
{
    Task<RomSnapshot> LoadAsync(
        IVfsSession session,
        string path,
        CancellationToken cancellationToken = default);
}