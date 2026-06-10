namespace AIKernel.Core.Memory;

/// <summary>
/// [EN] Declares the access mode requested for a native memory mapping.
/// [JA] AIKernel の公開参照サーフェスにおける MemoryAccessMode を説明します。
/// </summary>
public enum MemoryAccessMode
{
    /// <summary>
    /// [EN] Read-only memory mapping.
    /// [JA] 読み取り専用のメモリマッピングです。
    /// </summary>
    Read = 0,

    /// <summary>
    /// [EN] Read/write memory mapping.
    /// [JA] 読み取り/書き込み用のメモリマッピングです。
    /// </summary>
    ReadWrite = 1
}
