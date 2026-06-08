namespace AIKernel.Kernel.Memory;

using System.Runtime.InteropServices;
using AIKernel.Common.Results;
using AIKernel.Core.Memory;

internal sealed class PosixMemoryRegion : MemoryRegionBase
{
    private readonly int _fileDescriptor;

    public PosixMemoryRegion(
        MemoryRegionInfo info,
        IntPtr pointer,
        int fileDescriptor)
        : base(info, pointer)
    {
        _fileDescriptor = fileDescriptor;
    }

    protected override Result<bool> UnmapCore()
    {
        var unmapResult = Pointer == IntPtr.Zero
            ? 0
            : PosixNative.Munmap(Pointer, (UIntPtr)Length);
        var closeResult = PosixNative.Close(_fileDescriptor);

        return unmapResult == 0 && closeResult == 0
            ? Result<bool>.Success(true)
            : Result<bool>.Fail(KernelMemoryMappingErrors.Error(
                $"munmap/close failed with errno {Marshal.GetLastPInvokeError()}."));
    }
}
