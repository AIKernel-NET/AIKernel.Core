using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

/// <summary>
/// [EN] Maps files or native payloads into an addressable memory region.
/// [JA] AIKernel の公開参照サーフェスにおける IMemoryMapper を説明します。
/// </summary>
public interface IMemoryMapper : AIKernel.Abstractions.Memory.IMemoryMapper
{
    AIKernel.Abstractions.Memory.IMemoryRegion AIKernel.Abstractions.Memory.IMemoryMapper.Open(string path, AIKernel.Enums.MemoryAccessMode accessMode)
        => new ContractRegion(Open(path, accessMode switch
        {
            AIKernel.Enums.MemoryAccessMode.Read => MemoryAccessMode.Read,
            AIKernel.Enums.MemoryAccessMode.ReadWrite => MemoryAccessMode.ReadWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(accessMode))
        }), accessMode);

    private sealed class ContractRegion(IMemoryRegion region, AIKernel.Enums.MemoryAccessMode mode) : AIKernel.Abstractions.Memory.IMemoryRegion
    {
        public AIKernel.Dtos.Memory.MemoryRegionInfo Info => new(region.Info.Path, region.Length, mode);
        public IntPtr Pointer => region.Pointer;
        public long Length => region.Length;
        public bool IsMapped => region.IsMapped;
        public bool Unmap() => region.Unmap();
        public void Dispose() => region.Dispose();
    }

    /// <summary>
    /// [EN] Opens a memory region or throws when mapping fails.
    /// [JA] メモリ領域を開き、マッピングに失敗した場合は例外を送出します。
    /// </summary>
    IMemoryRegion Open(
        string path,
        MemoryAccessMode accessMode = MemoryAccessMode.Read);

    /// <summary>
    /// [EN] Opens a memory region and returns a fail-closed result.
    /// [JA] メモリ領域を開き、fail-closed な結果として返します。
    /// </summary>
    Result<IMemoryRegion> OpenResult(
        string path,
        MemoryAccessMode accessMode = MemoryAccessMode.Read);
}
