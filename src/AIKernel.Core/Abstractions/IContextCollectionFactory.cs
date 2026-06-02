#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Context;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Dtos.Rom;

public interface IContextCollectionFactory
{
    IContextCollection Create(
        IReadOnlyList<RomSnapshot> roms,
        IReadOnlyList<RomContextEdge> edges,
        ContextAssemblyScope scope);
}
