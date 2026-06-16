namespace AIKernel.Kernel.Memory;

using System.Runtime.InteropServices;
using AIKernel.Common.Results;
using AIKernel.Core.Memory;
using Microsoft.Win32.SafeHandles;

internal sealed class Win32MemoryRegion : MemoryRegionBase
{
    private readonly SafeFileHandle _mappingHandle;
    /// <summary>
    /// EN: Gets Win32MemoryRegion.
    /// [EN] Documents this public package API member. [JA] Win32MemoryRegion を取得します。
    /// </summary>

    public Win32MemoryRegion(
        MemoryRegionInfo info,
        IntPtr pointer,
        SafeFileHandle mappingHandle)
        : base(info, pointer)
    {
        _mappingHandle = mappingHandle ?? throw new ArgumentNullException(nameof(mappingHandle));
    }

    protected override Result<bool> UnmapCore()
    {
        var unmapped = Pointer == IntPtr.Zero || Win32Native.UnmapViewOfFile(Pointer);
        _mappingHandle.Dispose();

        return unmapped
            ? Result<bool>.Success(true)
            : Result<bool>.Fail(KernelMemoryMappingErrors.Error(
                $"UnmapViewOfFile failed with error {Marshal.GetLastWin32Error()}."));
    }
}
