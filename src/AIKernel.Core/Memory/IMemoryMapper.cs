using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

/// <summary>
/// [EN] Maps files or native payloads into an addressable memory region.
/// [JA] AIKernel の公開参照サーフェスにおける IMemoryMapper を説明します。
/// </summary>
public interface IMemoryMapper
{
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
