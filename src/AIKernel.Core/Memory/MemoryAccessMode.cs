namespace AIKernel.Core.Memory;

/// <summary>
/// Declares the access mode requested for a native memory mapping.
/// </summary>
public enum MemoryAccessMode
{
    /// <summary>
    /// Read-only memory mapping.
    /// </summary>
    Read = 0,

    /// <summary>
    /// Read/write memory mapping.
    /// </summary>
    ReadWrite = 1
}
