namespace AIKernel.Core.Memory;

/// <summary>Uploads fixed-size blocks to a device memory tier.</summary>
public interface IVirtualMemoryDevice
{
    /// <summary>Allocates a device block and copies all source bytes before returning.</summary>
    /// <exception cref="OutOfMemoryException">The device cannot allocate a block.</exception>
    IVirtualMemoryDeviceBlock Upload(ReadOnlySpan<byte> source);
}

/// <summary>Owns one block of device memory.</summary>
/// <remarks>Disposal is idempotent. A failed release leaves the block available for a retry.</remarks>
public interface IVirtualMemoryDeviceBlock : IDisposable
{
    /// <summary>Copies the entire block to an equally sized host buffer.</summary>
    void Download(Span<byte> destination);
}
