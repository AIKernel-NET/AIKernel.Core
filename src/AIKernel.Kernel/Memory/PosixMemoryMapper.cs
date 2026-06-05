namespace AIKernel.Kernel.Memory;

using System.Runtime.InteropServices;
using AIKernel.Common.Results;
using AIKernel.Core.Memory;

public sealed class PosixMemoryMapper : MemoryMapperBase
{
    protected override Result<IMemoryRegion> OpenCore(
        string path,
        MemoryAccessMode accessMode)
    {
        if (OperatingSystem.IsWindows())
        {
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                "POSIX memory mapping is not available on Windows."));
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                $"Memory mapped file was not found: {path}."));
        }

        if (fileInfo.Length <= 0)
        {
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                "Memory mapped file must not be empty."));
        }

        var flags = accessMode == MemoryAccessMode.Read
            ? PosixNative.OpenReadOnly
            : PosixNative.OpenReadWrite;
        var descriptor = PosixNative.Open(path, flags, 0);
        if (descriptor < 0)
        {
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                $"open failed with errno {Marshal.GetLastPInvokeError()}."));
        }

        var protection = accessMode == MemoryAccessMode.Read
            ? PosixNative.ProtRead
            : PosixNative.ProtRead | PosixNative.ProtWrite;
        var pointer = PosixNative.Mmap(
            IntPtr.Zero,
            (UIntPtr)fileInfo.Length,
            protection,
            PosixNative.MapShared,
            descriptor,
            IntPtr.Zero);

        if (pointer == new IntPtr(-1))
        {
            _ = PosixNative.Close(descriptor);
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                $"mmap failed with errno {Marshal.GetLastPInvokeError()}."));
        }

        return Result<IMemoryRegion>.Success(new PosixMemoryRegion(
            new MemoryRegionInfo(path, fileInfo.Length, accessMode),
            pointer,
            descriptor));
    }
}
