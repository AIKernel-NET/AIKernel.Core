using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

public abstract class MemoryRegionBase : IMemoryRegion
{
    private bool _disposed;

    protected MemoryRegionBase(
        MemoryRegionInfo info,
        IntPtr pointer)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        Pointer = pointer;
    }

    public MemoryRegionInfo Info { get; }

    public IntPtr Pointer { get; protected set; }

    public long Length => Info.Length;

    public bool IsMapped => !_disposed && Pointer != IntPtr.Zero;

    public Result<bool> Unmap()
    {
        if (_disposed)
            return Result<bool>.Success(true);

        try
        {
            var result = UnmapCore();
            if (result.IsSuccess)
            {
                Pointer = IntPtr.Zero;
                _disposed = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(MemoryMappingErrors.FromException(ex));
        }
    }

    public void Dispose()
    {
        _ = Unmap();
        GC.SuppressFinalize(this);
    }

    protected abstract Result<bool> UnmapCore();
}
